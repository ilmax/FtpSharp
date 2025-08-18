using System.Text;
using FtpServer.Core.InMemory;
using FtpServer.Core.Protocol;
using FtpServer.Core.Server.Commands;

namespace FtpServer.Tests;

public class HandlerTests
{
    private sealed class FakeContext : IFtpSessionContext
    {
        public string Cwd { get; set; } = "/";
        public char TransferType { get; set; } = 'I';
        public bool IsAuthenticated { get; set; }
        public string? PendingUser { get; set; }
        public bool ShouldQuit { get; set; }
        public string? PendingRenameFrom { get; set; }
        public System.Net.IPEndPoint? ActiveEndpoint { get; set; }
        public bool IsControlTls { get; set; }
        public char DataProtectionLevel { get; set; } = 'C';
        public string ResolvePath(string arg) => arg.StartsWith('/') ? arg : (Cwd == "/" ? "/" + arg : Cwd + "/" + arg);
        public Task<Stream> OpenDataStreamAsync(CancellationToken ct) => Task.FromException<Stream>(new IOException("no data"));
        public PassiveEndpoint EnterPassiveMode() => new PassiveEndpoint("127.0.0.1", 2121);
        public Task<Stream> UpgradeControlToTlsAsync(CancellationToken ct) => Task.FromResult<Stream>(new MemoryStream());
    }

    private static async IAsyncEnumerable<ReadOnlyMemory<byte>> GetChunks(string text)
    {
        var bytes = Encoding.ASCII.GetBytes(text);
        yield return bytes;
        await Task.CompletedTask;
    }

    [Fact]
    public async Task TypeHandler_Sets_Type()
    {
        var h = new TypeHandler();
        var ctx = new FakeContext();
        using var sw = new StringWriter();
        using var w = new StreamWriter(new MemoryStream()) { AutoFlush = true };
        await h.HandleAsync(ctx, new ParsedCommand("TYPE", "A"), w, CancellationToken.None);
        Assert.Equal('A', ctx.TransferType);
    }

    [Fact]
    public async Task ModeHandler_Accepts_S_Only()
    {
        var h = new ModeHandler();
        var ctx = new FakeContext();
        using var ms = new MemoryStream();
        using var w = new StreamWriter(ms) { AutoFlush = true };
        await h.HandleAsync(ctx, new ParsedCommand("MODE", "S"), w, CancellationToken.None);
        await h.HandleAsync(ctx, new ParsedCommand("MODE", "B"), w, CancellationToken.None);
        var output = Encoding.ASCII.GetString(ms.ToArray());
        Assert.Contains("200", output);
        Assert.Contains("504", output);
    }

    [Fact]
    public async Task StruHandler_Accepts_F_Only()
    {
        var h = new StruHandler();
        var ctx = new FakeContext();
        using var ms = new MemoryStream();
        using var w = new StreamWriter(ms) { AutoFlush = true };
        await h.HandleAsync(ctx, new ParsedCommand("STRU", "F"), w, CancellationToken.None);
        await h.HandleAsync(ctx, new ParsedCommand("STRU", "R"), w, CancellationToken.None);
        var output = Encoding.ASCII.GetString(ms.ToArray());
        Assert.Contains("200", output);
        Assert.Contains("504", output);
    }

    [Fact]
    public async Task AlloHandler_Always_202()
    {
        var h = new AlloHandler();
        var ctx = new FakeContext();
        using var ms = new MemoryStream();
        using var w = new StreamWriter(ms) { AutoFlush = true };
        await h.HandleAsync(ctx, new ParsedCommand("ALLO", "123"), w, CancellationToken.None);
        await h.HandleAsync(ctx, new ParsedCommand("ALLO", string.Empty), w, CancellationToken.None);
        var output = Encoding.ASCII.GetString(ms.ToArray());
        Assert.Contains("202", output);
    }

    [Fact]
    public async Task SizeHandler_File_And_Dir_Behavior()
    {
        var store = new InMemoryStorageProvider();
        await store.CreateDirectoryAsync("/d", CancellationToken.None);
        await store.WriteAsync("/d/a.txt", GetChunks("abc"), CancellationToken.None);
        var h = new SizeHandler(store);
        var ctx = new FakeContext();
        using var ms = new MemoryStream();
        using var w = new StreamWriter(ms) { AutoFlush = true };
        await h.HandleAsync(ctx, new ParsedCommand("SIZE", "/d/a.txt"), w, CancellationToken.None);
        await h.HandleAsync(ctx, new ParsedCommand("SIZE", "/d"), w, CancellationToken.None);
        var output = Encoding.ASCII.GetString(ms.ToArray());
        Assert.Contains("213 3", output);
        Assert.Contains("550 Not a plain file", output);
    }

    [Fact]
    public async Task Rnfr_Rnto_Sequence()
    {
        var store = new InMemoryStorageProvider();
        await store.WriteAsync("/a.txt", GetChunks("x"), CancellationToken.None);
        var rnfr = new RnfrHandler(store);
        var rnto = new RntoHandler(store);
        var ctx = new FakeContext();
        using var ms = new MemoryStream();
        using var w = new StreamWriter(ms) { AutoFlush = true };
        await rnfr.HandleAsync(ctx, new ParsedCommand("RNFR", "/a.txt"), w, CancellationToken.None);
        await rnto.HandleAsync(ctx, new ParsedCommand("RNTO", "/b.txt"), w, CancellationToken.None);
        Assert.True(await store.ExistsAsync("/b.txt", CancellationToken.None));
    }
}
