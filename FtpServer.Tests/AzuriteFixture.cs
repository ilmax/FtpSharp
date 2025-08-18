using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using Xunit;

namespace FtpServer.Tests;

public sealed class AzuriteFixture : IAsyncLifetime
{
    private IContainer? _azurite;
    public bool DockerAvailable { get; private set; }
    public string? ConnectionString { get; private set; }

    public async Task InitializeAsync()
    {
        try
        {
            _azurite = new ContainerBuilder()
                .WithImage("mcr.microsoft.com/azure-storage/azurite")
                .WithName($"azurite-{Guid.NewGuid():N}")
                .WithPortBinding(10000, true)
                .WithCommand("azurite-blob", "--blobHost", "0.0.0.0")
                .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(10000))
                .Build();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(25));
            await _azurite.StartAsync(cts.Token);
            var hostPort = _azurite.GetMappedPublicPort(10000);
            ConnectionString = $"DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://127.0.0.1:{hostPort}/devstoreaccount1;";

            // Ensure container exists
            var service = new BlobServiceClient(ConnectionString);
            var container = service.GetBlobContainerClient("test");
            await container.CreateIfNotExistsAsync(PublicAccessType.None);

            DockerAvailable = true;
        }
        catch
        {
            DockerAvailable = false;
            _azurite = null;
            ConnectionString = null;
        }
    }

    public async Task DisposeAsync()
    {
        if (_azurite is not null)
        {
            await _azurite.StopAsync();
            await _azurite.DisposeAsync();
        }
    }
}
