using System.Net;
using System.Net.Sockets;
using System.Text;
using FtpServer.Core.Abstractions;
using FtpServer.Core.Configuration;
using FtpServer.Core.Observability;
using FtpServer.Core.Protocol;
// Unused imports removed after handler extraction

using FtpServer.Core.Server.Commands;
using FtpServer.Core.Server.Commands.Handlers;
using Microsoft.Extensions.Options;

namespace FtpServer.Core.Server;

/// <summary>
/// Handles a single control connection session. Minimal MVP for iteration.
/// </summary>
public sealed class FtpSession : IFtpSessionContext
{
    private readonly TcpClient _client;
    private readonly IAuthenticator _auth;
    private readonly IStorageProvider _storage;
    private readonly IOptions<FtpServerOptions> _options;
    private readonly PassivePortPool _passivePool;

    private bool _isAuthenticated;
    private string? _pendingUser;
    private string? _pendingRenameFrom;
    private string _cwd = "/";
    private TcpListener? _pasvListener;
    private PassiveLease? _pasvLease;
    private char _type = 'I'; // I=binary, A=ascii
    public char TransferType { get => _type; set => _type = value; }
    private IPEndPoint? _activeEndpoint;
    private long _restartOffset;
    private Stream? _initialControlStream;
    internal long RestartOffset { get => _restartOffset; set => _restartOffset = value; }
    private readonly string _sessionId = Guid.NewGuid().ToString("N");
    public string SessionId => _sessionId;
    private bool _controlTls;
    public bool IsControlTls { get => _controlTls; set => _controlTls = value; }
    private char _prot = 'C';
    public char DataProtectionLevel { get => _prot; set => _prot = value; }

    public FtpSession(TcpClient client, IAuthenticator auth, IStorageProvider storage, IOptions<FtpServerOptions> options, PassivePortPool passivePool)
    {
        _client = client;
        _auth = auth;
        _storage = storage;
        _options = options;
        _passivePool = passivePool;
        _handlers = new Dictionary<string, IFtpCommandHandler>(StringComparer.OrdinalIgnoreCase)
        {
            ["NOOP"] = new NoopHandler(),
            ["SYST"] = new SystHandler(),
            ["PWD"] = new PwdHandler(),
            ["CDUP"] = new CdupHandler(),
            ["HELP"] = new HelpHandler(),
            ["FEAT"] = new FeatHandler(),
            ["STAT"] = new StatHandler(),
            ["TYPE"] = new TypeHandler(),
            ["SIZE"] = new SizeHandler(_storage),
            ["QUIT"] = new QuitHandler(),
            ["USER"] = new UserHandler(),
            ["PASS"] = new PassHandler(_auth),
            ["CWD"] = new CwdHandler(_storage),
            ["MKD"] = new MkdHandler(_storage),
            ["RMD"] = new RmdHandler(_storage),
            ["DELE"] = new DeleHandler(_storage),
            ["RNFR"] = new RnfrHandler(_storage),
            ["RNTO"] = new RntoHandler(_storage),
            ["LIST"] = new ListHandler(_storage),
            ["NLST"] = new NlstHandler(_storage),
            ["PASV"] = new PasvHandler(),
            ["EPSV"] = new EpsvHandler(),
            ["PORT"] = new PortHandler(),
            ["EPRT"] = new EprtHandler(),
            ["RETR"] = new RetrHandler(_storage, _options),
            ["STOR"] = new StorHandler(_storage, _options),
            ["REST"] = new RestHandler(this),
            ["APPE"] = new AppeHandler(_storage, _options, this),
            ["MODE"] = new ModeHandler(),
            ["STRU"] = new StruHandler(),
            ["ALLO"] = new AlloHandler(),
            ["AUTH"] = new AuthTlsHandler(),
            ["PBSZ"] = new PbszHandler(),
            ["PROT"] = new ProtHandler(),
        };
    }

    // Back-compat for tests and callers not wiring PassivePortPool
    public FtpSession(TcpClient client, IAuthenticator auth, IStorageProvider storage, IOptions<FtpServerOptions> options)
        : this(client, auth, storage, options, passivePool: null!) { }

    private readonly Dictionary<string, IFtpCommandHandler> _handlers;

    public string Cwd { get => _cwd; set => _cwd = value; }
    public bool IsAuthenticated { get => _isAuthenticated; set => _isAuthenticated = value; }
    public string? PendingUser { get => _pendingUser; set => _pendingUser = value; }
    public bool ShouldQuit { get; set; }
    public string? PendingRenameFrom { get => _pendingRenameFrom; set => _pendingRenameFrom = value; }
    public IPEndPoint? ActiveEndpoint { get => _activeEndpoint; set => _activeEndpoint = value; }


