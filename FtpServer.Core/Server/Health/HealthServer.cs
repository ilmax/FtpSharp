using System.Net;
using System.Text.Json;
using FtpServer.Core.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FtpServer.Core.Server.Health;

public sealed class HealthServer : IAsyncDisposable
{
    private readonly IOptions<FtpServerOptions> _options;
    private readonly ILogger<HealthServer> _logger;
    private HttpListener? _listener;
    private Task? _loop;

    public HealthServer(IOptions<FtpServerOptions> options, ILogger<HealthServer> logger)
    {
        _options = options;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        if (!_options.Value.HealthEnabled) return;
        string url = _options.Value.HealthUrl;
        _listener = new HttpListener();
        _listener.Prefixes.Add(url);
        _listener.Start();
        _logger.LogInformation("Health endpoint listening on {Url}", url);
        _loop = Task.Run(() => AcceptLoop(ct));
        await Task.CompletedTask;
    }

    private async Task AcceptLoop(CancellationToken ct)
    {
        if (_listener is null) return;
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var ctx = await _listener.GetContextAsync();
                _ = Task.Run(() => Handle(ctx));
            }
        }
        catch when (ct.IsCancellationRequested) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health endpoint error");
        }
    }

    private void Handle(HttpListenerContext ctx)
    {
        try
        {
            string path = ctx.Request.Url?.AbsolutePath ?? "/";
            if (path == "/health")
            {
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes("OK");
                ctx.Response.ContentType = "text/plain";
                ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
                ctx.Response.StatusCode = 200;
                ctx.Response.Close();
                return;
            }
            // Deprecated: metrics-snapshot removed; use Prometheus /metrics from ASP.NET host instead
            ctx.Response.StatusCode = 404;
            ctx.Response.Close();
        }
        catch { }
    }

    public async ValueTask DisposeAsync()
    {
        try { _listener?.Stop(); } catch { }
        if (_loop is not null)
        {
            try { await _loop; } catch { }
        }
    }
}
