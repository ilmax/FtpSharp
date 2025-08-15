using System.Buffers;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text;
using FtpServer.Core.Abstractions;

namespace FtpServer.Core.InMemory;

/// <summary>
/// Simple in-memory storage provider for testing and CI.
/// Not for production usage.
/// </summary>
public sealed class InMemoryStorageProvider : IStorageProvider
{
    private readonly ConcurrentDictionary<string, Node> _nodes = new(StringComparer.Ordinal);

    private abstract record Node;
    private sealed record DirNode(Dictionary<string, string> Children) : Node;
    private sealed record FileNode(byte[] Content) : Node;

    public InMemoryStorageProvider()
    {
        _nodes.TryAdd("/", new DirNode(new())) ;
    }

    public Task<bool> ExistsAsync(string path, CancellationToken ct)
        => Task.FromResult(_nodes.ContainsKey(Norm(path)));

    public Task<IReadOnlyList<FileSystemEntry>> ListAsync(string path, CancellationToken ct)
    {
        path = Norm(path);
        if (!_nodes.TryGetValue(path, out var node) || node is not DirNode dir)
            return Task.FromResult<IReadOnlyList<FileSystemEntry>>(Array.Empty<FileSystemEntry>());

        var list = new List<FileSystemEntry>();
        foreach (var (name, full) in dir.Children)
        {
            if (_nodes.TryGetValue(full, out var child))
            {
                if (child is DirNode)
                    list.Add(new FileSystemEntry(name, full, true, null));
                else if (child is FileNode f)
                    list.Add(new FileSystemEntry(name, full, false, f.Content.Length));
            }
        }
        return Task.FromResult<IReadOnlyList<FileSystemEntry>>(list);
    }

    public Task CreateDirectoryAsync(string path, CancellationToken ct)
    {
        path = Norm(path);
        EnsureParentDir(path);
        _nodes.TryAdd(path, new DirNode(new()));
        var parent = Parent(path);
        if (_nodes[parent] is DirNode pd)
        {
            pd.Children[Name(path)] = path;
        }
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string path, bool recursive, CancellationToken ct)
    {
        path = Norm(path);
        if (!_nodes.TryGetValue(path, out var node)) return Task.CompletedTask;
        if (node is DirNode d && d.Children.Count > 0 && !recursive)
            throw new IOException("Directory not empty");

        // naive delete
        if (node is DirNode d2 && recursive)
        {
            foreach (var (_, full) in d2.Children.ToArray())
                DeleteAsync(full, true, ct).GetAwaiter().GetResult();
        }
        _nodes.TryRemove(path, out _);
        return Task.CompletedTask;
    }

    public Task<long> GetSizeAsync(string path, CancellationToken ct)
    {
        path = Norm(path);
        if (_nodes.TryGetValue(path, out var node) && node is FileNode f)
            return Task.FromResult((long)f.Content.Length);
        return Task.FromResult(0L);
    }

    public async IAsyncEnumerable<ReadOnlyMemory<byte>> ReadAsync(string path, int bufferSize, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        path = Norm(path);
        if (_nodes.TryGetValue(path, out var node) && node is FileNode f)
        {
            var data = f.Content;
            for (int i = 0; i < data.Length; i += bufferSize)
            {
                var len = Math.Min(bufferSize, data.Length - i);
                yield return new ReadOnlyMemory<byte>(data, i, len);
                await Task.Yield();
                ct.ThrowIfCancellationRequested();
            }
        }
    }

    public async Task WriteAsync(string path, IAsyncEnumerable<ReadOnlyMemory<byte>> content, CancellationToken ct)
    {
        path = Norm(path);
        EnsureParentDir(path);
        using var ms = new MemoryStream();
        await foreach (var chunk in content.WithCancellation(ct))
        {
            if (MemoryMarshal.TryGetArray((ReadOnlyMemory<byte>)chunk, out var seg))
                await ms.WriteAsync(seg.Array!, seg.Offset, seg.Count, ct);
            else
                await ms.WriteAsync(chunk.ToArray(), ct);
        }
        _nodes[path] = new FileNode(ms.ToArray());
        var parent = Parent(path);
        if (_nodes[parent] is DirNode pd)
            pd.Children[Name(path)] = path;
    }

    private static string Norm(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return "/";
        path = path.Replace("\\", "/");
        if (!path.StartsWith('/')) path = "/" + path;
        return path.TrimEnd('/');
    }

    private string Parent(string p)
    {
        var i = p.LastIndexOf('/');
        return i <= 0 ? "/" : p.Substring(0, i);
    }

    private string Name(string p)
    {
        var i = p.LastIndexOf('/');
        return i < 0 ? p : p[(i + 1)..];
    }

    private void EnsureParentDir(string path)
    {
        var parent = Parent(path);
        if (!_nodes.ContainsKey(parent))
        {
            CreateDirectoryAsync(parent, CancellationToken.None).GetAwaiter().GetResult();
        }
    }
}
