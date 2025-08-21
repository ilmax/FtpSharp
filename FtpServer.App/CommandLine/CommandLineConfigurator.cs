using System.CommandLine;
using Microsoft.Extensions.Configuration;

namespace FtpServer.App.CommandLine;

public static class CommandLineConfigurator
{
    // Store options in a record for easy passing around
    public record CommandLineOptions(
        Option<int?> Port,
        Option<string?> Address,
        Option<int?> MaxSessions,
        Option<int?> PassiveStart,
        Option<int?> PassiveEnd,
        Option<string?> Auth,
        Option<string?> Storage,
        Option<string?> StorageRoot,
        Option<bool?> HealthEnabled,
        Option<string?> HealthUrl,
        Option<int?> DataOpenTimeout,
        Option<int?> DataTransferTimeout,
        Option<int?> ControlReadTimeout,
        Option<int?> DataRateLimit,
        Option<bool?> FtpsExplicit,
        Option<bool?> FtpsImplicit,
        Option<int?> FtpsImplicitPort,
        Option<string?> TlsCertPath,
        Option<string?> TlsCertPassword,
        Option<bool?> TlsSelfSigned
    );

    public static RootCommand CreateRootCommand(WebApplicationBuilder builder)
    {
        var portOption = new Option<int?>(name: "--port") { Description = "Control connection port" };
        var addressOption = new Option<string?>(name: "--listen") { Description = "IP address to bind" };
        var maxSessionsOption = new Option<int?>(name: "--max-sessions") { Description = "Max concurrent sessions" };
        var passiveStartOption = new Option<int?>(name: "--pasv-start") { Description = "Passive range start" };
        var passiveEndOption = new Option<int?>(name: "--pasv-end") { Description = "Passive range end" };
        var authOption = new Option<string?>(name: "--auth") { Description = "Authenticator plugin" };
        var storageOption = new Option<string?>(name: "--storage") { Description = "Storage provider plugin" };
        var storageRootOption = new Option<string?>(name: "--storage-root") { Description = "Storage root path" };
        var healthEnabled = new Option<bool?>(name: "--health") { Description = "Enable health endpoint" };
        var healthUrl = new Option<string?>(name: "--health-url") { Description = "Health URL prefix" };
        var dataOpenTimeout = new Option<int?>(name: "--data-open-timeout") { Description = "Data open timeout (ms)" };
        var dataTransferTimeout = new Option<int?>(name: "--data-transfer-timeout") { Description = "Data transfer timeout (ms)" };
        var controlReadTimeout = new Option<int?>(name: "--control-read-timeout") { Description = "Control read timeout (ms)" };
        var dataRateLimit = new Option<int?>(name: "--rate-limit") { Description = "Per-transfer data rate limit (bytes/sec)" };
        var ftpsExplicit = new Option<bool?>(name: "--ftps-explicit") { Description = "Enable explicit FTPS (AUTH TLS)" };
        var ftpsImplicit = new Option<bool?>(name: "--ftps-implicit") { Description = "Enable implicit FTPS" };
        var ftpsImplicitPort = new Option<int?>(name: "--ftps-implicit-port") { Description = "Port for implicit FTPS" };
        var tlsCertPath = new Option<string?>(name: "--tls-cert") { Description = "Path to PFX certificate" };
        var tlsCertPassword = new Option<string?>(name: "--tls-cert-pass") { Description = "Password for PFX certificate" };
        var tlsSelfSigned = new Option<bool?>(name: "--tls-self-signed") { Description = "Generate self-signed cert if none provided" };

        var cmd = new RootCommand("FTP Server host with ASP.NET Core health")
        {
            portOption, addressOption, maxSessionsOption, passiveStartOption, passiveEndOption,
            authOption, storageOption, storageRootOption,
            healthEnabled, healthUrl,
            dataOpenTimeout, dataTransferTimeout, controlReadTimeout, dataRateLimit,
            ftpsExplicit, ftpsImplicit, ftpsImplicitPort,
            tlsCertPath, tlsCertPassword, tlsSelfSigned
        };

        return cmd;
    }

    public static string[] ExtractCommandLineArguments(System.CommandLine.ParseResult parseResult)
    {
        var args = new List<string>();
        
        var cmd = parseResult.CommandResult.Command as RootCommand;
        if (cmd == null) return args.ToArray();

        // Convert parsed options back to command line format for ASP.NET Core configuration
        foreach (var option in cmd.Options)
        {
            var value = parseResult.GetValue(option);
            if (value == null) continue;

            string? configKey = null;
            switch (option.Name)
            {
                case "--port":
                    configKey = "FtpServer:Port";
                    break;
                case "--listen":
                    configKey = "FtpServer:ListenAddress";
                    break;
                case "--max-sessions":
                    configKey = "FtpServer:MaxSessions";
                    break;
                case "--pasv-start":
                    configKey = "FtpServer:PassivePortRangeStart";
                    break;
                case "--pasv-end":
                    configKey = "FtpServer:PassivePortRangeEnd";
                    break;
                case "--auth":
                    configKey = "FtpServer:Authenticator";
                    break;
                case "--storage":
                    configKey = "FtpServer:StorageProvider";
                    break;
                case "--storage-root":
                    configKey = "FtpServer:StorageRoot";
                    break;
                case "--health":
                    configKey = "FtpServer:HealthEnabled";
                    break;
                case "--health-url":
                    configKey = "FtpServer:HealthUrl";
                    break;
                case "--data-open-timeout":
                    configKey = "FtpServer:DataOpenTimeoutMs";
                    break;
                case "--data-transfer-timeout":
                    configKey = "FtpServer:DataTransferTimeoutMs";
                    break;
                case "--control-read-timeout":
                    configKey = "FtpServer:ControlReadTimeoutMs";
                    break;
                case "--rate-limit":
                    configKey = "FtpServer:DataRateLimitBytesPerSec";
                    break;
                case "--ftps-explicit":
                    configKey = "FtpServer:FtpsExplicitEnabled";
                    break;
                case "--ftps-implicit":
                    configKey = "FtpServer:FtpsImplicitEnabled";
                    break;
                case "--ftps-implicit-port":
                    configKey = "FtpServer:FtpsImplicitPort";
                    break;
                case "--tls-cert":
                    configKey = "FtpServer:TlsCertPath";
                    break;
                case "--tls-cert-pass":
                    configKey = "FtpServer:TlsCertPassword";
                    break;
                case "--tls-self-signed":
                    configKey = "FtpServer:TlsSelfSigned";
                    break;
            }
            
            if (configKey != null)
            {
                var stringValue = value is bool boolVal ? boolVal.ToString().ToLowerInvariant() : value.ToString();
                args.Add($"{configKey}={stringValue}");
            }
        }
        
        return args.ToArray();
    }
}
