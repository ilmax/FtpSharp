using System.CommandLine;
using System.CommandLine.Invocation;
using FtpServer.Core.Abstractions;
using FtpServer.Core.Configuration;
using FtpServer.Core.FileSystem;
using FtpServer.Core.InMemory;
using FtpServer.Core.Server;
using FtpServer.Core.Server.Health;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Minimal generic host bootstrapping the FTP server. Reads configuration from env and command line.
var builder = Host.CreateApplicationBuilder([]);

// Read from environment variables (prefix FTP_) and command-line switches (--FtpServer:Port=2121 etc)
builder.Configuration.AddEnvironmentVariables("FTP_");

builder.Services.AddOptions<FtpServerOptions>()
    .Bind(builder.Configuration.GetSection("FtpServer"))
    .ValidateDataAnnotations();

// Register plugins and registry for name-based selection
builder.Services.AddSingleton<InMemoryAuthenticator>();
builder.Services.AddSingleton<InMemoryStorageProvider>();
builder.Services.AddSingleton<FileSystemStorageProvider>();
builder.Services.AddSingleton<IAuthenticatorFactory, FtpServer.Core.Plugins.PluginRegistry>();
builder.Services.AddSingleton<IStorageProviderFactory, FtpServer.Core.Plugins.PluginRegistry>();

builder.Services.AddSingleton<FtpServerHost>();
builder.Services.AddSingleton<HealthServer>();

var portOption = new Option<int?>(name: "--port", description: "Control connection port to listen on (default 21)") { Arity = ArgumentArity.ZeroOrOne };
var addressOption = new Option<string>(name: "--listen", description: "IP address to bind (default 0.0.0.0)") { Arity = ArgumentArity.ZeroOrOne };
var maxSessionsOption = new Option<int?>(name: "--max-sessions", description: "Maximum concurrent sessions (default 100)") { Arity = ArgumentArity.ZeroOrOne };
var passiveStartOption = new Option<int?>(name: "--pasv-start", description: "Passive data port range start (default 50000)") { Arity = ArgumentArity.ZeroOrOne };
var passiveEndOption = new Option<int?>(name: "--pasv-end", description: "Passive data port range end (default 50100)") { Arity = ArgumentArity.ZeroOrOne };
var authOption = new Option<string>(name: "--auth", description: "Authenticator plugin name (e.g., InMemory)") { Arity = ArgumentArity.ZeroOrOne };
var storageOption = new Option<string>(name: "--storage", description: "Storage provider plugin name (e.g., InMemory, FileSystem)") { Arity = ArgumentArity.ZeroOrOne };
var healthEnabled = new Option<bool?>(name: "--health", description: "Enable health endpoint (default false)") { Arity = ArgumentArity.ZeroOrOne };
var healthUrl = new Option<string>(name: "--health-url", description: "Health endpoint URL prefix, e.g., http://127.0.0.1:8080/") { Arity = ArgumentArity.ZeroOrOne };

var root = new RootCommand("Minimal FTP Server with plugins")
{
    portOption, addressOption, maxSessionsOption, passiveStartOption, passiveEndOption, authOption, storageOption, healthEnabled, healthUrl
};

root.SetHandler(async (InvocationContext ctx) =>
{
    var pr = ctx.ParseResult;
    var port = pr.GetValueForOption(portOption);
    var address = pr.GetValueForOption(addressOption);
    var maxSessions = pr.GetValueForOption(maxSessionsOption);
    var pasvStart = pr.GetValueForOption(passiveStartOption);
    var pasvEnd = pr.GetValueForOption(passiveEndOption);
    var auth = pr.GetValueForOption(authOption);
    var storage = pr.GetValueForOption(storageOption);
    var health = pr.GetValueForOption(healthEnabled);
    var hUrl = pr.GetValueForOption(healthUrl);

    if (port is not null) builder.Configuration["FtpServer:Port"] = port.Value.ToString();
    if (address is not null) builder.Configuration["FtpServer:ListenAddress"] = address;
    if (maxSessions is not null) builder.Configuration["FtpServer:MaxSessions"] = maxSessions.Value.ToString();
    if (pasvStart is not null) builder.Configuration["FtpServer:PassivePortRangeStart"] = pasvStart.Value.ToString();
    if (pasvEnd is not null) builder.Configuration["FtpServer:PassivePortRangeEnd"] = pasvEnd.Value.ToString();
    if (auth is not null) builder.Configuration["FtpServer:Authenticator"] = auth;
    if (storage is not null) builder.Configuration["FtpServer:StorageProvider"] = storage;
    if (health is not null) builder.Configuration["FtpServer:HealthEnabled"] = (health.Value ? "true" : "false");
    if (hUrl is not null) builder.Configuration["FtpServer:HealthUrl"] = hUrl;

    using var host = builder.Build();
    var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("App");
    var server = host.Services.GetRequiredService<FtpServerHost>();
    var healthServer = host.Services.GetRequiredService<HealthServer>();
    var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

    await Task.WhenAll(server.StartAsync(cts.Token), healthServer.StartAsync(cts.Token));
    logger.LogInformation("Press Ctrl+C to stop.");
    try { await Task.Delay(Timeout.Infinite, cts.Token); } catch (OperationCanceledException) { }
});

await root.InvokeAsync(args);
