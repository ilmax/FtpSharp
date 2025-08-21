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
        
        // Helper method to add an argument if it has a value
        void AddIfHasValue<T>(Option<T> option, string configKey, T? value)
        {
            if (value != null)
            {
                var stringValue = value is bool boolVal ? boolVal.ToString().ToLowerInvariant() : value.ToString();
                args.Add($"{configKey}={stringValue}");
            }
        }

        // Get all the options from the root command
        var cmd = parseResult.CommandResult.Command as RootCommand;
        if (cmd == null) return args.ToArray();

        // Extract values for each option
        foreach (var option in cmd.Options)
        {
            switch (option.Name)
            {
                case "--port":
                    AddIfHasValue((Option<int?>)option, "FtpServer:Port", parseResult.GetValue((Option<int?>)option));
                    break;
                case "--listen":
                    AddIfHasValue((Option<string?>)option, "FtpServer:ListenAddress", parseResult.GetValue((Option<string?>)option));
                    break;
                case "--max-sessions":
                    AddIfHasValue((Option<int?>)option, "FtpServer:MaxSessions", parseResult.GetValue((Option<int?>)option));
                    break;
                case "--pasv-start":
                    AddIfHasValue((Option<int?>)option, "FtpServer:PassivePortRangeStart", parseResult.GetValue((Option<int?>)option));
                    break;
                case "--pasv-end":
                    AddIfHasValue((Option<int?>)option, "FtpServer:PassivePortRangeEnd", parseResult.GetValue((Option<int?>)option));
                    break;
                case "--auth":
                    AddIfHasValue((Option<string?>)option, "FtpServer:Authenticator", parseResult.GetValue((Option<string?>)option));
                    break;
                case "--storage":
                    AddIfHasValue((Option<string?>)option, "FtpServer:StorageProvider", parseResult.GetValue((Option<string?>)option));
                    break;
                case "--storage-root":
                    AddIfHasValue((Option<string?>)option, "FtpServer:StorageRoot", parseResult.GetValue((Option<string?>)option));
                    break;
                case "--health":
                    AddIfHasValue((Option<bool?>)option, "FtpServer:HealthEnabled", parseResult.GetValue((Option<bool?>)option));
                    break;
                case "--health-url":
                    AddIfHasValue((Option<string?>)option, "FtpServer:HealthUrl", parseResult.GetValue((Option<string?>)option));
                    break;
                case "--data-open-timeout":
                    AddIfHasValue((Option<int?>)option, "FtpServer:DataOpenTimeoutMs", parseResult.GetValue((Option<int?>)option));
                    break;
                case "--data-transfer-timeout":
                    AddIfHasValue((Option<int?>)option, "FtpServer:DataTransferTimeoutMs", parseResult.GetValue((Option<int?>)option));
                    break;
                case "--control-read-timeout":
                    AddIfHasValue((Option<int?>)option, "FtpServer:ControlReadTimeoutMs", parseResult.GetValue((Option<int?>)option));
                    break;
                case "--rate-limit":
                    AddIfHasValue((Option<int?>)option, "FtpServer:DataRateLimitBytesPerSec", parseResult.GetValue((Option<int?>)option));
                    break;
                case "--ftps-explicit":
                    AddIfHasValue((Option<bool?>)option, "FtpServer:FtpsExplicitEnabled", parseResult.GetValue((Option<bool?>)option));
                    break;
                case "--ftps-implicit":
                    AddIfHasValue((Option<bool?>)option, "FtpServer:FtpsImplicitEnabled", parseResult.GetValue((Option<bool?>)option));
                    break;
                case "--ftps-implicit-port":
                    AddIfHasValue((Option<int?>)option, "FtpServer:FtpsImplicitPort", parseResult.GetValue((Option<int?>)option));
                    break;
                case "--tls-cert":
                    AddIfHasValue((Option<string?>)option, "FtpServer:TlsCertPath", parseResult.GetValue((Option<string?>)option));
                    break;
                case "--tls-cert-pass":
                    AddIfHasValue((Option<string?>)option, "FtpServer:TlsCertPassword", parseResult.GetValue((Option<string?>)option));
                    break;
                case "--tls-self-signed":
                    AddIfHasValue((Option<bool?>)option, "FtpServer:TlsSelfSigned", parseResult.GetValue((Option<bool?>)option));
                    break;
            }
        }
        
        return args.ToArray();
    }
}
