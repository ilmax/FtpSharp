using System.Text;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using FtpServer.Core.Abstractions;
using FtpServer.Storage.AzureBlob;
using Microsoft.Extensions.Options;
using Xunit;

namespace FtpServer.Tests;

[CollectionDefinition(nameof(AzuriteCollection))]
public sealed class AzuriteCollection : ICollectionFixture<AzuriteFixture> { }

[Collection(nameof(AzuriteCollection))]
public sealed partial class AzureBlobStorageProviderIntegrationTests
{
    private readonly AzuriteFixture _fixture;
    private readonly bool _dockerAvailable;
    private readonly string? _connectionString;

    public AzureBlobStorageProviderIntegrationTests(AzuriteFixture fixture)
    {
        _fixture = fixture;
        _dockerAvailable = fixture.DockerAvailable;
        _connectionString = fixture.ConnectionString;
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

    [Fact(Timeout = 60000)]
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
// Additional tests moved to AzureBlobStorageProviderIntegrationTests.Additional.cs
