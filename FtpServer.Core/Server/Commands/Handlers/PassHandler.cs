using FtpServer.Core.Abstractions;
using FtpServer.Core.Protocol;

namespace FtpServer.Core.Server.Commands;

internal sealed class PassHandler : IFtpCommandHandler
{
    private readonly IAuthenticator _auth;
    public PassHandler(IAuthenticator auth) => _auth = auth;
    public string Command => "PASS";
    public async Task HandleAsync(IFtpSessionContext context, ParsedCommand parsed, StreamWriter writer, CancellationToken ct)
    {
        if (context.PendingUser is null)
        {
            await writer.WriteLineAsync("503 Bad sequence of commands");
            return;
        }
        var result = await _auth.AuthenticateAsync(context.PendingUser, parsed.Argument, ct);
        context.IsAuthenticated = result.Succeeded;
        await writer.WriteLineAsync(result.Succeeded ? "230 User logged in, proceed." : "530 Not logged in.");
    }
}
