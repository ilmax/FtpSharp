using FtpServer.Core.Abstractions;
using FtpServer.Core.Protocol;

namespace FtpServer.Core.Server.Commands.Handlers;

internal sealed class CwdHandler : IFtpCommandHandler
{
    private readonly IStorageProvider _storage;
    public CwdHandler(IStorageProvider storage) => _storage = storage;
    public string Command => "CWD";
    public async Task HandleAsync(IFtpSessionContext context, ParsedCommand parsed, StreamWriter writer, CancellationToken ct)
    {
        string path = context.ResolvePath(parsed.Argument);
        var entry = await _storage.GetEntryAsync(path, ct);
        if (entry is null || !entry.IsDirectory)
        {
            await writer.WriteLineAsync("550 Directory not found");
            return;
        }
        context.Cwd = path;
        await writer.WriteLineAsync("250 Requested file action okay, completed");
    }
}
