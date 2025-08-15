using System.IO;
using System.Threading;
using System.Threading.Tasks;
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
}
