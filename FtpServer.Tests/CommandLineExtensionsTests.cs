using FtpServer.App.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;

namespace FtpServer.Tests;

public class CommandLineExtensionsTests
{
    [Fact]
    public void ApplyCommandLine_WithValidArguments_ShouldAddToConfiguration()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder([]);
        var args = new[] { "--port", "2121", "--listen", "0.0.0.0", "--health" };

        // Act
        builder.ApplyCommandLine(args);

        // Assert
        var config = builder.Configuration;
        Assert.Equal("2121", config["FtpServer:Port"]);
        Assert.Equal("0.0.0.0", config["FtpServer:ListenAddress"]);
        Assert.Equal("true", config["FtpServer:HealthEnabled"]);
    }

    [Fact]
    public void ApplyCommandLine_WithNoArguments_ShouldNotThrow()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder([]);
        var args = Array.Empty<string>();

        // Act & Assert
        var exception = Record.Exception(() => builder.ApplyCommandLine(args));
        Assert.Null(exception);
    }

    [Fact]
    public void ApplyCommandLine_WithEmptyArray_ShouldNotThrow()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder([]);
        var args = new string[0];

        // Act & Assert
        var exception = Record.Exception(() => builder.ApplyCommandLine(args));
        Assert.Null(exception);
    }

    [Fact]
    public void ApplyCommandLine_WithInvalidOption_ShouldThrowArgumentException()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder([]);
        var args = new[] { "--unknown-option", "value" };

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => builder.ApplyCommandLine(args));
        Assert.Contains("Command line parsing failed", exception.Message);
        Assert.Contains("Unrecognized", exception.Message);
    }

    [Fact]
    public void ApplyCommandLine_WithComplexConfiguration_ShouldOverrideDefaults()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder([]);

        // Add some base configuration first
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["FtpServer:Port"] = "21",
            ["FtpServer:ListenAddress"] = "127.0.0.1",
            ["FtpServer:HealthEnabled"] = "false"
        });

        var args = new[] { "--port", "2121", "--health" };

        // Act
        builder.ApplyCommandLine(args);

        // Assert - Command line should override existing configuration
        var config = builder.Configuration;
        Assert.Equal("2121", config["FtpServer:Port"]); // Overridden
        Assert.Equal("127.0.0.1", config["FtpServer:ListenAddress"]); // Unchanged
        Assert.Equal("true", config["FtpServer:HealthEnabled"]); // Overridden
    }

    [Fact]
    public void ApplyCommandLine_WithAllSupportedTypes_ShouldConfigureCorrectly()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder([]);
        var args = new[]
        {
            "--port", "8021",                    // int
            "--storage-root", "/data/ftp",       // string
            "--ftps-explicit",                   // bool (flag)
            "--max-sessions", "100",             // int
            "--auth", "InMemory",                // string
            "--tls-self-signed"                  // bool (flag)
        };

        // Act
        builder.ApplyCommandLine(args);

        // Assert
        var config = builder.Configuration;
        Assert.Equal("8021", config["FtpServer:Port"]);
        Assert.Equal("/data/ftp", config["FtpServer:StorageRoot"]);
        Assert.Equal("true", config["FtpServer:FtpsExplicitEnabled"]);
        Assert.Equal("100", config["FtpServer:MaxSessions"]);
        Assert.Equal("InMemory", config["FtpServer:Authenticator"]);
        Assert.Equal("true", config["FtpServer:TlsSelfSigned"]);
    }

    [Fact]
    public void ApplyCommandLine_WithConfigurationPrecedence_ShouldRespectOrder()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder([]);

        // 1. Add base configuration (lowest priority)
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["FtpServer:Port"] = "21"
        });

        // 2. Add environment variables (medium priority)
        Environment.SetEnvironmentVariable("FTP_FtpServer__Port", "8021");
        builder.Configuration.AddEnvironmentVariables("FTP_");

        // 3. Apply command line (highest priority)
        var args = new[] { "--port", "2121" };

        // Act
        builder.ApplyCommandLine(args);

        // Assert - Command line should have highest priority
        var config = builder.Configuration;
        Assert.Equal("2121", config["FtpServer:Port"]);

        // Cleanup
        Environment.SetEnvironmentVariable("FTP_FtpServer__Port", null);
    }

    [Fact]
    public void ApplyCommandLine_WithNullBuilder_ShouldThrow()
    {
        // Arrange
        WebApplicationBuilder? builder = null;
        var args = new[] { "--port", "2121" };

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => builder!.ApplyCommandLine(args));
    }

    [Fact]
    public void ApplyCommandLine_WithNullArgs_ShouldThrow()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder([]);
        string[]? args = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => builder.ApplyCommandLine(args!));
    }
}
