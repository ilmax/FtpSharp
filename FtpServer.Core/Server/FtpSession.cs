using System.Net.Sockets;
using System.Text;
using FtpServer.Core.Abstractions;
using FtpServer.Core.Protocol;

namespace FtpServer.Core.Server;

/// <summary>
/// Handles a single control connection session. Minimal MVP for iteration.
/// </summary>
public sealed class FtpSession
{
    private readonly TcpClient _client;
    private readonly IAuthenticator _auth;
    private readonly IStorageProvider _storage;

    private bool _isAuthenticated;
    private string? _pendingUser;

    public FtpSession(TcpClient client, IAuthenticator auth, IStorageProvider storage)
    {
        _client = client;
        _auth = auth;
        _storage = storage;
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
                    await writer.WriteLineAsync("257 \"/\" is current directory");
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

    // Parsing moved to FtpCommandParser
}
