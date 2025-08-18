using FtpServer.Core.Abstractions;
using Microsoft.Extensions.Configuration;

namespace FtpServer.Core.Basic;

/// <summary>
/// Basic authenticator backed by configuration.
/// Reads users from configuration keys under 'FtpServer:Users:<username>' with plain-text passwords.
/// Example env: FTP_FtpServer__Users__alice=secret
/// </summary>
public sealed class BasicAuthenticator : IAuthenticator
{
    private readonly IConfiguration _config;

    public BasicAuthenticator(IConfiguration config)
    {
        _config = config;
    }

    public Task<AuthResult> AuthenticateAsync(string username, string password, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(username))
            return Task.FromResult(new AuthResult(false, "Empty username"));

        // Read "FtpServer:Users:<username>"
        string? configured = _config[$"FtpServer:Users:{username}"];
        if (configured is null)
            return Task.FromResult(new AuthResult(false, "Unknown user"));

        bool ok = string.Equals(configured, password, StringComparison.Ordinal);
        return Task.FromResult(new AuthResult(ok, ok ? null : "Invalid credentials"));
    }
}
