using System.Collections.Concurrent;
using FtpServer.Core.Abstractions;

namespace FtpServer.Core.InMemory;

/// <summary>
/// In-memory username/password store for Basic authentication.
/// </summary>
public sealed class InMemoryAuthenticator : IAuthenticator
{
    private readonly ConcurrentDictionary<string, string> _users = new(StringComparer.Ordinal);

    public InMemoryAuthenticator()
    {
        _users.TryAdd("anonymous", "");
    }

    public Task<AuthResult> AuthenticateAsync(string username, string password, CancellationToken ct)
    {
        if (_users.TryGetValue(username, out string? pwd) && pwd == password)
            return Task.FromResult(new AuthResult(true, null));
        return Task.FromResult(new AuthResult(false, "Invalid credentials"));
    }

    // helper for tests
    public void SetUser(string user, string password) => _users[user] = password;
}
