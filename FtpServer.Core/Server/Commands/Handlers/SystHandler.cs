using FtpServer.Core.Protocol;

namespace FtpServer.Core.Server.Commands.Handlers;

internal sealed class SystHandler : IFtpCommandHandler
{
    public string Command => "SYST";
    public Task HandleAsync(IFtpSessionContext context, ParsedCommand parsed, StreamWriter writer, CancellationToken ct)
        => writer.WriteLineAsync("215 UNIX Type: L8");
}
