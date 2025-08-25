using System.CommandLine;
using FtpServer.App.CommandLine;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;

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
        var parseResult = rootCommand.Parse(Array.Empty<string>());

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

    [Fact]
    public void ExtractCommandLineArguments_WithAllIntegerOptions_ShouldMapCorrectly()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder([]);
        var rootCommand = CommandLineConfigurator.CreateRootCommand(builder);
        var testArgs = new[]
        {
            "--port", "2121",
            "--max-sessions", "100",
            "--pasv-start", "5000",
            "--pasv-end", "5100",
            "--data-open-timeout", "30000",
            "--data-transfer-timeout", "60000",
            "--control-read-timeout", "10000",
            "--rate-limit", "1048576",
            "--ftps-implicit-port", "990"
        };
        var parseResult = rootCommand.Parse(testArgs);

        // Act
        var configArgs = CommandLineConfigurator.ExtractCommandLineArguments(parseResult);

        // Assert
        Assert.Contains("FtpServer:Port=2121", configArgs);
        Assert.Contains("FtpServer:MaxSessions=100", configArgs);
        Assert.Contains("FtpServer:PassivePortRangeStart=5000", configArgs);
        Assert.Contains("FtpServer:PassivePortRangeEnd=5100", configArgs);
        Assert.Contains("FtpServer:DataOpenTimeoutMs=30000", configArgs);
        Assert.Contains("FtpServer:DataTransferTimeoutMs=60000", configArgs);
        Assert.Contains("FtpServer:ControlReadTimeoutMs=10000", configArgs);
        Assert.Contains("FtpServer:DataRateLimitBytesPerSec=1048576", configArgs);
        Assert.Contains("FtpServer:FtpsImplicitPort=990", configArgs);
    }

    [Fact]
    public void ExtractCommandLineArguments_WithAllStringOptions_ShouldMapCorrectly()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder([]);
        var rootCommand = CommandLineConfigurator.CreateRootCommand(builder);
        var testArgs = new[]
        {
            "--listen", "192.168.1.1",
            "--auth", "Basic",
            "--storage", "FileSystem",
            "--storage-root", "/var/ftp",
            "--health-url", "/status",
            "--tls-cert", "/path/to/cert.pfx",
            "--tls-cert-pass", "secret123"
        };
        var parseResult = rootCommand.Parse(testArgs);

        // Act
        var configArgs = CommandLineConfigurator.ExtractCommandLineArguments(parseResult);

        // Assert
        Assert.Contains("FtpServer:ListenAddress=192.168.1.1", configArgs);
        Assert.Contains("FtpServer:Authenticator=Basic", configArgs);
        Assert.Contains("FtpServer:StorageProvider=FileSystem", configArgs);
        Assert.Contains("FtpServer:StorageRoot=/var/ftp", configArgs);
        Assert.Contains("FtpServer:HealthUrl=/status", configArgs);
        Assert.Contains("FtpServer:TlsCertPath=/path/to/cert.pfx", configArgs);
        Assert.Contains("FtpServer:TlsCertPassword=secret123", configArgs);
    }

    [Fact]
    public void ExtractCommandLineArguments_WithAllBooleanOptions_ShouldMapCorrectly()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder([]);
        var rootCommand = CommandLineConfigurator.CreateRootCommand(builder);
        var testArgs = new[]
        {
            "--health",
            "--ftps-explicit",
            "--ftps-implicit",
            "--tls-self-signed"
        };
        var parseResult = rootCommand.Parse(testArgs);

        // Act
        var configArgs = CommandLineConfigurator.ExtractCommandLineArguments(parseResult);

        // Assert
        Assert.Contains("FtpServer:HealthEnabled=true", configArgs);
        Assert.Contains("FtpServer:FtpsExplicitEnabled=true", configArgs);
        Assert.Contains("FtpServer:FtpsImplicitEnabled=true", configArgs);
        Assert.Contains("FtpServer:TlsSelfSigned=true", configArgs);
    }

    [Fact]
    public void ExtractCommandLineArguments_WithPartialOptions_ShouldOnlyMapProvidedValues()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder([]);
        var rootCommand = CommandLineConfigurator.CreateRootCommand(builder);
        var testArgs = new[] { "--port", "2121", "--health" };
        var parseResult = rootCommand.Parse(testArgs);

        // Act
        var configArgs = CommandLineConfigurator.ExtractCommandLineArguments(parseResult);

        // Assert
        Assert.Contains("FtpServer:Port=2121", configArgs);
        Assert.Contains("FtpServer:HealthEnabled=true", configArgs);
        Assert.Equal(2, configArgs.Length);
    }

    [Fact]
    public void ExtractCommandLineArguments_WithInvalidArguments_ShouldHandleErrors()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder([]);
        var rootCommand = CommandLineConfigurator.CreateRootCommand(builder);
        var testArgs = new[] { "--port", "invalid" };
        var parseResult = rootCommand.Parse(testArgs);

        // Act & Assert - This should not throw, but the parse result will contain errors
        var configArgs = CommandLineConfigurator.ExtractCommandLineArguments(parseResult);

        // With invalid integer, the GetValue should return null/default and not be included
        Assert.DoesNotContain(configArgs, arg => arg.StartsWith("FtpServer:Port"));
    }

    [Fact]
    public void CreateRootCommand_ShouldHaveCorrectDescriptionAndOptionCount()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder([]);

        // Act
        var rootCommand = CommandLineConfigurator.CreateRootCommand(builder);

        // Assert
        Assert.Equal("FTP Server host with ASP.NET Core health", rootCommand.Description);
        Assert.Equal(22, rootCommand.Options.Count); // Verify all options are added (20 custom + 2 built-in)
    }

    [Fact]
    public void CreateRootCommand_OptionsHaveCorrectTypes()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder([]);

        // Act
        var rootCommand = CommandLineConfigurator.CreateRootCommand(builder);

        // Assert
        var portOption = rootCommand.Options.FirstOrDefault(o => o.Name == "--port");
        Assert.NotNull(portOption);
        Assert.IsType<Option<int?>>(portOption);

        var listenOption = rootCommand.Options.FirstOrDefault(o => o.Name == "--listen");
        Assert.NotNull(listenOption);
        Assert.IsType<Option<string?>>(listenOption);

        var healthOption = rootCommand.Options.FirstOrDefault(o => o.Name == "--health");
        Assert.NotNull(healthOption);
        Assert.IsType<Option<bool?>>(healthOption);
    }

    [Theory]
    [InlineData("--port", typeof(Option<int?>), "Control connection port")]
    [InlineData("--listen", typeof(Option<string?>), "IP address to bind")]
    [InlineData("--max-sessions", typeof(Option<int?>), "Max concurrent sessions")]
    [InlineData("--health", typeof(Option<bool?>), "Enable health endpoint")]
    [InlineData("--auth", typeof(Option<string?>), "Authenticator plugin")]
    [InlineData("--storage", typeof(Option<string?>), "Storage provider plugin")]
    public void CreateRootCommand_SpecificOptionsHaveCorrectConfiguration(string optionName, Type expectedType, string expectedDescription)
    {
        // Arrange
        var builder = WebApplication.CreateBuilder([]);

        // Act
        var rootCommand = CommandLineConfigurator.CreateRootCommand(builder);

        // Assert
        var option = rootCommand.Options.FirstOrDefault(o => o.Name == optionName);
        Assert.NotNull(option);
        Assert.IsType(expectedType, option);
        Assert.Equal(expectedDescription, option.Description);
    }

    [Fact]
    public void ExtractCommandLineArguments_WithMixedCaseBoolean_ShouldUseLowerCase()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder([]);
        var rootCommand = CommandLineConfigurator.CreateRootCommand(builder);
        var testArgs = new[] { "--health" };
        var parseResult = rootCommand.Parse(testArgs);

        // Act
        var configArgs = CommandLineConfigurator.ExtractCommandLineArguments(parseResult);

        // Assert
        Assert.Contains("FtpServer:HealthEnabled=true", configArgs);
        Assert.DoesNotContain("FtpServer:HealthEnabled=True", configArgs);
    }

    [Fact]
    public void ExtractCommandLineArguments_WithZeroValues_ShouldIncludeZeros()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder([]);
        var rootCommand = CommandLineConfigurator.CreateRootCommand(builder);
        var testArgs = new[] { "--port", "0", "--max-sessions", "0" };
        var parseResult = rootCommand.Parse(testArgs);

        // Act
        var configArgs = CommandLineConfigurator.ExtractCommandLineArguments(parseResult);

        // Assert
        Assert.Contains("FtpServer:Port=0", configArgs);
        Assert.Contains("FtpServer:MaxSessions=0", configArgs);
    }

    [Fact]
    public void ExtractCommandLineArguments_WithEmptyStringValue_ShouldIncludeEmptyString()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder([]);
        var rootCommand = CommandLineConfigurator.CreateRootCommand(builder);
        var testArgs = new[] { "--auth", "", "--storage-root", "" };
        var parseResult = rootCommand.Parse(testArgs);

        // Act
        var configArgs = CommandLineConfigurator.ExtractCommandLineArguments(parseResult);

        // Assert
        Assert.Contains("FtpServer:Authenticator=", configArgs);
        Assert.Contains("FtpServer:StorageRoot=", configArgs);
    }

    [Fact]
    public void ExtractCommandLineArguments_WithNegativeNumbers_ShouldHandleCorrectly()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder([]);
        var rootCommand = CommandLineConfigurator.CreateRootCommand(builder);
        var testArgs = new[] { "--port", "-1", "--rate-limit", "-100" };
        var parseResult = rootCommand.Parse(testArgs);

        // Act
        var configArgs = CommandLineConfigurator.ExtractCommandLineArguments(parseResult);

        // Assert
        Assert.Contains("FtpServer:Port=-1", configArgs);
        Assert.Contains("FtpServer:DataRateLimitBytesPerSec=-100", configArgs);
    }

    [Fact]
    public void ExtractCommandLineArguments_WithSpecialCharactersInStrings_ShouldPreserveCharacters()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder([]);
        var rootCommand = CommandLineConfigurator.CreateRootCommand(builder);
        var testArgs = new[]
        {
            "--storage-root", "/path/with spaces/and-dashes",
            "--tls-cert-pass", "p@ssw0rd!#$%",
            "--health-url", "/health?detailed=true&format=json"
        };
        var parseResult = rootCommand.Parse(testArgs);

        // Act
        var configArgs = CommandLineConfigurator.ExtractCommandLineArguments(parseResult);

        // Assert
        Assert.Contains("FtpServer:StorageRoot=/path/with spaces/and-dashes", configArgs);
        Assert.Contains("FtpServer:TlsCertPassword=p@ssw0rd!#$%", configArgs);
        Assert.Contains("FtpServer:HealthUrl=/health?detailed=true&format=json", configArgs);
    }

    [Fact]
    public void ExtractCommandLineArguments_WithLargeNumbers_ShouldHandleCorrectly()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder([]);
        var rootCommand = CommandLineConfigurator.CreateRootCommand(builder);
        var testArgs = new[]
        {
            "--port", "65535",
            "--max-sessions", "10000",
            "--rate-limit", "1073741824" // 1GB
        };
        var parseResult = rootCommand.Parse(testArgs);

        // Act
        var configArgs = CommandLineConfigurator.ExtractCommandLineArguments(parseResult);

        // Assert
        Assert.Contains("FtpServer:Port=65535", configArgs);
        Assert.Contains("FtpServer:MaxSessions=10000", configArgs);
        Assert.Contains("FtpServer:DataRateLimitBytesPerSec=1073741824", configArgs);
    }

    [Fact]
    public void ExtractCommandLineArguments_WithAllOptionsAtOnce_ShouldMapAllCorrectly()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder([]);
        var rootCommand = CommandLineConfigurator.CreateRootCommand(builder);
        var testArgs = new[]
        {
            "--port", "2121",
            "--listen", "0.0.0.0",
            "--max-sessions", "50",
            "--pasv-start", "5000",
            "--pasv-end", "5100",
            "--auth", "Basic",
            "--storage", "FileSystem",
            "--storage-root", "/var/ftp",
            "--health",
            "--health-url", "/health",
            "--data-open-timeout", "30000",
            "--data-transfer-timeout", "300000",
            "--control-read-timeout", "60000",
            "--rate-limit", "1048576",
            "--ftps-explicit",
            "--ftps-implicit",
            "--ftps-implicit-port", "990",
            "--tls-cert", "/certs/server.pfx",
            "--tls-cert-pass", "certpass",
            "--tls-self-signed"
        };
        var parseResult = rootCommand.Parse(testArgs);

        // Act
        var configArgs = CommandLineConfigurator.ExtractCommandLineArguments(parseResult);

        // Assert
        Assert.Equal(20, configArgs.Length); // All 20 options should be mapped
        Assert.Contains("FtpServer:Port=2121", configArgs);
        Assert.Contains("FtpServer:ListenAddress=0.0.0.0", configArgs);
        Assert.Contains("FtpServer:MaxSessions=50", configArgs);
        Assert.Contains("FtpServer:PassivePortRangeStart=5000", configArgs);
        Assert.Contains("FtpServer:PassivePortRangeEnd=5100", configArgs);
        Assert.Contains("FtpServer:Authenticator=Basic", configArgs);
        Assert.Contains("FtpServer:StorageProvider=FileSystem", configArgs);
        Assert.Contains("FtpServer:StorageRoot=/var/ftp", configArgs);
        Assert.Contains("FtpServer:HealthEnabled=true", configArgs);
        Assert.Contains("FtpServer:HealthUrl=/health", configArgs);
        Assert.Contains("FtpServer:DataOpenTimeoutMs=30000", configArgs);
        Assert.Contains("FtpServer:DataTransferTimeoutMs=300000", configArgs);
        Assert.Contains("FtpServer:ControlReadTimeoutMs=60000", configArgs);
        Assert.Contains("FtpServer:DataRateLimitBytesPerSec=1048576", configArgs);
        Assert.Contains("FtpServer:FtpsExplicitEnabled=true", configArgs);
        Assert.Contains("FtpServer:FtpsImplicitEnabled=true", configArgs);
        Assert.Contains("FtpServer:FtpsImplicitPort=990", configArgs);
        Assert.Contains("FtpServer:TlsCertPath=/certs/server.pfx", configArgs);
        Assert.Contains("FtpServer:TlsCertPassword=certpass", configArgs);
        Assert.Contains("FtpServer:TlsSelfSigned=true", configArgs);
    }
}
