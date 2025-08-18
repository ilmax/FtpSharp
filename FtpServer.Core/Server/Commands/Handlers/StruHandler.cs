using FtpServer.Core.Protocol;

namespace FtpServer.Core.Server.Commands.Handlers;

internal sealed class StruHandler : IFtpCommandHandler
{
    public string Command => "STRU";

    public Task HandleAsync(IFtpSessionContext context, ParsedCommand parsed, StreamWriter writer, CancellationToken ct)
    {
        string arg = (parsed.Argument ?? string.Empty).Trim();
        if (string.Equals(arg, "F", StringComparison.OrdinalIgnoreCase))
            return writer.WriteLineAsync("200 Structure set to F");
        return writer.WriteLineAsync("504 Command not implemented for that parameter");
    }
}
