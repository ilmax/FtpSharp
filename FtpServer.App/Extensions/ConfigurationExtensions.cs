using System.CommandLine;
using Microsoft.Extensions.Configuration;

namespace FtpServer.App.Extensions;

public static class ConfigurationExtensions
{
    // This class is no longer needed with the new System.CommandLine API pattern
    // Configuration is now handled directly in CommandLineConfigurator.ApplyParsedOptions
}
