using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FtpServer.Core.Protocol;

namespace FtpServer.Core.Server.Commands;

internal sealed class HelpHandler : IFtpCommandHandler
{
    public string Command => "HELP";
    public Task HandleAsync(IFtpSessionContext context, ParsedCommand parsed, StreamWriter writer, CancellationToken ct)
    {
        return Write();
        async Task Write()
        {
            await writer.WriteLineAsync("214-The following commands are recognized.");
            await writer.WriteLineAsync(" USER PASS SYST FEAT PWD CWD CDUP TYPE PASV EPSV PORT EPRT LIST NLST RETR STOR DELE MKD RMD SIZE RNFR RNTO STAT HELP QUIT");
            await writer.WriteLineAsync("214 Help OK.");
        }
    }
}
