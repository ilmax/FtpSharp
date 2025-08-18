using FtpServer.Core.Protocol;

namespace FtpServer.Core.Server.Commands;

internal sealed class PbszHandler : IFtpCommandHandler
{
    public string Command => "PBSZ";
    public Task HandleAsync(IFtpSessionContext context, ParsedCommand parsed, StreamWriter writer, CancellationToken ct)
    {
        var arg = (parsed.Argument ?? "").Trim();
        if (arg != "0") return writer.WriteLineAsync("501 PBSZ=0 required for TLS");
        return writer.WriteLineAsync("200 PBSZ=0");
    }
}
