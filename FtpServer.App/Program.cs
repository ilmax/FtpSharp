using System.CommandLine;
using System.CommandLine.Invocation;
using FtpServer.Core.Abstractions;
using FtpServer.Core.Configuration;
using FtpServer.Core.FileSystem;
using FtpServer.Core.InMemory;
using FtpServer.Core.Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables("FTP_");

builder.Services.AddOptions<FtpServerOptions>()
    .Bind(builder.Configuration.GetSection("FtpServer"))
    .ValidateDataAnnotations();

builder.Services.AddSingleton<InMemoryAuthenticator>();
builder.Services.AddSingleton<InMemoryStorageProvider>();
builder.Services.AddSingleton<FileSystemStorageProvider>();
builder.Services.AddSingleton<IAuthenticatorFactory, FtpServer.Core.Plugins.PluginRegistry>();
builder.Services.AddSingleton<IStorageProviderFactory, FtpServer.Core.Plugins.PluginRegistry>();
builder.Services.AddSingleton<FtpServerHost>();

var portOption = new Option<int?>(name: "--port") { Description = "Control connection port", Arity = ArgumentArity.ZeroOrOne };
var addressOption = new Option<string>(name: "--listen") { Description = "IP address to bind", Arity = ArgumentArity.ZeroOrOne };
var maxSessionsOption = new Option<int?>(name: "--max-sessions") { Description = "Max concurrent sessions", Arity = ArgumentArity.ZeroOrOne };
var passiveStartOption = new Option<int?>(name: "--pasv-start") { Description = "Passive range start", Arity = ArgumentArity.ZeroOrOne };
var passiveEndOption = new Option<int?>(name: "--pasv-end") { Description = "Passive range end", Arity = ArgumentArity.ZeroOrOne };
var authOption = new Option<string>(name: "--auth") { Description = "Authenticator plugin", Arity = ArgumentArity.ZeroOrOne };
var storageOption = new Option<string>(name: "--storage") { Description = "Storage provider plugin", Arity = ArgumentArity.ZeroOrOne };
var healthEnabled = new Option<bool?>(name: "--health") { Description = "Enable health endpoint", Arity = ArgumentArity.ZeroOrOne };
var healthUrl = new Option<string>(name: "--health-url") { Description = "Health URL prefix", Arity = ArgumentArity.ZeroOrOne };

var cmd = new RootCommand("FTP Server host with ASP.NET Core health")
{
    portOption, addressOption, maxSessionsOption, passiveStartOption, passiveEndOption, authOption, storageOption, healthEnabled, healthUrl
};

cmd.SetHandler(async (InvocationContext ctx) =>
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
});

await cmd.InvokeAsync(args);

var app = builder.Build();

// Minimal health endpoints via ASP.NET Core
if (builder.Configuration.GetValue("FtpServer:HealthEnabled", false))
{
    app.MapGet("/health", () => Results.Text("OK", "text/plain"));
    app.MapGet("/metrics-snapshot", () => Results.Json(new { status = "ok", ts = DateTimeOffset.UtcNow }));
}

// Start FTP server in background
var ftp = app.Services.GetRequiredService<FtpServerHost>();
var appCts = new CancellationTokenSource();
_ = ftp.StartAsync(appCts.Token);

app.Lifetime.ApplicationStopping.Register(() => appCts.Cancel());

await app.RunAsync();
