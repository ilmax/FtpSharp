using FtpServer.Core.Protocol;

namespace FtpServer.Core.Server.Commands.Handlers;

internal sealed class EpsvHandler : IFtpCommandHandler
{
    public string Command => "EPSV";
    public Task HandleAsync(IFtpSessionContext context, ParsedCommand parsed, StreamWriter writer, CancellationToken ct)
    {
        var pe = context.EnterPassiveMode();
        return writer.WriteLineAsync($"229 Entering Extended Passive Mode (|||{pe.Port}|)");
    }
}
