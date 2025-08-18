using FtpServer.Core.Protocol;

namespace FtpServer.Core.Server.Commands;

internal sealed class ProtHandler : IFtpCommandHandler
{
    public string Command => "PROT";
    public Task HandleAsync(IFtpSessionContext context, ParsedCommand parsed, StreamWriter writer, CancellationToken ct)
    {
        string arg = (parsed.Argument ?? "").Trim().ToUpperInvariant();
        if (arg == "C") { context.DataProtectionLevel = 'C'; return writer.WriteLineAsync("200 PROT set to C"); }
        if (arg == "P") { context.DataProtectionLevel = 'P'; return writer.WriteLineAsync("200 PROT set to P"); }
        return writer.WriteLineAsync("504 Only PROT C or P supported");
    }
}
