using System.Text;
using FtpServer.Core.Protocol;
using FtpServer.Core.Server.Commands;

namespace FtpServer.Tests;

public class FtpsHandlerUnitTests
{
    private sealed class FakeCtx : IFtpSessionContext
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
        public string ResolvePath(string arg) => arg;
        public Task<Stream> OpenDataStreamAsync(CancellationToken ct) => Task.FromException<Stream>(new IOException());
        public PassiveEndpoint EnterPassiveMode() => new PassiveEndpoint("127.0.0.1", 0);
        public Task<Stream> UpgradeControlToTlsAsync(CancellationToken ct) => Task.FromResult<Stream>(Stream.Null);
    }

    [Fact]
    public async Task AuthTlsHandler_SetsFlag_And_Returns234()
    {
        var h = new AuthTlsHandler();
        var ctx = new FakeCtx();
        using var ms = new MemoryStream();
        using var w = new StreamWriter(ms) { AutoFlush = true, NewLine = "\r\n" };
        await h.HandleAsync(ctx, new ParsedCommand("AUTH", "TLS"), w, CancellationToken.None);
        string resp = Encoding.ASCII.GetString(ms.ToArray());
        Assert.True(ctx.IsControlTls);
        Assert.Contains("234", resp);
    }

    [Fact]
    public async Task PbszHandler_OnlyAcceptsZero()
    {
        var h = new PbszHandler();
        var ctx = new FakeCtx();
        using var ms = new MemoryStream();
        using var w = new StreamWriter(ms) { AutoFlush = true, NewLine = "\r\n" };
        await h.HandleAsync(ctx, new ParsedCommand("PBSZ", "1"), w, CancellationToken.None);
        await h.HandleAsync(ctx, new ParsedCommand("PBSZ", "0"), w, CancellationToken.None);
        string resp = Encoding.ASCII.GetString(ms.ToArray());
        Assert.Contains("501", resp);
        Assert.Contains("200 PBSZ=0", resp);
    }

    [Fact]
    public async Task ProtHandler_Sets_C_And_P()
    {
        var h = new ProtHandler();
        var ctx = new FakeCtx();
        using var ms = new MemoryStream();
        using var w = new StreamWriter(ms) { AutoFlush = true, NewLine = "\r\n" };
        await h.HandleAsync(ctx, new ParsedCommand("PROT", "P"), w, CancellationToken.None);
        Assert.Equal('P', ctx.DataProtectionLevel);
        await h.HandleAsync(ctx, new ParsedCommand("PROT", "C"), w, CancellationToken.None);
        Assert.Equal('C', ctx.DataProtectionLevel);
        await h.HandleAsync(ctx, new ParsedCommand("PROT", "X"), w, CancellationToken.None);
        string resp = Encoding.ASCII.GetString(ms.ToArray());
        Assert.Contains("200 PROT set to P", resp);
        Assert.Contains("200 PROT set to C", resp);
        Assert.Contains("504", resp);
    }
}
