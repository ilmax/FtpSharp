using FtpServer.Core.Basic;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace FtpServer.Tests;

public class BasicAuthenticatorTests
{
    [Fact]
    public async Task Accepts_Configured_User()
    {
        var dict = new Dictionary<string, string?>
        {
            ["FtpServer:Users:alice"] = "secret"
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
        var auth = new BasicAuthenticator(config);
        var res = await auth.AuthenticateAsync("alice", "secret", default);
        Assert.True(res.Succeeded);
    }

    [Fact]
    public async Task Rejects_Wrong_Password()
    {
        var dict = new Dictionary<string, string?>
        {
            ["FtpServer:Users:bob"] = "pw"
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
        var auth = new BasicAuthenticator(config);
        var res = await auth.AuthenticateAsync("bob", "nope", default);
        Assert.False(res.Succeeded);
    }
}
