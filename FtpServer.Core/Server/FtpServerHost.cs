using System.Net;
using System.Net.Sockets;
using FtpServer.Core.Abstractions;
using FtpServer.Core.Configuration;
using Microsoft.Extensions.Logging;
using FtpServer.Core.Observability;
using Microsoft.Extensions.Options;

namespace FtpServer.Core.Server;

/// <summary>
/// Lightweight TCP listener that accepts control connections and spawns sessions.
/// </summary>
public sealed class FtpServerHost : IAsyncDisposable
{
    private readonly IOptions<FtpServerOptions> _options;
    private readonly ILogger<FtpServerHost> _logger;
    private readonly IAuthenticatorFactory _authFactory;
    private readonly IStorageProviderFactory _storageFactory;
    private TcpListener? _listener;
    private readonly PassivePortPool _passivePool;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<Task, byte> _sessions = new();

    public FtpServerHost(
        IOptions<FtpServerOptions> options,
        ILogger<FtpServerHost> logger,
    IAuthenticatorFactory authFactory,
    IStorageProviderFactory storageFactory,
    PassivePortPool passivePool)
    {
        _options = options;
        _logger = logger;
        _authFactory = authFactory;
        _storageFactory = storageFactory;
        _passivePool = passivePool;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        var ep = new IPEndPoint(IPAddress.Parse(_options.Value.ListenAddress), _options.Value.Port);
        _listener = new TcpListener(ep);
        _listener.Start();
        _logger.LogInformation("FTP server listening on {Endpoint}", ep);

        _ = AcceptLoop(ct);
        await Task.CompletedTask;
    }

    private async Task AcceptLoop(CancellationToken ct)
    {
        if (_listener is null) return;
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync(ct);
                if (_sessions.Count >= _options.Value.MaxSessions)
                {
                    client.Close();
                    continue;
                }
                var opts = _options.Value;
                var session = new FtpSession(client, _authFactory.Create(opts.Authenticator), _storageFactory.Create(opts.StorageProvider), _options, _passivePool);
                Metrics.SessionsActive.Add(1);
                var task = session.RunAsync(ct);
                _sessions.TryAdd(task, 0);
                _ = task.ContinueWith(t =>
                {
                    _sessions.TryRemove(t, out _);
                    Metrics.SessionsActive.Add(-1);
                    if (t.IsFaulted)
                    {
                        Metrics.ErrorsTotal.Add(1);
                        _logger.LogError(t.Exception, "Session terminated with error");
                    }
                }, TaskScheduler.Default);
            }
        }
        catch (OperationCanceledException) { }
    }

    public async ValueTask DisposeAsync()
    {
        _listener?.Stop();
        try { await Task.WhenAll(_sessions.Keys); } catch { }
    }
}
