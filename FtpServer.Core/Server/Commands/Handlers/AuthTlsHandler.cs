using FtpServer.Core.Protocol;

namespace FtpServer.Core.Server.Commands.Handlers;

internal sealed class AuthTlsHandler : IFtpCommandHandler
{
    public string Command => "AUTH";
    public Task HandleAsync(IFtpSessionContext context, ParsedCommand parsed, StreamWriter writer, CancellationToken ct)
    {
        string arg = (parsed.Argument ?? string.Empty).Trim().ToUpperInvariant();
        if (arg != "TLS") return writer.WriteLineAsync("504 Only AUTH TLS supported");
        context.IsControlTls = true; // Session will upgrade transport
        return writer.WriteLineAsync("234 Enabling TLS connection");
    }
}
