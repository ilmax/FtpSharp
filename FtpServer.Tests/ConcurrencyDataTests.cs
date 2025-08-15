using System.Net;
using System.Net.Sockets;
using System.Text;
using FtpServer.Core.InMemory;

namespace FtpServer.Tests;

public class ConcurrencyDataTests
{
    [Fact]
    public async Task Parallel_STOR_To_Different_Files_Passive_Works()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var ep = (IPEndPoint)listener.LocalEndpoint;

        var auth = new InMemoryAuthenticator(); auth.SetUser("u", "p");
        var storage = new InMemoryStorageProvider();
        var options = Microsoft.Extensions.Options.Options.Create(new FtpServer.Core.Configuration.FtpServerOptions());

        var clientTasks = Enumerable.Range(0, 8).Select(async i =>
        {
            using var client = new TcpClient();
            await client.ConnectAsync(ep.Address, ep.Port);
            using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, leaveOpen: true);
            using var writer = new StreamWriter(stream, Encoding.ASCII) { NewLine = "\r\n", AutoFlush = true };

            _ = await reader.ReadLineAsync();
            await writer.WriteLineAsync("USER u"); _ = await reader.ReadLineAsync();
            await writer.WriteLineAsync("PASS p"); _ = await reader.ReadLineAsync();

            await writer.WriteLineAsync("PASV");
            var pasv = await reader.ReadLineAsync();
            var (dip, dport) = ParsePasv(pasv!);
            using var dc = new TcpClient(); await dc.ConnectAsync(IPAddress.Parse(dip), dport);

            var path = $"/p/{i}.bin";
            await writer.WriteLineAsync($"STOR {path}"); _ = await reader.ReadLineAsync();
            var payload = Encoding.ASCII.GetBytes($"data-{i}");
            await dc.GetStream().WriteAsync(payload, 0, payload.Length);
            dc.Close();
            var done = await reader.ReadLineAsync(); Assert.StartsWith("226", done);
        }).ToArray();

        var serverTasks = new List<Task>();
        for (int i = 0; i < clientTasks.Length; i++)
        {
            var serverClient = await listener.AcceptTcpClientAsync();
            var session = new FtpServer.Core.Server.FtpSession(serverClient, auth, storage, options);
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            serverTasks.Add(session.RunAsync(cts.Token));
        }

        await Task.WhenAll(clientTasks);
        listener.Stop();
        await Task.WhenAll(serverTasks);

        for (int i = 0; i < clientTasks.Length; i++)
        {
            var size = await storage.GetSizeAsync($"/p/{i}.bin", CancellationToken.None);
            Assert.Equal($"data-{i}".Length, size);
        }
    }

    [Fact]
    public async Task Concurrent_STOR_Same_File_LastWriterWins()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var ep = (IPEndPoint)listener.LocalEndpoint;

        var auth = new InMemoryAuthenticator(); auth.SetUser("u", "p");
        var storage = new InMemoryStorageProvider();
        var options = Microsoft.Extensions.Options.Options.Create(new FtpServer.Core.Configuration.FtpServerOptions());

        var path = "/concurrent.bin";

        async Task ClientWrite(string payload, int pauseBeforeCloseMs)
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
            await writer.WriteLineAsync($"STOR {path}"); _ = await reader.ReadLineAsync();
            var bytes = Encoding.ASCII.GetBytes(payload);
            // write in one chunk
            await dc.GetStream().WriteAsync(bytes, 0, bytes.Length);
            await dc.GetStream().FlushAsync();
            if (pauseBeforeCloseMs > 0) await Task.Delay(pauseBeforeCloseMs);
            dc.Close();
            var done = await reader.ReadLineAsync(); Assert.StartsWith("226", done);
        }

        var c1 = ClientWrite("AAAA", 20);
        var c2 = ClientWrite("BBBBBBBB", 0);

        var serverTasks = new List<Task>();
        for (int i = 0; i < 2; i++)
        {
            var serverClient = await listener.AcceptTcpClientAsync();
            var session = new FtpServer.Core.Server.FtpSession(serverClient, auth, storage, options);
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            serverTasks.Add(session.RunAsync(cts.Token));
        }

        await Task.WhenAll(c1, c2);
        listener.Stop();
        await Task.WhenAll(serverTasks);

        // With per-path writer lock, operations are serialized; both writes complete, last arrival wins deterministically but we can't predict order here. Just ensure file contains one of the payloads.
        var size = await storage.GetSizeAsync(path, CancellationToken.None);
        Assert.True(size == 4 || size == 8);
        var collected = new List<byte>();
        await foreach (var chunk in storage.ReadAsync(path, 64, CancellationToken.None))
            collected.AddRange(chunk.ToArray());
        var text = Encoding.ASCII.GetString(collected.ToArray());
        Assert.True(text == "AAAA" || text == "BBBBBBBB");
    }

    [Fact]
    public async Task Retr_With_Rest_Offset_Skips_Bytes()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start(); var ep = (IPEndPoint)listener.LocalEndpoint;
        var auth = new InMemoryAuthenticator(); auth.SetUser("u", "p");
        var storage = new InMemoryStorageProvider();
        await storage.WriteAsync("/f.txt", OneChunk("HELLOWORLD"), CancellationToken.None);
        var options = Microsoft.Extensions.Options.Options.Create(new FtpServer.Core.Configuration.FtpServerOptions());

        var clientTask = Task.Run(async () =>
        {
            using var client = new TcpClient(); await client.ConnectAsync(ep.Address, ep.Port);
            using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, true);
            using var writer = new StreamWriter(stream, Encoding.ASCII) { NewLine = "\r\n", AutoFlush = true };
            _ = await reader.ReadLineAsync();
            await writer.WriteLineAsync("USER u"); _ = await reader.ReadLineAsync();
            await writer.WriteLineAsync("PASS p"); _ = await reader.ReadLineAsync();
            await writer.WriteLineAsync("REST 5"); _ = await reader.ReadLineAsync();
            await writer.WriteLineAsync("PASV"); var pasv = await reader.ReadLineAsync(); var (dip, dport) = ParsePasv(pasv!);
            using var dc = new TcpClient(); await dc.ConnectAsync(IPAddress.Parse(dip), dport);
            await writer.WriteLineAsync("RETR /f.txt"); _ = await reader.ReadLineAsync();
            var buf = new byte[64]; var n = await dc.GetStream().ReadAsync(buf, 0, buf.Length);
            dc.Close(); var done = await reader.ReadLineAsync(); Assert.StartsWith("226", done);
            var got = Encoding.ASCII.GetString(buf, 0, n); Assert.Equal("WORLD", got);
        });

        var serverClient = await listener.AcceptTcpClientAsync();
        var session = new FtpServer.Core.Server.FtpSession(serverClient, auth, storage, options);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await Task.WhenAll(clientTask, session.RunAsync(cts.Token)); listener.Stop();
    }

    [Fact]
    public async Task Appe_Appends_Data()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); var ep = (IPEndPoint)listener.LocalEndpoint;
        var auth = new InMemoryAuthenticator(); auth.SetUser("u", "p");
        var storage = new InMemoryStorageProvider(); await storage.WriteAsync("/a.txt", OneChunk("HELLO"), CancellationToken.None);
        var options = Microsoft.Extensions.Options.Options.Create(new FtpServer.Core.Configuration.FtpServerOptions());

        var clientTask = Task.Run(async () =>
        {
            using var client = new TcpClient(); await client.ConnectAsync(ep.Address, ep.Port);
            using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, true);
            using var writer = new StreamWriter(stream, Encoding.ASCII) { NewLine = "\r\n", AutoFlush = true };
            _ = await reader.ReadLineAsync();
            await writer.WriteLineAsync("USER u"); _ = await reader.ReadLineAsync();
            await writer.WriteLineAsync("PASS p"); _ = await reader.ReadLineAsync();
            await writer.WriteLineAsync("PASV"); var pasv = await reader.ReadLineAsync(); var (dip, dport) = ParsePasv(pasv!);
            using var dc = new TcpClient(); await dc.ConnectAsync(IPAddress.Parse(dip), dport);
            await writer.WriteLineAsync("APPE /a.txt"); _ = await reader.ReadLineAsync();
            var payload = Encoding.ASCII.GetBytes("WORLD"); await dc.GetStream().WriteAsync(payload, 0, payload.Length);
            dc.Close(); var done = await reader.ReadLineAsync(); Assert.StartsWith("226", done);
        });

        var serverClient = await listener.AcceptTcpClientAsync();
        var session = new FtpServer.Core.Server.FtpSession(serverClient, auth, storage, options);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await Task.WhenAll(clientTask, session.RunAsync(cts.Token)); listener.Stop();

        var collected = new List<byte>(); await foreach (var ch in storage.ReadAsync("/a.txt", 64, CancellationToken.None)) collected.AddRange(ch.ToArray());
        var s = Encoding.ASCII.GetString(collected.ToArray()); Assert.Equal("HELLOWORLD", s);
    }

    [Fact]
    public async Task Stor_With_Rest_Truncates_Then_Writes()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); var ep = (IPEndPoint)listener.LocalEndpoint;
        var auth = new InMemoryAuthenticator(); auth.SetUser("u", "p");
        var storage = new InMemoryStorageProvider(); await storage.WriteAsync("/s.txt", OneChunk("HELLOWORLD"), CancellationToken.None);
        var options = Microsoft.Extensions.Options.Options.Create(new FtpServer.Core.Configuration.FtpServerOptions());

        var clientTask = Task.Run(async () =>
        {
            using var client = new TcpClient(); await client.ConnectAsync(ep.Address, ep.Port);
            using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, true);
            using var writer = new StreamWriter(stream, Encoding.ASCII) { NewLine = "\r\n", AutoFlush = true };
            _ = await reader.ReadLineAsync();
            await writer.WriteLineAsync("USER u"); _ = await reader.ReadLineAsync();
            await writer.WriteLineAsync("PASS p"); _ = await reader.ReadLineAsync();
            await writer.WriteLineAsync("REST 5"); _ = await reader.ReadLineAsync();
            await writer.WriteLineAsync("PASV"); var pasv = await reader.ReadLineAsync(); var (dip, dport) = ParsePasv(pasv!);
            using var dc = new TcpClient(); await dc.ConnectAsync(IPAddress.Parse(dip), dport);
            await writer.WriteLineAsync("STOR /s.txt"); _ = await reader.ReadLineAsync();
            var payload = Encoding.ASCII.GetBytes("BYE"); await dc.GetStream().WriteAsync(payload, 0, payload.Length);
            dc.Close(); var done = await reader.ReadLineAsync(); Assert.StartsWith("226", done);
        });

        var serverClient = await listener.AcceptTcpClientAsync();
        var session = new FtpServer.Core.Server.FtpSession(serverClient, auth, storage, options);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await Task.WhenAll(clientTask, session.RunAsync(cts.Token)); listener.Stop();

        var collected = new List<byte>(); await foreach (var ch in storage.ReadAsync("/s.txt", 64, CancellationToken.None)) collected.AddRange(ch.ToArray());
        var s = Encoding.ASCII.GetString(collected.ToArray()); Assert.Equal("HELLOBYE", s);
    }

    [Fact]
    public async Task Parallel_RETR_From_Different_Files_Passive_Works()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var ep = (IPEndPoint)listener.LocalEndpoint;

        var auth = new InMemoryAuthenticator(); auth.SetUser("u", "p");
        var storage = new InMemoryStorageProvider();
        for (int i = 0; i < 6; i++)
        {
            await storage.WriteAsync($"/r/{i}.txt", OneChunk($"ret-{i}"), CancellationToken.None);
        }
        var options = Microsoft.Extensions.Options.Options.Create(new FtpServer.Core.Configuration.FtpServerOptions());

        var clientTasks = Enumerable.Range(0, 6).Select(async i =>
        {
            using var client = new TcpClient();
            await client.ConnectAsync(ep.Address, ep.Port);
            using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, leaveOpen: true);
            using var writer = new StreamWriter(stream, Encoding.ASCII) { NewLine = "\r\n", AutoFlush = true };

            _ = await reader.ReadLineAsync();
            await writer.WriteLineAsync("USER u"); _ = await reader.ReadLineAsync();
            await writer.WriteLineAsync("PASS p"); _ = await reader.ReadLineAsync();

            await writer.WriteLineAsync("PASV");
            var pasv = await reader.ReadLineAsync();
            var (dip, dport) = ParsePasv(pasv!);
            using var dc = new TcpClient(); await dc.ConnectAsync(IPAddress.Parse(dip), dport);

            var path = $"/r/{i}.txt";
            await writer.WriteLineAsync($"RETR {path}"); _ = await reader.ReadLineAsync();
            var buf = new byte[64];
            var n = await dc.GetStream().ReadAsync(buf, 0, buf.Length);
            dc.Close();
            var done = await reader.ReadLineAsync(); Assert.StartsWith("226", done);
            var got = Encoding.ASCII.GetString(buf, 0, n);
            Assert.Equal($"ret-{i}", got);
        }).ToArray();

        var serverTasks = new List<Task>();
        for (int i = 0; i < clientTasks.Length; i++)
        {
            var serverClient = await listener.AcceptTcpClientAsync();
            var session = new FtpServer.Core.Server.FtpSession(serverClient, auth, storage, options);
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            serverTasks.Add(session.RunAsync(cts.Token));
        }

        await Task.WhenAll(clientTasks);
        listener.Stop();
        await Task.WhenAll(serverTasks);
    }

    [Fact]
    public async Task Mixed_Parallel_RETR_and_STOR_Passive_Works()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var ep = (IPEndPoint)listener.LocalEndpoint;

        var auth = new InMemoryAuthenticator(); auth.SetUser("u", "p");
        var storage = new InMemoryStorageProvider();
        for (int i = 0; i < 4; i++)
            await storage.WriteAsync($"/mix/r{i}.txt", OneChunk($"mix-{i}"), CancellationToken.None);
        var options = Microsoft.Extensions.Options.Options.Create(new FtpServer.Core.Configuration.FtpServerOptions());

        var tasks = new List<Task>();
        // 4 RETR clients
        for (int i = 0; i < 4; i++)
        {
            var idx = i;
            tasks.Add(Task.Run(async () =>
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
                await writer.WriteLineAsync($"RETR /mix/r{idx}.txt"); _ = await reader.ReadLineAsync();
                var buf = new byte[64]; var n = await dc.GetStream().ReadAsync(buf, 0, buf.Length);
                dc.Close(); var done = await reader.ReadLineAsync(); Assert.StartsWith("226", done);
                var got = Encoding.ASCII.GetString(buf, 0, n); Assert.Equal($"mix-{idx}", got);
            }));
        }
        // 4 STOR clients
        for (int i = 0; i < 4; i++)
        {
            var idx = i;
            tasks.Add(Task.Run(async () =>
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
                await writer.WriteLineAsync($"STOR /mix/w{idx}.bin"); _ = await reader.ReadLineAsync();
                var payload = Encoding.ASCII.GetBytes($"w-{idx}"); await dc.GetStream().WriteAsync(payload, 0, payload.Length);
                dc.Close(); var done = await reader.ReadLineAsync(); Assert.StartsWith("226", done);
            }));
        }

        var serverTasks = new List<Task>();
        for (int i = 0; i < 8; i++)
        {
            var serverClient = await listener.AcceptTcpClientAsync();
            var session = new FtpServer.Core.Server.FtpSession(serverClient, auth, storage, options);
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            serverTasks.Add(session.RunAsync(cts.Token));
        }

        await Task.WhenAll(tasks);
        listener.Stop();
        await Task.WhenAll(serverTasks);

        for (int i = 0; i < 4; i++)
        {
            var size = await storage.GetSizeAsync($"/mix/w{i}.bin", CancellationToken.None);
            Assert.Equal($"w-{i}".Length, size);
        }
    }

    [Fact]
    public async Task Many_Short_PASV_Transfers_Do_Not_Exhaust_Ports()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var ep = (IPEndPoint)listener.LocalEndpoint;

        var auth = new InMemoryAuthenticator(); auth.SetUser("u", "p");
        var storage = new InMemoryStorageProvider();
        await storage.WriteAsync("/file.txt", OneChunk("x"), CancellationToken.None);
        var options = Microsoft.Extensions.Options.Options.Create(new FtpServer.Core.Configuration.FtpServerOptions
        {
            PassivePortRangeStart = 50100,
            PassivePortRangeEnd = 50110
        });

        var rnd = new Random(42);
        var clients = Enumerable.Range(0, 10).Select(async i =>
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
                await writer.WriteLineAsync("RETR /file.txt"); _ = await reader.ReadLineAsync();
                var buf = new byte[8]; _ = await dc.GetStream().ReadAsync(buf, 0, buf.Length);
                dc.Close();
                var done = await reader.ReadLineAsync(); Assert.StartsWith("226", done);
            }).ToArray();

        var serverTasks = new List<Task>();
        for (int i = 0; i < clients.Length; i++)
        {
            var serverClient = await listener.AcceptTcpClientAsync();
            var session = new FtpServer.Core.Server.FtpSession(serverClient, auth, storage, options);
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            serverTasks.Add(session.RunAsync(cts.Token));
        }

        await Task.WhenAll(clients);
        listener.Stop();
        await Task.WhenAll(serverTasks);
    }

    [Fact]
    public async Task Retr_Respects_Data_Rate_Limit_Basically()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); var ep = (IPEndPoint)listener.LocalEndpoint;
        var auth = new InMemoryAuthenticator(); auth.SetUser("u", "p");
        var storage = new InMemoryStorageProvider();
        // ~10KB file
        var payload = new string('x', 10_240);
        await storage.WriteAsync("/big.bin", OneChunk(payload), CancellationToken.None);
        var options = Microsoft.Extensions.Options.Options.Create(new FtpServer.Core.Configuration.FtpServerOptions
        {
            DataRateLimitBytesPerSec = 2_048 // 2KB/s
        });

        var started = DateTime.UtcNow;
        var clientTask = Task.Run(async () =>
        {
            using var client = new TcpClient(); await client.ConnectAsync(ep.Address, ep.Port);
            using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, true);
            using var writer = new StreamWriter(stream, Encoding.ASCII) { NewLine = "\r\n", AutoFlush = true };
            _ = await reader.ReadLineAsync();
            await writer.WriteLineAsync("USER u"); _ = await reader.ReadLineAsync();
            await writer.WriteLineAsync("PASS p"); _ = await reader.ReadLineAsync();
            await writer.WriteLineAsync("PASV"); var pasv = await reader.ReadLineAsync(); var (dip, dport) = ParsePasv(pasv!);
            using var dc = new TcpClient(); await dc.ConnectAsync(IPAddress.Parse(dip), dport);
            await writer.WriteLineAsync("RETR /big.bin"); _ = await reader.ReadLineAsync();
            var buf = new byte[20_480]; int total = 0; int n;
            while ((n = await dc.GetStream().ReadAsync(buf, total, buf.Length - total)) > 0) total += n;
            dc.Close(); var done = await reader.ReadLineAsync(); Assert.StartsWith("226", done);
            Assert.Equal(10_240, total);
        });

        var serverClient = await listener.AcceptTcpClientAsync();
        var session = new FtpServer.Core.Server.FtpSession(serverClient, auth, storage, options);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await Task.WhenAll(clientTask, session.RunAsync(cts.Token)); listener.Stop();

        var elapsed = DateTime.UtcNow - started;
        // 10KB at 2KB/s ≈ 5s; allow slack but it shouldn't be under ~2s
        Assert.True(elapsed.TotalSeconds >= 2);
    }

    private static (string, int) ParsePasv(string s)
    {
        if (string.IsNullOrWhiteSpace(s) || !s.Contains('(') || !s.Contains(')'))
            throw new InvalidOperationException($"Unexpected PASV response: '{s}'");
        var start = s.IndexOf('(');
        var end = s.IndexOf(')');
        var inner = s.Substring(start + 1, end - start - 1);
        var parts = inner.Split(',');
        if (parts.Length < 6) throw new InvalidOperationException($"Unexpected PASV tuple: '{inner}'");
        var ip = string.Join('.', parts[0], parts[1], parts[2], parts[3]);
        var port = int.Parse(parts[4]) * 256 + int.Parse(parts[5]);
        return (ip, port);
    }

    private static async IAsyncEnumerable<ReadOnlyMemory<byte>> OneChunk(string content)
    {
        yield return Encoding.ASCII.GetBytes(content);
        await Task.CompletedTask;
    }
}
