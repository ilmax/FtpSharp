using System.Runtime.InteropServices;
using FtpServer.Core.Abstractions;
using FtpServer.Core.Configuration;
using Microsoft.Extensions.Options;

namespace FtpServer.Core.FileSystem;

/// <summary>
/// Storage provider backed by the local file system, rooted at FtpServerOptions.StorageRoot.
/// </summary>
public sealed class FileSystemStorageProvider : IStorageProvider
{
    private readonly string _root;

    public FileSystemStorageProvider(IOptions<FtpServerOptions> options)
    {
        _root = Path.GetFullPath(options.Value.StorageRoot);
        Directory.CreateDirectory(_root);
    }

    public Task<bool> ExistsAsync(string path, CancellationToken ct)
    {
        (string phys, _, bool isDir) = Physical(path);
        return Task.FromResult(isDir ? Directory.Exists(phys) : File.Exists(phys) || Directory.Exists(phys));
    }

    public Task<IReadOnlyList<FileSystemEntry>> ListAsync(string path, CancellationToken ct)
    {
        (string phys, string logical, _) = Physical(path);
        if (!Directory.Exists(phys))
            return Task.FromResult<IReadOnlyList<FileSystemEntry>>(Array.Empty<FileSystemEntry>());

        var list = new List<FileSystemEntry>();
        foreach (string d in Directory.GetDirectories(phys))
        {
            string name = Path.GetFileName(d);
            list.Add(new FileSystemEntry(name, CombineLogical(logical, name), true, null));
        }
        foreach (string f in Directory.GetFiles(phys))
        {
            string name = Path.GetFileName(f);
            var info = new FileInfo(f);
            list.Add(new FileSystemEntry(name, CombineLogical(logical, name), false, info.Length));
        }
        return Task.FromResult<IReadOnlyList<FileSystemEntry>>(list);
    }

    public Task CreateDirectoryAsync(string path, CancellationToken ct)
    {
        (string phys, _, _) = Physical(path);
        Directory.CreateDirectory(phys);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string path, bool recursive, CancellationToken ct)
    {
        (string phys, _, _) = Physical(path);
        if (Directory.Exists(phys))
        {
            Directory.Delete(phys, recursive);
        }
        else if (File.Exists(phys))
        {
            File.Delete(phys);
        }
        return Task.CompletedTask;
    }

    public Task<long> GetSizeAsync(string path, CancellationToken ct)
    {
        (string phys, _, _) = Physical(path);
        return Task.FromResult(File.Exists(phys) ? new FileInfo(phys).Length : 0L);
    }

    public Task<FileSystemEntry?> GetEntryAsync(string path, CancellationToken ct)
    {
        (string phys, string logical, _) = Physical(path);
        if (Directory.Exists(phys))
            return Task.FromResult<FileSystemEntry?>(new FileSystemEntry(Path.GetFileName(phys), logical, true, null));
        if (File.Exists(phys))
        {
            var info = new FileInfo(phys);
            return Task.FromResult<FileSystemEntry?>(new FileSystemEntry(Path.GetFileName(phys), logical, false, info.Length));
        }
        return Task.FromResult<FileSystemEntry?>(null);
    }

    public async IAsyncEnumerable<ReadOnlyMemory<byte>> ReadAsync(string path, int bufferSize, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        (string phys, _, _) = Physical(path);
        if (!File.Exists(phys)) yield break;
        using var fs = new FileStream(phys, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, useAsync: true);
        byte[] buffer = new byte[bufferSize];
        int read;
        while ((read = await fs.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
        {
            // Yield a slice of the buffer; consumer must consume before next MoveNext.
            yield return buffer.AsMemory(0, read);
            await Task.Yield();
            ct.ThrowIfCancellationRequested();
        }
    }

    public async IAsyncEnumerable<ReadOnlyMemory<byte>> ReadFromOffsetAsync(string path, long offset, int bufferSize, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        (string phys, _, _) = Physical(path);
        if (!File.Exists(phys)) yield break;
        using var fs = new FileStream(phys, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, useAsync: true);
        if (offset > 0) fs.Seek(offset, SeekOrigin.Begin);
        byte[] buffer = new byte[bufferSize];
        int read;
        while ((read = await fs.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
        {
            yield return buffer.AsMemory(0, read);
            await Task.Yield();
            ct.ThrowIfCancellationRequested();
        }
    }

    public async Task WriteAsync(string path, IAsyncEnumerable<ReadOnlyMemory<byte>> content, CancellationToken ct)
    {
        (string phys, _, _) = Physical(path);
        Directory.CreateDirectory(Path.GetDirectoryName(phys)!);
        using var fs = new FileStream(phys, FileMode.Create, FileAccess.Write, FileShare.None, 8192, useAsync: true);
        await foreach (var chunk in content.WithCancellation(ct))
        {
            await fs.WriteAsync(chunk, ct);
        }
    }

    public async Task AppendAsync(string path, IAsyncEnumerable<ReadOnlyMemory<byte>> content, CancellationToken ct)
    {
        (string phys, _, _) = Physical(path);
        Directory.CreateDirectory(Path.GetDirectoryName(phys)!);
        using var fs = new FileStream(phys, FileMode.Append, FileAccess.Write, FileShare.None, 8192, useAsync: true);
        await foreach (var chunk in content.WithCancellation(ct))
        {
            await fs.WriteAsync(chunk, ct);
        }
    }

    public async Task WriteTruncateThenAppendAsync(string path, long truncateTo, IAsyncEnumerable<ReadOnlyMemory<byte>> content, CancellationToken ct)
    {
        (string phys, _, _) = Physical(path);
        Directory.CreateDirectory(Path.GetDirectoryName(phys)!);
        using (var fs = new FileStream(phys, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None, 8192, useAsync: true))
        {
            fs.SetLength(Math.Max(0, truncateTo));
        }
        await AppendAsync(path, content, ct);
    }

    public Task RenameAsync(string fromPath, string toPath, CancellationToken ct)
    {
        (string fromPhys, _, _) = Physical(fromPath);
        (string toPhys, _, _) = Physical(toPath);
        Directory.CreateDirectory(Path.GetDirectoryName(toPhys)!);
        if (File.Exists(fromPhys))
            File.Move(fromPhys, toPhys, overwrite: true);
        else if (Directory.Exists(fromPhys))
            Directory.Move(fromPhys, toPhys);
        return Task.CompletedTask;
    }

    private (string physical, string logical, bool isDirectoryPath) Physical(string logicalPath)
    {
        string logical = NormalizeLogical(logicalPath);
        string rel = logical.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        string physical = Path.GetFullPath(Path.Combine(_root, rel));
        if (!physical.StartsWith(_root, StringComparison.Ordinal))
            throw new IOException("Path escapes storage root");
        bool isDir = logical.EndsWith('/');
        return (physical, logical, isDir);
    }

    private static string NormalizeLogical(string p)
    {
        if (string.IsNullOrWhiteSpace(p)) return "/";
        p = p.Replace("\\", "/");
        if (!p.StartsWith('/')) p = "/" + p;
        return p.TrimEnd('/');
    }

    private static string CombineLogical(string baseLogical, string name)
        => baseLogical == "/" ? "/" + name : baseLogical + "/" + name;
}
