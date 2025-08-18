using FtpServer.Core.Protocol;

namespace FtpServer.Core.Server.Commands.Handlers;

internal sealed class UserHandler : IFtpCommandHandler
{
    public string Command => "USER";
    public Task HandleAsync(IFtpSessionContext context, ParsedCommand parsed, StreamWriter writer, CancellationToken ct)
    {
        context.PendingUser = parsed.Argument;
        return writer.WriteLineAsync("331 User name okay, need password.");
    }
}
