using System.CommandLine;
using FtpServer.App.CommandLine;

namespace FtpServer.App.Extensions;

public static class CommandLineExtensions
{
    public static Task ApplyCommandLineAsync(this WebApplicationBuilder builder, string[] args)
    {
        RootCommand cmd = CommandLineConfigurator.CreateRootCommand(builder);
        return cmd.InvokeAsync(args);
    }
}
