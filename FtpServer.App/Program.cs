using System.CommandLine;
using System.CommandLine.Invocation;
using FtpServer.Core.Configuration;
using FtpServer.Core.Server;
using FtpServer.App.Extensions;
using FtpServer.App.CommandLine;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables("FTP_");

builder.Services.AddOptions<FtpServerOptions>()
    .Bind(builder.Configuration.GetSection("FtpServer"))
    .ValidateDataAnnotations();

builder.Services.AddFtpServerCore();
builder.Services.AddFtpServerObservability();

await builder.ApplyCommandLineAsync(args);

var app = builder.Build();

// Minimal health endpoints via ASP.NET Core
if (builder.Configuration.GetValue("FtpServer:HealthEnabled", false))
{
    app.MapGet("/health", () => Results.Text("OK", "text/plain"));
}

// Prometheus scrape endpoint
app.MapPrometheusScrapingEndpoint();

// Start FTP server in background
var ftp = app.Services.GetRequiredService<FtpServerHost>();
var appCts = new CancellationTokenSource();
_ = ftp.StartAsync(appCts.Token);

app.Lifetime.ApplicationStopping.Register(() => appCts.Cancel());

await app.RunAsync();
