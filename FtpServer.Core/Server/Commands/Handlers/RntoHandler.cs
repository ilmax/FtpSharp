using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FtpServer.Core.Protocol;
using FtpServer.Core.Abstractions;

namespace FtpServer.Core.Server.Commands;

internal sealed class RntoHandler : IFtpCommandHandler
{
    private readonly IStorageProvider _storage;
    public RntoHandler(IStorageProvider storage) => _storage = storage;
    public string Command => "RNTO";
    public async Task HandleAsync(IFtpSessionContext context, ParsedCommand parsed, StreamWriter writer, CancellationToken ct)
    {
        if (context.PendingRenameFrom is null)
        {
            await writer.WriteLineAsync("503 Bad sequence of commands");
            return;
        }
        await _storage.RenameAsync(context.PendingRenameFrom, context.ResolvePath(parsed.Argument), ct);
        context.PendingRenameFrom = null;
        await writer.WriteLineAsync("250 Requested file action okay, completed");
    }
}
