using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FtpServer.Core.Abstractions;
using FtpServer.Core.Protocol;

namespace FtpServer.Core.Server.Commands;

internal sealed class RetrHandler : IFtpCommandHandler
{
    private readonly IStorageProvider _storage;
    public RetrHandler(IStorageProvider storage) => _storage = storage;
    public string Command => "RETR";
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
        await writer.WriteLineAsync("150 Opening data connection for RETR");
        try
        {
            using var rs = await context.OpenDataStreamAsync(ct);
            await foreach (var chunk in _storage.ReadAsync(path, 8192, ct))
            {
                if (context.TransferType == 'A')
                {
                    var text = System.Text.Encoding.ASCII.GetString(chunk.Span);
                    var data = System.Text.Encoding.ASCII.GetBytes(text.Replace("\n", "\r\n"));
                    await rs.WriteAsync(data, 0, data.Length, ct);
                }
                else
                {
                    if (System.Runtime.InteropServices.MemoryMarshal.TryGetArray((ReadOnlyMemory<byte>)chunk, out var seg))
                        await rs.WriteAsync(seg.Array!, seg.Offset, seg.Count, ct);
                    else
                        await rs.WriteAsync(chunk.ToArray(), ct);
                }
            }
            await writer.WriteLineAsync("226 Transfer complete");
        }
        catch
        {
            await writer.WriteLineAsync("425 Can't open data connection");
        }
    }
}
