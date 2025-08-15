using FtpServer.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace FtpServer.Core.Plugins;

/// <summary>
/// Simple DI-backed plugin registry mapping provider names to factories.
/// </summary>
public sealed class PluginRegistry : IAuthenticatorFactory, IStorageProviderFactory
{
    private readonly IServiceProvider _sp;

    public PluginRegistry(IServiceProvider sp) => _sp = sp;

    IAuthenticator IAuthenticatorFactory.Create(string name)
        => name switch
        {
            "InMemory" => _sp.GetRequiredService<InMemory.InMemoryAuthenticator>(),
            _ => throw new NotSupportedException($"Unknown authenticator '{name}'")
        };

    IStorageProvider IStorageProviderFactory.Create(string name)
        => name switch
        {
            "InMemory" => _sp.GetRequiredService<InMemory.InMemoryStorageProvider>(),
            "FileSystem" => _sp.GetRequiredService<FileSystem.FileSystemStorageProvider>(),
            _ => throw new NotSupportedException($"Unknown storage provider '{name}'")
        };
}
