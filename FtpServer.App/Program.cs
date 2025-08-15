using System.CommandLine;
using System.CommandLine.Binding;
using FtpServer.Core.Abstractions;
using FtpServer.Core.Configuration;
using FtpServer.Core.FileSystem;
using FtpServer.Core.InMemory;
using FtpServer.Core.Server;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Minimal generic host bootstrapping the FTP server. Reads configuration from env and command line.
var builder = Host.CreateApplicationBuilder([]);

// Read from environment variables (prefix FTP_) and command-line switches (--FtpServer:Port=2121 etc)
builder.Configuration
    .AddEnvironmentVariables("FTP_");

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

var portOption = new Option<int?>(name: "--port", description: "Control connection port to listen on (default 21)") { Arity = ArgumentArity.ZeroOrOne };
var addressOption = new Option<string>(name: "--listen", description: "IP address to bind (default 0.0.0.0)") { Arity = ArgumentArity.ZeroOrOne };
var maxSessionsOption = new Option<int?>(name: "--max-sessions", description: "Maximum concurrent sessions (default 100)") { Arity = ArgumentArity.ZeroOrOne };
var passiveStartOption = new Option<int?>(name: "--pasv-start", description: "Passive data port range start (default 50000)") { Arity = ArgumentArity.ZeroOrOne };
var passiveEndOption = new Option<int?>(name: "--pasv-end", description: "Passive data port range end (default 50100)") { Arity = ArgumentArity.ZeroOrOne };
var authOption = new Option<string>(name: "--auth", description: "Authenticator plugin name (e.g., InMemory)") { Arity = ArgumentArity.ZeroOrOne };
var storageOption = new Option<string>(name: "--storage", description: "Storage provider plugin name (e.g., InMemory, FileSystem)") { Arity = ArgumentArity.ZeroOrOne };

var root = new RootCommand("Minimal FTP Server with plugins")
{
    portOption, addressOption, maxSessionsOption, passiveStartOption, passiveEndOption, authOption, storageOption
};

root.SetHandler<int?, string?, int?, int?, int?, string?, string?>(async (port, address, maxSessions, pasvStart, pasvEnd, auth, storage) =>
{
    if (port is not null) builder.Configuration["FtpServer:Port"] = port.Value.ToString();
    if (address is not null) builder.Configuration["FtpServer:ListenAddress"] = address;
    if (maxSessions is not null) builder.Configuration["FtpServer:MaxSessions"] = maxSessions.Value.ToString();
    if (pasvStart is not null) builder.Configuration["FtpServer:PassivePortRangeStart"] = pasvStart.Value.ToString();
    if (pasvEnd is not null) builder.Configuration["FtpServer:PassivePortRangeEnd"] = pasvEnd.Value.ToString();
    if (auth is not null) builder.Configuration["FtpServer:Authenticator"] = auth;
    if (storage is not null) builder.Configuration["FtpServer:StorageProvider"] = storage;

    using var host = builder.Build();
    var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("App");
    var server = host.Services.GetRequiredService<FtpServerHost>();
    var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

    await server.StartAsync(cts.Token);
    logger.LogInformation("Press Ctrl+C to stop.");
    try { await Task.Delay(Timeout.Infinite, cts.Token); } catch (OperationCanceledException) { }
}, portOption, addressOption, maxSessionsOption, passiveStartOption, passiveEndOption, authOption, storageOption);

await root.InvokeAsync(args);
