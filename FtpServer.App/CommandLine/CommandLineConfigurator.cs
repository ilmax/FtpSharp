using System.CommandLine;
using Microsoft.Extensions.Configuration;

namespace FtpServer.App.CommandLine;

public static class CommandLineConfigurator
{
    // Option definition with metadata for configuration mapping
    private record OptionDefinition(Option Option, string ConfigKey);

    // Centralized option definitions with their configuration mappings
    private static readonly OptionDefinition[] OptionDefinitions =
    [
        new(new Option<int?>(name: "--port") { Description = "Control connection port" }, "FtpServer:Port"),
        new(new Option<string?>(name: "--listen") { Description = "IP address to bind" }, "FtpServer:ListenAddress"),
        new(new Option<int?>(name: "--max-sessions") { Description = "Max concurrent sessions" }, "FtpServer:MaxSessions"),
        new(new Option<int?>(name: "--pasv-start") { Description = "Passive range start" }, "FtpServer:PassivePortRangeStart"),
        new(new Option<int?>(name: "--pasv-end") { Description = "Passive range end" }, "FtpServer:PassivePortRangeEnd"),
        new(new Option<string?>(name: "--auth") { Description = "Authenticator plugin" }, "FtpServer:Authenticator"),
        new(new Option<string?>(name: "--storage") { Description = "Storage provider plugin" }, "FtpServer:StorageProvider"),
        new(new Option<string?>(name: "--storage-root") { Description = "Storage root path" }, "FtpServer:StorageRoot"),
        new(new Option<bool?>(name: "--health") { Description = "Enable health endpoint" }, "FtpServer:HealthEnabled"),
        new(new Option<string?>(name: "--health-url") { Description = "Health URL prefix" }, "FtpServer:HealthUrl"),
        new(new Option<int?>(name: "--data-open-timeout") { Description = "Data open timeout (ms)" }, "FtpServer:DataOpenTimeoutMs"),
        new(new Option<int?>(name: "--data-transfer-timeout") { Description = "Data transfer timeout (ms)" }, "FtpServer:DataTransferTimeoutMs"),
        new(new Option<int?>(name: "--control-read-timeout") { Description = "Control read timeout (ms)" }, "FtpServer:ControlReadTimeoutMs"),
        new(new Option<int?>(name: "--rate-limit") { Description = "Per-transfer data rate limit (bytes/sec)" }, "FtpServer:DataRateLimitBytesPerSec"),
        new(new Option<bool?>(name: "--ftps-explicit") { Description = "Enable explicit FTPS (AUTH TLS)" }, "FtpServer:FtpsExplicitEnabled"),
        new(new Option<bool?>(name: "--ftps-implicit") { Description = "Enable implicit FTPS" }, "FtpServer:FtpsImplicitEnabled"),
        new(new Option<int?>(name: "--ftps-implicit-port") { Description = "Port for implicit FTPS" }, "FtpServer:FtpsImplicitPort"),
        new(new Option<string?>(name: "--tls-cert") { Description = "Path to PFX certificate" }, "FtpServer:TlsCertPath"),
        new(new Option<string?>(name: "--tls-cert-pass") { Description = "Password for PFX certificate" }, "FtpServer:TlsCertPassword"),
        new(new Option<bool?>(name: "--tls-self-signed") { Description = "Generate self-signed cert if none provided" }, "FtpServer:TlsSelfSigned")
    ];

    public static RootCommand CreateRootCommand(WebApplicationBuilder builder)
    {
        var cmd = new RootCommand("FTP Server host with ASP.NET Core health");
        
        // Add all options from the centralized definitions
        foreach (var optionDef in OptionDefinitions)
        {
            cmd.Add(optionDef.Option);
        }

        return cmd;
    }

    public static string[] ExtractCommandLineArguments(System.CommandLine.ParseResult parseResult)
    {
        var args = new List<string>();
        
        // Helper method to add an argument if it has a value
        void AddIfHasValue(object? value, string configKey)
        {
            if (value != null)
            {
                var stringValue = value is bool boolVal ? boolVal.ToString().ToLowerInvariant() : value.ToString();
                args.Add($"{configKey}={stringValue}");
            }
        }

        // Extract values for each option by casting to known types
        foreach (var optionDef in OptionDefinitions)
        {
            object? value = null;
            
            try
            {
                // Cast the option to its specific type and call GetValue
                switch (optionDef.Option)
                {
                    case Option<int?> intOption:
                        value = parseResult.GetValue(intOption);
                        break;
                    case Option<string?> stringOption:
                        value = parseResult.GetValue(stringOption);
                        break;
                    case Option<bool?> boolOption:
                        value = parseResult.GetValue(boolOption);
                        break;
                }
            }
            catch (InvalidOperationException)
            {
                // If value conversion fails, skip this option
                continue;
            }
            
            AddIfHasValue(value, optionDef.ConfigKey);
        }
        
        return args.ToArray();
    }
}
