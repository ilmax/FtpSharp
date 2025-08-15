using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FtpServer.Core.Protocol;
using FtpServer.Core.Abstractions;
using System.Text;

namespace FtpServer.Core.Server.Commands;

internal sealed class FeatHandler : IFtpCommandHandler
{
    public string Command => "FEAT";
    public async Task HandleAsync(IFtpSessionContext context, ParsedCommand parsed, StreamWriter writer, CancellationToken ct)
    {
        await writer.WriteLineAsync("211-Features");
        await writer.WriteLineAsync(" UTF8");
        await writer.WriteLineAsync(" PASV");
        await writer.WriteLineAsync(" EPSV");
        await writer.WriteLineAsync(" PORT");
        await writer.WriteLineAsync(" EPRT");
        await writer.WriteLineAsync(" SIZE");
        await writer.WriteLineAsync(" NLST");
        await writer.WriteLineAsync(" RNFR RNTO");
        await writer.WriteLineAsync(" TYPE A;I");
        await writer.WriteLineAsync("211 End");
    }
}

internal sealed class StatHandler : IFtpCommandHandler
{
    public string Command => "STAT";
    public async Task HandleAsync(IFtpSessionContext context, ParsedCommand parsed, StreamWriter writer, CancellationToken ct)
    {
        await writer.WriteLineAsync("211-FTP Server status");
        await writer.WriteLineAsync($" Current directory: {context.Cwd}");
        await writer.WriteLineAsync(" Features: UTF8 PASV PORT EPSV EPRT TYPE SIZE NLST RNFR RNTO");
        await writer.WriteLineAsync("211 End");
    }
}

internal sealed class TypeHandler : IFtpCommandHandler
{
    public string Command => "TYPE";
    public Task HandleAsync(IFtpSessionContext context, ParsedCommand parsed, StreamWriter writer, CancellationToken ct)
    {
        var t = (parsed.Argument ?? string.Empty).ToUpperInvariant();
        if (t == "I" || t.StartsWith("I ")) { context.TransferType = 'I'; return writer.WriteLineAsync("200 Type set to I"); }
        if (t == "A" || t.StartsWith("A ")) { context.TransferType = 'A'; return writer.WriteLineAsync("200 Type set to A"); }
        return writer.WriteLineAsync("504 Command not implemented for that parameter");
    }
}

internal sealed class SizeHandler : IFtpCommandHandler
{
    private readonly IStorageProvider _storage;
    public SizeHandler(IStorageProvider storage) => _storage = storage;
    public string Command => "SIZE";
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
        var size = await _storage.GetSizeAsync(path, ct);
        await writer.WriteLineAsync($"213 {size}");
    }
}

internal sealed class CwdHandler : IFtpCommandHandler
{
    private readonly IStorageProvider _storage;
    public CwdHandler(IStorageProvider storage) => _storage = storage;
    public string Command => "CWD";
    public async Task HandleAsync(IFtpSessionContext context, ParsedCommand parsed, StreamWriter writer, CancellationToken ct)
    {
        var path = context.ResolvePath(parsed.Argument);
        var entry = await _storage.GetEntryAsync(path, ct);
        if (entry is null || !entry.IsDirectory)
        {
            await writer.WriteLineAsync("550 Directory not found");
            return;
        }
        context.Cwd = path;
        await writer.WriteLineAsync("250 Requested file action okay, completed");
    }
}

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

internal sealed class RmdHandler : IFtpCommandHandler
{
    private readonly IStorageProvider _storage;
    public RmdHandler(IStorageProvider storage) => _storage = storage;
    public string Command => "RMD";
    public async Task HandleAsync(IFtpSessionContext context, ParsedCommand parsed, StreamWriter writer, CancellationToken ct)
    {
        var path = context.ResolvePath(parsed.Argument);
        var entry = await _storage.GetEntryAsync(path, ct);
        if (entry is null)
        {
            await writer.WriteLineAsync("550 Directory not found");
            return;
        }
        if (!entry.IsDirectory)
        {
            await writer.WriteLineAsync("550 Not a directory");
            return;
        }
        try
        {
            await _storage.DeleteAsync(path, recursive: false, ct);
            await writer.WriteLineAsync("250 Requested file action okay, completed");
        }
        catch (IOException)
        {
            await writer.WriteLineAsync("550 Directory not empty");
        }
    }
}

internal sealed class DeleHandler : IFtpCommandHandler
{
    private readonly IStorageProvider _storage;
    public DeleHandler(IStorageProvider storage) => _storage = storage;
    public string Command => "DELE";
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
        await _storage.DeleteAsync(path, recursive: false, ct);
        await writer.WriteLineAsync("250 Requested file action okay, completed");
    }
}

