using System.Net;
using System.Net.Sockets;
using System.Text;
using FtpServer.Core.InMemory;

namespace FtpServer.Tests;

public class ActiveModeTests
{
    [Fact]
    public async Task PORT_Retr_Works()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var ep = (IPEndPoint)listener.LocalEndpoint;
        var auth = new InMemoryAuthenticator(); auth.SetUser("u", "p");
        var storage = new InMemoryStorageProvider();
        await storage.WriteAsync("/file.txt", OneChunk("ABC"), CancellationToken.None);
        var options = Microsoft.Extensions.Options.Options.Create(new Core.Configuration.FtpServerOptions());

        var clientTask = Task.Run(async () =>
        {
            using var ctrl = new TcpClient(); await ctrl.ConnectAsync(ep.Address, ep.Port);
            using var stream = ctrl.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, true);
            using var writer = new StreamWriter(stream, Encoding.ASCII) { NewLine = "\r\n", AutoFlush = true };
            _ = await reader.ReadLineAsync();
            await writer.WriteLineAsync("USER u"); _ = await reader.ReadLineAsync();
            await writer.WriteLineAsync("PASS p"); _ = await reader.ReadLineAsync();

            // Client data listener
            var dataListener = new TcpListener(IPAddress.Loopback, 0); dataListener.Start(); var dep = (IPEndPoint)dataListener.LocalEndpoint;
            int p1 = dep.Port / 256, p2 = dep.Port % 256;
            await writer.WriteLineAsync($"PORT 127,0,0,1,{p1},{p2}"); _ = await reader.ReadLineAsync();

            await writer.WriteLineAsync("RETR /file.txt"); _ = await reader.ReadLineAsync();
            using var dc = await dataListener.AcceptTcpClientAsync();
            var buf = new byte[16]; var n = await dc.GetStream().ReadAsync(buf, 0, buf.Length);
            dc.Close();
            var done = await reader.ReadLineAsync(); Assert.StartsWith("226", done);
            Assert.Equal("ABC", Encoding.ASCII.GetString(buf, 0, n));
        });

        using var serverClient = await listener.AcceptTcpClientAsync();
        var session = new Core.Server.FtpSession(serverClient, auth, storage, options);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await Task.WhenAll(clientTask, session.RunAsync(cts.Token)); listener.Stop();
    }

    [Fact]
    public async Task EPRT_Stor_Works()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var ep = (IPEndPoint)listener.LocalEndpoint;
        var auth = new InMemoryAuthenticator(); auth.SetUser("u", "p");
        var storage = new InMemoryStorageProvider();
        var options = Microsoft.Extensions.Options.Options.Create(new Core.Configuration.FtpServerOptions());

        var clientTask = Task.Run(async () =>
        {
            using var ctrl = new TcpClient(); await ctrl.ConnectAsync(ep.Address, ep.Port);
            using var stream = ctrl.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, true);
            using var writer = new StreamWriter(stream, Encoding.ASCII) { NewLine = "\r\n", AutoFlush = true };
            _ = await reader.ReadLineAsync();
            await writer.WriteLineAsync("USER u"); _ = await reader.ReadLineAsync();
            await writer.WriteLineAsync("PASS p"); _ = await reader.ReadLineAsync();

            var dataListener = new TcpListener(IPAddress.Loopback, 0); dataListener.Start(); var dep = (IPEndPoint)dataListener.LocalEndpoint;
            await writer.WriteLineAsync($"EPRT |1|127.0.0.1|{dep.Port}|"); _ = await reader.ReadLineAsync();
            await writer.WriteLineAsync("STOR /eprt.bin"); _ = await reader.ReadLineAsync();
            using var dc = await dataListener.AcceptTcpClientAsync();
            var payload = Encoding.ASCII.GetBytes("DATA"); await dc.GetStream().WriteAsync(payload, 0, payload.Length);
            dc.Close();
            var done = await reader.ReadLineAsync(); Assert.StartsWith("226", done);
        });

        using var serverClient = await listener.AcceptTcpClientAsync();
        var session = new Core.Server.FtpSession(serverClient, auth, storage, options);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await Task.WhenAll(clientTask, session.RunAsync(cts.Token)); listener.Stop();

        var size = await storage.GetSizeAsync("/eprt.bin", CancellationToken.None);
        Assert.Equal(4, size);
    }

    private static async IAsyncEnumerable<ReadOnlyMemory<byte>> OneChunk(string s)
    {
        yield return Encoding.ASCII.GetBytes(s);
        await Task.CompletedTask;
    }
}
