using FtpServer.Core.Abstractions;
using FtpServer.Core.Protocol;

namespace FtpServer.Core.Server.Commands.Handlers;

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
        if (result.Succeeded)
        {
            await writer.WriteLineAsync("230 User logged in, proceed.");
        }
        else
        {
            var reason = string.IsNullOrWhiteSpace(result.Reason) ? "Not logged in." : $"Not logged in. {result.Reason}";
            await writer.WriteLineAsync($"530 {reason}");
        }
    }
}
