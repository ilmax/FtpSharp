using FtpServer.Core.InMemory;

namespace FtpServer.Tests;

public class AuthenticatorTests
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
