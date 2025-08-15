using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FtpServer.Core.Abstractions;
using FtpServer.Core.Protocol;

namespace FtpServer.Core.Server.Commands;

internal sealed class ListHandler : IFtpCommandHandler
{
    private readonly IStorageProvider _storage;
    public ListHandler(IStorageProvider storage) => _storage = storage;
    public string Command => "LIST";
    public async Task HandleAsync(IFtpSessionContext context, ParsedCommand parsed, StreamWriter writer, CancellationToken ct)
    {
        if (!context.IsAuthenticated)
        {
            await writer.WriteLineAsync("530 Not logged in.");
            return;
        }
        await writer.WriteLineAsync("150 Opening data connection for LIST");
        try
        {
            using var data = await context.OpenDataStreamAsync(ct);
            using var dw = new StreamWriter(data, Encoding.ASCII) { NewLine = "\r\n", AutoFlush = true };
            var entries = await _storage.ListAsync(context.Cwd, ct);
            foreach (var e in entries)
            {
                await dw.WriteLineAsync(FormatUnixListLine(e));
            }
            await writer.WriteLineAsync("226 Closing data connection. Requested file action successful");
        }
        catch
        {
            await writer.WriteLineAsync("425 Can't open data connection");
        }
    }

    private static string FormatUnixListLine(FileSystemEntry e)
    {
        var perms = e.IsDirectory ? 'd' : '-';
        var rights = "rwxr-xr-x";
        var links = 1;
        var owner = "owner";
        var group = "group";
        var size = e.Length ?? 0;
        var date = System.DateTimeOffset.Now.ToString("MMM dd HH:mm", System.Globalization.CultureInfo.InvariantCulture);
        return $"{perms}{rights} {links,3} {owner,5} {group,5} {size,8} {date} {e.Name}";
    }
}
