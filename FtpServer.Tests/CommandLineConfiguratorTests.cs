using FtpServer.App.CommandLine;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using System.CommandLine;

namespace FtpServer.Tests;

public class CommandLineConfiguratorTests
{
    [Fact]
    public void CreateRootCommand_ShouldCreateCommandWithAllOptions()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder([]);

        // Act
        var rootCommand = CommandLineConfigurator.CreateRootCommand(builder);

        // Assert
        Assert.NotNull(rootCommand);
        Assert.Equal("FTP Server host with ASP.NET Core health", rootCommand.Description);
        
        // Verify all expected options are present
        var optionNames = rootCommand.Options.Select(o => o.Name).ToList();
        
        var expectedOptions = new[]
        {
            "--port", "--listen", "--max-sessions", "--pasv-start", "--pasv-end",
            "--auth", "--storage", "--storage-root", "--health", "--health-url",
            "--data-open-timeout", "--data-transfer-timeout", "--control-read-timeout",
            "--rate-limit", "--ftps-explicit", "--ftps-implicit", "--ftps-implicit-port",
            "--tls-cert", "--tls-cert-pass", "--tls-self-signed"
        };

        foreach (var expectedOption in expectedOptions)
        {
            Assert.Contains(expectedOption, optionNames);
        }
    }

    [Fact]
    public void ExtractCommandLineArguments_ShouldReturnCorrectMappings()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder([]);
        var rootCommand = CommandLineConfigurator.CreateRootCommand(builder);
        var testArgs = new[] { "--port", "2121", "--listen", "0.0.0.0", "--health" };
        var parseResult = rootCommand.Parse(testArgs);

        // Act
        var configArgs = CommandLineConfigurator.ExtractCommandLineArguments(parseResult);

        // Assert
        Assert.Contains("FtpServer:Port=2121", configArgs);
        Assert.Contains("FtpServer:ListenAddress=0.0.0.0", configArgs);
        Assert.Contains("FtpServer:HealthEnabled=true", configArgs);
    }

    [Fact]
    public void ExtractCommandLineArguments_WithNoValues_ShouldReturnEmptyArray()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder([]);
        var rootCommand = CommandLineConfigurator.CreateRootCommand(builder);
        var parseResult = rootCommand.Parse([]);

        // Act
        var configArgs = CommandLineConfigurator.ExtractCommandLineArguments(parseResult);

        // Assert
        Assert.Empty(configArgs);
    }

    [Fact]
    public void ExtractCommandLineArguments_WithBooleanOptions_ShouldHandleCorrectly()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder([]);
        var rootCommand = CommandLineConfigurator.CreateRootCommand(builder);
        var testArgs = new[] { "--ftps-explicit", "--tls-self-signed" };
        var parseResult = rootCommand.Parse(testArgs);

        // Act
        var configArgs = CommandLineConfigurator.ExtractCommandLineArguments(parseResult);

        // Assert
        Assert.Contains("FtpServer:FtpsExplicitEnabled=true", configArgs);
        Assert.Contains("FtpServer:TlsSelfSigned=true", configArgs);
    }
}