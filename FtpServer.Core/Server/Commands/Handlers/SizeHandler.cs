using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FtpServer.Core.Abstractions;
using FtpServer.Core.Protocol;

namespace FtpServer.Core.Server.Commands;

internal sealed class SizeHandler : IFtpCommandHandler
{
    private readonly IStorageProvider _storage;
    public SizeHandler(IStorageProvider storage) => _storage = storage;
    public string Command => "SIZE";
    public async Task HandleAsync(IFtpSessionContext context, ParsedCommand parsed, StreamWriter writer, CancellationToken ct)
    {
        var path = context.ResolvePath(parsed.Argument);
        var entry = await _storage.GetEntryAsync(path, ct);
        if (entry is null)
        {
            await writer.WriteLineAsync("550 File not found");
            return;
        }
        if (entry.IsDirectory)
        {
            await writer.WriteLineAsync("550 Not a plain file");
            return;
        }
        var size = await _storage.GetSizeAsync(path, ct);
        await writer.WriteLineAsync($"213 {size}");
    }
}
