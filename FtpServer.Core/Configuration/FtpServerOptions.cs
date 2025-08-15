using System.ComponentModel.DataAnnotations;

namespace FtpServer.Core.Configuration;

/// <summary>
/// Options controlling the FTP server runtime behavior.
/// Values can be provided via appsettings, environment variables, or command-line switches.
/// </summary>
public sealed class FtpServerOptions
{
    /// <summary>Control connection listen IP (default: 0.0.0.0).</summary>
    [Required]
    public string ListenAddress { get; init; } = "0.0.0.0";

    /// <summary>Control connection listen port (default: 21).</summary>
    [Range(1, 65535)]
    public int Port { get; init; } = 21;

    /// <summary>Max concurrent active sessions (default: 100).</summary>
    [Range(1, 10_000)]
    public int MaxSessions { get; init; } = 100;

    /// <summary>Passive mode data port range start (default: 50000).</summary>
    [Range(1, 65535)]
    public int PassivePortRangeStart { get; init; } = 50_000;

    /// <summary>Passive mode data port range end (default: 50100).</summary>
    [Range(1, 65535)]
    public int PassivePortRangeEnd { get; init; } = 50_100;

    /// <summary>Root path or container prefix for the storage provider.</summary>
    [Required]
    public string StorageRoot { get; init; } = "/data";

    /// <summary>Selected storage provider plugin name (e.g., "InMemory", "Local", "AzureBlob").</summary>
    [Required]
    public string StorageProvider { get; init; } = "InMemory";

    /// <summary>Selected authenticator plugin name (e.g., "InMemory", "Basic").</summary>
    [Required]
    public string Authenticator { get; init; } = "InMemory";

    /// <summary>Timeout in milliseconds for opening a data connection (PASV accept or active connect). Default: 5000.</summary>
    [Range(100, 600_000)]
    public int DataOpenTimeoutMs { get; init; } = 5_000;

    /// <summary>Timeout in milliseconds for a data transfer operation (read/write). Default: 30000.</summary>
    [Range(100, 3_600_000)]
    public int DataTransferTimeoutMs { get; init; } = 30_000;

    /// <summary>Timeout in milliseconds for reading control commands (idle). 0 disables timeout. Default: 0.</summary>
    [Range(0, 3_600_000)]
    public int ControlReadTimeoutMs { get; init; } = 0;
    /// <summary>Optional per-transfer data rate limit in bytes per second. 0 disables throttling.</summary>
    public int DataRateLimitBytesPerSec { get; set; } = 0;
}
