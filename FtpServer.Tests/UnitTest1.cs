using System.Net.Sockets;
using System.Text;
using FtpServer.Core.InMemory;
using FtpServer.Core.Server;

namespace FtpServer.Tests;

public class UnitTest1
{
    [Fact]
    public async Task InMemoryAuthenticator_Works()
    {
        var auth = new InMemoryAuthenticator();
        auth.SetUser("u", "p");
        var ok = await auth.AuthenticateAsync("u", "p", CancellationToken.None);
        var bad = await auth.AuthenticateAsync("u", "x", CancellationToken.None);
        Assert.True(ok.Succeeded);
        Assert.False(bad.Succeeded);
    }
}
