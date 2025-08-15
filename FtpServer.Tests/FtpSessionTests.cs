using System.Net;
using System.Net.Sockets;
using System.Text;
using FtpServer.Core.InMemory;
using FtpServer.Core.Server;

namespace FtpServer.Tests;

public class FtpSessionTests
{
    [Fact]
    public async Task Login_And_Quit_Works()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var ep = (IPEndPoint)listener.LocalEndpoint;

        var clientTask = Task.Run(async () =>
        {
            using var client = new TcpClient();
            await client.ConnectAsync(ep.Address, ep.Port);
            using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, leaveOpen: true);
            using var writer = new StreamWriter(stream, Encoding.ASCII) { NewLine = "\r\n", AutoFlush = true };

            var greet = await reader.ReadLineAsync();
            Assert.StartsWith("220", greet);
            await writer.WriteLineAsync("USER u");
            var resp1 = await reader.ReadLineAsync();
            Assert.StartsWith("331", resp1);
            await writer.WriteLineAsync("PASS p");
            var resp2 = await reader.ReadLineAsync();
            Assert.StartsWith("230", resp2);
            await writer.WriteLineAsync("QUIT");
            var bye = await reader.ReadLineAsync();
            Assert.StartsWith("221", bye);
        });

        using var serverClient = await listener.AcceptTcpClientAsync();
        var auth = new InMemoryAuthenticator();
        auth.SetUser("u", "p");
        var storage = new InMemoryStorageProvider();
        var session = new FtpServer.Core.Server.FtpSession(serverClient, auth, storage);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await Task.WhenAll(session.RunAsync(cts.Token), clientTask);
    }
}
