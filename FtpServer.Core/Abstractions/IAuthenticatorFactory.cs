namespace FtpServer.Core.Abstractions;

/// <summary>
/// Factory to resolve an <see cref="IAuthenticator"/> by name.
/// </summary>
public interface IAuthenticatorFactory
{
    IAuthenticator Create(string name);
}
