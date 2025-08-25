using System.ComponentModel.DataAnnotations;

namespace FtpServer.Storage.AzureBlob;

public sealed class AzureBlobStorageOptions
{
    /// <summary>
    /// Optional connection string. When provided, it takes precedence over AccountUrl/DefaultAzureCredential.
    /// Useful for Azurite/emulator/local testing.
    /// </summary>
    public string? ConnectionString { get; init; }

    /// <summary>
    /// Blob service account URL, e.g., https://myaccount.blob.core.windows.net. Required if ConnectionString is not set.
    /// </summary>
    public string AccountUrl { get; init; } = string.Empty; // e.g., https://myaccount.blob.core.windows.net

    /// <summary>
    /// Target container name. Required for both ConnectionString and AccountUrl configurations.
    /// </summary>
    public string Container { get; init; } = string.Empty;

    public string? Prefix { get; init; }
}
