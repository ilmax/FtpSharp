using System.Net.Sockets;
using FtpServer.Core.Configuration;
using FtpServer.Core.Server;
using Microsoft.Extensions.Options;

namespace FtpServer.Tests;

public class PassivePortPoolTests
{
    [Fact]
    public async Task Lease_And_Release_Works()
    {
        var opts = Options.Create(new FtpServerOptions { PassivePortRangeStart = 58000, PassivePortRangeEnd = 58010 });
        var pool = new PassivePortPool(opts);
        await using var lease = await pool.LeaseAsync(CancellationToken.None);
        Assert.InRange(lease.Port, 58000, 58010);
        // Ensure listener is accepting
        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", lease.Port);
    }
}
