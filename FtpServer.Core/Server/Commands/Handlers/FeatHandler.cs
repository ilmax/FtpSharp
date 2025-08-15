using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FtpServer.Core.Protocol;

namespace FtpServer.Core.Server.Commands;

internal sealed class FeatHandler : IFtpCommandHandler
{
    public string Command => "FEAT";

    public async Task HandleAsync(IFtpSessionContext context, ParsedCommand parsed, StreamWriter writer, CancellationToken ct)
    {
        await writer.WriteLineAsync("211-Features");
        await writer.WriteLineAsync(" UTF8");
        await writer.WriteLineAsync(" PASV");
        await writer.WriteLineAsync(" EPSV");
        await writer.WriteLineAsync(" PORT");
        await writer.WriteLineAsync(" EPRT");
        await writer.WriteLineAsync(" SIZE");
        await writer.WriteLineAsync(" NLST");
        await writer.WriteLineAsync(" RNFR RNTO");
        await writer.WriteLineAsync(" TYPE A;I");
        await writer.WriteLineAsync(" MODE S");
        await writer.WriteLineAsync(" STRU F");
        // Not a standard FEAT; only core features are listed. Timeouts are options, not FEAT entries.
        await writer.WriteLineAsync("211 End");
    }
}
