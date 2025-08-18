using FtpServer.Core.Protocol;

namespace FtpServer.Core.Server.Commands;

internal sealed class PasvHandler : IFtpCommandHandler
{
    public string Command => "PASV";
    public Task HandleAsync(IFtpSessionContext context, ParsedCommand parsed, StreamWriter writer, CancellationToken ct)
    {
        var pe = context.EnterPassiveMode();
        int p1 = pe.Port / 256; int p2 = pe.Port % 256;
        return writer.WriteLineAsync($"227 Entering Passive Mode ({pe.IpAddress.Replace('.', ',')},{p1},{p2})");
    }
}
