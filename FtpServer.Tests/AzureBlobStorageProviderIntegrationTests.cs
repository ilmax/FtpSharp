using System.Text;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using FtpServer.Core.Abstractions;
using FtpServer.Storage.AzureBlob;
using Microsoft.Extensions.Options;
using Xunit;

namespace FtpServer.Tests;

public sealed class AzureBlobStorageProviderIntegrationTests : IAsyncLifetime
{
    private IContainer? _azurite;
    private string? _connectionString;
    private bool _dockerAvailable;

    public async Task InitializeAsync()
    {
        try
        {
            // Start Azurite container (blob only is fine; default account devstoreaccount1)
            _azurite = new ContainerBuilder()
                .WithImage("mcr.microsoft.com/azure-storage/azurite")
                .WithName($"azurite-{Guid.NewGuid():N}")
                .WithPortBinding(10000, true)
                .WithCommand("azurite-blob", "--blobHost", "0.0.0.0")
                .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(10000))
                .Build();
            await _azurite.StartAsync();
            var hostPort = _azurite.GetMappedPublicPort(10000);
            _connectionString = $"DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://127.0.0.1:{hostPort}/devstoreaccount1;";

            // Ensure container exists
            var service = new BlobServiceClient(_connectionString);
            var container = service.GetBlobContainerClient("test");
            await container.CreateIfNotExistsAsync(PublicAccessType.None);

            _dockerAvailable = true;
        }
        catch
        {
            // Docker likely not available or misconfigured; mark test as skippable.
            _dockerAvailable = false;
            _azurite = null;
            _connectionString = null;
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

    private AzureBlobStorageProvider CreateProvider(string? prefix = null)
    {
        var opts = new AzureBlobStorageOptions
        {
            ConnectionString = _connectionString,
            Container = "test",
            AccountUrl = "http://devstore.local/", // unused when ConnectionString set
            Prefix = prefix
        };
        return new AzureBlobStorageProvider(Options.Create(opts));
    }

    private static async IAsyncEnumerable<ReadOnlyMemory<byte>> Bytes(params string[] parts)
    {
        foreach (var p in parts)
        {
            yield return Encoding.UTF8.GetBytes(p);
            await Task.Yield();
        }
    }

    [Fact]
    public async Task Write_Read_List_Delete_Works()
    {
        if (!_dockerAvailable)
        {
            // Docker is not available; treat as no-op to avoid false failures in environments without Docker.
            return;
        }
        var provider = CreateProvider(prefix: "it");
        var ct = CancellationToken.None;

        // Write a file
        var content = Bytes("hello world");
        await provider.WriteAsync("/dir/file.txt", content, ct);

        // Exists via directory prefix
        Assert.True(await provider.ExistsAsync("/dir/file.txt", ct));

        // List parent
        var list = await provider.ListAsync("/dir", ct);
        Assert.Contains(list, e => !e.IsDirectory && e.Name == "file.txt" && e.Length == 11);

        // Read back
        var buf = new List<byte>();
        await foreach (var chunk in provider.ReadAsync("/dir/file.txt", 4096, ct))
        {
            buf.AddRange(chunk.ToArray());
        }
        Assert.Equal("hello world", Encoding.UTF8.GetString(buf.ToArray()));

        // Rename
        await provider.RenameAsync("/dir/file.txt", "/dir/file2.txt", ct);
        Assert.False(await provider.ExistsAsync("/dir/file.txt", ct));
        Assert.True(await provider.ExistsAsync("/dir/file2.txt", ct));

        // Append
        var more = Bytes("!");
        await provider.AppendAsync("/dir/file2.txt", more, ct);
        var size = await provider.GetSizeAsync("/dir/file2.txt", ct);
        Assert.Equal(12, size);

        // Delete directory recursively
        await provider.DeleteAsync("/dir", recursive: true, ct);
        Assert.False(await provider.ExistsAsync("/dir/file2.txt", ct));
    }
}
