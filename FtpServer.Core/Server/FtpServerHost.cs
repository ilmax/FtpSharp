using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using FtpServer.Core.Abstractions;
using FtpServer.Core.Configuration;
using FtpServer.Core.Observability;
using Microsoft.Extensions.Logging;
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
    private TcpListener? _implicitListener;
    private readonly PassivePortPool _passivePool;
    private readonly TlsCertificateProvider _certProvider;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<Task, byte> _sessions = new();

    public FtpServerHost(
        IOptions<FtpServerOptions> options,
        ILogger<FtpServerHost> logger,
    IAuthenticatorFactory authFactory,
    IStorageProviderFactory storageFactory,
    PassivePortPool passivePool,
    TlsCertificateProvider certProvider)
    {
        _options = options;
        _logger = logger;
        _authFactory = authFactory;
        _storageFactory = storageFactory;
        _passivePool = passivePool;
        _certProvider = certProvider;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        var ep = new IPEndPoint(IPAddress.Parse(_options.Value.ListenAddress), _options.Value.Port);
        _listener = new TcpListener(ep);
        _listener.Start();
        _logger.LogInformation("FTP server listening on {Endpoint}", ep);

        _ = AcceptLoop(ct);

        if (_options.Value.FtpsImplicitEnabled)
        {
            var iep = new IPEndPoint(IPAddress.Parse(_options.Value.ListenAddress), _options.Value.FtpsImplicitPort);
            _implicitListener = new TcpListener(iep);
            _implicitListener.Start();
            _logger.LogInformation("Implicit FTPS listening on {Endpoint}", iep);
            _ = AcceptLoopImplicit(ct);
        }
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

    private async Task AcceptLoopImplicit(CancellationToken ct)
    {
        if (_implicitListener is null) return;
        try
        {
            var cert = _certProvider.GetOrCreate(_options);
            while (!ct.IsCancellationRequested)
            {
                var client = await _implicitListener.AcceptTcpClientAsync(ct);
                if (_sessions.Count >= _options.Value.MaxSessions)
                {
                    client.Close();
                    continue;
                }
                Metrics.SessionsActive.Add(1);
                var task = Task.Run(async () =>
                {
                    try
                    {
                        var ssl = new SslStream(client.GetStream(), leaveInnerStreamOpen: false);
                        await ssl.AuthenticateAsServerAsync(cert, clientCertificateRequired: false, SslProtocols.Tls12 | SslProtocols.Tls13, checkCertificateRevocation: false);
                        var opts = _options.Value;
                        var session = new FtpSession(client, _authFactory.Create(opts.Authenticator), _storageFactory.Create(opts.StorageProvider), _options, _passivePool, ssl, isTls: true);
                        await session.RunAsync(ct);
                    }
                    catch (Exception ex)
                    {
                        Metrics.ErrorsTotal.Add(1);
                        _logger.LogError(ex, "Implicit FTPS session terminated with error");
                    }
                    finally
                    {
                        Metrics.SessionsActive.Add(-1);
                    }
                }, ct);
                _sessions.TryAdd(task, 0);
                _ = task.ContinueWith(t => _sessions.TryRemove(t, out _), TaskScheduler.Default);
            }
        }
        catch (OperationCanceledException) { }
    }

    public async ValueTask DisposeAsync()
    {
        _listener?.Stop();
        _implicitListener?.Stop();
        try { await Task.WhenAll(_sessions.Keys); } catch { }
    }
}
