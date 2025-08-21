using System.CommandLine;
using Microsoft.Extensions.Configuration;

namespace FtpServer.App.Extensions;

public static class ConfigurationExtensions
{
    public static RootCommand AddFtpCliOptions(this RootCommand cmd,
        Option<int?> portOption,
        Option<string> addressOption,
        Option<int?> maxSessionsOption,
        Option<int?> passiveStartOption,
        Option<int?> passiveEndOption,
        Option<string> authOption,
        Option<string> storageOption,
        Option<string> storageRootOption,
        Option<bool?> healthEnabled,
        Option<string> healthUrl,
        Option<int?> dataOpenTimeout,
        Option<int?> dataTransferTimeout,
        Option<int?> controlReadTimeout,
        Option<int?> dataRateLimit,
        Option<bool?> ftpsExplicit,
        Option<bool?> ftpsImplicit,
        Option<int?> ftpsImplicitPort,
        Option<string> tlsCertPath,
        Option<string> tlsCertPassword,
        Option<bool?> tlsSelfSigned,
        IConfiguration configuration)
    {
        // Temporarily simplified - the command line processing is disabled
        // TODO: Implement proper System.CommandLine integration with the new API
        // For now, configuration will come from appsettings and environment variables
        return cmd;
    }
}
