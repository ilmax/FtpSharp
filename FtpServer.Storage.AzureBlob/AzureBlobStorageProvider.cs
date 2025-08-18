using System.Buffers;
using System.Runtime.InteropServices;
using Azure;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using FtpServer.Core.Abstractions;
using Microsoft.Extensions.Options;

namespace FtpServer.Storage.AzureBlob;

public sealed class AzureBlobStorageProvider : IStorageProvider
{
    private readonly BlobContainerClient _container;
    private readonly string _prefix;

    public AzureBlobStorageProvider(IOptions<AzureBlobStorageOptions> options)
    {
        var o = options.Value;
        // Validate options: Either ConnectionString must be set, or AccountUrl must be provided. Container is always required.
        if (string.IsNullOrWhiteSpace(o.Container))
        {
            throw new ArgumentException("AzureBlobStorageOptions.Container is required.");
        }
        bool hasConnStr = !string.IsNullOrWhiteSpace(o.ConnectionString);
        bool hasAccountUrl = !string.IsNullOrWhiteSpace(o.AccountUrl);
        if (!hasConnStr && !hasAccountUrl)
        {
            throw new ArgumentException("Either AzureBlobStorageOptions.ConnectionString or AccountUrl must be provided.");
        }
        BlobServiceClient service;
        if (hasConnStr)
        {
            service = new BlobServiceClient(o.ConnectionString);
        }
        else
        {
            var cred = new DefaultAzureCredential();
            service = new BlobServiceClient(new Uri(o.AccountUrl), cred);
        }
        _container = service.GetBlobContainerClient(o.Container);
        _prefix = (o.Prefix ?? string.Empty).Trim('/');
    }

    private string ToKey(string logicalPath)
    {
        if (string.IsNullOrWhiteSpace(logicalPath) || logicalPath == "/") return _prefix;
        var p = logicalPath.Replace("\\", "/").Trim('/');
        return string.IsNullOrEmpty(_prefix) ? p : $"{_prefix}/{p}";
    }

    public async Task<bool> ExistsAsync(string path, CancellationToken ct)
    {
        string key = ToKey(path);
        if (string.IsNullOrEmpty(key)) return true; // root exists
        var blob = _container.GetBlobClient(key);
        if (await blob.ExistsAsync(ct).ConfigureAwait(false)) return true;
        // Check for virtual directory (any item under prefix)
        await foreach (var _ in _container.GetBlobsAsync(prefix: key.EndsWith('/') ? key : key + "/", cancellationToken: ct))
        {
            return true;
        }
        return false;
    }

    public async Task<IReadOnlyList<FileSystemEntry>> ListAsync(string path, CancellationToken ct)
    {
        string key = ToKey(path);
        string prefix = string.IsNullOrEmpty(key) ? _prefix : key.TrimEnd('/') + "/";
        var result = new List<FileSystemEntry>();
        var seenDirs = new HashSet<string>(StringComparer.Ordinal);
        await foreach (var item in _container.GetBlobsByHierarchyAsync(prefix: prefix, delimiter: "/", cancellationToken: ct))
        {
            if (item.IsPrefix)
            {
                string name = item.Prefix!.TrimEnd('/');
                name = name[(name.LastIndexOf('/') + 1)..];
                if (seenDirs.Add(name))
                    result.Add(new FileSystemEntry(name, true, null));
            }
            else
            {
                string name = item.Blob.Name[(item.Blob.Name.LastIndexOf('/') + 1)..];
                result.Add(new FileSystemEntry(name, false, item.Blob.Properties.ContentLength));
            }
        }
        return result;
    }

    public async Task CreateDirectoryAsync(string path, CancellationToken ct)
    {
        // Azure Blob has virtual directories; create a zero-length blob with a trailing slash marker (optional).
        string key = ToKey(path).TrimEnd('/') + "/";
        var blob = _container.GetBlobClient(key);
        await blob.UploadAsync(BinaryData.FromBytes(Array.Empty<byte>()), overwrite: true, ct);
    }

