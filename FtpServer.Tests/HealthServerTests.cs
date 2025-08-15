using FtpServer.Core.Configuration;
using FtpServer.Core.Server.Health;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;

namespace FtpServer.Tests;

public class HealthServerTests
{
    [Fact]
    public async Task Starts_And_Responds_When_Enabled()
    {
        int port;  
        using (var listener = new TcpListener(IPAddress.Loopback, 0))  
        {  
            listener.Start();  
            port = ((IPEndPoint)listener.LocalEndpoint).Port;  
        }  
        var url = $"http://127.0.0.1:{port}/";
        var opts = Options.Create(new FtpServerOptions { HealthEnabled = true, HealthUrl = url });
        var srv = new HealthServer(opts, NullLogger<HealthServer>.Instance);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await srv.StartAsync(cts.Token);

    using var http = new HttpClient();
    var s = await http.GetStringAsync(url + "health");
    Assert.Equal("OK", s);
    var snapshot = await http.GetStringAsync(url + "metrics-snapshot");
    Assert.Contains("timestamp", snapshot);

        await srv.DisposeAsync();
    }
}
