using System.Threading;
using System.Threading.Tasks;

namespace FtpServer.Core.Abstractions;

/// <summary>
/// Authentication plugin abstraction. Implement Basic auth or external providers.
/// </summary>
public interface IAuthenticator
{
    Task<AuthResult> AuthenticateAsync(string username, string password, CancellationToken ct);
}

public sealed record AuthResult(bool Succeeded, string? Reason);
