using System.Net.Sockets;
using System.Text;
using FtpServer.Core.Abstractions;
using FtpServer.Core.Protocol;
using FtpServer.Core.Configuration;
using Microsoft.Extensions.Options;
using System.Runtime.InteropServices;

namespace FtpServer.Core.Server;

/// <summary>
/// Handles a single control connection session. Minimal MVP for iteration.
/// </summary>
public sealed class FtpSession
{
    private readonly TcpClient _client;
    private readonly IAuthenticator _auth;
    private readonly IStorageProvider _storage;
    private readonly IOptions<FtpServerOptions> _options;

    private bool _isAuthenticated;
    private string? _pendingUser;
    private string _cwd = "/";
    private TcpListener? _pasvListener;
    private char _type = 'I'; // I=binary, A=ascii

    public FtpSession(TcpClient client, IAuthenticator auth, IStorageProvider storage, IOptions<FtpServerOptions> options)
    {
        _client = client;
        _auth = auth;
        _storage = storage;
        _options = options;
    }

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
        switch (parsed.Command)
            {
                case "USER":
            _pendingUser = parsed.Argument;
                    await writer.WriteLineAsync("331 User name okay, need password.");
                    break;
                case "PASS":
                    if (_pendingUser is null)
                    {
                        await writer.WriteLineAsync("503 Bad sequence of commands");
                        break;
                    }
            var result = await _auth.AuthenticateAsync(_pendingUser, parsed.Argument, ct);
                    _isAuthenticated = result.Succeeded;
                    await writer.WriteLineAsync(result.Succeeded ? "230 User logged in, proceed." : "530 Not logged in.");
                    break;
                case "SYST":
                    await writer.WriteLineAsync("215 UNIX Type: L8");
                    break;
                case "PWD":
                    await writer.WriteLineAsync($"257 \"{_cwd}\" is current directory");
                    break;
                case "CWD":
                    if (!await _storage.ExistsAsync(parsed.Argument, ct))
                    {
                        await writer.WriteLineAsync("550 Directory not found");
                        break;
                    }
                    _cwd = parsed.Argument;
                    await writer.WriteLineAsync("250 Requested file action okay, completed");
                    break;
                case "TYPE":
                    var t = parsed.Argument.ToUpperInvariant();
                    if (t == "I" || t.StartsWith("I ")) { _type = 'I'; await writer.WriteLineAsync("200 Type set to I"); }
                    else if (t == "A" || t.StartsWith("A ")) { _type = 'A'; await writer.WriteLineAsync("200 Type set to A"); }
                    else await writer.WriteLineAsync("504 Command not implemented for that parameter");
                    break;
                case "PASV":
                    var pe = EnterPassiveMode();
                    var p1 = pe.Port / 256; var p2 = pe.Port % 256;
                    await writer.WriteLineAsync($"227 Entering Passive Mode ({pe.IpAddress.Replace('.', ',')},{p1},{p2})");
                    break;
                case "LIST":
                    if (!_isAuthenticated)
                    {
                        await writer.WriteLineAsync("530 Not logged in.");
                        break;
                    }
                    if (_pasvListener is null)
                    {
                        await writer.WriteLineAsync("425 Use PASV first.");
                        break;
                    }
                    await writer.WriteLineAsync("150 Opening data connection for LIST");
                    using (var dataClient = await _pasvListener.AcceptTcpClientAsync(ct))
                    using (var data = dataClient.GetStream())
                    using (var dw = new StreamWriter(data, Encoding.ASCII) { NewLine = "\r\n", AutoFlush = true })
                    {
                        var entries = await _storage.ListAsync(_cwd, ct);
                        foreach (var e in entries)
                        {
                            await dw.WriteLineAsync(e.Name);
                        }
                    }
                    _pasvListener.Stop();
                    _pasvListener = null;
                    await writer.WriteLineAsync("226 Closing data connection. Requested file action successful");
                    break;
                case "MKD":
                    await _storage.CreateDirectoryAsync(ResolvePath(parsed.Argument), ct);
                    await writer.WriteLineAsync($"257 \"{ResolvePath(parsed.Argument)}\" created");
                    break;
                case "RMD":
                    await _storage.DeleteAsync(ResolvePath(parsed.Argument), recursive: true, ct);
                    await writer.WriteLineAsync("250 Requested file action okay, completed");
                    break;
                case "DELE":
                    await _storage.DeleteAsync(ResolvePath(parsed.Argument), recursive: false, ct);
                    await writer.WriteLineAsync("250 Requested file action okay, completed");
                    break;
                case "RETR":
                    if (_pasvListener is null) { await writer.WriteLineAsync("425 Use PASV first."); break; }
                    await writer.WriteLineAsync("150 Opening data connection for RETR");
                    using (var retrClient = await _pasvListener.AcceptTcpClientAsync(ct))
                    using (var rs = retrClient.GetStream())
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
                    _pasvListener.Stop();
                    _pasvListener = null;
                    await writer.WriteLineAsync("226 Transfer complete");
                    break;
                case "STOR":
                    if (_pasvListener is null) { await writer.WriteLineAsync("425 Use PASV first."); break; }
                    await writer.WriteLineAsync("150 Opening data connection for STOR");
                    using (var storClient = await _pasvListener.AcceptTcpClientAsync(ct))
                    {
                        var ns = storClient.GetStream();
                        async IAsyncEnumerable<ReadOnlyMemory<byte>> ReadStream([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token)
                        {
                            var buffer = new byte[8192];
                            int read;
                            while ((read = await ns.ReadAsync(buffer, 0, buffer.Length, token)) > 0)
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
                    _pasvListener.Stop();
                    _pasvListener = null;
                    await writer.WriteLineAsync("226 Transfer complete");
                    break;
                case "QUIT":
                    await writer.WriteLineAsync("221 Service closing control connection");
                    return;
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

    private string ResolvePath(string arg)
    {
        if (string.IsNullOrWhiteSpace(arg)) return _cwd;
        if (arg.StartsWith('/')) return arg.TrimEnd('/');
        if (_cwd == "/") return "/" + arg.TrimEnd('/');
        return _cwd.TrimEnd('/') + "/" + arg.TrimEnd('/');
    }

    // Parsing moved to FtpCommandParser
}
