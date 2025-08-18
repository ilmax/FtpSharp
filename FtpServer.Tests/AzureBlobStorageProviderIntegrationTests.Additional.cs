using System.Text;
using FtpServer.Storage.AzureBlob;
using Xunit;

namespace FtpServer.Tests;

public sealed partial class AzureBlobStorageProviderIntegrationTests
{
    [Fact(Timeout = 60000)]
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

    [Fact(Timeout = 60000)]
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

    [Fact(Timeout = 60000)]
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

    [Fact(Timeout = 60000)]
    public async Task Delete_NonRecursive_Throws_On_Dir()
    {
        if (!_dockerAvailable) return;
        var provider = CreateProvider(prefix: "it5");
        var ct = CancellationToken.None;

        await provider.WriteAsync("/d/a.txt", Bytes("x"), ct);
        await Assert.ThrowsAsync<IOException>(async () => await provider.DeleteAsync("/d", recursive: false, ct));

        await provider.DeleteAsync("/d", recursive: true, ct);
    }

    [Fact(Timeout = 60000)]
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
