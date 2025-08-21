using FtpServer.App.CommandLine;

namespace FtpServer.App.Extensions;

public static class CommandLineExtensions
{
    public static Task ApplyCommandLineAsync(this WebApplicationBuilder builder, string[] args)
    {
        // Temporarily simplified - just return completed task
        // The command line argument processing will be handled differently
        // TODO: Implement proper System.CommandLine integration once API is clarified
        return Task.CompletedTask;
    }
}
