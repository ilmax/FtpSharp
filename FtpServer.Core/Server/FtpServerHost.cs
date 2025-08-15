using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using FtpServer.Core.Configuration;
using FtpServer.Core.Abstractions;

namespace FtpServer.Core.Server;

/// <summary>
/// Lightweight TCP listener that accepts control connections and spawns sessions.
/// </summary>
public sealed class FtpServerHost : IAsyncDisposable
{
    private readonly IOptions<FtpServerOptions> _options;
    private readonly ILogger<FtpServerHost> _logger;
    private readonly Func<IAuthenticator> _authFactory;
    private readonly Func<IStorageProvider> _storageFactory;
    private TcpListener? _listener;
    private readonly HashSet<Task> _sessions = new();

    public FtpServerHost(
        IOptions<FtpServerOptions> options,
        ILogger<FtpServerHost> logger,
        Func<IAuthenticator> authFactory,
        Func<IStorageProvider> storageFactory)
    {
        _options = options;
        _logger = logger;
        _authFactory = authFactory;
        _storageFactory = storageFactory;
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
                var session = new FtpSession(client, _authFactory(), _storageFactory());
                var task = session.RunAsync(ct);
                _sessions.Add(task);
                _ = task.ContinueWith(t => _sessions.Remove(t), TaskScheduler.Default);
            }
        }
        catch (OperationCanceledException) { }
    }

    public async ValueTask DisposeAsync()
    {
        _listener?.Stop();
        try { await Task.WhenAll(_sessions); } catch { }
    }
}
