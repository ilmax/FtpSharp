using System.Buffers;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FtpServer.Core.Abstractions;

/// <summary>
/// Storage provider abstraction used by the FTP server to handle file system operations.
/// Implementations might target local disk, cloud blob storage, or in-memory store.
/// </summary>
public interface IStorageProvider
{
    Task<bool> ExistsAsync(string path, CancellationToken ct);
    Task<IReadOnlyList<FileSystemEntry>> ListAsync(string path, CancellationToken ct);
    Task CreateDirectoryAsync(string path, CancellationToken ct);
    Task DeleteAsync(string path, bool recursive, CancellationToken ct);
    Task<long> GetSizeAsync(string path, CancellationToken ct);
    /// <summary>Get a single entry for a path, or null if missing.</summary>
    Task<FileSystemEntry?> GetEntryAsync(string path, CancellationToken ct);
    /// <summary>Rename a file from one path to another. Implementations may throw NotSupportedException for directories.</summary>
    Task RenameAsync(string fromPath, string toPath, CancellationToken ct);

    /// <summary>Read a file as a sequence of buffers to avoid large copies.</summary>
    IAsyncEnumerable<ReadOnlyMemory<byte>> ReadAsync(string path, int bufferSize, CancellationToken ct);

    /// <summary>Write a file from a sequence of buffers.</summary>
    Task WriteAsync(string path, IAsyncEnumerable<ReadOnlyMemory<byte>> content, CancellationToken ct);
}

public sealed record FileSystemEntry(string Name, string FullPath, bool IsDirectory, long? Length);
