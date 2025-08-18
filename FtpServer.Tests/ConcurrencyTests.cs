using System.Net;
using System.Net.Sockets;
using System.Text;
using FtpServer.Core.InMemory;

namespace FtpServer.Tests;

public class ConcurrencyTests
{
    [Fact]
    public async Task Multiple_Sessions_Handle_Parallel_Logins()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var ep = (IPEndPoint)listener.LocalEndpoint;

        var clients = Enumerable.Range(0, 10).Select(async i =>
            {
                using var client = new TcpClient();
                await client.ConnectAsync(ep.Address, ep.Port);
                using var stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, leaveOpen: true);
                using var writer = new StreamWriter(stream, Encoding.ASCII) { NewLine = "\r\n", AutoFlush = true };

                string? greet = await reader.ReadLineAsync();
                Assert.StartsWith("220", greet);
                await writer.WriteLineAsync("USER u"); await reader.ReadLineAsync();
                await writer.WriteLineAsync("PASS p"); string? ok = await reader.ReadLineAsync();
                Assert.StartsWith("230", ok);
                await writer.WriteLineAsync("QUIT"); string? bye = await reader.ReadLineAsync();
                Assert.StartsWith("221", bye);
            }).ToArray();

        var auth = new InMemoryAuthenticator(); auth.SetUser("u", "p");
        var storage = new InMemoryStorageProvider();
        var options = Microsoft.Extensions.Options.Options.Create(new Core.Configuration.FtpServerOptions());

        var serverTasks = new List<Task>();
        for (int i = 0; i < clients.Length; i++)
        {
            var serverClient = await listener.AcceptTcpClientAsync();
            var session = new Core.Server.FtpSession(serverClient, auth, storage, options);
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            serverTasks.Add(session.RunAsync(cts.Token));
        }

        await Task.WhenAll(clients);
        listener.Stop();
        await Task.WhenAll(serverTasks);
    }
}
