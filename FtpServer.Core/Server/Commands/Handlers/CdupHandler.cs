using FtpServer.Core.Protocol;

namespace FtpServer.Core.Server.Commands;

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
