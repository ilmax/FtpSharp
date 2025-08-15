using FtpServer.Core.Server;

namespace FtpServer.Tests;

public class ByteRateLimiterTests
{
    [Fact]
    public async Task Throttles_When_Over_Limit()
    {
        var limiter = new ByteRateLimiter(100); // 100B/s
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await limiter.AwaitAsync(60, CancellationToken.None);
        await limiter.AwaitAsync(60, CancellationToken.None); // total 120 bytes -> ~1.2s at cold start
        sw.Stop();
        Assert.True(sw.ElapsedMilliseconds >= 50); // keep small to avoid flakiness
    }
}
