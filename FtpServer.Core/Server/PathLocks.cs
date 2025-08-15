using System.Collections.Concurrent;

namespace FtpServer.Core.Server;

/// <summary>
/// Simple async reader-writer lock based on two SemaphoreSlims.
/// Readers coordinate via _mutex and block writers via _roomEmpty.
/// Writers acquire exclusive access via _roomEmpty.
/// </summary>
internal sealed class AsyncReaderWriterLock
{
    private readonly SemaphoreSlim _mutex = new(1, 1);
    private readonly SemaphoreSlim _roomEmpty = new(1, 1);
    private int _readers;

    public async Task<IAsyncDisposable> AcquireReadAsync(CancellationToken ct)
    {
        await _mutex.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            _readers++;
            if (_readers == 1)
            {
                await _roomEmpty.WaitAsync(ct).ConfigureAwait(false);
            }
        }
        finally
        {
            _ = _mutex.Release();
        }
        return new Releaser(this, isWrite: false);
    }

    public async Task<IAsyncDisposable> AcquireWriteAsync(CancellationToken ct)
    {
        await _roomEmpty.WaitAsync(ct).ConfigureAwait(false);
        return new Releaser(this, isWrite: true);
    }

    private sealed class Releaser : IAsyncDisposable
    {
        private AsyncReaderWriterLock _owner;
        private readonly bool _isWrite;
        private bool _disposed;
        public Releaser(AsyncReaderWriterLock owner, bool isWrite)
        {
            _owner = owner;
            _isWrite = isWrite;
        }
        public ValueTask DisposeAsync()
        {
            if (_disposed) return ValueTask.CompletedTask;
            _disposed = true;
            if (_isWrite)
            {
                _owner._roomEmpty.Release();
                return ValueTask.CompletedTask;
            }
            return ReleaseReadAsync();
        }

        private ValueTask ReleaseReadAsync()
        {
            _owner._mutex.Wait();
            try
            {
                _owner._readers--;
                if (_owner._readers == 0)
                {
                    _owner._roomEmpty.Release();
                }
            }
            finally
            {
                _owner._mutex.Release();
            }
            return ValueTask.CompletedTask;
        }
    }
}

/// <summary>
/// Global per-path lock manager to coordinate access across sessions.
/// </summary>
internal static class PathLocks
{
    private static readonly ConcurrentDictionary<string, AsyncReaderWriterLock> _locks = new(StringComparer.Ordinal);

    public static async Task<IAsyncDisposable> AcquireReadAsync(string path, CancellationToken ct)
        => await Get(path).AcquireReadAsync(ct).ConfigureAwait(false);

    public static async Task<IAsyncDisposable> AcquireWriteAsync(string path, CancellationToken ct)
        => await Get(path).AcquireWriteAsync(ct).ConfigureAwait(false);

    public static async Task<IAsyncDisposable[]> AcquireManyWriteAsync(IEnumerable<string> paths, CancellationToken ct)
    {
        var ordered = paths.Distinct(StringComparer.Ordinal).OrderBy(p => p, StringComparer.Ordinal).ToArray();
        var leases = new List<IAsyncDisposable>(ordered.Length);
        try
        {
            foreach (var p in ordered)
            {
                leases.Add(await AcquireWriteAsync(p, ct).ConfigureAwait(false));
            }
            return leases.ToArray();
        }
        catch
        {
            foreach (var l in leases)
                await l.DisposeAsync();
            throw;
        }
    }

    private static AsyncReaderWriterLock Get(string path)
    {
        return _locks.GetOrAdd(path, _ => new AsyncReaderWriterLock());
    }
}
