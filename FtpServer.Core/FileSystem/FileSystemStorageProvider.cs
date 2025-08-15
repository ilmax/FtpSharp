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
        _root = System.IO.Path.GetFullPath(options.Value.StorageRoot);
        Directory.CreateDirectory(_root);
    }

    public Task<bool> ExistsAsync(string path, CancellationToken ct)
    {
        var (phys, _, isDir) = Physical(path);
        return Task.FromResult(isDir ? Directory.Exists(phys) : File.Exists(phys) || Directory.Exists(phys));
    }

    public Task<IReadOnlyList<FileSystemEntry>> ListAsync(string path, CancellationToken ct)
    {
        var (phys, logical, _) = Physical(path);
        if (!Directory.Exists(phys))
            return Task.FromResult<IReadOnlyList<FileSystemEntry>>(Array.Empty<FileSystemEntry>());

        var list = new List<FileSystemEntry>();
        foreach (var d in Directory.GetDirectories(phys))
        {
            var name = System.IO.Path.GetFileName(d);
            list.Add(new FileSystemEntry(name, CombineLogical(logical, name), true, null));
        }
        foreach (var f in Directory.GetFiles(phys))
        {
            var name = System.IO.Path.GetFileName(f);
            var info = new FileInfo(f);
            list.Add(new FileSystemEntry(name, CombineLogical(logical, name), false, info.Length));
        }
        return Task.FromResult<IReadOnlyList<FileSystemEntry>>(list);
    }

    public Task CreateDirectoryAsync(string path, CancellationToken ct)
    {
        var (phys, _, _) = Physical(path);
        Directory.CreateDirectory(phys);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string path, bool recursive, CancellationToken ct)
    {
        var (phys, _, _) = Physical(path);
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
        var (phys, _, _) = Physical(path);
        return Task.FromResult(File.Exists(phys) ? new FileInfo(phys).Length : 0L);
    }

    public Task<FileSystemEntry?> GetEntryAsync(string path, CancellationToken ct)
    {
        var (phys, logical, _) = Physical(path);
        if (Directory.Exists(phys))
            return Task.FromResult<FileSystemEntry?>(new FileSystemEntry(System.IO.Path.GetFileName(phys), logical, true, null));
        if (File.Exists(phys))
        {
            var info = new FileInfo(phys);
            return Task.FromResult<FileSystemEntry?>(new FileSystemEntry(System.IO.Path.GetFileName(phys), logical, false, info.Length));
        }
        return Task.FromResult<FileSystemEntry?>(null);
    }

    public async IAsyncEnumerable<ReadOnlyMemory<byte>> ReadAsync(string path, int bufferSize, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        var (phys, _, _) = Physical(path);
        if (!File.Exists(phys)) yield break;
        using var fs = new FileStream(phys, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, useAsync: true);
        var buffer = new byte[bufferSize];
        int read;
        while ((read = await fs.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
        {
            // copy to avoid exposing mutable buffer
            yield return buffer.AsMemory(0, read).ToArray();
            await Task.Yield();
            ct.ThrowIfCancellationRequested();
        }
    }

    public async IAsyncEnumerable<ReadOnlyMemory<byte>> ReadFromOffsetAsync(string path, long offset, int bufferSize, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        var (phys, _, _) = Physical(path);
        if (!File.Exists(phys)) yield break;
        using var fs = new FileStream(phys, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, useAsync: true);
        if (offset > 0) fs.Seek(offset, SeekOrigin.Begin);
        var buffer = new byte[bufferSize];
        int read;
        while ((read = await fs.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
        {
            yield return buffer.AsMemory(0, read).ToArray();
            await Task.Yield();
            ct.ThrowIfCancellationRequested();
        }
    }

    public async Task WriteAsync(string path, IAsyncEnumerable<ReadOnlyMemory<byte>> content, CancellationToken ct)
    {
        var (phys, _, _) = Physical(path);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(phys)!);
        using var fs = new FileStream(phys, FileMode.Create, FileAccess.Write, FileShare.None, 8192, useAsync: true);
        await foreach (var chunk in content.WithCancellation(ct))
        {
            if (MemoryMarshal.TryGetArray((ReadOnlyMemory<byte>)chunk, out var seg))
                await fs.WriteAsync(seg.Array!, seg.Offset, seg.Count, ct);
            else
                await fs.WriteAsync(chunk.ToArray(), ct);
        }
    }

    public async Task AppendAsync(string path, IAsyncEnumerable<ReadOnlyMemory<byte>> content, CancellationToken ct)
    {
        var (phys, _, _) = Physical(path);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(phys)!);
        using var fs = new FileStream(phys, FileMode.Append, FileAccess.Write, FileShare.None, 8192, useAsync: true);
        await foreach (var chunk in content.WithCancellation(ct))
        {
            if (MemoryMarshal.TryGetArray((ReadOnlyMemory<byte>)chunk, out var seg))
                await fs.WriteAsync(seg.Array!, seg.Offset, seg.Count, ct);
            else
                await fs.WriteAsync(chunk.ToArray(), ct);
        }
    }

    public async Task WriteTruncateThenAppendAsync(string path, long truncateTo, IAsyncEnumerable<ReadOnlyMemory<byte>> content, CancellationToken ct)
    {
        var (phys, _, _) = Physical(path);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(phys)!);
        using (var fs = new FileStream(phys, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None, 8192, useAsync: true))
        {
            fs.SetLength(Math.Max(0, truncateTo));
        }
        await AppendAsync(path, content, ct);
    }

    public Task RenameAsync(string fromPath, string toPath, CancellationToken ct)
    {
        var (fromPhys, _, _) = Physical(fromPath);
        var (toPhys, _, _) = Physical(toPath);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(toPhys)!);
        if (File.Exists(fromPhys))
            File.Move(fromPhys, toPhys, overwrite: true);
        else if (Directory.Exists(fromPhys))
            Directory.Move(fromPhys, toPhys);
        return Task.CompletedTask;
    }

    private (string physical, string logical, bool isDirectoryPath) Physical(string logicalPath)
    {
        var logical = NormalizeLogical(logicalPath);
        var rel = logical.TrimStart('/').Replace('/', System.IO.Path.DirectorySeparatorChar);
        var physical = System.IO.Path.GetFullPath(System.IO.Path.Combine(_root, rel));
        if (!physical.StartsWith(_root, StringComparison.Ordinal))
            throw new IOException("Path escapes storage root");
        var isDir = logical.EndsWith('/');
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