    public async Task DeleteAsync(string path, bool recursive, CancellationToken ct)
    {
        string key = ToKey(path);
        var blob = _container.GetBlobClient(key);
        if (await blob.ExistsAsync(ct).ConfigureAwait(false))
        {
            await blob.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: ct);
            return;
        }
        if (!recursive) throw new IOException("Directory not empty or recursive=false");
        string prefix = key.TrimEnd('/') + "/";
        await foreach (var item in _container.GetBlobsAsync(prefix: prefix, cancellationToken: ct))
        {
            await _container.DeleteBlobIfExistsAsync(item.Name, DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: ct);
        }
    }

    public async Task<long> GetSizeAsync(string path, CancellationToken ct)
    {
        string key = ToKey(path);
        var blob = _container.GetBlobClient(key);
        if (!await blob.ExistsAsync(ct).ConfigureAwait(false)) return 0;
        var props = await blob.GetPropertiesAsync(cancellationToken: ct);
        return props.Value.ContentLength;
    }

    public async Task<FileSystemEntry?> GetEntryAsync(string path, CancellationToken ct)
    {
        string key = ToKey(path);
        if (string.IsNullOrEmpty(key)) return new FileSystemEntry("/", true, null);
        var blob = _container.GetBlobClient(key);
        if (await blob.ExistsAsync(ct).ConfigureAwait(false))
        {
            var props = await blob.GetPropertiesAsync(cancellationToken: ct);
            string name = key[(key.LastIndexOf('/') + 1)..];
            return new FileSystemEntry(name, false, props.Value.ContentLength);
        }
        // directory?
        await foreach (var _ in _container.GetBlobsAsync(prefix: key.TrimEnd('/') + "/", cancellationToken: ct))
        {
            string name = key.TrimEnd('/');
            name = name[(name.LastIndexOf('/') + 1)..];
            return new FileSystemEntry(name, true, null);
        }
        return null;
    }

    public async Task RenameAsync(string fromPath, string toPath, CancellationToken ct)
    {
        string src = ToKey(fromPath);
        string dst = ToKey(toPath);
        var srcBlob = _container.GetBlobClient(src);
        if (await srcBlob.ExistsAsync(ct).ConfigureAwait(false))
        {
            var dstBlob = _container.GetBlobClient(dst);
            var copyResp = await dstBlob.StartCopyFromUriAsync(srcBlob.Uri, cancellationToken: ct);
            // poll for completion to avoid deleting source too early
            for (int i = 0; i < 60; i++)
            {
                var props = await dstBlob.GetPropertiesAsync(cancellationToken: ct);
                if (props.Value.CopyStatus == CopyStatus.Success)
                {
                    await srcBlob.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: ct);
                    return;
                }
                if (props.Value.CopyStatus == CopyStatus.Failed || props.Value.CopyStatus == CopyStatus.Aborted)
                {
                    throw new IOException($"Copy failed: {props.Value.CopyStatusDescription}");
                }
                await Task.Delay(TimeSpan.FromSeconds(1), ct);
            }
            throw new TimeoutException("Timed out waiting for blob copy to complete in RenameAsync");
        }
        // directory copy
        string prefix = src.TrimEnd('/') + "/";
        await foreach (var item in _container.GetBlobsAsync(prefix: prefix, cancellationToken: ct))
        {
            string suffix = item.Name[prefix.Length..];
            string dstKey = (dst.TrimEnd('/') + "/" + suffix).Trim('/');
            var dstBlob = _container.GetBlobClient(dstKey);
            await dstBlob.StartCopyFromUriAsync(_container.GetBlobClient(item.Name).Uri, cancellationToken: ct);
            await _container.DeleteBlobIfExistsAsync(item.Name, DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: ct);
        }
    }

    public async IAsyncEnumerable<ReadOnlyMemory<byte>> ReadAsync(string path, int bufferSize, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        string key = ToKey(path);
        var blob = _container.GetBlobClient(key);
        var resp = await blob.DownloadStreamingAsync(cancellationToken: ct);
        var stream = resp.Value.Content;
        byte[] buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
        try
        {
            int read;
            while ((read = await stream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
            {
                yield return new ReadOnlyMemory<byte>(buffer, 0, read);
                await Task.Yield();
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public async IAsyncEnumerable<ReadOnlyMemory<byte>> ReadFromOffsetAsync(string path, long offset, int bufferSize, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        string key = ToKey(path);
        var blob = _container.GetBlobClient(key);
        var resp = await blob.DownloadStreamingAsync(new BlobDownloadOptions { Range = new HttpRange(offset) }, ct);
        var stream = resp.Value.Content;
        byte[] buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
        try
        {
            int read;
            while ((read = await stream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
            {
                yield return new ReadOnlyMemory<byte>(buffer, 0, read);
                await Task.Yield();
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public async Task WriteAsync(string path, IAsyncEnumerable<ReadOnlyMemory<byte>> content, CancellationToken ct)
    {
        string key = ToKey(path);
        var blob = _container.GetBlobClient(key);
        await using var ms = new MemoryStream(); // For simplicity; can stream via StageBlock if needed
        await foreach (var chunk in content.WithCancellation(ct))
        {
            if (MemoryMarshal.TryGetArray((ReadOnlyMemory<byte>)chunk, out var seg))
                await ms.WriteAsync(seg.Array!, seg.Offset, seg.Count, ct);
            else
                await ms.WriteAsync(chunk.ToArray(), ct);
        }
        ms.Position = 0;
        await blob.UploadAsync(ms, overwrite: true, cancellationToken: ct);
    }

    public async Task AppendAsync(string path, IAsyncEnumerable<ReadOnlyMemory<byte>> content, CancellationToken ct)
    {
        string key = ToKey(path);
        var blob = _container.GetBlobClient(key);
        using var ms = new MemoryStream();
        if (await blob.ExistsAsync(ct).ConfigureAwait(false))
        {
            var dl = await blob.DownloadContentAsync(ct);
            dl.Value.Content.ToStream().CopyTo(ms);
        }
        await foreach (var chunk in content.WithCancellation(ct))
        {
            if (MemoryMarshal.TryGetArray((ReadOnlyMemory<byte>)chunk, out var seg))
                await ms.WriteAsync(seg.Array!, seg.Offset, seg.Count, ct);
            else
                await ms.WriteAsync(chunk.ToArray(), ct);
        }
        ms.Position = 0;
        await blob.UploadAsync(ms, overwrite: true, cancellationToken: ct);
    }

    public async Task WriteTruncateThenAppendAsync(string path, long truncateTo, IAsyncEnumerable<ReadOnlyMemory<byte>> content, CancellationToken ct)
    {
        string key = ToKey(path);
        var blob = _container.GetBlobClient(key);
        using var ms = new MemoryStream();
        if (await blob.ExistsAsync(ct).ConfigureAwait(false))
        {
            var dl = await blob.DownloadStreamingAsync(cancellationToken: ct);
            await dl.Value.Content.CopyToAsync(ms, ct);
            if (ms.Length > truncateTo) ms.SetLength(truncateTo);
        }
        await foreach (var chunk in content.WithCancellation(ct))
        {
            if (MemoryMarshal.TryGetArray((ReadOnlyMemory<byte>)chunk, out var seg))
                await ms.WriteAsync(seg.Array!, seg.Offset, seg.Count, ct);
            else
                await ms.WriteAsync(chunk.ToArray(), ct);
        }
        ms.Position = 0;
        await blob.UploadAsync(ms, overwrite: true, cancellationToken: ct);
    }
}
