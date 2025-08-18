using System.CommandLine;
using System.CommandLine.Invocation;
using FtpServer.Core.Configuration;
using FtpServer.Core.Server;
using FtpServer.App.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables("FTP_");

builder.Services.AddOptions<FtpServerOptions>()
    .Bind(builder.Configuration.GetSection("FtpServer"))
    .ValidateDataAnnotations();

builder.Services.AddFtpServerCore();
builder.Services.AddFtpServerObservability();

var portOption = new Option<int?>(name: "--port") { Description = "Control connection port", Arity = ArgumentArity.ZeroOrOne };
var addressOption = new Option<string>(name: "--listen") { Description = "IP address to bind", Arity = ArgumentArity.ZeroOrOne };
var maxSessionsOption = new Option<int?>(name: "--max-sessions") { Description = "Max concurrent sessions", Arity = ArgumentArity.ZeroOrOne };
var passiveStartOption = new Option<int?>(name: "--pasv-start") { Description = "Passive range start", Arity = ArgumentArity.ZeroOrOne };
var passiveEndOption = new Option<int?>(name: "--pasv-end") { Description = "Passive range end", Arity = ArgumentArity.ZeroOrOne };
var authOption = new Option<string>(name: "--auth") { Description = "Authenticator plugin", Arity = ArgumentArity.ZeroOrOne };
var storageOption = new Option<string>(name: "--storage") { Description = "Storage provider plugin", Arity = ArgumentArity.ZeroOrOne };
var storageRootOption = new Option<string>(name: "--storage-root") { Description = "Storage root path", Arity = ArgumentArity.ZeroOrOne };
var healthEnabled = new Option<bool?>(name: "--health") { Description = "Enable health endpoint", Arity = ArgumentArity.ZeroOrOne };
var healthUrl = new Option<string>(name: "--health-url") { Description = "Health URL prefix", Arity = ArgumentArity.ZeroOrOne };
var dataOpenTimeout = new Option<int?>(name: "--data-open-timeout") { Description = "Data open timeout (ms)", Arity = ArgumentArity.ZeroOrOne };
var dataTransferTimeout = new Option<int?>(name: "--data-transfer-timeout") { Description = "Data transfer timeout (ms)", Arity = ArgumentArity.ZeroOrOne };
var controlReadTimeout = new Option<int?>(name: "--control-read-timeout") { Description = "Control read timeout (ms)", Arity = ArgumentArity.ZeroOrOne };
var dataRateLimit = new Option<int?>(name: "--rate-limit") { Description = "Per-transfer data rate limit (bytes/sec)", Arity = ArgumentArity.ZeroOrOne };
var ftpsExplicit = new Option<bool?>(name: "--ftps-explicit") { Description = "Enable explicit FTPS (AUTH TLS)", Arity = ArgumentArity.ZeroOrOne };
var ftpsImplicit = new Option<bool?>(name: "--ftps-implicit") { Description = "Enable implicit FTPS", Arity = ArgumentArity.ZeroOrOne };
var ftpsImplicitPort = new Option<int?>(name: "--ftps-implicit-port") { Description = "Port for implicit FTPS", Arity = ArgumentArity.ZeroOrOne };
var tlsCertPath = new Option<string>(name: "--tls-cert") { Description = "Path to PFX certificate", Arity = ArgumentArity.ZeroOrOne };
var tlsCertPassword = new Option<string>(name: "--tls-cert-pass") { Description = "Password for PFX certificate", Arity = ArgumentArity.ZeroOrOne };
var tlsSelfSigned = new Option<bool?>(name: "--tls-self-signed") { Description = "Generate self-signed cert if none provided", Arity = ArgumentArity.ZeroOrOne };

var cmd = new RootCommand("FTP Server host with ASP.NET Core health")
{
    portOption, addressOption, maxSessionsOption, passiveStartOption, passiveEndOption,
    authOption, storageOption, storageRootOption,
    healthEnabled, healthUrl,
    dataOpenTimeout, dataTransferTimeout, controlReadTimeout, dataRateLimit,
    ftpsExplicit, ftpsImplicit, ftpsImplicitPort,
    tlsCertPath, tlsCertPassword, tlsSelfSigned
};

cmd.AddFtpCliOptions(
    portOption,
    addressOption,
    maxSessionsOption,
    passiveStartOption,
    passiveEndOption,
    authOption,
    storageOption,
    storageRootOption,
    healthEnabled,
    healthUrl,
    dataOpenTimeout,
    dataTransferTimeout,
    controlReadTimeout,
    dataRateLimit,
    ftpsExplicit,
    ftpsImplicit,
    ftpsImplicitPort,
    tlsCertPath,
    tlsCertPassword,
    tlsSelfSigned,
    builder.Configuration);

await cmd.InvokeAsync(args);

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
