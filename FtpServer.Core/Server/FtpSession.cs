using System.Net.Sockets;
using System.Text;
using FtpServer.Core.Abstractions;
using FtpServer.Core.Protocol;
using FtpServer.Core.Configuration;
using Microsoft.Extensions.Options;

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
                case "PASV":
                    var pe = await EnterPassiveModeAsync(ct);
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
                case "QUIT":
                    await writer.WriteLineAsync("221 Service closing control connection");
                    return;
                default:
                    await writer.WriteLineAsync("502 Command not implemented");
                    break;
            }
        }
    }

    private async Task<PassiveEndpoint> EnterPassiveModeAsync(CancellationToken ct)
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

    // Parsing moved to FtpCommandParser
}
