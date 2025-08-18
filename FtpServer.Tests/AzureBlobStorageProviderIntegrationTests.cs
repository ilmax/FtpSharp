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

public sealed partial class AzureBlobStorageProviderIntegrationTests : IAsyncLifetime
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

    private static async Task<string> ReadAllStringAsync(AzureBlobStorageProvider provider, string path, CancellationToken ct)
    {
        var buf = new List<byte>();
        await foreach (var chunk in provider.ReadAsync(path, 4096, ct))
        {
            buf.AddRange(chunk.ToArray());
        }
        return Encoding.UTF8.GetString(buf.ToArray());
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
public sealed partial class AzureBlobStorageProviderIntegrationTests
{
        [Fact]
        public async Task ReadFromOffset_Works()
        {
            if (!_dockerAvailable) return;
            var provider = CreateProvider(prefix: "it2");
            var ct = CancellationToken.None;

            await provider.WriteAsync("/dir/file.txt", Bytes("abcdef"), ct);
            var buf = new List<byte>();
            await foreach (var chunk in provider.ReadFromOffsetAsync("/dir/file.txt", 3, 4096, ct))
            {
                buf.AddRange(chunk.ToArray());
            }
            Assert.Equal("def", Encoding.UTF8.GetString(buf.ToArray()));

            await provider.DeleteAsync("/dir", recursive: true, ct);
        }

        [Fact]
        public async Task TruncateThenAppend_Works()
        {
            if (!_dockerAvailable) return;
            var provider = CreateProvider(prefix: "it3");
            var ct = CancellationToken.None;

            await provider.WriteAsync("/file.txt", Bytes("abcdef"), ct);
            await provider.WriteTruncateThenAppendAsync("/file.txt", 3, Bytes("XYZ"), ct);
            var all = await ReadAllStringAsync(provider, "/file.txt", ct);
            Assert.Equal("abcXYZ", all);

            await provider.DeleteAsync("/file.txt", recursive: false, ct);
        }

        [Fact]
        public async Task GetEntry_ForDir_And_File_Works()
        {
            if (!_dockerAvailable) return;
            var provider = CreateProvider(prefix: "it4");
            var ct = CancellationToken.None;

            await provider.WriteAsync("/dir/a.txt", Bytes("x"), ct);

            var dirEntry = await provider.GetEntryAsync("/dir", ct);
            Assert.NotNull(dirEntry);
            Assert.True(dirEntry!.IsDirectory);
            Assert.Equal("dir", dirEntry.Name);

            var fileEntry = await provider.GetEntryAsync("/dir/a.txt", ct);
            Assert.NotNull(fileEntry);
            Assert.False(fileEntry!.IsDirectory);
            Assert.Equal(1, fileEntry.Length);

            await provider.DeleteAsync("/dir", recursive: true, ct);
        }

        [Fact]
        public async Task Delete_NonRecursive_Throws_On_Dir()
        {
            if (!_dockerAvailable) return;
            var provider = CreateProvider(prefix: "it5");
            var ct = CancellationToken.None;

            await provider.WriteAsync("/d/a.txt", Bytes("x"), ct);
            await Assert.ThrowsAsync<IOException>(async () => await provider.DeleteAsync("/d", recursive: false, ct));

            await provider.DeleteAsync("/d", recursive: true, ct);
        }

        [Fact]
        public async Task Rename_Directory_Works()
        {
            if (!_dockerAvailable) return;
            var provider = CreateProvider(prefix: "it6");
            var ct = CancellationToken.None;

            await provider.WriteAsync("/dir1/a.txt", Bytes("hello"), ct);
            await provider.RenameAsync("/dir1", "/dir2", ct);

            Assert.False(await provider.ExistsAsync("/dir1/a.txt", ct));
            Assert.True(await provider.ExistsAsync("/dir2/a.txt", ct));

            var content = await ReadAllStringAsync(provider, "/dir2/a.txt", ct);
            Assert.Equal("hello", content);

            await provider.DeleteAsync("/dir2", recursive: true, ct);
        }
    }
