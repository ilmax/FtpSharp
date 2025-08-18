using FtpServer.Core.Protocol;

namespace FtpServer.Core.Server.Commands;

internal sealed class PortHandler : IFtpCommandHandler
{
    public string Command => "PORT";
    public Task HandleAsync(IFtpSessionContext context, ParsedCommand parsed, StreamWriter writer, CancellationToken ct)
    {
        if (!TryParsePort(parsed.Argument, out var ep))
            return writer.WriteLineAsync("501 Syntax error in parameters or arguments");
        context.ActiveEndpoint = ep;
        return writer.WriteLineAsync("200 Command okay");
    }

    private static bool TryParsePort(string arg, out System.Net.IPEndPoint? ep)
    {
        ep = null;
        string[] parts = arg.Split(',');
        if (parts.Length != 6) return false;
        if (!byte.TryParse(parts[0], out byte h1) || !byte.TryParse(parts[1], out byte h2) ||
            !byte.TryParse(parts[2], out byte h3) || !byte.TryParse(parts[3], out byte h4) ||
            !byte.TryParse(parts[4], out byte p1) || !byte.TryParse(parts[5], out byte p2)) return false;
        var addr = new System.Net.IPAddress(new byte[] { h1, h2, h3, h4 });
        int port = p1 * 256 + p2;
        ep = new System.Net.IPEndPoint(addr, port);
        return true;
    }
}
