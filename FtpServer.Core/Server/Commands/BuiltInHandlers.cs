using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FtpServer.Core.Protocol;
using FtpServer.Core.Abstractions;

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
