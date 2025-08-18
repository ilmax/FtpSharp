using FtpServer.Core.Abstractions;
using FtpServer.Core.Configuration;
using FtpServer.Core.Observability;
using FtpServer.Core.Protocol;
using Microsoft.Extensions.Options;

namespace FtpServer.Core.Server.Commands.Handlers;

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
        string path = context.ResolvePath(parsed.Argument);
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
            await using var _lease = await PathLocks.AcquireReadAsync(path, ct);
            using var rs = await context.OpenDataStreamAsync(ct);
            using var xferCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            xferCts.CancelAfter(_options.Value.DataTransferTimeoutMs);
            var token = xferCts.Token;
            long sent = 0; var sw = new System.Diagnostics.Stopwatch(); long limit = (long)_options.Value.DataRateLimitBytesPerSec;
            long offset = (context as FtpSession)!.RestartOffset;
            (context as FtpSession)!.RestartOffset = 0; // consume
            string sid = (context as FtpSession)!.SessionId;
            await foreach (var chunk in (offset > 0 ? _storage.ReadFromOffsetAsync(path, offset, 8192, token) : _storage.ReadAsync(path, 8192, token)))
            {
                if (context.TransferType == 'A')
                {
                    string text = System.Text.Encoding.ASCII.GetString(chunk.Span);
                    byte[] data = System.Text.Encoding.ASCII.GetBytes(text.Replace("\n", "\r\n"));
                    await rs.WriteAsync(data, 0, data.Length, token);
                    Metrics.BytesSent.Add(data.Length);
                    Metrics.SessionBytesSent.Add(data.Length, new KeyValuePair<string, object?>("session_id", sid));
                    sent += data.Length; sent = await Throttle.ApplyAsync(sent, limit, sw, token);
                }
                else
                {
                    await rs.WriteAsync(chunk, token);
                    Metrics.BytesSent.Add(chunk.Length);
                    Metrics.SessionBytesSent.Add(chunk.Length, new KeyValuePair<string, object?>("session_id", sid));
                    sent += chunk.Length; sent = await Throttle.ApplyAsync(sent, limit, sw, token);
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
