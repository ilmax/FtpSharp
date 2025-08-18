using System.CommandLine;
using System.CommandLine.Invocation;
using FtpServer.Core.Abstractions;
using FtpServer.Core.Configuration;
using FtpServer.Core.FileSystem;
using FtpServer.Core.InMemory;
using FtpServer.Core.Server;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables("FTP_");

builder.Services.AddOptions<FtpServerOptions>()
    .Bind(builder.Configuration.GetSection("FtpServer"))
    .ValidateDataAnnotations();

builder.Services.AddSingleton<InMemoryAuthenticator>();
builder.Services.AddSingleton<FtpServer.Core.Basic.BasicAuthenticator>();
builder.Services.AddSingleton<InMemoryStorageProvider>();
builder.Services.AddSingleton<FileSystemStorageProvider>();
builder.Services.AddSingleton<IAuthenticatorFactory, FtpServer.Core.Plugins.PluginRegistry>();
builder.Services.AddSingleton<IStorageProviderFactory, FtpServer.Core.Plugins.PluginRegistry>();
builder.Services.AddSingleton<FtpServerHost>();
builder.Services.AddSingleton<PassivePortPool>();
builder.Services.AddSingleton<TlsCertificateProvider>();

// OpenTelemetry metrics with Prometheus exporter
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService(serviceName: "FtpServer", serviceVersion: "1.0.0"))
    .WithMetrics(metrics =>
    {
        metrics
            .AddMeter(FtpServer.Core.Observability.Metrics.MeterName)
            .AddAspNetCoreInstrumentation()
            .AddRuntimeInstrumentation()
            .AddPrometheusExporter();
    });

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

cmd.SetHandler((InvocationContext ctx) =>
{
    var pr = ctx.ParseResult;
    var port = pr.GetValueForOption(portOption);
    var address = pr.GetValueForOption(addressOption);
    var maxSessions = pr.GetValueForOption(maxSessionsOption);
    var pasvStart = pr.GetValueForOption(passiveStartOption);
    var pasvEnd = pr.GetValueForOption(passiveEndOption);
    var auth = pr.GetValueForOption(authOption);
    var storage = pr.GetValueForOption(storageOption);
    var storageRoot = pr.GetValueForOption(storageRootOption);
    var health = pr.GetValueForOption(healthEnabled);
    var hUrl = pr.GetValueForOption(healthUrl);
    var dOpen = pr.GetValueForOption(dataOpenTimeout);
    var dXfer = pr.GetValueForOption(dataTransferTimeout);
    var ctlRead = pr.GetValueForOption(controlReadTimeout);
    var rate = pr.GetValueForOption(dataRateLimit);
    var exp = pr.GetValueForOption(ftpsExplicit);
    var imp = pr.GetValueForOption(ftpsImplicit);
    var impPort = pr.GetValueForOption(ftpsImplicitPort);
    var certPath = pr.GetValueForOption(tlsCertPath);
    var certPass = pr.GetValueForOption(tlsCertPassword);
    var selfSigned = pr.GetValueForOption(tlsSelfSigned);

    if (port is not null) builder.Configuration["FtpServer:Port"] = port.Value.ToString();
    if (address is not null) builder.Configuration["FtpServer:ListenAddress"] = address;
    if (maxSessions is not null) builder.Configuration["FtpServer:MaxSessions"] = maxSessions.Value.ToString();
    if (pasvStart is not null) builder.Configuration["FtpServer:PassivePortRangeStart"] = pasvStart.Value.ToString();
    if (pasvEnd is not null) builder.Configuration["FtpServer:PassivePortRangeEnd"] = pasvEnd.Value.ToString();
    if (auth is not null) builder.Configuration["FtpServer:Authenticator"] = auth;
    if (storage is not null) builder.Configuration["FtpServer:StorageProvider"] = storage;
    if (storageRoot is not null) builder.Configuration["FtpServer:StorageRoot"] = storageRoot;
    if (health is not null) builder.Configuration["FtpServer:HealthEnabled"] = (health.Value ? "true" : "false");
    if (hUrl is not null) builder.Configuration["FtpServer:HealthUrl"] = hUrl;
    if (dOpen is not null) builder.Configuration["FtpServer:DataOpenTimeoutMs"] = dOpen.Value.ToString();
    if (dXfer is not null) builder.Configuration["FtpServer:DataTransferTimeoutMs"] = dXfer.Value.ToString();
    if (ctlRead is not null) builder.Configuration["FtpServer:ControlReadTimeoutMs"] = ctlRead.Value.ToString();
    if (rate is not null) builder.Configuration["FtpServer:DataRateLimitBytesPerSec"] = rate.Value.ToString();
    if (exp is not null) builder.Configuration["FtpServer:FtpsExplicitEnabled"] = (exp.Value ? "true" : "false");
    if (imp is not null) builder.Configuration["FtpServer:FtpsImplicitEnabled"] = (imp.Value ? "true" : "false");
    if (impPort is not null) builder.Configuration["FtpServer:FtpsImplicitPort"] = impPort.Value.ToString();
    if (certPath is not null) builder.Configuration["FtpServer:TlsCertPath"] = certPath;
    if (certPass is not null) builder.Configuration["FtpServer:TlsCertPassword"] = certPass;
    if (selfSigned is not null) builder.Configuration["FtpServer:TlsSelfSigned"] = (selfSigned.Value ? "true" : "false");
});

await cmd.InvokeAsync(args);

var app = builder.Build();

// Minimal health endpoints via ASP.NET Core
if (builder.Configuration.GetValue("FtpServer:HealthEnabled", false))
{
    app.MapGet("/health", () => Results.Text("OK", "text/plain"));
    app.MapGet("/metrics-snapshot", () => Results.Json(new { status = "ok", ts = DateTimeOffset.UtcNow }));
}

// Prometheus scrape endpoint
app.MapPrometheusScrapingEndpoint();

// Start FTP server in background
var ftp = app.Services.GetRequiredService<FtpServerHost>();
var appCts = new CancellationTokenSource();
_ = ftp.StartAsync(appCts.Token);

app.Lifetime.ApplicationStopping.Register(() => appCts.Cancel());

await app.RunAsync();
