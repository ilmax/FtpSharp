using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace FtpServer.Core.Server;

internal sealed class ByteRateLimiter
{
    private readonly long _limitBytesPerSec;
    private readonly Stopwatch _sw = new();
    private long _consumed;
    private readonly object _gate = new();

    public ByteRateLimiter(long limitBytesPerSec)
    {
        _limitBytesPerSec = limitBytesPerSec;
    }

    public async Task AwaitAsync(int justProcessedBytes, CancellationToken ct)
    {
        if (_limitBytesPerSec <= 0 || justProcessedBytes <= 0) return;
        lock (_gate)
        {
            if (!_sw.IsRunning) _sw.Start();
            _consumed += justProcessedBytes;
        }
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            double elapsed;
            long consumed;
            lock (_gate)
            {
                elapsed = _sw.Elapsed.TotalSeconds;
                consumed = _consumed;
            }
            if (elapsed <= 0) break;
            var desiredSeconds = consumed / (double)_limitBytesPerSec;
            var deltaMs = (int)Math.Ceiling((desiredSeconds - elapsed) * 1000);
            if (deltaMs <= 0) break;
            await Task.Delay(Math.Min(deltaMs, 5000), ct).ConfigureAwait(false);
        }
    }
}
