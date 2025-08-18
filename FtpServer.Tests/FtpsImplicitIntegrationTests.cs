using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using FtpServer.Core.Configuration;
using FtpServer.Core.Server;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FtpServer.Tests;

public class FtpsImplicitIntegrationTests
{
    [Fact]
    public async Task ImplicitFtps_Accepts_Tls_And_Sends_Greeting()
    {
        string? enable = Environment.GetEnvironmentVariable("FTP_TEST_IMPLICIT");
        if (!string.Equals(enable, "true", StringComparison.OrdinalIgnoreCase) && enable != "1")
        {
            // Opt-in only; implicit FTPS can be environment-sensitive.
            return;
        }
        // Arrange options with implicit FTPS enabled
        int p = GetFreePort();
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
            string? greet = await reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(3));
            Assert.NotNull(greet);
            Assert.StartsWith("220", greet);

            // Politely terminate session
            using var writer = new StreamWriter(ssl) { AutoFlush = true, NewLine = "\r\n" };
            await writer.WriteLineAsync("QUIT");
            string? bye = await reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(3));
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
        int p = ((IPEndPoint)l.LocalEndpoint).Port;
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

    private sealed class DummyAuthFactory : Core.Abstractions.IAuthenticatorFactory
    {
        public Core.Abstractions.IAuthenticator Create(string name) => new Core.InMemory.InMemoryAuthenticator();
    }
    private sealed class DummyStoreFactory : Core.Abstractions.IStorageProviderFactory
    {
        public Core.Abstractions.IStorageProvider Create(string name) => new Core.InMemory.InMemoryStorageProvider();
    }
}
