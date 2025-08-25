using FtpServer.App.Extensions;
using FtpServer.Core.Abstractions;
using FtpServer.Core.Configuration;
using FtpServer.Core.FileSystem;
using FtpServer.Core.InMemory;
using FtpServer.Core.Server;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FtpServer.Tests;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddFtpServerCore_ShouldRegisterAllRequiredServices()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddFtpServerCore();

        // Assert - Just verify that services are registered, not instantiate them
        var serviceDescriptors = services.ToList();

        Assert.Contains(serviceDescriptors, s => s.ServiceType == typeof(InMemoryAuthenticator));
        Assert.Contains(serviceDescriptors, s => s.ServiceType == typeof(Core.Basic.BasicAuthenticator));
        Assert.Contains(serviceDescriptors, s => s.ServiceType == typeof(InMemoryStorageProvider));
        Assert.Contains(serviceDescriptors, s => s.ServiceType == typeof(FileSystemStorageProvider));
        Assert.Contains(serviceDescriptors, s => s.ServiceType == typeof(IAuthenticatorFactory));
        Assert.Contains(serviceDescriptors, s => s.ServiceType == typeof(IStorageProviderFactory));
        Assert.Contains(serviceDescriptors, s => s.ServiceType == typeof(FtpServerHost));
        Assert.Contains(serviceDescriptors, s => s.ServiceType == typeof(PassivePortPool));
        Assert.Contains(serviceDescriptors, s => s.ServiceType == typeof(TlsCertificateProvider));
    }

    [Fact]
    public void AddFtpServerCore_ShouldRegisterAllServicesAsSingleton()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddFtpServerCore();

        // Assert
        var serviceDescriptors = services.ToList();

        var ftpServerServices = serviceDescriptors.Where(s =>
            s.ServiceType == typeof(InMemoryAuthenticator) ||
            s.ServiceType == typeof(Core.Basic.BasicAuthenticator) ||
            s.ServiceType == typeof(InMemoryStorageProvider) ||
            s.ServiceType == typeof(FileSystemStorageProvider) ||
            s.ServiceType == typeof(IAuthenticatorFactory) ||
            s.ServiceType == typeof(IStorageProviderFactory) ||
            s.ServiceType == typeof(FtpServerHost) ||
            s.ServiceType == typeof(PassivePortPool) ||
            s.ServiceType == typeof(TlsCertificateProvider)
        );

        Assert.All(ftpServerServices, descriptor =>
            Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime));
    }

    [Fact]
    public void AddFtpServerObservability_ShouldRegisterTelemetryServices()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddFtpServerObservability();

        // Assert - The method should complete without throwing and add some services
        var serviceDescriptors = services.ToList();
        Assert.NotEmpty(serviceDescriptors);

        // OpenTelemetry services are registered internally
        // We just verify that the method runs successfully and adds services
    }

    [Fact]
    public void AddFtpServerCore_WithExistingServices_ShouldNotConflict()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<string>("test-service");

        // Act
        services.AddFtpServerCore();

        // Assert
        var serviceDescriptors = services.ToList();
        Assert.Contains(serviceDescriptors, s => s.ServiceType == typeof(string));
        Assert.Contains(serviceDescriptors, s => s.ServiceType == typeof(FtpServerHost));
    }

    [Fact]
    public void AddFtpServerObservability_WithExistingServices_ShouldNotConflict()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<string>("test-service");

        // Act
        services.AddFtpServerObservability();

        // Assert
        var serviceDescriptors = services.ToList();
        Assert.Contains(serviceDescriptors, s => s.ServiceType == typeof(string));
    }

    [Fact]
    public void AddFtpServerCore_ShouldReturnServiceCollection()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var result = services.AddFtpServerCore();

        // Assert
        Assert.Same(services, result);
    }

    [Fact]
    public void AddFtpServerObservability_ShouldReturnServiceCollection()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var result = services.AddFtpServerObservability();

        // Assert
        Assert.Same(services, result);
    }

    [Fact]
    public void AddFtpServerCore_CalledMultipleTimes_ShouldAddServicesMultipleTimes()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddFtpServerCore();
        services.AddFtpServerCore();

        // Assert
        var serviceDescriptors = services.ToList();
        var ftpServerHostServices = serviceDescriptors.Where(s => s.ServiceType == typeof(FtpServerHost));
        Assert.Equal(2, ftpServerHostServices.Count());
    }
}
