using FtpServer.Core.Protocol;

namespace FtpServer.Core.Server.Commands.Handlers;

internal sealed class NoopHandler : IFtpCommandHandler
{
    public string Command => "NOOP";
    public Task HandleAsync(IFtpSessionContext context, ParsedCommand parsed, StreamWriter writer, CancellationToken ct)
        => writer.WriteLineAsync("200 NOOP ok");
}
