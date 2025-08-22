using FtpServer.App.CommandLine;

namespace FtpServer.App.Extensions;

public static class CommandLineExtensions
{
    public static void ApplyCommandLine(this WebApplicationBuilder builder, string[] args)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(args);
        
        var cmd = CommandLineConfigurator.CreateRootCommand(builder);
        var parseResult = cmd.Parse(args);
        
        // Handle parse errors
        if (parseResult.Errors.Count > 0)
        {
            var errorMessages = parseResult.Errors.Select(e => e.Message);
            throw new ArgumentException($"Command line parsing failed: {string.Join("; ", errorMessages)}");
        }
        
        // Extract command line arguments and add them as configuration
        var commandLineArgs = CommandLineConfigurator.ExtractCommandLineArguments(parseResult);
        if (commandLineArgs.Length > 0)
        {
            builder.Configuration.AddCommandLine(commandLineArgs);
        }
    }
}
