using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using FtpServer.Core.Configuration;
using FtpServer.Core.Protocol;
using FtpServer.Core.Server;
using FtpServer.Core.Server.Commands;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FtpServer.Tests;

public class FtpsHandlerUnitTests
{
    private sealed class FakeCtx : IFtpSessionContext
    {
        public string Cwd { get; set; } = "/";
        public char TransferType { get; set; } = 'I';
        public bool IsAuthenticated { get; set; }
        public string? PendingUser { get; set; }
        public bool ShouldQuit { get; set; }
        public string? PendingRenameFrom { get; set; }
        public System.Net.IPEndPoint? ActiveEndpoint { get; set; }
        public bool IsControlTls { get; set; }
        public char DataProtectionLevel { get; set; } = 'C';
        public string ResolvePath(string arg) => arg;
        public Task<Stream> OpenDataStreamAsync(CancellationToken ct) => Task.FromException<Stream>(new IOException());
        public PassiveEndpoint EnterPassiveMode() => new PassiveEndpoint("127.0.0.1", 0);
        public Task<Stream> UpgradeControlToTlsAsync(CancellationToken ct) => Task.FromResult<Stream>(Stream.Null);
    }

    [Fact]
    public async Task AuthTlsHandler_SetsFlag_And_Returns234()
    {
        var h = new AuthTlsHandler();
        var ctx = new FakeCtx();
        using var ms = new MemoryStream();
        using var w = new StreamWriter(ms) { AutoFlush = true, NewLine = "\r\n" };
        await h.HandleAsync(ctx, new ParsedCommand("AUTH", "TLS"), w, CancellationToken.None);
        var resp = Encoding.ASCII.GetString(ms.ToArray());
        Assert.True(ctx.IsControlTls);
        Assert.Contains("234", resp);
    }

    [Fact]
    public async Task PbszHandler_OnlyAcceptsZero()
    {
        var h = new PbszHandler();
        var ctx = new FakeCtx();
        using var ms = new MemoryStream();
        using var w = new StreamWriter(ms) { AutoFlush = true, NewLine = "\r\n" };
        await h.HandleAsync(ctx, new ParsedCommand("PBSZ", "1"), w, CancellationToken.None);
        await h.HandleAsync(ctx, new ParsedCommand("PBSZ", "0"), w, CancellationToken.None);
        var resp = Encoding.ASCII.GetString(ms.ToArray());
        Assert.Contains("501", resp);
        Assert.Contains("200 PBSZ=0", resp);
    }

    [Fact]
    public async Task ProtHandler_Sets_C_And_P()
    {
        var h = new ProtHandler();
        var ctx = new FakeCtx();
        using var ms = new MemoryStream();
        using var w = new StreamWriter(ms) { AutoFlush = true, NewLine = "\r\n" };
        await h.HandleAsync(ctx, new ParsedCommand("PROT", "P"), w, CancellationToken.None);
        Assert.Equal('P', ctx.DataProtectionLevel);
        await h.HandleAsync(ctx, new ParsedCommand("PROT", "C"), w, CancellationToken.None);
        Assert.Equal('C', ctx.DataProtectionLevel);
        await h.HandleAsync(ctx, new ParsedCommand("PROT", "X"), w, CancellationToken.None);
        var resp = Encoding.ASCII.GetString(ms.ToArray());
        Assert.Contains("200 PROT set to P", resp);
        Assert.Contains("200 PROT set to C", resp);
        Assert.Contains("504", resp);
    }
}

public class FtpsImplicitIntegrationTests
{
    [Fact]
    public async Task ImplicitFtps_Accepts_Tls_And_Sends_Greeting()
    {
        var enable = Environment.GetEnvironmentVariable("FTP_TEST_IMPLICIT");
        if (!string.Equals(enable, "true", StringComparison.OrdinalIgnoreCase) && enable != "1")
        {
            // Opt-in only; implicit FTPS can be environment-sensitive.
            return;
        }
        // Arrange options with implicit FTPS enabled
        var p = GetFreePort();
        var opts = Options.Create(new FtpServerOptions
        {
            ListenAddress = "127.0.0.1",
            Port = GetFreePort(),
            FtpsImplicitEnabled = true,
            FtpsImplicitPort = p,
            PassivePortRangeStart = 58200,
            PassivePortRangeEnd = 58210,
            TlsSelfSigned = true,
        });
        var host = new FtpServerHost(opts, NullLogger<FtpServerHost>.Instance, new DummyAuthFactory(), new DummyStoreFactory(), new PassivePortPool(opts), new TlsCertificateProvider());
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        await host.StartAsync(cts.Token);
        // Wait briefly until implicit port is accepting
        await WaitUntilAcceptingAsync(p, TimeSpan.FromSeconds(5));

        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, p);

            using var ssl = new SslStream(client.GetStream(), leaveInnerStreamOpen: false, (s, cert, chain, err) => true);
            using var authCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
            {
                TargetHost = "localhost",
                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
            }, authCts.Token);

            using var reader = new StreamReader(ssl, Encoding.ASCII, false, 1024, true);
            var greet = await reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(3));
            Assert.NotNull(greet);
            Assert.StartsWith("220", greet);

            // Politely terminate session
            using var writer = new StreamWriter(ssl) { AutoFlush = true, NewLine = "\r\n" };
            await writer.WriteLineAsync("QUIT");
            var bye = await reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(3));
            Assert.NotNull(bye);
        }
        finally
        {
            cts.Cancel();
            await host.DisposeAsync();
        }
    }

    private static int GetFreePort()
    {
        var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        var p = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return p;
    }

    private static async Task WaitUntilAcceptingAsync(int port, TimeSpan timeout)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            try
            {
                using var c = new TcpClient();
                var ct = new CancellationTokenSource(TimeSpan.FromMilliseconds(200)).Token;
                await c.ConnectAsync(IPAddress.Loopback, port, ct);
                return;
            }
            catch { await Task.Delay(50); }
        }
    }

    private sealed class DummyAuthFactory : FtpServer.Core.Abstractions.IAuthenticatorFactory
    {
        public FtpServer.Core.Abstractions.IAuthenticator Create(string name) => new FtpServer.Core.InMemory.InMemoryAuthenticator();
    }
    private sealed class DummyStoreFactory : FtpServer.Core.Abstractions.IStorageProviderFactory
    {
        public FtpServer.Core.Abstractions.IStorageProvider Create(string name) => new FtpServer.Core.InMemory.InMemoryStorageProvider();
    }
}
