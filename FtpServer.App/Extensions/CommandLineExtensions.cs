using FtpServer.App.CommandLine;

namespace FtpServer.App.Extensions;

public static class CommandLineExtensions
{
    public static void ApplyCommandLine(this WebApplicationBuilder builder, string[] args)
    {
        var cmd = CommandLineConfigurator.CreateRootCommand(builder);
        var parseResult = cmd.Parse(args);
        
        // Handle parse errors
        if (parseResult.Errors.Count > 0)
        {
            foreach (var error in parseResult.Errors)
            {
                Console.Error.WriteLine($"Command line error: {error}");
            }
            Environment.Exit(1);
        }
        
        // Extract command line arguments and add them as configuration
        var commandLineArgs = CommandLineConfigurator.ExtractCommandLineArguments(parseResult);
        if (commandLineArgs.Length > 0)
        {
            builder.Configuration.AddCommandLine(commandLineArgs);
        }
    }
}
