using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FtpServer.Core.Protocol;

namespace FtpServer.Core.Server.Commands;

internal sealed class NoopHandler : IFtpCommandHandler
{
    public string Command => "NOOP";
    public Task HandleAsync(IFtpSessionContext context, ParsedCommand parsed, StreamWriter writer, CancellationToken ct)
        => writer.WriteLineAsync("200 NOOP ok");
}

internal sealed class SystHandler : IFtpCommandHandler
{
    public string Command => "SYST";
    public Task HandleAsync(IFtpSessionContext context, ParsedCommand parsed, StreamWriter writer, CancellationToken ct)
        => writer.WriteLineAsync("215 UNIX Type: L8");
}

internal sealed class PwdHandler : IFtpCommandHandler
{
    public string Command => "PWD";
    public Task HandleAsync(IFtpSessionContext context, ParsedCommand parsed, StreamWriter writer, CancellationToken ct)
        => writer.WriteLineAsync($"257 \"{context.Cwd}\" is current directory");
}

internal sealed class CdupHandler : IFtpCommandHandler
{
    public string Command => "CDUP";
    public Task HandleAsync(IFtpSessionContext context, ParsedCommand parsed, StreamWriter writer, CancellationToken ct)
    {
        var cwd = context.Cwd;
        cwd = cwd == "/" ? "/" : cwd.Substring(0, cwd.LastIndexOf('/'));
        if (string.IsNullOrEmpty(cwd)) cwd = "/";
        context.Cwd = cwd;
        return writer.WriteLineAsync("200 Directory changed to parent");
    }
}

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
