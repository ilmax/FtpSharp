using FtpServer.Core.Protocol;

namespace FtpServer.Tests;

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
