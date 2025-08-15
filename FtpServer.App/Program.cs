using FtpServer.Core.Abstractions;
using FtpServer.Core.Configuration;
using FtpServer.Core.InMemory;
using FtpServer.Core.Server;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Minimal generic host bootstrapping the FTP server. Reads configuration from env and command line.
var builder = Host.CreateApplicationBuilder(args);

// Read from environment variables (prefix FTP_) and command-line switches (--FtpServer:Port=2121 etc)
builder.Configuration
	.AddEnvironmentVariables("FTP_")
	.AddCommandLine(args);

builder.Services.AddOptions<FtpServerOptions>()
	.Bind(builder.Configuration.GetSection("FtpServer"))
	.ValidateDataAnnotations();

// Register plugin factories; for now wire InMemory as default. Later, select by name from options.
builder.Services.AddSingleton<InMemoryAuthenticator>();
builder.Services.AddSingleton<InMemoryStorageProvider>();
builder.Services.AddSingleton<Func<IAuthenticator>>(sp => () => sp.GetRequiredService<InMemoryAuthenticator>());
builder.Services.AddSingleton<Func<IStorageProvider>>(sp => () => sp.GetRequiredService<InMemoryStorageProvider>());

builder.Services.AddSingleton<FtpServerHost>();

using var host = builder.Build();
var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("App");
var server = host.Services.GetRequiredService<FtpServerHost>();
var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

await server.StartAsync(cts.Token);
logger.LogInformation("Press Ctrl+C to stop.");
try
{
	await Task.Delay(Timeout.Infinite, cts.Token);
}
catch (OperationCanceledException) { }
