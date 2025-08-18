using System.CommandLine;
using System.CommandLine.Invocation;
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
        cmd.SetHandler((InvocationContext ctx) =>
        {
            var pr = ctx.ParseResult;
            void Set(string key, string? value) { if (value is not null) configuration[key] = value; }

            Set("FtpServer:Port", pr.GetValueForOption(portOption)?.ToString());
            Set("FtpServer:ListenAddress", pr.GetValueForOption(addressOption));
            Set("FtpServer:MaxSessions", pr.GetValueForOption(maxSessionsOption)?.ToString());
            Set("FtpServer:PassivePortRangeStart", pr.GetValueForOption(passiveStartOption)?.ToString());
            Set("FtpServer:PassivePortRangeEnd", pr.GetValueForOption(passiveEndOption)?.ToString());
            Set("FtpServer:Authenticator", pr.GetValueForOption(authOption));
            Set("FtpServer:StorageProvider", pr.GetValueForOption(storageOption));
            Set("FtpServer:StorageRoot", pr.GetValueForOption(storageRootOption));
            var h = pr.GetValueForOption(healthEnabled); if (h is not null) Set("FtpServer:HealthEnabled", h.Value ? "true" : "false");
            Set("FtpServer:HealthUrl", pr.GetValueForOption(healthUrl));
            Set("FtpServer:DataOpenTimeoutMs", pr.GetValueForOption(dataOpenTimeout)?.ToString());
            Set("FtpServer:DataTransferTimeoutMs", pr.GetValueForOption(dataTransferTimeout)?.ToString());
            Set("FtpServer:ControlReadTimeoutMs", pr.GetValueForOption(controlReadTimeout)?.ToString());
            Set("FtpServer:DataRateLimitBytesPerSec", pr.GetValueForOption(dataRateLimit)?.ToString());
            var exp = pr.GetValueForOption(ftpsExplicit); if (exp is not null) Set("FtpServer:FtpsExplicitEnabled", exp.Value ? "true" : "false");
            var imp = pr.GetValueForOption(ftpsImplicit); if (imp is not null) Set("FtpServer:FtpsImplicitEnabled", imp.Value ? "true" : "false");
            Set("FtpServer:FtpsImplicitPort", pr.GetValueForOption(ftpsImplicitPort)?.ToString());
            Set("FtpServer:TlsCertPath", pr.GetValueForOption(tlsCertPath));
            Set("FtpServer:TlsCertPassword", pr.GetValueForOption(tlsCertPassword));
            var ss = pr.GetValueForOption(tlsSelfSigned); if (ss is not null) Set("FtpServer:TlsSelfSigned", ss.Value ? "true" : "false");
        });
        return cmd;
    }
}
