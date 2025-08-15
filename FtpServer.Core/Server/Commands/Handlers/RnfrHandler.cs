using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FtpServer.Core.Protocol;
using FtpServer.Core.Abstractions;

namespace FtpServer.Core.Server.Commands;

internal sealed class RnfrHandler : IFtpCommandHandler
{
    private readonly IStorageProvider _storage;
    public RnfrHandler(IStorageProvider storage) => _storage = storage;
    public string Command => "RNFR";
    public async Task HandleAsync(IFtpSessionContext context, ParsedCommand parsed, StreamWriter writer, CancellationToken ct)
    {
        var from = context.ResolvePath(parsed.Argument);
        if (!await _storage.ExistsAsync(from, ct))
        {
            await writer.WriteLineAsync("550 File not found");
            return;
        }
        context.PendingRenameFrom = from;
        await writer.WriteLineAsync("350 Requested file action pending further information");
    }
}
