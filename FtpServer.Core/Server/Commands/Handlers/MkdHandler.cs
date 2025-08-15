using FtpServer.Core.Abstractions;
using FtpServer.Core.Protocol;

namespace FtpServer.Core.Server.Commands;

internal sealed class MkdHandler : IFtpCommandHandler
{
    private readonly IStorageProvider _storage;
    public MkdHandler(IStorageProvider storage) => _storage = storage;
    public string Command => "MKD";
    public async Task HandleAsync(IFtpSessionContext context, ParsedCommand parsed, StreamWriter writer, CancellationToken ct)
    {
        var path = context.ResolvePath(parsed.Argument);
        if (await _storage.ExistsAsync(path, ct))
        {
            await writer.WriteLineAsync("550 Directory already exists");
            return;
        }
        await _storage.CreateDirectoryAsync(path, ct);
        await writer.WriteLineAsync($"257 \"{path}\" created");
    }
}
