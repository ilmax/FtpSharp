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
    var options = Microsoft.Extensions.Options.Options.Create(new FtpServer.Core.Configuration.FtpServerOptions());
    var session = new FtpServer.Core.Server.FtpSession(serverClient, auth, storage, options);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await Task.WhenAll(session.RunAsync(cts.Token), clientTask);
    }

    [Fact]
    public async Task Pasv_List_Works()
    {
        var storage = new InMemoryStorageProvider();
        await storage.CreateDirectoryAsync("/d", CancellationToken.None);
        await storage.WriteAsync("/d/a.txt", OneChunk("content"), CancellationToken.None);

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

            _ = await reader.ReadLineAsync(); // 220
            await writer.WriteLineAsync("USER u");
            _ = await reader.ReadLineAsync(); // 331
            await writer.WriteLineAsync("PASS p");
            _ = await reader.ReadLineAsync(); // 230
            await writer.WriteLineAsync("CWD /d");
            _ = await reader.ReadLineAsync(); // 250
            await writer.WriteLineAsync("PASV");
            var pasv = await reader.ReadLineAsync();
            Assert.StartsWith("227", pasv);
            var (dip, dport) = ParsePasv(pasv!);

            using var dataClient = new TcpClient();
            await dataClient.ConnectAsync(IPAddress.Parse(dip), dport);

            await writer.WriteLineAsync("LIST");
            var status150 = await reader.ReadLineAsync();
            Assert.StartsWith("150", status150);
            using var dr = new StreamReader(dataClient.GetStream(), Encoding.ASCII);
            var line = await dr.ReadLineAsync();
            Assert.EndsWith(" a.txt", line);
            var status226 = await reader.ReadLineAsync();
            Assert.StartsWith("226", status226);
            await writer.WriteLineAsync("QUIT");
            _ = await reader.ReadLineAsync();
        });

        using var serverClient = await listener.AcceptTcpClientAsync();
        var auth = new InMemoryAuthenticator();
        auth.SetUser("u", "p");
        var options = Microsoft.Extensions.Options.Options.Create(new FtpServer.Core.Configuration.FtpServerOptions());
        var session = new FtpServer.Core.Server.FtpSession(serverClient, auth, storage, options);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await Task.WhenAll(session.RunAsync(cts.Token), clientTask);
    }

    [Fact]
    public async Task Mkdir_Delete_File_And_Dir_Works()
    {
        var storage = new InMemoryStorageProvider();
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

            _ = await reader.ReadLineAsync();
            await writer.WriteLineAsync("USER u"); _ = await reader.ReadLineAsync();
            await writer.WriteLineAsync("PASS p"); _ = await reader.ReadLineAsync();
            await writer.WriteLineAsync("MKD /m"); var mk = await reader.ReadLineAsync(); Assert.StartsWith("257", mk);
            await writer.WriteLineAsync("CWD /m"); _ = await reader.ReadLineAsync();
            await writer.WriteLineAsync("PASV"); var pasv = await reader.ReadLineAsync(); var (dip, dport) = ParsePasv(pasv!);
            using var dataClient = new TcpClient(); await dataClient.ConnectAsync(IPAddress.Parse(dip), dport);
            await writer.WriteLineAsync("STOR f.txt"); _ = await reader.ReadLineAsync();
            await using (var ns = dataClient.GetStream())
            {
                var bytes = Encoding.ASCII.GetBytes("abc");
                await ns.WriteAsync(bytes, 0, bytes.Length);
            }
            var done = await reader.ReadLineAsync(); Assert.StartsWith("226", done);
            await writer.WriteLineAsync("DELE /m/f.txt"); _ = await reader.ReadLineAsync();
            await writer.WriteLineAsync("RMD /m"); var rm = await reader.ReadLineAsync(); Assert.StartsWith("250", rm);
            await writer.WriteLineAsync("QUIT"); _ = await reader.ReadLineAsync();
        });

        using var serverClient = await listener.AcceptTcpClientAsync();
        var auth = new InMemoryAuthenticator(); auth.SetUser("u", "p");
        var options = Microsoft.Extensions.Options.Options.Create(new FtpServer.Core.Configuration.FtpServerOptions());
        var session = new FtpServer.Core.Server.FtpSession(serverClient, auth, storage, options);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await Task.WhenAll(session.RunAsync(cts.Token), clientTask);
    }

    [Fact]
    public async Task Retr_Stor_Roundtrip_Binary_Works()
    {
        var storage = new InMemoryStorageProvider();
        await storage.WriteAsync("/file.bin", OneChunk("hello"), CancellationToken.None);
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

            _ = await reader.ReadLineAsync();
            await writer.WriteLineAsync("USER u"); _ = await reader.ReadLineAsync();
            await writer.WriteLineAsync("PASS p"); _ = await reader.ReadLineAsync();
            await writer.WriteLineAsync("TYPE I"); _ = await reader.ReadLineAsync();
            await writer.WriteLineAsync("PASV"); var pasv1 = await reader.ReadLineAsync(); var (dip1, dp1) = ParsePasv(pasv1!);
            using (var d1 = new TcpClient())
            {
                await d1.ConnectAsync(IPAddress.Parse(dip1), dp1);
                await writer.WriteLineAsync("RETR /file.bin"); _ = await reader.ReadLineAsync();
                using var ns = d1.GetStream();
                using var ms = new MemoryStream();
                await ns.CopyToAsync(ms);
                var done1 = await reader.ReadLineAsync(); Assert.StartsWith("226", done1);
                Assert.Equal("hello", Encoding.ASCII.GetString(ms.ToArray()));
            }

            await writer.WriteLineAsync("PASV"); var pasv2 = await reader.ReadLineAsync(); var (dip2, dp2) = ParsePasv(pasv2!);
            using (var d2 = new TcpClient())
            {
                await d2.ConnectAsync(IPAddress.Parse(dip2), dp2);
                await writer.WriteLineAsync("STOR /new.bin"); _ = await reader.ReadLineAsync();
                var buf = Encoding.ASCII.GetBytes("world");
                await d2.GetStream().WriteAsync(buf, 0, buf.Length);
            }
            var done2 = await reader.ReadLineAsync(); Assert.StartsWith("226", done2);
            await writer.WriteLineAsync("QUIT"); _ = await reader.ReadLineAsync();
        });

        using var serverClient = await listener.AcceptTcpClientAsync();
        var auth = new InMemoryAuthenticator(); auth.SetUser("u", "p");
        var options = Microsoft.Extensions.Options.Options.Create(new FtpServer.Core.Configuration.FtpServerOptions());
        var session = new FtpServer.Core.Server.FtpSession(serverClient, auth, storage, options);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await Task.WhenAll(session.RunAsync(cts.Token), clientTask);

        var size = await storage.GetSizeAsync("/new.bin", CancellationToken.None);
        Assert.Equal(5, size);
    }

    [Fact]
    public async Task Port_Retr_Works()
    {
        var storage = new InMemoryStorageProvider();
        await storage.WriteAsync("/p.txt", OneChunk("x"), CancellationToken.None);
        var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); var ep = (IPEndPoint)listener.LocalEndpoint;

        var clientTask = Task.Run(async () =>
        {
            using var client = new TcpClient();
            await client.ConnectAsync(ep.Address, ep.Port);
            using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, leaveOpen: true);
            using var writer = new StreamWriter(stream, Encoding.ASCII) { NewLine = "\r\n", AutoFlush = true };

            _ = await reader.ReadLineAsync();
            await writer.WriteLineAsync("USER u"); _ = await reader.ReadLineAsync();
            await writer.WriteLineAsync("PASS p"); _ = await reader.ReadLineAsync();

            var dataListener = new TcpListener(IPAddress.Loopback, 0); dataListener.Start();
            var dEp = (IPEndPoint)dataListener.LocalEndpoint;
            var ip = dEp.Address.ToString().Replace('.', ',');
            var p1 = dEp.Port / 256; var p2 = dEp.Port % 256;
            await writer.WriteLineAsync($"PORT {ip},{p1},{p2}");
            _ = await reader.ReadLineAsync();
            var acceptTask = dataListener.AcceptTcpClientAsync();
            await writer.WriteLineAsync("RETR /p.txt"); _ = await reader.ReadLineAsync();
            using var dataClient = await acceptTask;
            using var ns = dataClient.GetStream();
            var b = new byte[1];
            var r = await ns.ReadAsync(b, 0, 1);
            Assert.Equal(1, r);
            var done = await reader.ReadLineAsync(); Assert.StartsWith("226", done);
        });

        using var serverClient = await listener.AcceptTcpClientAsync();
        var auth = new InMemoryAuthenticator(); auth.SetUser("u", "p");
        var options = Microsoft.Extensions.Options.Options.Create(new FtpServer.Core.Configuration.FtpServerOptions());
        var session = new FtpServer.Core.Server.FtpSession(serverClient, auth, storage, options);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await Task.WhenAll(session.RunAsync(cts.Token), clientTask);
    }

    private static (string, int) ParsePasv(string s)
    {
        var start = s.IndexOf('(');
        var end = s.IndexOf(')');
        var parts = s.Substring(start + 1, end - start - 1).Split(',');
        var ip = string.Join('.', parts[..4]);
        var port = int.Parse(parts[4]) * 256 + int.Parse(parts[5]);
        return (ip, port);
    }

    private static async IAsyncEnumerable<ReadOnlyMemory<byte>> OneChunk(string content)
    {
        yield return Encoding.ASCII.GetBytes(content);
        await Task.CompletedTask;
    }
}
