using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FtpServer.Core.Abstractions;
using FtpServer.Core.Configuration;
using FtpServer.Core.Protocol;
using Microsoft.Extensions.Options;

namespace FtpServer.Core.Server.Commands;

internal sealed class StorHandler : IFtpCommandHandler
{
    private readonly IStorageProvider _storage;
    private readonly IOptions<FtpServerOptions> _options;
    public StorHandler(IStorageProvider storage, IOptions<FtpServerOptions> options)
    {
        _storage = storage;
        _options = options;
    }
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
            await using var _lease = await FtpServer.Core.Server.PathLocks.AcquireWriteAsync(path, ct);
            using var storStream = await context.OpenDataStreamAsync(ct);
            using var xferCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            xferCts.CancelAfter(_options.Value.DataTransferTimeoutMs);
            var token = xferCts.Token;
            var restOffset = (context as FtpServer.Core.Server.FtpSession)!.RestartOffset;
            (context as FtpServer.Core.Server.FtpSession)!.RestartOffset = 0; // consume
            async IAsyncEnumerable<ReadOnlyMemory<byte>> ReadStream([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token)
            {
                long sent = 0; var sw = new System.Diagnostics.Stopwatch(); var limit = (long)_options.Value.DataRateLimitBytesPerSec;
                var buffer = new byte[8192];
                int read;
                while ((read = await storStream.ReadAsync(buffer, 0, buffer.Length, token)) > 0)
                {
                    if (context.TransferType == 'A')
                    {
                        var text = System.Text.Encoding.ASCII.GetString(buffer, 0, read).Replace("\r\n", "\n");
                        var data = System.Text.Encoding.ASCII.GetBytes(text);
                        yield return data; sent += data.Length; sent = await FtpServer.Core.Server.Throttle.ApplyAsync(sent, limit, sw, token);
                    }
                    else
                    {
                        var data = new ReadOnlyMemory<byte>(buffer, 0, read).ToArray();
                        yield return data; sent += data.Length; sent = await FtpServer.Core.Server.Throttle.ApplyAsync(sent, limit, sw, token);
                    }
                }
            }
            await _storage.WriteTruncateThenAppendAsync(path, restOffset, ReadStream(token), token);
            await writer.WriteLineAsync("226 Transfer complete");
        }
        catch
        {
            await writer.WriteLineAsync("425 Can't open data connection");
        }
    }
}
