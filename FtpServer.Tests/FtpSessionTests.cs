using System.Net;
using System.Net.Sockets;
using System.Text;
using FtpServer.Core.InMemory;

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

            string? greet = await reader.ReadLineAsync();
            Assert.StartsWith("220", greet);
            await writer.WriteLineAsync("USER u");
            string? resp1 = await reader.ReadLineAsync();
            Assert.StartsWith("331", resp1);
            await writer.WriteLineAsync("PASS p");
            string? resp2 = await reader.ReadLineAsync();
            Assert.StartsWith("230", resp2);
            await writer.WriteLineAsync("QUIT");
            string? bye = await reader.ReadLineAsync();
            Assert.StartsWith("221", bye);
        });

        using var serverClient = await listener.AcceptTcpClientAsync();
        var auth = new InMemoryAuthenticator();
        auth.SetUser("u", "p");
        var storage = new InMemoryStorageProvider();
        var options = Microsoft.Extensions.Options.Options.Create(new Core.Configuration.FtpServerOptions());
        var session = new Core.Server.FtpSession(serverClient, auth, storage, options);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await Task.WhenAll(session.RunAsync(cts.Token), clientTask);
    }

    [Fact]
    public async Task Dele_On_Directory_Returns_550()
    {
        var storage = new InMemoryStorageProvider();
        await storage.CreateDirectoryAsync("/d1", CancellationToken.None);
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
            await writer.WriteLineAsync("DELE /d1"); string? resp = await reader.ReadLineAsync();
            Assert.StartsWith("550", resp);
        });

        using var serverClient = await listener.AcceptTcpClientAsync();
        var auth = new InMemoryAuthenticator(); auth.SetUser("u", "p");
        var options = Microsoft.Extensions.Options.Options.Create(new Core.Configuration.FtpServerOptions());
        var session = new Core.Server.FtpSession(serverClient, auth, storage, options);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await Task.WhenAll(session.RunAsync(cts.Token), clientTask);
    }

    [Fact]
    public async Task Mkd_On_Existing_Returns_550()
    {
        var storage = new InMemoryStorageProvider();
        await storage.CreateDirectoryAsync("/exists", CancellationToken.None);
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
            await writer.WriteLineAsync("MKD /exists"); string? resp = await reader.ReadLineAsync();
            Assert.StartsWith("550", resp);
        });

        using var serverClient = await listener.AcceptTcpClientAsync();
        var auth = new InMemoryAuthenticator(); auth.SetUser("u", "p");
        var options = Microsoft.Extensions.Options.Options.Create(new Core.Configuration.FtpServerOptions());
        var session = new Core.Server.FtpSession(serverClient, auth, storage, options);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await Task.WhenAll(session.RunAsync(cts.Token), clientTask);
    }

    [Fact]
    public async Task Retr_Missing_Returns_550()
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
            await writer.WriteLineAsync("RETR /missing.txt"); string? resp = await reader.ReadLineAsync();
            Assert.StartsWith("550", resp);
        });

        using var serverClient = await listener.AcceptTcpClientAsync();
        var auth = new InMemoryAuthenticator(); auth.SetUser("u", "p");
        var options = Microsoft.Extensions.Options.Options.Create(new Core.Configuration.FtpServerOptions());
        var session = new Core.Server.FtpSession(serverClient, auth, storage, options);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await Task.WhenAll(session.RunAsync(cts.Token), clientTask);
    }

    [Fact]
    public async Task Retr_On_Directory_Returns_550()
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
            await writer.WriteLineAsync("RETR /dir"); string? resp = await reader.ReadLineAsync();
            Assert.StartsWith("550", resp);
        });

        using var serverClient = await listener.AcceptTcpClientAsync();
        var auth = new InMemoryAuthenticator(); auth.SetUser("u", "p");
        var options = Microsoft.Extensions.Options.Options.Create(new Core.Configuration.FtpServerOptions());
        var session = new Core.Server.FtpSession(serverClient, auth, storage, options);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await Task.WhenAll(session.RunAsync(cts.Token), clientTask);
    }

    [Fact]
    public async Task Stor_On_Directory_Returns_550()
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
            await writer.WriteLineAsync("STOR /dir"); string? resp = await reader.ReadLineAsync();
            Assert.StartsWith("550", resp);
        });

        using var serverClient = await listener.AcceptTcpClientAsync();
        var auth = new InMemoryAuthenticator(); auth.SetUser("u", "p");
        var options = Microsoft.Extensions.Options.Options.Create(new Core.Configuration.FtpServerOptions());
        var session = new Core.Server.FtpSession(serverClient, auth, storage, options);
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
            await writer.WriteLineAsync("SIZE /dir"); string? resp = await reader.ReadLineAsync();
            Assert.StartsWith("550", resp);
        });

        using var serverClient = await listener.AcceptTcpClientAsync();
        var auth = new InMemoryAuthenticator(); auth.SetUser("u", "p");
        var options = Microsoft.Extensions.Options.Options.Create(new Core.Configuration.FtpServerOptions());
        var session = new Core.Server.FtpSession(serverClient, auth, storage, options);
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
            await writer.WriteLineAsync("CWD /f.txt"); string? resp = await reader.ReadLineAsync(); Assert.StartsWith("550", resp);
            await writer.WriteLineAsync("PWD"); string? pwd = await reader.ReadLineAsync(); Assert.Contains("\"/\"", pwd);
        });

        using var serverClient = await listener.AcceptTcpClientAsync();
        var auth = new InMemoryAuthenticator(); auth.SetUser("u", "p");
        var options = Microsoft.Extensions.Options.Options.Create(new Core.Configuration.FtpServerOptions());
        var session = new Core.Server.FtpSession(serverClient, auth, storage, options);
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
            await writer.WriteLineAsync("TYPE X"); string? resp = await reader.ReadLineAsync();
            Assert.StartsWith("504", resp);
        });

        using var serverClient = await listener.AcceptTcpClientAsync();
        var auth = new InMemoryAuthenticator(); auth.SetUser("u", "p");
        var options = Microsoft.Extensions.Options.Options.Create(new Core.Configuration.FtpServerOptions());
        var session = new Core.Server.FtpSession(serverClient, auth, storage, options);
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
            string? l1 = await reader.ReadLineAsync(); Assert.StartsWith("211-", l1);
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
            Assert.Contains("REST STREAM", features);
            Assert.Contains("APPE", features);
        });

        using var serverClient = await listener.AcceptTcpClientAsync();
        var auth = new InMemoryAuthenticator(); auth.SetUser("u", "p");
        var options = Microsoft.Extensions.Options.Options.Create(new Core.Configuration.FtpServerOptions());
        var session = new Core.Server.FtpSession(serverClient, auth, storage, options);
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
            await writer.WriteLineAsync("RMD /dir"); string? resp = await reader.ReadLineAsync();
            Assert.StartsWith("550", resp);
        });

        using var serverClient = await listener.AcceptTcpClientAsync();
        var auth = new InMemoryAuthenticator(); auth.SetUser("u", "p");
        var options = Microsoft.Extensions.Options.Options.Create(new Core.Configuration.FtpServerOptions());
        var session = new Core.Server.FtpSession(serverClient, auth, storage, options);
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
            await writer.WriteLineAsync("PASV"); string? pasv = await reader.ReadLineAsync(); (string dip, int dport) = ParsePasv(pasv!);
            using var dc = new TcpClient(); await dc.ConnectAsync(IPAddress.Parse(dip), dport);
            await writer.WriteLineAsync("STOR /z/new.bin"); _ = await reader.ReadLineAsync();
            byte[] buf = Encoding.ASCII.GetBytes("hi");
            var dns = dc.GetStream();
            await dns.WriteAsync(buf, 0, buf.Length);
            await dns.FlushAsync();
            dc.Close();
            string? done = await reader.ReadLineAsync(); Assert.StartsWith("226", done);
        });

        using var serverClient = await listener.AcceptTcpClientAsync();
        var auth = new InMemoryAuthenticator(); auth.SetUser("u", "p");
        var options = Microsoft.Extensions.Options.Options.Create(new Core.Configuration.FtpServerOptions());
        var session = new Core.Server.FtpSession(serverClient, auth, storage, options);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await Task.WhenAll(session.RunAsync(cts.Token), clientTask);

        long size = await storage.GetSizeAsync("/z/new.bin", CancellationToken.None);
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
            await writer.WriteLineAsync("DELE /missing.txt"); string? resp = await reader.ReadLineAsync();
            Assert.StartsWith("550", resp);
        });

        using var serverClient = await listener.AcceptTcpClientAsync();
        var auth = new InMemoryAuthenticator(); auth.SetUser("u", "p");
        var options = Microsoft.Extensions.Options.Options.Create(new Core.Configuration.FtpServerOptions());
        var session = new Core.Server.FtpSession(serverClient, auth, storage, options);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await Task.WhenAll(session.RunAsync(cts.Token), clientTask);
    }
    [Fact]
    public async Task Port_Unreachable_List_Returns_425()
    {
        var storage = new InMemoryStorageProvider();
        var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); var ep = (IPEndPoint)listener.LocalEndpoint;

        // Prepare a port that will be closed/unreachable
        var temp = new TcpListener(IPAddress.Loopback, 0); temp.Start(); int badPort = ((IPEndPoint)temp.LocalEndpoint).Port; temp.Stop();

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
            string ip = IPAddress.Loopback.ToString().Replace('.', ',');
            int p1 = badPort / 256; int p2 = badPort % 256;
            await writer.WriteLineAsync($"PORT {ip},{p1},{p2}"); _ = await reader.ReadLineAsync();
            await writer.WriteLineAsync("LIST"); string? r150 = await reader.ReadLineAsync(); Assert.StartsWith("150", r150);
            string? r425 = await reader.ReadLineAsync(); Assert.StartsWith("425", r425);
        });

        using var serverClient = await listener.AcceptTcpClientAsync();
        var auth = new InMemoryAuthenticator(); auth.SetUser("u", "p");
        var options = Microsoft.Extensions.Options.Options.Create(new Core.Configuration.FtpServerOptions());
        var session = new Core.Server.FtpSession(serverClient, auth, storage, options);
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
            await writer.WriteLineAsync("EPRT |2|127.0.0.1|65000|"); string? resp = await reader.ReadLineAsync();
            Assert.StartsWith("501", resp);
        });

        using var serverClient = await listener.AcceptTcpClientAsync();
        var auth = new InMemoryAuthenticator(); auth.SetUser("u", "p");
        var options = Microsoft.Extensions.Options.Options.Create(new Core.Configuration.FtpServerOptions());
        var session = new Core.Server.FtpSession(serverClient, auth, storage, options);
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
            await writer.WriteLineAsync("NLST"); string? resp = await reader.ReadLineAsync();
            Assert.StartsWith("530", resp);
        });

        using var serverClient = await listener.AcceptTcpClientAsync();
        var auth = new InMemoryAuthenticator(); auth.SetUser("u", "p");
        var options = Microsoft.Extensions.Options.Options.Create(new Core.Configuration.FtpServerOptions());
        var session = new Core.Server.FtpSession(serverClient, auth, storage, options);
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
            await writer.WriteLineAsync("CWD /nope"); string? resp = await reader.ReadLineAsync(); Assert.StartsWith("550", resp);
            await writer.WriteLineAsync("PWD"); string? pwd = await reader.ReadLineAsync(); Assert.Contains("\"/\"", pwd);
        });

        using var serverClient = await listener.AcceptTcpClientAsync();
        var auth = new InMemoryAuthenticator(); auth.SetUser("u", "p");
        var options = Microsoft.Extensions.Options.Options.Create(new Core.Configuration.FtpServerOptions());
        var session = new Core.Server.FtpSession(serverClient, auth, storage, options);
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
            await writer.WriteLineAsync("ABOR"); string? resp = await reader.ReadLineAsync();
            Assert.StartsWith("502", resp);
        });

        using var serverClient = await listener.AcceptTcpClientAsync();
        var auth = new InMemoryAuthenticator(); auth.SetUser("u", "p");
        var options = Microsoft.Extensions.Options.Options.Create(new Core.Configuration.FtpServerOptions());
        var session = new Core.Server.FtpSession(serverClient, auth, storage, options);
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
            await writer.WriteLineAsync("RNFR /notthere.txt"); string? resp = await reader.ReadLineAsync();
            Assert.StartsWith("550", resp);
        });

        using var serverClient = await listener.AcceptTcpClientAsync();
        var auth = new InMemoryAuthenticator(); auth.SetUser("u", "p");
        var options = Microsoft.Extensions.Options.Options.Create(new Core.Configuration.FtpServerOptions());
        var session = new Core.Server.FtpSession(serverClient, auth, storage, options);
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
            await writer.WriteLineAsync("RNTO /any.txt"); string? resp = await reader.ReadLineAsync();
            Assert.StartsWith("503", resp);
        });

        using var serverClient = await listener.AcceptTcpClientAsync();
        var auth = new InMemoryAuthenticator(); auth.SetUser("u", "p");
        var options = Microsoft.Extensions.Options.Options.Create(new Core.Configuration.FtpServerOptions());
        var session = new Core.Server.FtpSession(serverClient, auth, storage, options);
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
            await writer.WriteLineAsync("SIZE /missing.txt"); string? resp = await reader.ReadLineAsync();
            Assert.StartsWith("550", resp);
        });

        using var serverClient = await listener.AcceptTcpClientAsync();
        var auth = new InMemoryAuthenticator(); auth.SetUser("u", "p");
        var options = Microsoft.Extensions.Options.Options.Create(new Core.Configuration.FtpServerOptions());
        var session = new Core.Server.FtpSession(serverClient, auth, storage, options);
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
            await writer.WriteLineAsync("EPRT nonsense"); string? resp = await reader.ReadLineAsync();
            Assert.StartsWith("501", resp);
        });

        using var serverClient = await listener.AcceptTcpClientAsync();
        var auth = new InMemoryAuthenticator(); auth.SetUser("u", "p");
        var options = Microsoft.Extensions.Options.Options.Create(new Core.Configuration.FtpServerOptions());
        var session = new Core.Server.FtpSession(serverClient, auth, storage, options);
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
            await writer.WriteLineAsync("LIST"); string? resp = await reader.ReadLineAsync();
            Assert.StartsWith("530", resp);
        });

        using var serverClient = await listener.AcceptTcpClientAsync();
        var auth = new InMemoryAuthenticator(); auth.SetUser("u", "p");
        var options = Microsoft.Extensions.Options.Options.Create(new Core.Configuration.FtpServerOptions());
        var session = new Core.Server.FtpSession(serverClient, auth, storage, options);
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
            string? pasv = await reader.ReadLineAsync();
            Assert.StartsWith("227", pasv);
            (string dip, int dport) = ParsePasv(pasv!);

            using var dataClient = new TcpClient();
            await dataClient.ConnectAsync(IPAddress.Parse(dip), dport);

            await writer.WriteLineAsync("LIST");
            string? status150 = await reader.ReadLineAsync();
            Assert.StartsWith("150", status150);
            using var dr = new StreamReader(dataClient.GetStream(), Encoding.ASCII);
            string? line = await dr.ReadLineAsync();
            Assert.EndsWith(" a.txt", line);
            string? status226 = await reader.ReadLineAsync();
            Assert.StartsWith("226", status226);
            await writer.WriteLineAsync("QUIT");
            _ = await reader.ReadLineAsync();
        });

        using var serverClient = await listener.AcceptTcpClientAsync();
        var auth = new InMemoryAuthenticator();
        auth.SetUser("u", "p");
        var options = Microsoft.Extensions.Options.Options.Create(new Core.Configuration.FtpServerOptions());
        var session = new Core.Server.FtpSession(serverClient, auth, storage, options);
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
            await writer.WriteLineAsync("MKD /m"); string? mk = await reader.ReadLineAsync(); Assert.StartsWith("257", mk);
            await writer.WriteLineAsync("CWD /m"); _ = await reader.ReadLineAsync();
            await writer.WriteLineAsync("PASV"); string? pasv = await reader.ReadLineAsync(); (string dip, int dport) = ParsePasv(pasv!);
            using var dataClient = new TcpClient(); await dataClient.ConnectAsync(IPAddress.Parse(dip), dport);
            await writer.WriteLineAsync("STOR f.txt"); _ = await reader.ReadLineAsync();
            await using (var ns = dataClient.GetStream())
            {
                byte[] bytes = Encoding.ASCII.GetBytes("abc");
                await ns.WriteAsync(bytes, 0, bytes.Length);
            }
            string? done = await reader.ReadLineAsync(); Assert.StartsWith("226", done);
            await writer.WriteLineAsync("DELE /m/f.txt"); _ = await reader.ReadLineAsync();
            await writer.WriteLineAsync("RMD /m"); string? rm = await reader.ReadLineAsync(); Assert.StartsWith("250", rm);
            await writer.WriteLineAsync("QUIT"); _ = await reader.ReadLineAsync();
        });

        using var serverClient = await listener.AcceptTcpClientAsync();
        var auth = new InMemoryAuthenticator(); auth.SetUser("u", "p");
        var options = Microsoft.Extensions.Options.Options.Create(new Core.Configuration.FtpServerOptions());
        var session = new Core.Server.FtpSession(serverClient, auth, storage, options);
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
            await writer.WriteLineAsync("PASV"); string? pasv1 = await reader.ReadLineAsync(); (string dip1, int dp1) = ParsePasv(pasv1!);
            using (var d1 = new TcpClient())
            {
                await d1.ConnectAsync(IPAddress.Parse(dip1), dp1);
                await writer.WriteLineAsync("RETR /file.bin"); _ = await reader.ReadLineAsync();
                using var ns = d1.GetStream();
                using var ms = new MemoryStream();
                await ns.CopyToAsync(ms);
                string? done1 = await reader.ReadLineAsync(); Assert.StartsWith("226", done1);
                Assert.Equal("hello", Encoding.ASCII.GetString(ms.ToArray()));
            }

            await writer.WriteLineAsync("PASV"); string? pasv2 = await reader.ReadLineAsync(); (string dip2, int dp2) = ParsePasv(pasv2!);
            using (var d2 = new TcpClient())
            {
                await d2.ConnectAsync(IPAddress.Parse(dip2), dp2);
                await writer.WriteLineAsync("STOR /new.bin"); _ = await reader.ReadLineAsync();
                byte[] buf = Encoding.ASCII.GetBytes("world");
                await d2.GetStream().WriteAsync(buf, 0, buf.Length);
            }
            string? done2 = await reader.ReadLineAsync(); Assert.StartsWith("226", done2);
            await writer.WriteLineAsync("QUIT"); _ = await reader.ReadLineAsync();
        });

        using var serverClient = await listener.AcceptTcpClientAsync();
        var auth = new InMemoryAuthenticator(); auth.SetUser("u", "p");
        var options = Microsoft.Extensions.Options.Options.Create(new Core.Configuration.FtpServerOptions());
        var session = new Core.Server.FtpSession(serverClient, auth, storage, options);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await Task.WhenAll(session.RunAsync(cts.Token), clientTask);

        long size = await storage.GetSizeAsync("/new.bin", CancellationToken.None);
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
            string ip = dEp.Address.ToString().Replace('.', ',');
            int p1 = dEp.Port / 256; int p2 = dEp.Port % 256;
            await writer.WriteLineAsync($"PORT {ip},{p1},{p2}");
            _ = await reader.ReadLineAsync();
            var acceptTask = dataListener.AcceptTcpClientAsync();
            await writer.WriteLineAsync("RETR /p.txt"); _ = await reader.ReadLineAsync();
            using var dataClient = await acceptTask;
            using var ns = dataClient.GetStream();
            byte[] b = new byte[1];
            int r = await ns.ReadAsync(b, 0, 1);
            Assert.Equal(1, r);
            string? done = await reader.ReadLineAsync(); Assert.StartsWith("226", done);
        });

        using var serverClient = await listener.AcceptTcpClientAsync();
        var auth = new InMemoryAuthenticator(); auth.SetUser("u", "p");
        var options = Microsoft.Extensions.Options.Options.Create(new Core.Configuration.FtpServerOptions());
        var session = new Core.Server.FtpSession(serverClient, auth, storage, options);
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
            await writer.WriteLineAsync("EPSV"); string? epsv = await reader.ReadLineAsync();
            int start = epsv!.IndexOf('(');
            int end = epsv!.IndexOf(')', start + 1);
            string inside = epsv!.Substring(start + 1, end - start - 1);
            string token = inside.Split('|', StringSplitOptions.RemoveEmptyEntries).First();
            int port = int.Parse(token);
            using var dataClient = new TcpClient(); await dataClient.ConnectAsync(IPAddress.Loopback, port);
            await writer.WriteLineAsync("LIST"); _ = await reader.ReadLineAsync();
            using var dr = new StreamReader(dataClient.GetStream(), Encoding.ASCII);
            string? line = await dr.ReadLineAsync();
            Assert.EndsWith(" b.txt", line);
            string? done = await reader.ReadLineAsync(); Assert.StartsWith("226", done);
        });

        using var serverClient = await listener.AcceptTcpClientAsync();
        var auth = new InMemoryAuthenticator(); auth.SetUser("u", "p");
        var options = Microsoft.Extensions.Options.Options.Create(new Core.Configuration.FtpServerOptions());
        var session = new Core.Server.FtpSession(serverClient, auth, storage, options);
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
            byte[] bytes = Encoding.ASCII.GetBytes("z"); await ns.WriteAsync(bytes, 0, bytes.Length);
            await ns.FlushAsync();
            dc.Close();
            string? done = await reader.ReadLineAsync(); Assert.StartsWith("226", done);
        });

        using var serverClient = await listener.AcceptTcpClientAsync();
        var auth = new InMemoryAuthenticator(); auth.SetUser("u", "p");
        var options = Microsoft.Extensions.Options.Options.Create(new Core.Configuration.FtpServerOptions());
        var session = new Core.Server.FtpSession(serverClient, auth, storage, options);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await Task.WhenAll(session.RunAsync(cts.Token), clientTask);

        long size = await storage.GetSizeAsync("/e.bin", CancellationToken.None);
        Assert.Equal(1, size);
    }

    private static (string, int) ParsePasv(string s)
    {
        int start = s.IndexOf('(');
        int end = s.IndexOf(')');
        string[] parts = s.Substring(start + 1, end - start - 1).Split(',');
        string ip = string.Join('.', parts[..4]);
        int port = int.Parse(parts[4]) * 256 + int.Parse(parts[5]);
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

            await writer.WriteLineAsync("NOOP"); string? noop = await reader.ReadLineAsync(); Assert.StartsWith("200", noop);

            await writer.WriteLineAsync("HELP");
            string? h1 = await reader.ReadLineAsync(); Assert.StartsWith("214-", h1);
            string? h2 = await reader.ReadLineAsync(); Assert.Contains("USER", h2);
            string? h3 = await reader.ReadLineAsync(); Assert.StartsWith("214", h3);

            await writer.WriteLineAsync("STAT");
            string? s1 = await reader.ReadLineAsync(); Assert.StartsWith("211-", s1);
            string? line;
            do { line = await reader.ReadLineAsync(); } while (line is not null && !line.StartsWith("211 End"));

            await writer.WriteLineAsync("QUIT"); _ = await reader.ReadLineAsync();
        });

        using var serverClient = await listener.AcceptTcpClientAsync();
        var auth = new InMemoryAuthenticator(); auth.SetUser("u", "p");
        var options = Microsoft.Extensions.Options.Options.Create(new Core.Configuration.FtpServerOptions());
        var session = new Core.Server.FtpSession(serverClient, auth, storage, options);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await Task.WhenAll(session.RunAsync(cts.Token), clientTask);
    }

    [Fact]
    public async Task Control_Read_Timeout_Closes_Session()
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
            // Don't send anything else; expect server to close within timeout
            byte[] buf = new byte[1];
            var t0 = DateTime.UtcNow;
            try { await stream.ReadAsync(buf, 0, 1).WaitAsync(TimeSpan.FromSeconds(5)); } catch { }
            var dt = DateTime.UtcNow - t0;
            Assert.True(dt.TotalMilliseconds >= 300, $"Expected at least ~300ms before close, got {dt}");
        });

        using var serverClient = await listener.AcceptTcpClientAsync();
        var auth = new InMemoryAuthenticator(); auth.SetUser("u", "p");
        var options = Microsoft.Extensions.Options.Options.Create(new Core.Configuration.FtpServerOptions { ControlReadTimeoutMs = 300 });
        var session = new Core.Server.FtpSession(serverClient, auth, storage, options);
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
            await writer.WriteLineAsync("CDUP"); string? cdup = await reader.ReadLineAsync(); Assert.StartsWith("200", cdup);
            await writer.WriteLineAsync("PWD"); string? pwd = await reader.ReadLineAsync(); Assert.Contains("\"/\"", pwd);
        });

        using var serverClient = await listener.AcceptTcpClientAsync();
        var auth = new InMemoryAuthenticator(); auth.SetUser("u", "p");
        var options = Microsoft.Extensions.Options.Options.Create(new Core.Configuration.FtpServerOptions());
        var session = new Core.Server.FtpSession(serverClient, auth, storage, options);
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
            await writer.WriteLineAsync("PASV"); string? pasv = await reader.ReadLineAsync(); (string dip, int dport) = ParsePasv(pasv!);
            using var dc = new TcpClient(); await dc.ConnectAsync(IPAddress.Parse(dip), dport);
            await writer.WriteLineAsync("NLST"); string? s150 = await reader.ReadLineAsync(); Assert.StartsWith("150", s150);
            using var dr = new StreamReader(dc.GetStream(), Encoding.ASCII);
            string? n1 = await dr.ReadLineAsync(); string? n2 = await dr.ReadLineAsync();
            Assert.Contains(n1, new[] { "a.txt", "b.txt" });
            Assert.Contains(n2, new[] { "a.txt", "b.txt" });
            string? s226 = await reader.ReadLineAsync(); Assert.StartsWith("226", s226);
        });

        using var serverClient = await listener.AcceptTcpClientAsync();
        var auth = new InMemoryAuthenticator(); auth.SetUser("u", "p");
        var options = Microsoft.Extensions.Options.Options.Create(new Core.Configuration.FtpServerOptions());
        var session = new Core.Server.FtpSession(serverClient, auth, storage, options);
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
            await writer.WriteLineAsync("SIZE /r.txt"); string? size = await reader.ReadLineAsync(); Assert.Equal("213 3", size);
            await writer.WriteLineAsync("RNFR /r.txt"); _ = await reader.ReadLineAsync();
            await writer.WriteLineAsync("RNTO /s.txt"); string? rn = await reader.ReadLineAsync(); Assert.StartsWith("250", rn);
            await writer.WriteLineAsync("SIZE /s.txt"); string? size2 = await reader.ReadLineAsync(); Assert.Equal("213 3", size2);
        });

        using var serverClient = await listener.AcceptTcpClientAsync();
        var auth = new InMemoryAuthenticator(); auth.SetUser("u", "p");
        var options = Microsoft.Extensions.Options.Options.Create(new Core.Configuration.FtpServerOptions());
        var session = new Core.Server.FtpSession(serverClient, auth, storage, options);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await Task.WhenAll(session.RunAsync(cts.Token), clientTask);
    }
}
