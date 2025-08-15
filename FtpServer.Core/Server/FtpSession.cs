using System.Net.Sockets;
using System.Text;
using FtpServer.Core.Abstractions;
using FtpServer.Core.Configuration;
using FtpServer.Core.Protocol;
// Unused imports removed after handler extraction

using FtpServer.Core.Server.Commands;
using Microsoft.Extensions.Options;
using FtpServer.Core.Observability;

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
    private System.Net.IPEndPoint? _activeEndpoint;
    private long _restartOffset;
    internal long RestartOffset { get => _restartOffset; set => _restartOffset = value; }

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
    public System.Net.IPEndPoint? ActiveEndpoint { get => _activeEndpoint; set => _activeEndpoint = value; }

    public async Task RunAsync(CancellationToken ct)
    {
        using var client = _client;
        using var stream = client.GetStream();
        var writer = new StreamWriter(stream, Encoding.ASCII) { NewLine = "\r\n", AutoFlush = true };
        var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, true);

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
            return new PassiveEndpoint("127.0.0.1", lease.Port);
        }
        // Fallback linear scan
        var start = _options.Value.PassivePortRangeStart;
        var end = _options.Value.PassivePortRangeEnd;
        for (int p = start; p <= end; p++)
        {
            try
            {
                var l = new TcpListener(System.Net.IPAddress.Loopback, p);
                l.Start();
                _pasvListener = l;
                return new PassiveEndpoint("127.0.0.1", p);
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
            return client.GetStream();
        }
        if (_activeEndpoint is not null)
        {
            var client = new TcpClient();
            await client.ConnectAsync(_activeEndpoint, tok);
            _activeEndpoint = null;
            return client.GetStream();
        }
        throw new IOException("425 Can't open data connection");
    }

    // All command-specific parsing and formatting moved to handlers

    // Parsing moved to FtpCommandParser
}
