using System.Diagnostics;

namespace FtpServer.Core.Server;

internal static class Throttle
{
    public static async Task<long> ApplyAsync(long sentBytes, long limitBytesPerSec, Stopwatch sw, CancellationToken ct)
    {
        if (limitBytesPerSec <= 0) return sentBytes;
        if (!sw.IsRunning) sw.Start();
        var elapsed = sw.Elapsed.TotalSeconds;
        if (elapsed <= 0) return sentBytes;
        var rate = sentBytes / elapsed;
        if (rate > limitBytesPerSec)
        {
            // Sleep proportionally to bring rate below limit
            var desiredSeconds = sentBytes / (double)limitBytesPerSec;
            var sleepMs = Math.Clamp((int)((desiredSeconds - elapsed) * 1000), 1, 5000);
            await Task.Delay(sleepMs, ct).ConfigureAwait(false);
        }
        return sentBytes;
    }
}
