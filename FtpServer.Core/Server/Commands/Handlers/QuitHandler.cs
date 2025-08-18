using FtpServer.Core.Protocol;

namespace FtpServer.Core.Server.Commands.Handlers;

internal sealed class QuitHandler : IFtpCommandHandler
{
    public string Command => "QUIT";
    public async Task HandleAsync(IFtpSessionContext context, ParsedCommand parsed, StreamWriter writer, CancellationToken ct)
    {
        await writer.WriteLineAsync("221 Service closing control connection");
        context.ShouldQuit = true;
    }
}
