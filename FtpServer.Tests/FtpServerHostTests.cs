using System.Net;
using System.Net.Sockets;
using FtpServer.Core.Abstractions;
using FtpServer.Core.Configuration;
using FtpServer.Core.Server;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FtpServer.Tests;

public class FtpServerHostTests
{
    private sealed class DummyAuthFactory : IAuthenticatorFactory
    {
        public IAuthenticator Create(string name) => new Core.InMemory.InMemoryAuthenticator();
    }
    private sealed class DummyStoreFactory : IStorageProviderFactory
    {
        public Core.Abstractions.IStorageProvider Create(string name) => new Core.InMemory.InMemoryStorageProvider();
    }

    [Fact]
    public async Task Host_Starts_Accepts_And_Disposes()
    {
        var opts = Options.Create(new FtpServerOptions
        {
            ListenAddress = "127.0.0.1",
            Port = GetFreePort(),
            PassivePortRangeStart = 58020,
            PassivePortRangeEnd = 58030,
        });
        var pool = new PassivePortPool(opts);
        var cert = new TlsCertificateProvider();
        var host = new FtpServerHost(opts, NullLogger<FtpServerHost>.Instance, new DummyAuthFactory(), new DummyStoreFactory(), pool, cert);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await host.StartAsync(cts.Token);

        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, opts.Value.Port);
        var s = client.GetStream();
        using var reader = new StreamReader(s, System.Text.Encoding.ASCII, false, 1024, true);
        string? greet = await reader.ReadLineAsync();
        Assert.StartsWith("220", greet);

        await host.DisposeAsync();
    }

    private static int GetFreePort()
    {
        var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        int p = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return p;
    }
}
