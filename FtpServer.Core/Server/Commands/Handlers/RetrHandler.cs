using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FtpServer.Core.Abstractions;
using FtpServer.Core.Configuration;
using FtpServer.Core.Protocol;
using Microsoft.Extensions.Options;
using FtpServer.Core.Observability;

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
            long sent = 0; var sw = new System.Diagnostics.Stopwatch(); var limit = (long)_options.Value.DataRateLimitBytesPerSec;
            var offset = (context as FtpServer.Core.Server.FtpSession)!.RestartOffset;
            (context as FtpServer.Core.Server.FtpSession)!.RestartOffset = 0; // consume
            await foreach (var chunk in (offset > 0 ? _storage.ReadFromOffsetAsync(path, offset, 8192, token) : _storage.ReadAsync(path, 8192, token)))
            {
                if (context.TransferType == 'A')
                {
                    var text = System.Text.Encoding.ASCII.GetString(chunk.Span);
                    var data = System.Text.Encoding.ASCII.GetBytes(text.Replace("\n", "\r\n"));
                    await rs.WriteAsync(data, 0, data.Length, token);
                    Metrics.BytesSent.Add(data.Length);
                    sent += data.Length; sent = await FtpServer.Core.Server.Throttle.ApplyAsync(sent, limit, sw, token);
                }
                else
                {
                    await rs.WriteAsync(chunk, token);
                    Metrics.BytesSent.Add(chunk.Length);
                    sent += chunk.Length; sent = await FtpServer.Core.Server.Throttle.ApplyAsync(sent, limit, sw, token);
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
