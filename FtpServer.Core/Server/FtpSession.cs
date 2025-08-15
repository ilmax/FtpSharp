using System.Net.Sockets;
using System.Text;
using FtpServer.Core.Abstractions;

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
            var (cmd, arg) = Parse(line);
            switch (cmd)
            {
                case "USER":
                    _pendingUser = arg;
                    await writer.WriteLineAsync("331 User name okay, need password.");
                    break;
                case "PASS":
                    if (_pendingUser is null)
                    {
                        await writer.WriteLineAsync("503 Bad sequence of commands");
                        break;
                    }
                    var result = await _auth.AuthenticateAsync(_pendingUser, arg, ct);
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

    private static (string cmd, string arg) Parse(string line)
    {
        var idx = line.IndexOf(' ');
        if (idx < 0) return (line.ToUpperInvariant(), string.Empty);
        return (line[..idx].ToUpperInvariant(), line[(idx + 1)..]);
    }
}
