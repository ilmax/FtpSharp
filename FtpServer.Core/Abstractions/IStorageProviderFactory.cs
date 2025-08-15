namespace FtpServer.Core.Abstractions;

/// <summary>
/// Factory to resolve an <see cref="IStorageProvider"/> by name.
/// </summary>
public interface IStorageProviderFactory
{
    IStorageProvider Create(string name);
}
