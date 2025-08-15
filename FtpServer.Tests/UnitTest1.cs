using System.Net.Sockets;
using System.Text;
using FtpServer.Core.InMemory;
using FtpServer.Core.Server;
using FtpServer.Core.Protocol;

namespace FtpServer.Tests;

public class UnitTest1
{
    [Fact]
    public async Task InMemoryAuthenticator_Works()
    {
        var auth = new InMemoryAuthenticator();
        auth.SetUser("u", "p");
        var ok = await auth.AuthenticateAsync("u", "p", CancellationToken.None);
        var bad = await auth.AuthenticateAsync("u", "x", CancellationToken.None);
        Assert.True(ok.Succeeded);
        Assert.False(bad.Succeeded);
    }
}

public class ParserTests
{
    [Theory]
    [InlineData("USER bob", "USER", "bob")]
    [InlineData("PASS secret", "PASS", "secret")]
    [InlineData("QUIT", "QUIT", "")]
    public void Parse_Basic(string line, string cmd, string arg)
    {
        var p = FtpCommandParser.Parse(line);
        Assert.Equal(cmd, p.Command);
        Assert.Equal(arg, p.Argument);
    }
}

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
        var bytes = Encoding.ASCII.GetBytes(text);
        yield return bytes;
        await Task.CompletedTask;
    }
}
