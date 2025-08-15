using System.Net;
using System.Net.Sockets;
using FtpServer.Core.Configuration;
using Microsoft.Extensions.Options;

namespace FtpServer.Core.Server;

public sealed class PassivePortPool : IAsyncDisposable
{
    private readonly IOptions<FtpServerOptions> _options;

    public PassivePortPool(IOptions<FtpServerOptions> options)
    {
        _options = options;
    }

    public async Task<PassiveLease> LeaseAsync(CancellationToken ct)
    {
        var start = _options.Value.PassivePortRangeStart;
        var end = _options.Value.PassivePortRangeEnd;
        var span = Enumerable.Range(start, end - start + 1).ToArray();
        var rng = new Random(unchecked(Environment.TickCount * 7919) ^ Guid.NewGuid().GetHashCode());
        for (int i = span.Length - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (span[i], span[j]) = (span[j], span[i]);
        }
        foreach (var p in span)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var l = new TcpListener(IPAddress.Loopback, p);
                l.Start();
                return new PassiveLease(p, l, this);
            }
            catch
            {
                // busy; try next
            }
        }
        throw new IOException("No passive ports available");
    }

    internal void Release(int port, TcpListener listener)
    {
        try { listener.Stop(); } catch { }
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

public sealed class PassiveLease : IAsyncDisposable
{
    private readonly PassivePortPool _pool;
    internal TcpListener Listener { get; }
    public int Port { get; }
    private int _disposed;

    internal PassiveLease(int port, TcpListener listener, PassivePortPool pool)
    {
        Port = port;
        Listener = listener;
        _pool = pool;
    }

    public ValueTask DisposeAsync()
    {
        if (System.Threading.Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _pool.Release(Port, Listener);
        }
        return ValueTask.CompletedTask;
    }
}
