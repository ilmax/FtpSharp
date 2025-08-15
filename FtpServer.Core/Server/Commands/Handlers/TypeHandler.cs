using FtpServer.Core.Protocol;

namespace FtpServer.Core.Server.Commands;

internal sealed class TypeHandler : IFtpCommandHandler
{
    public string Command => "TYPE";
    public Task HandleAsync(IFtpSessionContext context, ParsedCommand parsed, StreamWriter writer, CancellationToken ct)
    {
        var t = (parsed.Argument ?? string.Empty).ToUpperInvariant();
        if (t == "I" || t.StartsWith("I ")) { context.TransferType = 'I'; return writer.WriteLineAsync("200 Type set to I"); }
        if (t == "A" || t.StartsWith("A ")) { context.TransferType = 'A'; return writer.WriteLineAsync("200 Type set to A"); }
        return writer.WriteLineAsync("504 Command not implemented for that parameter");
    }
}
