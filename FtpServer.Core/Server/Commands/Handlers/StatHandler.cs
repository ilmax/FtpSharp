using FtpServer.Core.Protocol;

namespace FtpServer.Core.Server.Commands;

internal sealed class StatHandler : IFtpCommandHandler
{
    public string Command => "STAT";
    public async Task HandleAsync(IFtpSessionContext context, ParsedCommand parsed, StreamWriter writer, CancellationToken ct)
    {
        await writer.WriteLineAsync("211-FTP Server status");
        await writer.WriteLineAsync($" Current directory: {context.Cwd}");
        await writer.WriteLineAsync(" Features: UTF8 PASV PORT EPSV EPRT TYPE SIZE NLST RNFR RNTO");
        await writer.WriteLineAsync("211 End");
    }
}