internal sealed class ListHandler : IFtpCommandHandler
{
    private readonly IStorageProvider _storage;
    public ListHandler(IStorageProvider storage) => _storage = storage;
    public string Command => "LIST";
    public async Task HandleAsync(IFtpSessionContext context, ParsedCommand parsed, StreamWriter writer, CancellationToken ct)
    {
        if (!context.IsAuthenticated)
        {
            await writer.WriteLineAsync("530 Not logged in.");
            return;
        }
        await writer.WriteLineAsync("150 Opening data connection for LIST");
        try
        {
            using var data = await context.OpenDataStreamAsync(ct);
            using var dw = new StreamWriter(data, Encoding.ASCII) { NewLine = "\r\n", AutoFlush = true };
            var entries = await _storage.ListAsync(context.Cwd, ct);
            foreach (var e in entries)
            {
                await dw.WriteLineAsync(FormatUnixListLine(e));
            }
            await writer.WriteLineAsync("226 Closing data connection. Requested file action successful");
        }
        catch
        {
            await writer.WriteLineAsync("425 Can't open data connection");
        }
    }

    private static string FormatUnixListLine(FileSystemEntry e)
    {
        var perms = e.IsDirectory ? 'd' : '-';
        var rights = "rwxr-xr-x";
        var links = 1;
        var owner = "owner";
        var group = "group";
        var size = e.Length ?? 0;
        var date = System.DateTimeOffset.Now.ToString("MMM dd HH:mm", System.Globalization.CultureInfo.InvariantCulture);
        return $"{perms}{rights} {links,3} {owner,5} {group,5} {size,8} {date} {e.Name}";
    }
}

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

internal sealed class RnfrHandler : IFtpCommandHandler
{
    private readonly IStorageProvider _storage;
    public RnfrHandler(IStorageProvider storage) => _storage = storage;
    public string Command => "RNFR";
    public async Task HandleAsync(IFtpSessionContext context, ParsedCommand parsed, StreamWriter writer, CancellationToken ct)
    {
        var from = context.ResolvePath(parsed.Argument);
        if (!await _storage.ExistsAsync(from, ct))
        {
            await writer.WriteLineAsync("550 File not found");
            return;
        }
        context.PendingRenameFrom = from;
        await writer.WriteLineAsync("350 Requested file action pending further information");
    }
}

internal sealed class RntoHandler : IFtpCommandHandler
{
    private readonly IStorageProvider _storage;
    public RntoHandler(IStorageProvider storage) => _storage = storage;
    public string Command => "RNTO";
    public async Task HandleAsync(IFtpSessionContext context, ParsedCommand parsed, StreamWriter writer, CancellationToken ct)
    {
        if (context.PendingRenameFrom is null)
        {
            await writer.WriteLineAsync("503 Bad sequence of commands");
            return;
        }
        await _storage.RenameAsync(context.PendingRenameFrom, context.ResolvePath(parsed.Argument), ct);
        context.PendingRenameFrom = null;
        await writer.WriteLineAsync("250 Requested file action okay, completed");
    }
}

internal sealed class QuitHandler : IFtpCommandHandler
{
    public string Command => "QUIT";
    public async Task HandleAsync(IFtpSessionContext context, ParsedCommand parsed, StreamWriter writer, CancellationToken ct)
    {
        await writer.WriteLineAsync("221 Service closing control connection");
        context.ShouldQuit = true;
    }
}

internal sealed class UserHandler : IFtpCommandHandler
{
    public string Command => "USER";
    public Task HandleAsync(IFtpSessionContext context, ParsedCommand parsed, StreamWriter writer, CancellationToken ct)
    {
        context.PendingUser = parsed.Argument;
        return writer.WriteLineAsync("331 User name okay, need password.");
    }
}

internal sealed class PassHandler : IFtpCommandHandler
{
    private readonly IAuthenticator _auth;
    public PassHandler(IAuthenticator auth) => _auth = auth;
    public string Command => "PASS";
    public async Task HandleAsync(IFtpSessionContext context, ParsedCommand parsed, StreamWriter writer, CancellationToken ct)
    {
        if (context.PendingUser is null)
        {
            await writer.WriteLineAsync("503 Bad sequence of commands");
            return;
        }
        var result = await _auth.AuthenticateAsync(context.PendingUser, parsed.Argument, ct);
        context.IsAuthenticated = result.Succeeded;
        await writer.WriteLineAsync(result.Succeeded ? "230 User logged in, proceed." : "530 Not logged in.");
    }
}

internal sealed class NoopHandler : IFtpCommandHandler
{
    public string Command => "NOOP";
    public Task HandleAsync(IFtpSessionContext context, ParsedCommand parsed, StreamWriter writer, CancellationToken ct)
        => writer.WriteLineAsync("200 NOOP ok");
}

internal sealed class SystHandler : IFtpCommandHandler
{
    public string Command => "SYST";
    public Task HandleAsync(IFtpSessionContext context, ParsedCommand parsed, StreamWriter writer, CancellationToken ct)
        => writer.WriteLineAsync("215 UNIX Type: L8");
}

