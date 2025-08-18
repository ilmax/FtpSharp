using System.ComponentModel.DataAnnotations;

namespace FtpServer.Storage.AzureBlob;

public sealed class AzureBlobStorageOptions
{
    /// <summary>
    /// Optional connection string. When provided, it takes precedence over AccountUrl/DefaultAzureCredential.
    /// Useful for Azurite/emulator/local testing.
    /// </summary>
    public string? ConnectionString { get; init; }

    [Required]
    public string AccountUrl { get; init; } = string.Empty; // e.g., https://myaccount.blob.core.windows.net

    [Required]
    public string Container { get; init; } = string.Empty;

    public string? Prefix { get; init; }
}
