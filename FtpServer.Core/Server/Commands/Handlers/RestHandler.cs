using FtpServer.Core.Protocol;

namespace FtpServer.Core.Server.Commands.Handlers;

internal sealed class RestHandler : IFtpCommandHandler
{
    private readonly FtpSession _session;
    public RestHandler(FtpSession session) => _session = session;
    public string Command => "REST";
    public Task HandleAsync(IFtpSessionContext context, ParsedCommand parsed, StreamWriter writer, CancellationToken ct)
    {
        if (!long.TryParse(parsed.Argument, out long offset) || offset < 0)
            return writer.WriteLineAsync("501 Syntax error in parameters or arguments");
        _session.RestartOffset = offset;
        return writer.WriteLineAsync($"350 Restarting at {offset}. Send STORE or RETR to resume.");
    }
}
