using FtpServer.Core.Protocol;

namespace FtpServer.Core.Server.Commands.Handlers;

internal sealed class AlloHandler : IFtpCommandHandler
{
    public string Command => "ALLO";

    public Task HandleAsync(IFtpSessionContext context, ParsedCommand parsed, StreamWriter writer, CancellationToken ct)
    {
        // ALLO is superfluous for modern filesystems; acknowledge without action.
        return writer.WriteLineAsync("202 Command not implemented, superfluous at this site");
    }
}
