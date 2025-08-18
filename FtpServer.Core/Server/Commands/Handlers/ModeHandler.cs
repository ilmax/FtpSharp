using FtpServer.Core.Protocol;

namespace FtpServer.Core.Server.Commands;

internal sealed class ModeHandler : IFtpCommandHandler
{
    public string Command => "MODE";

    public Task HandleAsync(IFtpSessionContext context, ParsedCommand parsed, StreamWriter writer, CancellationToken ct)
    {
        string arg = (parsed.Argument ?? string.Empty).Trim();
        if (string.Equals(arg, "S", StringComparison.OrdinalIgnoreCase))
            return writer.WriteLineAsync("200 Mode set to S");
        return writer.WriteLineAsync("504 Command not implemented for that parameter");
    }
}
