using System.Net.Sockets;
using System.Text;
using FtpServer.Core.Abstractions;
using FtpServer.Core.Protocol;
using FtpServer.Core.Configuration;
using Microsoft.Extensions.Options;
using System.Runtime.InteropServices;
using System.Globalization;

using FtpServer.Core.Server.Commands;

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

    private bool _isAuthenticated;
    private string? _pendingUser;
    private string? _pendingRenameFrom;
    private string _cwd = "/";
    private TcpListener? _pasvListener;
    private char _type = 'I'; // I=binary, A=ascii
    public char TransferType { get => _type; set => _type = value; }
    private System.Net.IPEndPoint? _activeEndpoint;

    public FtpSession(TcpClient client, IAuthenticator auth, IStorageProvider storage, IOptions<FtpServerOptions> options)
    {
        _client = client;
        _auth = auth;
        _storage = storage;
        _options = options;
        _handlers = new Dictionary<string, IFtpCommandHandler>(StringComparer.OrdinalIgnoreCase)
        {
            ["NOOP"] = new NoopHandler(),
            ["SYST"] = new SystHandler(),
            ["PWD"]  = new PwdHandler(),
            ["CDUP"] = new CdupHandler(),
            ["HELP"] = new HelpHandler(),
            ["FEAT"] = new FeatHandler(),
            ["STAT"] = new StatHandler(),
            ["TYPE"] = new TypeHandler(),
            ["SIZE"] = new SizeHandler(_storage),
            ["QUIT"] = new QuitHandler(),
            ["USER"] = new UserHandler(),
            ["PASS"] = new PassHandler(_auth),
            ["CWD"]  = new CwdHandler(_storage),
            ["MKD"]  = new MkdHandler(_storage),
            ["RMD"]  = new RmdHandler(_storage),
            ["DELE"] = new DeleHandler(_storage),
            ["RNFR"] = new RnfrHandler(_storage),
            ["RNTO"] = new RntoHandler(_storage),
        };
    }

    private readonly Dictionary<string, IFtpCommandHandler> _handlers;

    public string Cwd { get => _cwd; set => _cwd = value; }
    public bool IsAuthenticated { get => _isAuthenticated; set => _isAuthenticated = value; }
    public string? PendingUser { get => _pendingUser; set => _pendingUser = value; }
    public bool ShouldQuit { get; set; }
    public string? PendingRenameFrom { get => _pendingRenameFrom; set => _pendingRenameFrom = value; }

    public async Task RunAsync(CancellationToken ct)
    {
        using var client = _client;
        using var stream = client.GetStream();
        var writer = new StreamWriter(stream, Encoding.ASCII) { NewLine = "\r\n", AutoFlush = true };
        var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, true);

        await writer.WriteLineAsync("220 Service ready");
        while (!ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync();
            if (line is null) break;
        var parsed = FtpCommandParser.Parse(line);
        if (_handlers.TryGetValue(parsed.Command, out var handler))
        {
            await handler.HandleAsync(this, parsed, writer, ct);
            if (ShouldQuit) return;
            continue;
        }
        switch (parsed.Command)
            {
                case "PASV":
                    var pe = EnterPassiveMode();
                    var p1 = pe.Port / 256; var p2 = pe.Port % 256;
                    await writer.WriteLineAsync($"227 Entering Passive Mode ({pe.IpAddress.Replace('.', ',')},{p1},{p2})");
                    break;
                case "EPSV":
                    var epe = EnterPassiveMode();
                    await writer.WriteLineAsync($"229 Entering Extended Passive Mode (|||{epe.Port}|)");
                    break;
                case "PORT":
                    if (!TryParsePort(parsed.Argument, out var ep))
                    {
                        await writer.WriteLineAsync("501 Syntax error in parameters or arguments");
                        break;
                    }
                    _activeEndpoint = ep;
                    await writer.WriteLineAsync("200 Command okay");
                    break;
                case "EPRT":
                    if (!TryParseEprt(parsed.Argument, out var ep2))
                    {
                        await writer.WriteLineAsync("501 Syntax error in parameters or arguments");
                        break;
                    }
                    _activeEndpoint = ep2;
                    await writer.WriteLineAsync("200 Command okay");
                    break;
                case "LIST":
                    if (!_isAuthenticated)
                    {
                        await writer.WriteLineAsync("530 Not logged in.");
                        break;
                    }
                    await writer.WriteLineAsync("150 Opening data connection for LIST");
                    try
                    {
                        using (var data = await OpenDataStreamAsync(ct))
                        using (var dw = new StreamWriter(data, Encoding.ASCII) { NewLine = "\r\n", AutoFlush = true })
                        {
                            var entries = await _storage.ListAsync(_cwd, ct);
                            foreach (var e in entries)
                            {
                                await dw.WriteLineAsync(FormatUnixListLine(e));
                            }
                        }
                        await writer.WriteLineAsync("226 Closing data connection. Requested file action successful");
                    }
                    catch (Exception)
                    {
                        await writer.WriteLineAsync("425 Can't open data connection");
                    }
                    break;
                case "NLST":
                    if (!_isAuthenticated)
                    {
                        await writer.WriteLineAsync("530 Not logged in.");
                        break;
                    }
                    await writer.WriteLineAsync("150 Opening data connection for NLST");
                    try
                    {
                        using (var data2 = await OpenDataStreamAsync(ct))
                        using (var dw2 = new StreamWriter(data2, Encoding.ASCII) { NewLine = "\r\n", AutoFlush = true })
                        {
                            var entries = await _storage.ListAsync(_cwd, ct);
                            foreach (var e in entries)
                            {
                                await dw2.WriteLineAsync(e.Name);
                            }
                        }
                        await writer.WriteLineAsync("226 NLST complete");
                    }
                    catch (Exception)
                    {
                        await writer.WriteLineAsync("425 Can't open data connection");
                    }
                    break;
                
                case "RETR":
                    {
                        var path = ResolvePath(parsed.Argument);
                        var entry = await _storage.GetEntryAsync(path, ct);
                        if (entry is null)
                        {
                            await writer.WriteLineAsync("550 File not found");
                            break;
                        }
                        if (entry.IsDirectory)
                        {
                            await writer.WriteLineAsync("550 Not a plain file");
                            break;
                        }
                    }
                    await writer.WriteLineAsync("150 Opening data connection for RETR");
                    try
                    {
                        using (var rs = await OpenDataStreamAsync(ct))
                        using (var bw = new BinaryWriter(rs, Encoding.ASCII, leaveOpen: true))
                        {
                            await foreach (var chunk in _storage.ReadAsync(ResolvePath(parsed.Argument), 8192, ct))
                            {
                                if (_type == 'A')
                                {
                                    // naive ASCII: convert \n to \r\n
                                    var text = Encoding.ASCII.GetString(chunk.Span);
                                    var data = Encoding.ASCII.GetBytes(text.Replace("\n", "\r\n"));
                                    await rs.WriteAsync(data, 0, data.Length, ct);
                                }
                                else
                                {
                                    if (MemoryMarshal.TryGetArray((ReadOnlyMemory<byte>)chunk, out var seg))
                                        await rs.WriteAsync(seg.Array!, seg.Offset, seg.Count, ct);
                                    else
                                        await rs.WriteAsync(chunk.ToArray(), ct);
                                }
                            }
                        }
                        await writer.WriteLineAsync("226 Transfer complete");
                    }
                    catch (Exception)
                    {
                        await writer.WriteLineAsync("425 Can't open data connection");
                    }
                    break;
                case "STOR":
                    {
                        var path = ResolvePath(parsed.Argument);
                        var entry = await _storage.GetEntryAsync(path, ct);
                        if (entry is not null && entry.IsDirectory)
                        {
                            await writer.WriteLineAsync("550 Not a plain file");
                            break;
                        }
                    }
                    await writer.WriteLineAsync("150 Opening data connection for STOR");
                    try
                    {
                        using (var storStream = await OpenDataStreamAsync(ct))
                        {
                            async IAsyncEnumerable<ReadOnlyMemory<byte>> ReadStream([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token)
                            {
                                var buffer = new byte[8192];
                                int read;
                                while ((read = await storStream.ReadAsync(buffer, 0, buffer.Length, token)) > 0)
                                {
                                    if (_type == 'A')
                                    {
                                        // collapse CRLF to LF for ASCII storage
                                        var text = Encoding.ASCII.GetString(buffer, 0, read).Replace("\r\n", "\n");
                                        yield return Encoding.ASCII.GetBytes(text);
                                    }
                                    else
                                    {
                                        yield return new ReadOnlyMemory<byte>(buffer, 0, read).ToArray();
                                    }
                                }
                            }
                            await _storage.WriteAsync(ResolvePath(parsed.Argument), ReadStream(ct), ct);
                        }
                        await writer.WriteLineAsync("226 Transfer complete");
                    }
                    catch (Exception)
                    {
                        await writer.WriteLineAsync("425 Can't open data connection");
                    }
                    break;
                
                
                
                default:
                    await writer.WriteLineAsync("502 Command not implemented");
                    break;
            }
        }
    }

    private PassiveEndpoint EnterPassiveMode()
    {
        _pasvListener?.Stop();
        _pasvListener = null;
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
            catch
            {
                // try next
            }
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

    private async Task<NetworkStream> OpenDataStreamAsync(CancellationToken ct)
    {
        if (_pasvListener is not null)
        {
            var client = await _pasvListener.AcceptTcpClientAsync(ct);
            _pasvListener.Stop();
            _pasvListener = null;
            return client.GetStream();
        }
        if (_activeEndpoint is not null)
        {
            var client = new TcpClient();
            await client.ConnectAsync(_activeEndpoint, ct);
            _activeEndpoint = null;
            return client.GetStream();
        }
        throw new IOException("425 Can't open data connection");
    }

    private static bool TryParsePort(string arg, out System.Net.IPEndPoint? ep)
    {
        ep = null;
        // Format: h1,h2,h3,h4,p1,p2
        var parts = arg.Split(',');
        if (parts.Length != 6) return false;
        if (!byte.TryParse(parts[0], out var h1) || !byte.TryParse(parts[1], out var h2) ||
            !byte.TryParse(parts[2], out var h3) || !byte.TryParse(parts[3], out var h4) ||
            !byte.TryParse(parts[4], out var p1) || !byte.TryParse(parts[5], out var p2)) return false;
        var addr = new System.Net.IPAddress(new byte[] { h1, h2, h3, h4 });
        var port = p1 * 256 + p2;
        ep = new System.Net.IPEndPoint(addr, port);
        return true;
    }

    private static bool TryParseEprt(string arg, out System.Net.IPEndPoint? ep)
    {
        ep = null;
        if (string.IsNullOrEmpty(arg)) return false;
        var delim = arg[0];
        var parts = arg.Split(delim);
        // Expected: "" af addr port ""
        if (parts.Length < 5) return false;
        if (!int.TryParse(parts[1], out var af)) return false;
        var addrStr = parts[2];
        if (!int.TryParse(parts[3], out var port)) return false;
        try
        {
            var addr = System.Net.IPAddress.Parse(addrStr);
            // Optionally, validate af: 1=IPv4, 2=IPv6
            if ((af == 1 && addr.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork) ||
                (af == 2 && addr.AddressFamily != System.Net.Sockets.AddressFamily.InterNetworkV6))
                return false;
            ep = new System.Net.IPEndPoint(addr, port);
            return true;
        }
        catch { return false; }
    }

    private static string FormatUnixListLine(FileSystemEntry e)
    {
        var perms = e.IsDirectory ? 'd' : '-';
        var rights = "rwxr-xr-x"; // placeholder
        var links = 1;
        var owner = "owner";
        var group = "group";
        var size = e.Length ?? 0;
        var date = DateTimeOffset.Now.ToString("MMM dd HH:mm", CultureInfo.InvariantCulture);
        return $"{perms}{rights} {links,3} {owner,5} {group,5} {size,8} {date} {e.Name}";
    }

    // Parsing moved to FtpCommandParser
}
