using System.Text;
using FtpServer.Core.InMemory;

namespace FtpServer.Tests;

public class StorageTests
{
    [Fact]
    public async Task Create_List_Write_Read()
    {
        var store = new InMemoryStorageProvider();
        await store.CreateDirectoryAsync("/foo", CancellationToken.None);
        var chunks = GetChunks("hello world");
        await store.WriteAsync("/foo/a.txt", chunks, CancellationToken.None);
        var list = await store.ListAsync("/foo", CancellationToken.None);
        Assert.Single(list);
        Assert.Equal("a.txt", list[0].Name);
        var read = new List<byte>();
        await foreach (var c in store.ReadAsync("/foo/a.txt", 4, CancellationToken.None))
            read.AddRange(c.Span.ToArray());
        Assert.Equal("hello world", Encoding.ASCII.GetString(read.ToArray()));
    }

    private static async IAsyncEnumerable<ReadOnlyMemory<byte>> GetChunks(string text)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(text);
        yield return bytes;
        await Task.CompletedTask;
    }
}
