using System.Diagnostics;

namespace FtpServer.Core.Server;

internal static class Throttle
{
    public static async Task<long> ApplyAsync(long sentBytes, long limitBytesPerSec, Stopwatch sw, CancellationToken ct)
    {
        if (limitBytesPerSec <= 0) return sentBytes;
        if (!sw.IsRunning) sw.Start();
        double elapsed = sw.Elapsed.TotalSeconds;
        if (elapsed <= 0) return sentBytes;
        double rate = sentBytes / elapsed;
        if (rate > limitBytesPerSec)
        {
            // Sleep proportionally to bring rate below limit
            double desiredSeconds = sentBytes / (double)limitBytesPerSec;
            int sleepMs = Math.Clamp((int)((desiredSeconds - elapsed) * 1000), 1, 5000);
            await Task.Delay(sleepMs, ct).ConfigureAwait(false);
        }
        return sentBytes;
    }
}
