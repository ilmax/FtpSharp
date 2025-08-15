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
    public async Task Size_On_Directory_Returns_550()
    {
        var storage = new InMemoryStorageProvider();
        await storage.CreateDirectoryAsync("/dir", CancellationToken.None);
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
            await writer.WriteLineAsync("SIZE /dir"); var resp = await reader.ReadLineAsync();
            Assert.StartsWith("550", resp);
        });

        using var serverClient = await listener.AcceptTcpClientAsync();
        var auth = new InMemoryAuthenticator(); auth.SetUser("u", "p");
        var options = Microsoft.Extensions.Options.Options.Create(new FtpServer.Core.Configuration.FtpServerOptions());
        var session = new FtpServer.Core.Server.FtpSession(serverClient, auth, storage, options);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await Task.WhenAll(session.RunAsync(cts.Token), clientTask);
    }

    [Fact]
    public async Task Cwd_To_File_Returns_550_And_Pwd_Unchanged()
    {
        var storage = new InMemoryStorageProvider();
        await storage.WriteAsync("/f.txt", OneChunk("x"), CancellationToken.None);
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
            await writer.WriteLineAsync("CWD /f.txt"); var resp = await reader.ReadLineAsync(); Assert.StartsWith("550", resp);
            await writer.WriteLineAsync("PWD"); var pwd = await reader.ReadLineAsync(); Assert.Contains("\"/\"", pwd);
        });

        using var serverClient = await listener.AcceptTcpClientAsync();
        var auth = new InMemoryAuthenticator(); auth.SetUser("u", "p");
        var options = Microsoft.Extensions.Options.Options.Create(new FtpServer.Core.Configuration.FtpServerOptions());
        var session = new FtpServer.Core.Server.FtpSession(serverClient, auth, storage, options);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await Task.WhenAll(session.RunAsync(cts.Token), clientTask);
    }
    [Fact]
    public async Task Type_Unsupported_Returns_504()
    {
        var storage = new InMemoryStorageProvider();
        var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); var ep = (IPEndPoint)listener.LocalEndpoint;

        var clientTask = Task.Run(async () =>
        {
            using var client = new TcpClient();
            await client.ConnectAsync(ep.Address, ep.Port);
            using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, leaveOpen: true);
            using var writer = new StreamWriter(stream, Encoding.ASCII) { NewLine = "\r\n", AutoFlush = true };

            _ = await reader.ReadLineAsync();
            await writer.WriteLineAsync("TYPE X"); var resp = await reader.ReadLineAsync();
            Assert.StartsWith("504", resp);
        });

        using var serverClient = await listener.AcceptTcpClientAsync();
        var auth = new InMemoryAuthenticator(); auth.SetUser("u", "p");
        var options = Microsoft.Extensions.Options.Options.Create(new FtpServer.Core.Configuration.FtpServerOptions());
        var session = new FtpServer.Core.Server.FtpSession(serverClient, auth, storage, options);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await Task.WhenAll(session.RunAsync(cts.Token), clientTask);
    }

    [Fact]
    public async Task Feat_Contains_Core_Features()
    {
        var storage = new InMemoryStorageProvider();
        var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); var ep = (IPEndPoint)listener.LocalEndpoint;

        var clientTask = Task.Run(async () =>
        {
            using var client = new TcpClient();
            await client.ConnectAsync(ep.Address, ep.Port);
            using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, leaveOpen: true);
            using var writer = new StreamWriter(stream, Encoding.ASCII) { NewLine = "\r\n", AutoFlush = true };

            _ = await reader.ReadLineAsync();
            await writer.WriteLineAsync("FEAT");
            var l1 = await reader.ReadLineAsync(); Assert.StartsWith("211-", l1);
            var features = new List<string>(); string? line;
            while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync()) && !line.StartsWith("211 End"))
                features.Add(line!.Trim());
            Assert.Contains("UTF8", features);
            Assert.Contains("PASV", features);
            Assert.Contains("EPSV", features);
            Assert.Contains("PORT", features);
            Assert.Contains("EPRT", features);
            Assert.Contains("SIZE", features);
            Assert.Contains("TYPE A;I", features);
        });

        using var serverClient = await listener.AcceptTcpClientAsync();
        var auth = new InMemoryAuthenticator(); auth.SetUser("u", "p");
        var options = Microsoft.Extensions.Options.Options.Create(new FtpServer.Core.Configuration.FtpServerOptions());
        var session = new FtpServer.Core.Server.FtpSession(serverClient, auth, storage, options);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await Task.WhenAll(session.RunAsync(cts.Token), clientTask);
    }

    [Fact]
    public async Task Rmd_NonEmpty_Returns_550()
    {
        var storage = new InMemoryStorageProvider();
        await storage.CreateDirectoryAsync("/dir", CancellationToken.None);
        await storage.WriteAsync("/dir/a.txt", OneChunk("x"), CancellationToken.None);
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
            await writer.WriteLineAsync("RMD /dir"); var resp = await reader.ReadLineAsync();
            Assert.StartsWith("550", resp);
        });

        using var serverClient = await listener.AcceptTcpClientAsync();
        var auth = new InMemoryAuthenticator(); auth.SetUser("u", "p");
        var options = Microsoft.Extensions.Options.Options.Create(new FtpServer.Core.Configuration.FtpServerOptions());
        var session = new FtpServer.Core.Server.FtpSession(serverClient, auth, storage, options);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await Task.WhenAll(session.RunAsync(cts.Token), clientTask);
    }
    [Fact]
    public async Task Stor_Creates_Missing_Parent_And_Succeeds()
    {
        var storage = new InMemoryStorageProvider();
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
            await writer.WriteLineAsync("PASV"); var pasv = await reader.ReadLineAsync(); var (dip, dport) = ParsePasv(pasv!);
            using var dc = new TcpClient(); await dc.ConnectAsync(IPAddress.Parse(dip), dport);
            await writer.WriteLineAsync("STOR /z/new.bin"); _ = await reader.ReadLineAsync();
            var buf = Encoding.ASCII.GetBytes("hi");
            var dns = dc.GetStream();
            await dns.WriteAsync(buf, 0, buf.Length);
            await dns.FlushAsync();
            dc.Close();
            var done = await reader.ReadLineAsync(); Assert.StartsWith("226", done);
        });

        using var serverClient = await listener.AcceptTcpClientAsync();
        var auth = new InMemoryAuthenticator(); auth.SetUser("u", "p");
        var options = Microsoft.Extensions.Options.Options.Create(new FtpServer.Core.Configuration.FtpServerOptions());
        var session = new FtpServer.Core.Server.FtpSession(serverClient, auth, storage, options);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await Task.WhenAll(session.RunAsync(cts.Token), clientTask);

        var size = await storage.GetSizeAsync("/z/new.bin", CancellationToken.None);
        Assert.Equal(2, size);
    }

    [Fact]
    public async Task Dele_Missing_Returns_550()
    {
        var storage = new InMemoryStorageProvider();
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
            await writer.WriteLineAsync("DELE /missing.txt"); var resp = await reader.ReadLineAsync();
            Assert.StartsWith("550", resp);
        });

        using var serverClient = await listener.AcceptTcpClientAsync();
        var auth = new InMemoryAuthenticator(); auth.SetUser("u", "p");
        var options = Microsoft.Extensions.Options.Options.Create(new FtpServer.Core.Configuration.FtpServerOptions());
        var session = new FtpServer.Core.Server.FtpSession(serverClient, auth, storage, options);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await Task.WhenAll(session.RunAsync(cts.Token), clientTask);
    }
    [Fact]
    public async Task Port_Unreachable_List_Returns_425()
    {
        var storage = new InMemoryStorageProvider();
        var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); var ep = (IPEndPoint)listener.LocalEndpoint;

        // Prepare a port that will be closed/unreachable
        var temp = new TcpListener(IPAddress.Loopback, 0); temp.Start(); var badPort = ((IPEndPoint)temp.LocalEndpoint).Port; temp.Stop();

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
            var ip = IPAddress.Loopback.ToString().Replace('.', ',');
            var p1 = badPort / 256; var p2 = badPort % 256;
            await writer.WriteLineAsync($"PORT {ip},{p1},{p2}"); _ = await reader.ReadLineAsync();
            await writer.WriteLineAsync("LIST"); var r150 = await reader.ReadLineAsync(); Assert.StartsWith("150", r150);
            var r425 = await reader.ReadLineAsync(); Assert.StartsWith("425", r425);
        });

        using var serverClient = await listener.AcceptTcpClientAsync();
        var auth = new InMemoryAuthenticator(); auth.SetUser("u", "p");
        var options = Microsoft.Extensions.Options.Options.Create(new FtpServer.Core.Configuration.FtpServerOptions());
        var session = new FtpServer.Core.Server.FtpSession(serverClient, auth, storage, options);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await Task.WhenAll(session.RunAsync(cts.Token), clientTask);
    }

    [Fact]
    public async Task Eprt_With_Ipv6_Family_And_Ipv4_Address_Returns_501()
    {
        var storage = new InMemoryStorageProvider();
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
            await writer.WriteLineAsync("EPRT |2|127.0.0.1|65000|"); var resp = await reader.ReadLineAsync();
            Assert.StartsWith("501", resp);
        });

        using var serverClient = await listener.AcceptTcpClientAsync();
        var auth = new InMemoryAuthenticator(); auth.SetUser("u", "p");
        var options = Microsoft.Extensions.Options.Options.Create(new FtpServer.Core.Configuration.FtpServerOptions());
        var session = new FtpServer.Core.Server.FtpSession(serverClient, auth, storage, options);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await Task.WhenAll(session.RunAsync(cts.Token), clientTask);
    }
    [Fact]
    public async Task Nlst_Without_Login_Returns_530()
    {
        var storage = new InMemoryStorageProvider();
        var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); var ep = (IPEndPoint)listener.LocalEndpoint;

        var clientTask = Task.Run(async () =>
        {
            using var client = new TcpClient();
            await client.ConnectAsync(ep.Address, ep.Port);
            using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, leaveOpen: true);
            using var writer = new StreamWriter(stream, Encoding.ASCII) { NewLine = "\r\n", AutoFlush = true };

            _ = await reader.ReadLineAsync();
            await writer.WriteLineAsync("NLST"); var resp = await reader.ReadLineAsync();
            Assert.StartsWith("530", resp);
        });

        using var serverClient = await listener.AcceptTcpClientAsync();
        var auth = new InMemoryAuthenticator(); auth.SetUser("u", "p");
        var options = Microsoft.Extensions.Options.Options.Create(new FtpServer.Core.Configuration.FtpServerOptions());
        var session = new FtpServer.Core.Server.FtpSession(serverClient, auth, storage, options);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await Task.WhenAll(session.RunAsync(cts.Token), clientTask);
    }

    [Fact]
    public async Task Cwd_Missing_Returns_550_And_Pwd_Unchanged()
    {
        var storage = new InMemoryStorageProvider();
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
            await writer.WriteLineAsync("CWD /nope"); var resp = await reader.ReadLineAsync(); Assert.StartsWith("550", resp);
            await writer.WriteLineAsync("PWD"); var pwd = await reader.ReadLineAsync(); Assert.Contains("\"/\"", pwd);
        });

        using var serverClient = await listener.AcceptTcpClientAsync();
        var auth = new InMemoryAuthenticator(); auth.SetUser("u", "p");
        var options = Microsoft.Extensions.Options.Options.Create(new FtpServer.Core.Configuration.FtpServerOptions());
        var session = new FtpServer.Core.Server.FtpSession(serverClient, auth, storage, options);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await Task.WhenAll(session.RunAsync(cts.Token), clientTask);
    }

    [Fact]
    public async Task Abor_Not_Implemented_Returns_502()
    {
        var storage = new InMemoryStorageProvider();
        var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); var ep = (IPEndPoint)listener.LocalEndpoint;

        var clientTask = Task.Run(async () =>
        {
            using var client = new TcpClient();
            await client.ConnectAsync(ep.Address, ep.Port);
            using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, leaveOpen: true);
            using var writer = new StreamWriter(stream, Encoding.ASCII) { NewLine = "\r\n", AutoFlush = true };

            _ = await reader.ReadLineAsync();
            await writer.WriteLineAsync("ABOR"); var resp = await reader.ReadLineAsync();
            Assert.StartsWith("502", resp);
        });

        using var serverClient = await listener.AcceptTcpClientAsync();
        var auth = new InMemoryAuthenticator(); auth.SetUser("u", "p");
        var options = Microsoft.Extensions.Options.Options.Create(new FtpServer.Core.Configuration.FtpServerOptions());
        var session = new FtpServer.Core.Server.FtpSession(serverClient, auth, storage, options);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await Task.WhenAll(session.RunAsync(cts.Token), clientTask);
    }

    [Fact]
    public async Task Rnfr_Missing_Returns_550()
    {
        var storage = new InMemoryStorageProvider();
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
            await writer.WriteLineAsync("RNFR /notthere.txt"); var resp = await reader.ReadLineAsync();
            Assert.StartsWith("550", resp);
        });

        using var serverClient = await listener.AcceptTcpClientAsync();
        var auth = new InMemoryAuthenticator(); auth.SetUser("u", "p");
        var options = Microsoft.Extensions.Options.Options.Create(new FtpServer.Core.Configuration.FtpServerOptions());
        var session = new FtpServer.Core.Server.FtpSession(serverClient, auth, storage, options);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await Task.WhenAll(session.RunAsync(cts.Token), clientTask);
    }
    [Fact]
    public async Task Rnto_Without_Rnfr_Returns_503()
    {
        var storage = new InMemoryStorageProvider();
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
            await writer.WriteLineAsync("RNTO /any.txt"); var resp = await reader.ReadLineAsync();
            Assert.StartsWith("503", resp);
        });

        using var serverClient = await listener.AcceptTcpClientAsync();
        var auth = new InMemoryAuthenticator(); auth.SetUser("u", "p");
        var options = Microsoft.Extensions.Options.Options.Create(new FtpServer.Core.Configuration.FtpServerOptions());
        var session = new FtpServer.Core.Server.FtpSession(serverClient, auth, storage, options);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await Task.WhenAll(session.RunAsync(cts.Token), clientTask);
    }

    [Fact]
    public async Task Size_Missing_File_Returns_550()
    {
        var storage = new InMemoryStorageProvider();
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
            await writer.WriteLineAsync("SIZE /missing.txt"); var resp = await reader.ReadLineAsync();
            Assert.StartsWith("550", resp);
        });

        using var serverClient = await listener.AcceptTcpClientAsync();
        var auth = new InMemoryAuthenticator(); auth.SetUser("u", "p");
        var options = Microsoft.Extensions.Options.Options.Create(new FtpServer.Core.Configuration.FtpServerOptions());
        var session = new FtpServer.Core.Server.FtpSession(serverClient, auth, storage, options);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await Task.WhenAll(session.RunAsync(cts.Token), clientTask);
    }

    [Fact]
    public async Task Eprt_Invalid_Returns_501()
    {
        var storage = new InMemoryStorageProvider();
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
            await writer.WriteLineAsync("EPRT nonsense"); var resp = await reader.ReadLineAsync();
            Assert.StartsWith("501", resp);
        });

        using var serverClient = await listener.AcceptTcpClientAsync();
        var auth = new InMemoryAuthenticator(); auth.SetUser("u", "p");
        var options = Microsoft.Extensions.Options.Options.Create(new FtpServer.Core.Configuration.FtpServerOptions());
        var session = new FtpServer.Core.Server.FtpSession(serverClient, auth, storage, options);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await Task.WhenAll(session.RunAsync(cts.Token), clientTask);
    }

    [Fact]
    public async Task List_Without_Login_Returns_530()
    {
        var storage = new InMemoryStorageProvider();
        var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); var ep = (IPEndPoint)listener.LocalEndpoint;

        var clientTask = Task.Run(async () =>
        {
            using var client = new TcpClient();
            await client.ConnectAsync(ep.Address, ep.Port);
            using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, leaveOpen: true);
            using var writer = new StreamWriter(stream, Encoding.ASCII) { NewLine = "\r\n", AutoFlush = true };

            _ = await reader.ReadLineAsync();
            await writer.WriteLineAsync("LIST"); var resp = await reader.ReadLineAsync();
            Assert.StartsWith("530", resp);
        });

        using var serverClient = await listener.AcceptTcpClientAsync();
        var auth = new InMemoryAuthenticator(); auth.SetUser("u", "p");
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

    [Fact]
    public async Task Epsv_List_Works()
    {
        var storage = new InMemoryStorageProvider();
        await storage.CreateDirectoryAsync("/d2", CancellationToken.None);
        await storage.WriteAsync("/d2/b.txt", OneChunk("y"), CancellationToken.None);
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
            await writer.WriteLineAsync("CWD /d2"); _ = await reader.ReadLineAsync();
            await writer.WriteLineAsync("EPSV"); var epsv = await reader.ReadLineAsync();
            var start = epsv!.IndexOf('(');
            var end = epsv!.IndexOf(')', start + 1);
            var inside = epsv!.Substring(start + 1, end - start - 1);
            var token = inside.Split('|', StringSplitOptions.RemoveEmptyEntries).First();
            var port = int.Parse(token);
            using var dataClient = new TcpClient(); await dataClient.ConnectAsync(IPAddress.Loopback, port);
            await writer.WriteLineAsync("LIST"); _ = await reader.ReadLineAsync();
            using var dr = new StreamReader(dataClient.GetStream(), Encoding.ASCII);
            var line = await dr.ReadLineAsync();
            Assert.EndsWith(" b.txt", line);
            var done = await reader.ReadLineAsync(); Assert.StartsWith("226", done);
        });

        using var serverClient = await listener.AcceptTcpClientAsync();
        var auth = new InMemoryAuthenticator(); auth.SetUser("u", "p");
        var options = Microsoft.Extensions.Options.Options.Create(new FtpServer.Core.Configuration.FtpServerOptions());
        var session = new FtpServer.Core.Server.FtpSession(serverClient, auth, storage, options);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await Task.WhenAll(session.RunAsync(cts.Token), clientTask);
    }

    [Fact]
    public async Task Eprt_Stor_Works()
    {
        var storage = new InMemoryStorageProvider();
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

            var dataListener = new TcpListener(IPAddress.Loopback, 0); dataListener.Start(); var dEp = (IPEndPoint)dataListener.LocalEndpoint;
            await writer.WriteLineAsync($"EPRT |1|{dEp.Address}|{dEp.Port}|"); _ = await reader.ReadLineAsync();
            var acceptTask = dataListener.AcceptTcpClientAsync();
            await writer.WriteLineAsync("STOR /e.bin"); _ = await reader.ReadLineAsync();
            using var dc = await acceptTask; using var ns = dc.GetStream();
            var bytes = Encoding.ASCII.GetBytes("z"); await ns.WriteAsync(bytes, 0, bytes.Length);
            await ns.FlushAsync();
            dc.Close();
            var done = await reader.ReadLineAsync(); Assert.StartsWith("226", done);
        });

        using var serverClient = await listener.AcceptTcpClientAsync();
        var auth = new InMemoryAuthenticator(); auth.SetUser("u", "p");
        var options = Microsoft.Extensions.Options.Options.Create(new FtpServer.Core.Configuration.FtpServerOptions());
        var session = new FtpServer.Core.Server.FtpSession(serverClient, auth, storage, options);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await Task.WhenAll(session.RunAsync(cts.Token), clientTask);

        var size = await storage.GetSizeAsync("/e.bin", CancellationToken.None);
        Assert.Equal(1, size);
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

    [Fact]
    public async Task Noop_And_Help_And_Stat_Work()
    {
        var storage = new InMemoryStorageProvider();
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

            await writer.WriteLineAsync("NOOP"); var noop = await reader.ReadLineAsync(); Assert.StartsWith("200", noop);

            await writer.WriteLineAsync("HELP");
            var h1 = await reader.ReadLineAsync(); Assert.StartsWith("214-", h1);
            var h2 = await reader.ReadLineAsync(); Assert.Contains("USER", h2);
            var h3 = await reader.ReadLineAsync(); Assert.StartsWith("214", h3);

            await writer.WriteLineAsync("STAT");
            var s1 = await reader.ReadLineAsync(); Assert.StartsWith("211-", s1);
            string? line;
            do { line = await reader.ReadLineAsync(); } while (line is not null && !line.StartsWith("211 End"));

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
    public async Task Cdup_Changes_Directory()
    {
        var storage = new InMemoryStorageProvider();
        await storage.CreateDirectoryAsync("/x", CancellationToken.None);
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
            await writer.WriteLineAsync("CWD /x"); _ = await reader.ReadLineAsync();
            await writer.WriteLineAsync("CDUP"); var cdup = await reader.ReadLineAsync(); Assert.StartsWith("200", cdup);
            await writer.WriteLineAsync("PWD"); var pwd = await reader.ReadLineAsync(); Assert.Contains("\"/\"", pwd);
        });

        using var serverClient = await listener.AcceptTcpClientAsync();
        var auth = new InMemoryAuthenticator(); auth.SetUser("u", "p");
        var options = Microsoft.Extensions.Options.Options.Create(new FtpServer.Core.Configuration.FtpServerOptions());
        var session = new FtpServer.Core.Server.FtpSession(serverClient, auth, storage, options);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await Task.WhenAll(session.RunAsync(cts.Token), clientTask);
    }

    [Fact]
    public async Task Nlst_Lists_Names()
    {
        var storage = new InMemoryStorageProvider();
        await storage.CreateDirectoryAsync("/d3", CancellationToken.None);
        await storage.WriteAsync("/d3/a.txt", OneChunk("a"), CancellationToken.None);
        await storage.WriteAsync("/d3/b.txt", OneChunk("b"), CancellationToken.None);

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
            await writer.WriteLineAsync("CWD /d3"); _ = await reader.ReadLineAsync();
            await writer.WriteLineAsync("PASV"); var pasv = await reader.ReadLineAsync(); var (dip, dport) = ParsePasv(pasv!);
            using var dc = new TcpClient(); await dc.ConnectAsync(IPAddress.Parse(dip), dport);
            await writer.WriteLineAsync("NLST"); var s150 = await reader.ReadLineAsync(); Assert.StartsWith("150", s150);
            using var dr = new StreamReader(dc.GetStream(), Encoding.ASCII);
            var n1 = await dr.ReadLineAsync(); var n2 = await dr.ReadLineAsync();
            Assert.Contains(n1, new[] { "a.txt", "b.txt" });
            Assert.Contains(n2, new[] { "a.txt", "b.txt" });
            var s226 = await reader.ReadLineAsync(); Assert.StartsWith("226", s226);
        });

        using var serverClient = await listener.AcceptTcpClientAsync();
        var auth = new InMemoryAuthenticator(); auth.SetUser("u", "p");
        var options = Microsoft.Extensions.Options.Options.Create(new FtpServer.Core.Configuration.FtpServerOptions());
        var session = new FtpServer.Core.Server.FtpSession(serverClient, auth, storage, options);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await Task.WhenAll(session.RunAsync(cts.Token), clientTask);
    }

    [Fact]
    public async Task Size_And_Rename_Work()
    {
        var storage = new InMemoryStorageProvider();
        await storage.WriteAsync("/r.txt", OneChunk("xyz"), CancellationToken.None);
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
            await writer.WriteLineAsync("SIZE /r.txt"); var size = await reader.ReadLineAsync(); Assert.Equal("213 3", size);
            await writer.WriteLineAsync("RNFR /r.txt"); _ = await reader.ReadLineAsync();
            await writer.WriteLineAsync("RNTO /s.txt"); var rn = await reader.ReadLineAsync(); Assert.StartsWith("250", rn);
            await writer.WriteLineAsync("SIZE /s.txt"); var size2 = await reader.ReadLineAsync(); Assert.Equal("213 3", size2);
        });

        using var serverClient = await listener.AcceptTcpClientAsync();
        var auth = new InMemoryAuthenticator(); auth.SetUser("u", "p");
        var options = Microsoft.Extensions.Options.Options.Create(new FtpServer.Core.Configuration.FtpServerOptions());
        var session = new FtpServer.Core.Server.FtpSession(serverClient, auth, storage, options);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await Task.WhenAll(session.RunAsync(cts.Token), clientTask);
    }
}
