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
    private readonly object _gate = new();

    private abstract record Node;
    private sealed record DirNode(Dictionary<string, string> Children) : Node;
    private sealed record FileNode(byte[] Content) : Node;

    public InMemoryStorageProvider()
    {
        _nodes.TryAdd("/", new DirNode(new()));
    }

    public Task<bool> ExistsAsync(string path, CancellationToken ct)
    {
        path = Norm(path);
        lock (_gate)
        {
            return Task.FromResult(_nodes.ContainsKey(path));
        }
    }

    public Task<IReadOnlyList<FileSystemEntry>> ListAsync(string path, CancellationToken ct)
    {
        path = Norm(path);
        lock (_gate)
        {
            if (!_nodes.TryGetValue(path, out var node) || node is not DirNode dir)
                return Task.FromResult<IReadOnlyList<FileSystemEntry>>(Array.Empty<FileSystemEntry>());

            var list = new List<FileSystemEntry>(dir.Children.Count);
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
    }

    public Task CreateDirectoryAsync(string path, CancellationToken ct)
    {
        path = Norm(path);
        lock (_gate)
        {
            EnsureParentDirLocked(path);
            _nodes.TryAdd(path, new DirNode(new()));
            var parent = Parent(path);
            if (_nodes[parent] is DirNode pd)
            {
                pd.Children[Name(path)] = path;
            }
            return Task.CompletedTask;
        }
    }

    public Task DeleteAsync(string path, bool recursive, CancellationToken ct)
    {
        path = Norm(path);
        lock (_gate)
        {
            if (!_nodes.TryGetValue(path, out var node)) return Task.CompletedTask;
            if (node is DirNode d && d.Children.Count > 0 && !recursive)
                throw new IOException("Directory not empty");

            DeleteLocked(path, recursive);
            return Task.CompletedTask;
        }
    }

    public Task<long> GetSizeAsync(string path, CancellationToken ct)
    {
        path = Norm(path);
        lock (_gate)
        {
            if (_nodes.TryGetValue(path, out var node) && node is FileNode f)
                return Task.FromResult((long)f.Content.Length);
            return Task.FromResult(0L);
        }
    }

    public Task<FileSystemEntry?> GetEntryAsync(string path, CancellationToken ct)
    {
        path = Norm(path);
        lock (_gate)
        {
            if (_nodes.TryGetValue(path, out var node))
            {
                if (node is DirNode)
                    return Task.FromResult<FileSystemEntry?>(new FileSystemEntry(Name(path), path, true, null));
                if (node is FileNode f)
                    return Task.FromResult<FileSystemEntry?>(new FileSystemEntry(Name(path), path, false, f.Content.Length));
            }
            return Task.FromResult<FileSystemEntry?>(null);
        }
    }

    public Task RenameAsync(string fromPath, string toPath, CancellationToken ct)
    {
        fromPath = Norm(fromPath);
        toPath = Norm(toPath);
        lock (_gate)
        {
            if (!_nodes.TryGetValue(fromPath, out var node)) return Task.CompletedTask;

            // Ensure destination parent exists
            EnsureParentDirLocked(toPath);

            // Move node reference
            _nodes[toPath] = node;
            _nodes.TryRemove(fromPath, out _);

            // Update parent directory child listings
            var fromParent = Parent(fromPath);
            var toParent = Parent(toPath);
            var name = Name(toPath);
            if (_nodes.TryGetValue(fromParent, out var fp) && fp is DirNode fpd)
            {
                fpd.Children.Remove(Name(fromPath));
            }
            if (_nodes.TryGetValue(toParent, out var tp) && tp is DirNode tpd)
            {
                tpd.Children[name] = toPath;
            }
            return Task.CompletedTask;
        }
    }

    public async IAsyncEnumerable<ReadOnlyMemory<byte>> ReadAsync(string path, int bufferSize, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        path = Norm(path);
        byte[]? snapshot = null;
        lock (_gate)
        {
            if (_nodes.TryGetValue(path, out var node) && node is FileNode f)
            {
                snapshot = f.Content.ToArray();
            }
        }
        if (snapshot is null) yield break;
        var data = snapshot;
        for (int i = 0; i < data.Length; i += bufferSize)
        {
            var len = Math.Min(bufferSize, data.Length - i);
            yield return new ReadOnlyMemory<byte>(data, i, len);
            await Task.Yield();
            ct.ThrowIfCancellationRequested();
        }
    }

    public async IAsyncEnumerable<ReadOnlyMemory<byte>> ReadFromOffsetAsync(string path, long offset, int bufferSize, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        path = Norm(path);
        byte[]? snapshot = null;
        lock (_gate)
        {
            if (_nodes.TryGetValue(path, out var node) && node is FileNode f)
            {
                snapshot = f.Content.ToArray();
            }
        }
        if (snapshot is null) yield break;
        var start = (int)Math.Clamp(offset, 0, snapshot.Length);
        for (int i = start; i < snapshot.Length; i += bufferSize)
        {
            var len = Math.Min(bufferSize, snapshot.Length - i);
            yield return new ReadOnlyMemory<byte>(snapshot, i, len);
            await Task.Yield(); ct.ThrowIfCancellationRequested();
        }
    }

    public async Task WriteAsync(string path, IAsyncEnumerable<ReadOnlyMemory<byte>> content, CancellationToken ct)
    {
        path = Norm(path);
        using var ms = new MemoryStream();
        await foreach (var chunk in content.WithCancellation(ct))
        {
            if (MemoryMarshal.TryGetArray((ReadOnlyMemory<byte>)chunk, out var seg))
                await ms.WriteAsync(seg.Array!, seg.Offset, seg.Count, ct);
            else
                await ms.WriteAsync(chunk.ToArray(), ct);
        }
        var data = ms.ToArray();
        lock (_gate)
        {
            EnsureParentDirLocked(path);
            _nodes[path] = new FileNode(data);
            var parent = Parent(path);
            if (_nodes[parent] is DirNode pd)
                pd.Children[Name(path)] = path;
        }
    }

    public async Task AppendAsync(string path, IAsyncEnumerable<ReadOnlyMemory<byte>> content, CancellationToken ct)
    {
        path = Norm(path);
        using var ms = new MemoryStream();
        lock (_gate)
        {
            if (_nodes.TryGetValue(path, out var node) && node is FileNode f)
                ms.Write(f.Content, 0, f.Content.Length);
        }
        await foreach (var chunk in content.WithCancellation(ct))
        {
            if (MemoryMarshal.TryGetArray((ReadOnlyMemory<byte>)chunk, out var seg))
                await ms.WriteAsync(seg.Array!, seg.Offset, seg.Count, ct);
            else
                await ms.WriteAsync(chunk.ToArray(), ct);
        }
        var data = ms.ToArray();
        lock (_gate)
        {
            EnsureParentDirLocked(path);
            _nodes[path] = new FileNode(data);
            var parent = Parent(path);
            if (_nodes[parent] is DirNode pd)
                pd.Children[Name(path)] = path;
        }
    }

    public async Task WriteTruncateThenAppendAsync(string path, long truncateTo, IAsyncEnumerable<ReadOnlyMemory<byte>> content, CancellationToken ct)
    {
        path = Norm(path);
        using var ms = new MemoryStream();
        lock (_gate)
        {
            if (_nodes.TryGetValue(path, out var node) && node is FileNode f)
            {
                var keep = (int)Math.Clamp(truncateTo, 0, f.Content.Length);
                ms.Write(f.Content, 0, keep);
            }
        }
        await foreach (var chunk in content.WithCancellation(ct))
        {
            if (MemoryMarshal.TryGetArray((ReadOnlyMemory<byte>)chunk, out var seg))
                await ms.WriteAsync(seg.Array!, seg.Offset, seg.Count, ct);
            else
                await ms.WriteAsync(chunk.ToArray(), ct);
        }
        var data = ms.ToArray();
        lock (_gate)
        {
            EnsureParentDirLocked(path);
            _nodes[path] = new FileNode(data);
            var parent = Parent(path);
            if (_nodes[parent] is DirNode pd)
                pd.Children[Name(path)] = path;
        }
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

    private void EnsureParentDirLocked(string path)
    {
        // assumes _gate is held
        var parent = Parent(path);
        if (_nodes.ContainsKey(parent)) return;
        // create chain of missing directories
        var stack = new Stack<string>();
        var cur = parent;
        while (cur != "/" && !_nodes.ContainsKey(cur))
        {
            stack.Push(cur);
            cur = Parent(cur);
        }
        // ensure root exists
        if (!_nodes.ContainsKey("/"))
            _nodes.TryAdd("/", new DirNode(new()));
        while (stack.Count > 0)
        {
            var dir = stack.Pop();
            _nodes.TryAdd(dir, new DirNode(new()));
            var p = Parent(dir);
            if (_nodes[p] is DirNode pd)
                pd.Children[Name(dir)] = dir;
        }
    }

    private void DeleteLocked(string path, bool recursive)
    {
        // assumes _gate is held
        if (!_nodes.TryGetValue(path, out var node)) return;
        if (node is DirNode d)
        {
            if (!recursive && d.Children.Count > 0)
                throw new IOException("Directory not empty");
            if (recursive)
            {
                foreach (var (_, full) in d.Children.ToArray())
                {
                    DeleteLocked(full, true);
                }
            }
        }
        var parent = Parent(path);
        if (_nodes.TryGetValue(parent, out var pnode) && pnode is DirNode pd)
        {
            pd.Children.Remove(Name(path));
        }
        _nodes.TryRemove(path, out _);
    }
}
