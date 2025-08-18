using System.ComponentModel.DataAnnotations;

namespace FtpServer.Storage.AzureBlob;

public sealed class AzureBlobStorageOptions
{
    [Required]
    public string AccountUrl { get; init; } = string.Empty; // e.g., https://myaccount.blob.core.windows.net

    [Required]
    public string Container { get; init; } = string.Empty;

    public string? Prefix { get; init; }
}
