using FtpServer.App.Extensions;
using FtpServer.Core.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FtpServer.Tests;

/// <summary>
/// Integration tests for command-line functionality to ensure it works end-to-end
/// </summary>
public class CommandLineIntegrationTests
{
    [Fact]
    public void CommandLineIntegration_FullFlow_ShouldConfigureFtpServerOptions()
    {
        // Arrange
        var args = new[]
        {
            "--port", "2121",
            "--listen", "192.168.1.1",
            "--max-sessions", "50",
            "--pasv-start", "5000",
            "--pasv-end", "5100",
            "--auth", "Basic",
            "--storage", "FileSystem",
            "--storage-root", "/var/ftp",
            "--health",
            "--health-url", "/status",
            "--data-open-timeout", "30000",
            "--data-transfer-timeout", "300000",
            "--control-read-timeout", "60000",
            "--rate-limit", "1048576",
            "--ftps-explicit",
            "--ftps-implicit",
            "--ftps-implicit-port", "990",
            "--tls-cert", "/certs/server.pfx",
            "--tls-cert-pass", "secretpass",
            "--tls-self-signed"
        };

        var builder = WebApplication.CreateBuilder([]);
        
        // Act
        builder.ApplyCommandLine(args);
        
        // Configure FtpServerOptions binding as in Program.cs
        builder.Services.AddOptions<FtpServerOptions>()
            .Bind(builder.Configuration.GetSection("FtpServer"))
            .ValidateDataAnnotations();

        var app = builder.Build();
        var options = app.Services.GetRequiredService<IOptions<FtpServerOptions>>().Value;

        // Assert - Verify all command-line options are properly mapped to FtpServerOptions
        Assert.Equal(2121, options.Port);
        Assert.Equal("192.168.1.1", options.ListenAddress);
        Assert.Equal(50, options.MaxSessions);
        Assert.Equal(5000, options.PassivePortRangeStart);
        Assert.Equal(5100, options.PassivePortRangeEnd);
        Assert.Equal("Basic", options.Authenticator);
        Assert.Equal("FileSystem", options.StorageProvider);
        Assert.Equal("/var/ftp", options.StorageRoot);
        Assert.True(options.HealthEnabled);
        Assert.Equal("/status", options.HealthUrl);
        Assert.Equal(30000, options.DataOpenTimeoutMs);
        Assert.Equal(300000, options.DataTransferTimeoutMs);
        Assert.Equal(60000, options.ControlReadTimeoutMs);
        Assert.Equal(1048576, options.DataRateLimitBytesPerSec);
        Assert.True(options.FtpsExplicitEnabled);
        Assert.True(options.FtpsImplicitEnabled);
        Assert.Equal(990, options.FtpsImplicitPort);
        Assert.Equal("/certs/server.pfx", options.TlsCertPath);
        Assert.Equal("secretpass", options.TlsCertPassword);
        Assert.True(options.TlsSelfSigned);
    }

    [Fact]
    public void CommandLineIntegration_WithEnvironmentVariables_ShouldRespectPrecedence()
    {
        // Arrange - Set environment variables first
        Environment.SetEnvironmentVariable("FTP_FtpServer__Port", "8021");
        Environment.SetEnvironmentVariable("FTP_FtpServer__ListenAddress", "10.0.0.1");
        
        var args = new[] 
        {
            "--port", "2121",  // Should override environment variable
            // No --listen argument, so environment variable should be used
            "--health"
        };

        var builder = WebApplication.CreateBuilder([]);
        
        // Act
        builder.Configuration.AddEnvironmentVariables("FTP_");
        builder.ApplyCommandLine(args);
        
        builder.Services.AddOptions<FtpServerOptions>()
            .Bind(builder.Configuration.GetSection("FtpServer"))
            .ValidateDataAnnotations();

        var app = builder.Build();
        var options = app.Services.GetRequiredService<IOptions<FtpServerOptions>>().Value;

        // Assert
        Assert.Equal(2121, options.Port); // Command line overrides environment
        Assert.Equal("10.0.0.1", options.ListenAddress); // Environment variable used
        Assert.True(options.HealthEnabled); // Command line
        
        // Cleanup
        Environment.SetEnvironmentVariable("FTP_FtpServer__Port", null);
        Environment.SetEnvironmentVariable("FTP_FtpServer__ListenAddress", null);
    }

    [Fact]
    public void CommandLineIntegration_WithDefaultValues_ShouldUseDefaultsWhenNotSpecified()
    {
        // Arrange
        var args = new[] { "--health" }; // Only specify one option

        var builder = WebApplication.CreateBuilder([]);
        
        // Act
        builder.ApplyCommandLine(args);
        
        builder.Services.AddOptions<FtpServerOptions>()
            .Bind(builder.Configuration.GetSection("FtpServer"))
            .ValidateDataAnnotations();

        var app = builder.Build();
        var options = app.Services.GetRequiredService<IOptions<FtpServerOptions>>().Value;

        // Assert - Verify defaults are used when not overridden
        Assert.Equal(21, options.Port); // Default port
        Assert.Equal("0.0.0.0", options.ListenAddress); // Default listen address
        Assert.True(options.HealthEnabled); // Explicitly set via command line
        Assert.False(options.FtpsExplicitEnabled); // Default
        Assert.False(options.FtpsImplicitEnabled); // Default
    }

    [Fact]
    public void CommandLineIntegration_WithMixedConfiguration_ShouldMergeCorrectly()
    {
        // Arrange
        var args = new[] 
        {
            "--port", "2121",
            "--health"
        };

        var builder = WebApplication.CreateBuilder([]);
        
        // Add base configuration
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["FtpServer:ListenAddress"] = "0.0.0.0",
            ["FtpServer:MaxSessions"] = "100",
            ["FtpServer:StorageProvider"] = "InMemory"
        });
        
        // Act
        builder.ApplyCommandLine(args);
        
        builder.Services.AddOptions<FtpServerOptions>()
            .Bind(builder.Configuration.GetSection("FtpServer"))
            .ValidateDataAnnotations();

        var app = builder.Build();
        var options = app.Services.GetRequiredService<IOptions<FtpServerOptions>>().Value;

        // Assert
        Assert.Equal(2121, options.Port); // From command line
        Assert.Equal("0.0.0.0", options.ListenAddress); // From base config
        Assert.Equal(100, options.MaxSessions); // From base config
        Assert.Equal("InMemory", options.StorageProvider); // From base config
        Assert.True(options.HealthEnabled); // From command line
    }

    [Fact]
    public void CommandLineIntegration_WithValidation_ShouldPassValidation()
    {
        // Arrange
        var args = new[]
        {
            "--port", "2121",
            "--max-sessions", "50",
            "--pasv-start", "5000",
            "--pasv-end", "5100"
        };

        var builder = WebApplication.CreateBuilder([]);
        
        // Act
        builder.ApplyCommandLine(args);
        
        builder.Services.AddOptions<FtpServerOptions>()
            .Bind(builder.Configuration.GetSection("FtpServer"))
            .ValidateDataAnnotations();

        // Assert - Should not throw during validation
        var app = builder.Build();
        var options = app.Services.GetRequiredService<IOptions<FtpServerOptions>>().Value;
        
        Assert.Equal(2121, options.Port);
        Assert.Equal(50, options.MaxSessions);
        Assert.Equal(5000, options.PassivePortRangeStart);
        Assert.Equal(5100, options.PassivePortRangeEnd);
    }
}