    // Overload to supply an already-wrapped control stream (e.g., implicit FTPS)
    public FtpSession(TcpClient client, IAuthenticator auth, IStorageProvider storage, IOptions<FtpServerOptions> options, PassivePortPool passivePool, Stream initialControlStream, bool isTls)
        : this(client, auth, storage, options, passivePool)
    {
        _initialControlStream = initialControlStream;
        _controlTls = isTls;
    }
    public async Task RunAsync(CancellationToken ct)
    {
        using var client = _client;
        using var stream = client.GetStream();
        Stream controlStream = _initialControlStream ?? stream;
        var writer = new StreamWriter(controlStream, Encoding.ASCII) { NewLine = "\r\n", AutoFlush = true };
        var reader = new StreamReader(controlStream, Encoding.ASCII, false, 1024, true);

        await writer.WriteLineAsync("220 Service ready");
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var token = ct;
                if (_options.Value.ControlReadTimeoutMs > 0)
                {
                    using var readCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    readCts.CancelAfter(_options.Value.ControlReadTimeoutMs);
                    token = readCts.Token;
                }
                string? line;
                try
                {
                    // StreamReader doesn't accept a token; use WaitAsync to apply timeout
                    line = await reader.ReadLineAsync().WaitAsync(token);
                }
                catch (OperationCanceledException)
                {
                    // idle timeout or cancellation; close session
                    break;
                }
                if (line is null) break;
                var parsed = FtpCommandParser.Parse(line);
                Metrics.CommandsTotal.Add(1, new KeyValuePair<string, object?>("command", parsed.Command));
                if (_handlers.TryGetValue(parsed.Command, out var handler))
                {
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    await handler.HandleAsync(this, parsed, writer, ct);
                    if (parsed.Command.Equals("AUTH", StringComparison.OrdinalIgnoreCase) && _controlTls)
                    {
                        controlStream = await UpgradeControlToTlsAsync(ct);
                        writer = new StreamWriter(controlStream, Encoding.ASCII) { NewLine = "\r\n", AutoFlush = true };
                        reader = new StreamReader(controlStream, Encoding.ASCII, false, 1024, true);
                    }
                    sw.Stop();
                    Metrics.CommandDurationMs.Record(sw.Elapsed.TotalMilliseconds, new KeyValuePair<string, object?>("command", parsed.Command));
                    if (ShouldQuit) return;
                    continue;
                }
                await writer.WriteLineAsync("502 Command not implemented");
            }
        }
        finally
        {
            // Ensure any passive data listener is closed when the session ends
            try { _pasvListener?.Stop(); } catch { }
            try { if (_pasvLease is not null) _ = _pasvLease.DisposeAsync(); } catch { }
            _pasvListener = null;
            _pasvLease = null;
        }
    }

    private string GetPassiveAdvertisedIp()
    {
        string? configuredPublic = _options.Value.PassivePublicIp;
        if (!string.IsNullOrWhiteSpace(configuredPublic)) return configuredPublic!;
        string configured = _options.Value.ListenAddress;
        if (string.IsNullOrWhiteSpace(configured) || configured == "0.0.0.0" || configured == "::")
        {
            if (_client.Client.LocalEndPoint is IPEndPoint lep)
            {
                return lep.Address.ToString();
            }
            return "127.0.0.1";
        }
        return configured;
    }

    public async Task<Stream> UpgradeControlToTlsAsync(CancellationToken ct)
    {
        var certProvider = new TlsCertificateProvider();
        var cert = certProvider.GetOrCreate(_options);
        var ssl = new System.Net.Security.SslStream(_client.GetStream(), leaveInnerStreamOpen: false);
        await ssl.AuthenticateAsServerAsync(cert, clientCertificateRequired: false, System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13, checkCertificateRevocation: false);
        return ssl;
    }

    public PassiveEndpoint EnterPassiveMode()
    {
        try { _pasvListener?.Stop(); } catch { }
        _pasvListener = null;
        try { if (_pasvLease is not null) _ = _pasvLease.DisposeAsync(); } catch { }
        _pasvLease = null;
        if (_passivePool is not null)
        {
            var lease = _passivePool.LeaseAsync(CancellationToken.None).GetAwaiter().GetResult();
            _pasvLease = lease;
            _pasvListener = lease.Listener;
            string ip = GetPassiveAdvertisedIp();
            return new PassiveEndpoint(ip, lease.Port);
        }
        // Fallback linear scan
        int start = _options.Value.PassivePortRangeStart;
        int end = _options.Value.PassivePortRangeEnd;
        for (int p = start; p <= end; p++)
        {
            try
            {
                // Bind to all interfaces for container/NAT friendliness
                var l = new TcpListener(IPAddress.Any, p);
                l.Start();
                _pasvListener = l;
                string ip = GetPassiveAdvertisedIp();
                return new PassiveEndpoint(ip, p);
            }
            catch { }
        }
        throw new IOException("No passive ports available");
    }

    public string ResolvePath(string arg)
    {
        if (string.IsNullOrWhiteSpace(arg)) return _cwd;
        if (arg.StartsWith('/')) return arg.TrimEnd('/');
        if (_cwd == "/") return "/" + arg.TrimEnd('/');
        return _cwd.TrimEnd('/') + "/" + arg.TrimEnd('/');
    }

    public async Task<Stream> OpenDataStreamAsync(CancellationToken ct)
    {
        using var openTimeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        openTimeoutCts.CancelAfter(_options.Value.DataOpenTimeoutMs);
        var tok = openTimeoutCts.Token;
        if (_pasvListener is not null)
        {
            var client = await _pasvListener.AcceptTcpClientAsync(tok);
            _pasvListener.Stop();
            _pasvListener = null;
            // Release the passive port lease immediately after accepting the data connection.
            // The accepted TcpClient uses its own socket and does not require the listener to remain bound.
            // Keeping the lease would unnecessarily hold the port and could exhaust the passive range under load.
            if (_pasvLease is not null)
            {
                try { await _pasvLease.DisposeAsync(); } catch { }
                _pasvLease = null;
            }
            Metrics.SessionActiveTransfers.Add(1, new KeyValuePair<string, object?>("session_id", _sessionId));
            Stream ds = client.GetStream();
            if (_prot == 'P')
            {
                var cert = new TlsCertificateProvider().GetOrCreate(_options);
                var ssl = new System.Net.Security.SslStream(ds, leaveInnerStreamOpen: false);
                await ssl.AuthenticateAsServerAsync(cert, clientCertificateRequired: false, System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13, checkCertificateRevocation: false);
                ds = ssl;
            }
            return new SessionTaggedStream(ds, () => Metrics.SessionActiveTransfers.Add(-1, new KeyValuePair<string, object?>("session_id", _sessionId)));
        }
        if (_activeEndpoint is not null)
        {
            var client = new TcpClient();
            await client.ConnectAsync(_activeEndpoint, tok);
            _activeEndpoint = null;
            Metrics.SessionActiveTransfers.Add(1, new KeyValuePair<string, object?>("session_id", _sessionId));
            Stream ds = client.GetStream();
            if (_prot == 'P')
            {
                var cert = new TlsCertificateProvider().GetOrCreate(_options);
                var ssl = new System.Net.Security.SslStream(ds, leaveInnerStreamOpen: false);
                await ssl.AuthenticateAsServerAsync(cert, clientCertificateRequired: false, System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13, checkCertificateRevocation: false);
                ds = ssl;
            }
            return new SessionTaggedStream(ds, () => Metrics.SessionActiveTransfers.Add(-1, new KeyValuePair<string, object?>("session_id", _sessionId)));
        }
        throw new IOException("425 Can't open data connection");
    }

    // All command-specific parsing and formatting moved to handlers

    // Parsing moved to FtpCommandParser
}

