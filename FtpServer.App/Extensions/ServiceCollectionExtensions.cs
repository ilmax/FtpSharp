using FtpServer.Core.Abstractions;
using FtpServer.Core.FileSystem;
using FtpServer.Core.InMemory;
using FtpServer.Core.Server;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

namespace FtpServer.App.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFtpServerCore(this IServiceCollection services)
    {
        services.AddSingleton<InMemoryAuthenticator>();
        services.AddSingleton<Core.Basic.BasicAuthenticator>();
        services.AddSingleton<InMemoryStorageProvider>();
        services.AddSingleton<FileSystemStorageProvider>();
        services.AddSingleton<IAuthenticatorFactory, Core.Plugins.PluginRegistry>();
        services.AddSingleton<IStorageProviderFactory, Core.Plugins.PluginRegistry>();
        services.AddSingleton<FtpServerHost>();
        services.AddSingleton<PassivePortPool>();
        services.AddSingleton<TlsCertificateProvider>();
        return services;
    }

    public static IServiceCollection AddFtpServerObservability(this IServiceCollection services)
    {
        services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(serviceName: "FtpServer", serviceVersion: "1.0.0"))
            .WithMetrics(metrics =>
            {
                metrics
                    .AddMeter(Core.Observability.Metrics.MeterName)
                    .AddAspNetCoreInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddPrometheusExporter();
            });
        return services;
    }
}
