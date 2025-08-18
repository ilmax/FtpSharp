using FtpServer.Core.Abstractions;
using FtpServer.Core.Configuration;
using FtpServer.Core.Protocol;
using Microsoft.Extensions.Options;

namespace FtpServer.Core.Server.Commands.Handlers;

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
        string path = context.ResolvePath(parsed.Argument);
        var entry = await _storage.GetEntryAsync(path, ct);
        if (entry is not null && entry.IsDirectory)
        {
            await writer.WriteLineAsync("550 Not a plain file");
            return;
        }
        await writer.WriteLineAsync("150 Opening data connection for STOR");
        try
        {
            await using var _lease = await PathLocks.AcquireWriteAsync(path, ct);
            using var storStream = await context.OpenDataStreamAsync(ct);
            using var xferCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            xferCts.CancelAfter(_options.Value.DataTransferTimeoutMs);
            var token = xferCts.Token;
            long restOffset = (context as FtpSession)!.RestartOffset;
            (context as FtpSession)!.RestartOffset = 0; // consume
            string sid = (context as FtpSession)!.SessionId;
            async IAsyncEnumerable<ReadOnlyMemory<byte>> ReadStream([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token)
            {
                long sent = 0; var sw = new System.Diagnostics.Stopwatch(); long limit = (long)_options.Value.DataRateLimitBytesPerSec;
                byte[] buffer = new byte[8192];
                int read;
                while ((read = await storStream.ReadAsync(buffer, 0, buffer.Length, token)) > 0)
                {
                    if (context.TransferType == 'A')
                    {
                        string text = System.Text.Encoding.ASCII.GetString(buffer, 0, read).Replace("\r\n", "\n");
                        byte[] data = System.Text.Encoding.ASCII.GetBytes(text);
                        yield return data; sent += data.Length; Observability.Metrics.BytesReceived.Add(data.Length); Observability.Metrics.SessionBytesReceived.Add(data.Length, new KeyValuePair<string, object?>("session_id", sid)); sent = await Throttle.ApplyAsync(sent, limit, sw, token);
                    }
                    else
                    {
                        var data = new ReadOnlyMemory<byte>(buffer, 0, read);
                        yield return data; sent += data.Length; Observability.Metrics.BytesReceived.Add(data.Length); Observability.Metrics.SessionBytesReceived.Add(data.Length, new KeyValuePair<string, object?>("session_id", sid)); sent = await Throttle.ApplyAsync(sent, limit, sw, token);
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
