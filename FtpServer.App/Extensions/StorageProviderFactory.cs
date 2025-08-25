using FtpServer.Core.Abstractions;

namespace FtpServer.App.Extensions;

internal sealed class AppStorageProviderFactory : IStorageProviderFactory
{
    private readonly IServiceProvider _sp;
    public AppStorageProviderFactory(IServiceProvider sp) => _sp = sp;

    public IStorageProvider Create(string name)
        => name switch
        {
            "AzureBlob" => _sp.GetRequiredService<Storage.AzureBlob.AzureBlobStorageProvider>(),
            _ => ((IStorageProviderFactory)_sp.GetRequiredService<Core.Plugins.PluginRegistry>()).Create(name)
        };
}
