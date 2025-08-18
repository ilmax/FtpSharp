using FtpServer.Core.Protocol;

namespace FtpServer.Core.Server.Commands;

/// <summary>
/// Defines a handler for a specific FTP command.
/// </summary>
public interface IFtpCommandHandler
{
    string Command { get; }
    Task HandleAsync(IFtpSessionContext context, ParsedCommand parsed, StreamWriter writer, CancellationToken ct);
}

/// <summary>
/// Minimal session context surface for handlers. Can be expanded incrementally.
/// </summary>
public interface IFtpSessionContext
{
    string Cwd { get; set; }
    char TransferType { get; set; }
    string ResolvePath(string arg);
    bool IsAuthenticated { get; set; }
    string? PendingUser { get; set; }
    bool ShouldQuit { get; set; }
    string? PendingRenameFrom { get; set; }
    Task<Stream> OpenDataStreamAsync(CancellationToken ct);
    PassiveEndpoint EnterPassiveMode();
    System.Net.IPEndPoint? ActiveEndpoint { get; set; }
    // TLS/FTPS state
    bool IsControlTls { get; set; }
    char DataProtectionLevel { get; set; } // 'C' (clear) or 'P' (private)
    Task<Stream> UpgradeControlToTlsAsync(CancellationToken ct);
}