internal sealed class PwdHandler : IFtpCommandHandler
{
    public string Command => "PWD";
    public Task HandleAsync(IFtpSessionContext context, ParsedCommand parsed, StreamWriter writer, CancellationToken ct)
        => writer.WriteLineAsync($"257 \"{context.Cwd}\" is current directory");
}

internal sealed class CdupHandler : IFtpCommandHandler
{
    public string Command => "CDUP";
    public Task HandleAsync(IFtpSessionContext context, ParsedCommand parsed, StreamWriter writer, CancellationToken ct)
    {
        var cwd = context.Cwd;
        cwd = cwd == "/" ? "/" : cwd.Substring(0, cwd.LastIndexOf('/'));
        if (string.IsNullOrEmpty(cwd)) cwd = "/";
        context.Cwd = cwd;
        return writer.WriteLineAsync("200 Directory changed to parent");
    }
}

internal sealed class HelpHandler : IFtpCommandHandler
{
    public string Command => "HELP";
    public Task HandleAsync(IFtpSessionContext context, ParsedCommand parsed, StreamWriter writer, CancellationToken ct)
    {
        return Write();
        async Task Write()
        {
            await writer.WriteLineAsync("214-The following commands are recognized.");
            await writer.WriteLineAsync(" USER PASS SYST FEAT PWD CWD CDUP TYPE PASV EPSV PORT EPRT LIST NLST RETR STOR DELE MKD RMD SIZE RNFR RNTO STAT HELP QUIT");
            await writer.WriteLineAsync("214 Help OK.");
        }
    }
}

internal sealed class PasvHandler : IFtpCommandHandler
{
    public string Command => "PASV";
    public Task HandleAsync(IFtpSessionContext context, ParsedCommand parsed, StreamWriter writer, CancellationToken ct)
    {
        var pe = context.EnterPassiveMode();
        var p1 = pe.Port / 256; var p2 = pe.Port % 256;
        return writer.WriteLineAsync($"227 Entering Passive Mode ({pe.IpAddress.Replace('.', ',')},{p1},{p2})");
    }
}

internal sealed class EpsvHandler : IFtpCommandHandler
{
    public string Command => "EPSV";
    public Task HandleAsync(IFtpSessionContext context, ParsedCommand parsed, StreamWriter writer, CancellationToken ct)
    {
        var pe = context.EnterPassiveMode();
        return writer.WriteLineAsync($"229 Entering Extended Passive Mode (|||{pe.Port}|)");
    }
}

internal sealed class PortHandler : IFtpCommandHandler
{
    public string Command => "PORT";
    public Task HandleAsync(IFtpSessionContext context, ParsedCommand parsed, StreamWriter writer, CancellationToken ct)
    {
        if (!TryParsePort(parsed.Argument, out var ep))
            return writer.WriteLineAsync("501 Syntax error in parameters or arguments");
        context.ActiveEndpoint = ep;
        return writer.WriteLineAsync("200 Command okay");
    }

    private static bool TryParsePort(string arg, out System.Net.IPEndPoint? ep)
    {
        ep = null;
        var parts = arg.Split(',');
        if (parts.Length != 6) return false;
        if (!byte.TryParse(parts[0], out var h1) || !byte.TryParse(parts[1], out var h2) ||
            !byte.TryParse(parts[2], out var h3) || !byte.TryParse(parts[3], out var h4) ||
            !byte.TryParse(parts[4], out var p1) || !byte.TryParse(parts[5], out var p2)) return false;
        var addr = new System.Net.IPAddress(new byte[] { h1, h2, h3, h4 });
        var port = p1 * 256 + p2;
        ep = new System.Net.IPEndPoint(addr, port);
        return true;
    }
}

internal sealed class EprtHandler : IFtpCommandHandler
{
    public string Command => "EPRT";
    public Task HandleAsync(IFtpSessionContext context, ParsedCommand parsed, StreamWriter writer, CancellationToken ct)
    {
        if (!TryParseEprt(parsed.Argument, out var ep))
            return writer.WriteLineAsync("501 Syntax error in parameters or arguments");
        context.ActiveEndpoint = ep;
        return writer.WriteLineAsync("200 Command okay");
    }

    private static bool TryParseEprt(string arg, out System.Net.IPEndPoint? ep)
    {
        ep = null;
        if (string.IsNullOrEmpty(arg)) return false;
        var delim = arg[0];
        var parts = arg.Split(delim);
        if (parts.Length < 5) return false;
        if (!int.TryParse(parts[1], out var af)) return false;
        var addrStr = parts[2];
        if (!int.TryParse(parts[3], out var port)) return false;
        try
        {
            var addr = System.Net.IPAddress.Parse(addrStr);
            if ((af == 1 && addr.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork) ||
                (af == 2 && addr.AddressFamily != System.Net.Sockets.AddressFamily.InterNetworkV6))
                return false;
            ep = new System.Net.IPEndPoint(addr, port);
            return true;
        }
        catch { return false; }
    }
}
