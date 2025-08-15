using FtpServer.Core.Protocol;

namespace FtpServer.Core.Server.Commands;

internal sealed class EprtHandler : IFtpCommandHandler
{
    public string Command => "EPRT";
    public Task HandleAsync(IFtpSessionContext context, ParsedCommand parsed, StreamWriter writer, CancellationToken ct)
    {
        if (!TryParseEprt(parsed.Argument, out var ep))
            return writer.WriteLineAsync("501 Syntax error in parameters or arguments");
        context.ActiveEndpoint = ep;
        return writer.WriteLineAsync("200 Command okay");
    }

    private static bool TryParseEprt(string arg, out System.Net.IPEndPoint? ep)
    {
        ep = null;
        if (string.IsNullOrEmpty(arg)) return false;
        var delim = arg[0];
        var parts = arg.Split(delim);
        if (parts.Length < 5) return false;
        if (!int.TryParse(parts[1], out var af)) return false;
        var addrStr = parts[2];
        if (!int.TryParse(parts[3], out var port)) return false;
        try
        {
            var addr = System.Net.IPAddress.Parse(addrStr);
            if ((af == 1 && addr.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork) ||
                (af == 2 && addr.AddressFamily != System.Net.Sockets.AddressFamily.InterNetworkV6))
                return false;
            ep = new System.Net.IPEndPoint(addr, port);
            return true;
        }
        catch { return false; }
    }
}
