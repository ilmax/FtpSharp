using System.IO;
using System.Threading;
using System.Threading.Tasks;
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
        var parts = arg.Split(',');
        if (parts.Length != 6) return false;
        if (!byte.TryParse(parts[0], out var h1) || !byte.TryParse(parts[1], out var h2) ||
            !byte.TryParse(parts[2], out var h3) || !byte.TryParse(parts[3], out var h4) ||
            !byte.TryParse(parts[4], out var p1) || !byte.TryParse(parts[5], out var p2)) return false;
        var addr = new System.Net.IPAddress(new byte[] { h1, h2, h3, h4 });
        var port = p1 * 256 + p2;
        ep = new System.Net.IPEndPoint(addr, port);
        return true;
    }
}
