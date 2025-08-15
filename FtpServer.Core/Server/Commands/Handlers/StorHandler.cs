using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FtpServer.Core.Abstractions;
using FtpServer.Core.Protocol;

namespace FtpServer.Core.Server.Commands;

internal sealed class StorHandler : IFtpCommandHandler
{
    private readonly IStorageProvider _storage;
    public StorHandler(IStorageProvider storage) => _storage = storage;
    public string Command => "STOR";
    public async Task HandleAsync(IFtpSessionContext context, ParsedCommand parsed, StreamWriter writer, CancellationToken ct)
    {
        var path = context.ResolvePath(parsed.Argument);
        var entry = await _storage.GetEntryAsync(path, ct);
        if (entry is not null && entry.IsDirectory)
        {
            await writer.WriteLineAsync("550 Not a plain file");
            return;
        }
        await writer.WriteLineAsync("150 Opening data connection for STOR");
        try
        {
            using var storStream = await context.OpenDataStreamAsync(ct);
            async IAsyncEnumerable<ReadOnlyMemory<byte>> ReadStream([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token)
            {
                var buffer = new byte[8192];
                int read;
                while ((read = await storStream.ReadAsync(buffer, 0, buffer.Length, token)) > 0)
                {
                    if (context.TransferType == 'A')
                    {
                        var text = System.Text.Encoding.ASCII.GetString(buffer, 0, read).Replace("\r\n", "\n");
                        yield return System.Text.Encoding.ASCII.GetBytes(text);
                    }
                    else
                    {
                        yield return new ReadOnlyMemory<byte>(buffer, 0, read).ToArray();
                    }
                }
            }
            await _storage.WriteAsync(path, ReadStream(ct), ct);
            await writer.WriteLineAsync("226 Transfer complete");
        }
        catch
        {
            await writer.WriteLineAsync("425 Can't open data connection");
        }
    }
}
