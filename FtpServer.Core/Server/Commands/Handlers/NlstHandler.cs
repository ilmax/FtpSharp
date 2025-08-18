using System.Text;
using FtpServer.Core.Abstractions;
using FtpServer.Core.Protocol;

namespace FtpServer.Core.Server.Commands.Handlers;

internal sealed class NlstHandler : IFtpCommandHandler
{
    private readonly IStorageProvider _storage;
    public NlstHandler(IStorageProvider storage) => _storage = storage;
    public string Command => "NLST";
    public async Task HandleAsync(IFtpSessionContext context, ParsedCommand parsed, StreamWriter writer, CancellationToken ct)
    {
        if (!context.IsAuthenticated)
        {
            await writer.WriteLineAsync("530 Not logged in.");
            return;
        }
        await writer.WriteLineAsync("150 Opening data connection for NLST");
        try
        {
            using var data = await context.OpenDataStreamAsync(ct);
            using var dw = new StreamWriter(data, Encoding.ASCII) { NewLine = "\r\n", AutoFlush = true };
            var entries = await _storage.ListAsync(context.Cwd, ct);
            foreach (var e in entries)
            {
                await dw.WriteLineAsync(e.Name);
            }
            await writer.WriteLineAsync("226 NLST complete");
        }
        catch
        {
            await writer.WriteLineAsync("425 Can't open data connection");
        }
    }
}
