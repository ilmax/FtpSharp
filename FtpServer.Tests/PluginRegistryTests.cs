using FtpServer.Core.Plugins;

namespace FtpServer.Tests;

public class PluginRegistryTests
{
    [Fact]
    public void Resolves_Known_Providers_And_Throws_On_Unknown()
    {
        var sp = new FakeServiceProvider(new()
        {
            { typeof(Core.InMemory.InMemoryStorageProvider), new Core.InMemory.InMemoryStorageProvider() },
            { typeof(Core.InMemory.InMemoryAuthenticator), new Core.InMemory.InMemoryAuthenticator() },
        });
        var reg = new PluginRegistry(sp);

        var auth = ((Core.Abstractions.IAuthenticatorFactory)reg).Create("InMemory");
        Assert.NotNull(auth);
        var store = ((Core.Abstractions.IStorageProviderFactory)reg).Create("InMemory");
        Assert.NotNull(store);
        Assert.Throws<NotSupportedException>(() => ((Core.Abstractions.IStorageProviderFactory)reg).Create("Unknown"));
        Assert.Throws<NotSupportedException>(() => ((Core.Abstractions.IAuthenticatorFactory)reg).Create("Unknown"));
    }

    private sealed class FakeServiceProvider : IServiceProvider
    {
        private readonly Dictionary<Type, object> _map;
        public FakeServiceProvider(Dictionary<Type, object> map) => _map = map;
        public object? GetService(Type serviceType) => _map.TryGetValue(serviceType, out object? o) ? o : null;
    }
}
