using FtpServer.Core.Abstractions;
using FtpServer.Core.Protocol;

namespace FtpServer.Core.Server.Commands;

internal sealed class RmdHandler : IFtpCommandHandler
{
    private readonly IStorageProvider _storage;
    public RmdHandler(IStorageProvider storage) => _storage = storage;
    public string Command => "RMD";
    public async Task HandleAsync(IFtpSessionContext context, ParsedCommand parsed, StreamWriter writer, CancellationToken ct)
    {
        var path = context.ResolvePath(parsed.Argument);
        var entry = await _storage.GetEntryAsync(path, ct);
        if (entry is null)
        {
            await writer.WriteLineAsync("550 Directory not found");
            return;
        }
        if (!entry.IsDirectory)
        {
            await writer.WriteLineAsync("550 Not a directory");
            return;
        }
        try
        {
            await _storage.DeleteAsync(path, recursive: false, ct);
            await writer.WriteLineAsync("250 Requested file action okay, completed");
        }
        catch (IOException)
        {
            await writer.WriteLineAsync("550 Directory not empty");
        }
    }
}