internal sealed class SessionTaggedStream : Stream
{
    private readonly Stream _inner;
    private readonly Action _onDispose;
    public SessionTaggedStream(Stream inner, Action onDispose)
    {
        _inner = inner;
        _onDispose = onDispose;
    }
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        try { _inner.Dispose(); } finally { _onDispose(); }
    }
    public override async ValueTask DisposeAsync()
    {
        try { await _inner.DisposeAsync(); } finally { _onDispose(); }
    }
    public override bool CanRead => _inner.CanRead;
    public override bool CanSeek => _inner.CanSeek;
    public override bool CanWrite => _inner.CanWrite;
    public override long Length => _inner.Length;
    public override long Position { get => _inner.Position; set => _inner.Position = value; }
    public override void Flush() => _inner.Flush();
    public override Task FlushAsync(CancellationToken cancellationToken) => _inner.FlushAsync(cancellationToken);
    public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
    public override int Read(Span<byte> buffer) => _inner.Read(buffer);
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => _inner.ReadAsync(buffer, offset, count, cancellationToken);
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => _inner.ReadAsync(buffer, cancellationToken);
    public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
    public override void SetLength(long value) => _inner.SetLength(value);
    public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);
    public override void Write(ReadOnlySpan<byte> buffer) => _inner.Write(buffer);
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => _inner.WriteAsync(buffer, offset, count, cancellationToken);
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => _inner.WriteAsync(buffer, cancellationToken);
}
