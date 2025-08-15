using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FtpServer.Core.Abstractions;
using FtpServer.Core.Configuration;
using FtpServer.Core.Protocol;
using Microsoft.Extensions.Options;

namespace FtpServer.Core.Server.Commands;

internal sealed class AppeHandler : IFtpCommandHandler
{
    private readonly IStorageProvider _storage;
    private readonly IOptions<FtpServerOptions> _options;
    private readonly FtpServer.Core.Server.FtpSession _session;
    public AppeHandler(IStorageProvider storage, IOptions<FtpServerOptions> options, FtpServer.Core.Server.FtpSession session)
    { _storage = storage; _options = options; _session = session; }
    public string Command => "APPE";
    public async Task HandleAsync(IFtpSessionContext context, ParsedCommand parsed, StreamWriter writer, CancellationToken ct)
    {
        var path = context.ResolvePath(parsed.Argument);
        var entry = await _storage.GetEntryAsync(path, ct);
        if (entry is not null && entry.IsDirectory)
        {
            await writer.WriteLineAsync("550 Not a plain file");
            return;
        }
        await writer.WriteLineAsync("150 Opening data connection for APPE");
        try
        {
            await using var _lease = await FtpServer.Core.Server.PathLocks.AcquireWriteAsync(path, ct);
            using var ds = await context.OpenDataStreamAsync(ct);
            using var xferCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            xferCts.CancelAfter(_options.Value.DataTransferTimeoutMs);
            var token = xferCts.Token;

            // Build content enumerable, appending to existing bytes if present, honoring REST offset if set
            async IAsyncEnumerable<ReadOnlyMemory<byte>> Content([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token)
            {
                var offset = _session.RestartOffset;
                _session.RestartOffset = 0; // consume
                byte[]? existing = null;
                if (entry is not null)
                {
                    // read existing into memory (simple baseline implementation)
                    using var ms = new MemoryStream();
                    await foreach (var chunk in _storage.ReadAsync(path, 8192, token))
                    {
                        if (System.Runtime.InteropServices.MemoryMarshal.TryGetArray((ReadOnlyMemory<byte>)chunk, out var seg))
                            await ms.WriteAsync(seg.Array!, seg.Offset, seg.Count, token);
                        else
                            await ms.WriteAsync(chunk.ToArray(), token);
                    }
                    existing = ms.ToArray();
                }

                if (existing is not null)
                {
                    // If REST offset provided, keep only up to offset
                    if (offset > 0 && offset <= existing.Length)
                    {
                        yield return new ReadOnlyMemory<byte>(existing, 0, (int)offset).ToArray();
                    }
                    else
                    {
                        yield return existing;
                    }
                }

                var buffer = new byte[8192];
                int read;
                while ((read = await ds.ReadAsync(buffer, 0, buffer.Length, token)) > 0)
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

            await _storage.WriteAsync(path, Content(token), token);
            await writer.WriteLineAsync("226 Transfer complete");
        }
        catch
        {
            await writer.WriteLineAsync("425 Can't open data connection");
        }
    }
}
