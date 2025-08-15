using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FtpServer.Core.Abstractions;
using FtpServer.Core.Configuration;
using FtpServer.Core.Protocol;
using Microsoft.Extensions.Options;

namespace FtpServer.Core.Server.Commands;

internal sealed class RetrHandler : IFtpCommandHandler
{
    private readonly IStorageProvider _storage;
    private readonly IOptions<FtpServerOptions> _options;
    public RetrHandler(IStorageProvider storage, IOptions<FtpServerOptions> options)
    {
        _storage = storage;
        _options = options;
    }
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
            await using var _lease = await FtpServer.Core.Server.PathLocks.AcquireReadAsync(path, ct);
            using var rs = await context.OpenDataStreamAsync(ct);
            using var xferCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            xferCts.CancelAfter(_options.Value.DataTransferTimeoutMs);
            var token = xferCts.Token;
            var offset = (context as FtpServer.Core.Server.FtpSession)!.RestartOffset;
            (context as FtpServer.Core.Server.FtpSession)!.RestartOffset = 0; // consume
            long skipped = 0;
            await foreach (var chunk in _storage.ReadAsync(path, 8192, token))
            {
                if (offset > 0 && skipped + chunk.Length <= offset)
                {
                    skipped += chunk.Length;
                    continue;
                }
                if (offset > 0 && skipped < offset)
                {
                    var sliceOffset = (int)(offset - skipped);
                    var span = chunk.Span[sliceOffset..];
                    var sliced = new ReadOnlyMemory<byte>(span.ToArray());
                    skipped = offset;
                    if (context.TransferType == 'A')
                    {
                        var textS = System.Text.Encoding.ASCII.GetString(sliced.Span);
                        var dataS = System.Text.Encoding.ASCII.GetBytes(textS.Replace("\n", "\r\n"));
                        await rs.WriteAsync(dataS, 0, dataS.Length, token);
                        continue;
                    }
                    else
                    {
                        await rs.WriteAsync(sliced, token);
                        continue;
                    }
                }
                if (context.TransferType == 'A')
                {
                    var text = System.Text.Encoding.ASCII.GetString(chunk.Span);
                    var data = System.Text.Encoding.ASCII.GetBytes(text.Replace("\n", "\r\n"));
                    await rs.WriteAsync(data, 0, data.Length, token);
                }
                else
                {
                    await rs.WriteAsync(chunk, token);
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
