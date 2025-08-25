// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net.Security;

namespace FtpServer.Core.Server;

internal sealed class TlsDataStream(SslStream sslStream) : Stream
{
    private readonly SslStream _sslStream = sslStream ?? throw new ArgumentNullException(nameof(sslStream));
    private bool _disposed;

    public override bool CanRead => _sslStream.CanRead;
    public override bool CanSeek => _sslStream.CanSeek;
    public override bool CanWrite => _sslStream.CanWrite;
    public override long Length => _sslStream.Length;
    public override long Position
    {
        get => _sslStream.Position;
        set => _sslStream.Position = value;
    }

    public override void Flush() => _sslStream.Flush();
    public override Task FlushAsync(CancellationToken cancellationToken) => _sslStream.FlushAsync(cancellationToken);

    public override int Read(byte[] buffer, int offset, int count) => _sslStream.Read(buffer, offset, count);
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        _sslStream.ReadAsync(buffer, offset, count, cancellationToken);
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        _sslStream.ReadAsync(buffer, cancellationToken);

    public override long Seek(long offset, SeekOrigin origin) => _sslStream.Seek(offset, origin);
    public override void SetLength(long value) => _sslStream.SetLength(value);

    public override void Write(byte[] buffer, int offset, int count) => _sslStream.Write(buffer, offset, count);
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        _sslStream.WriteAsync(buffer, offset, count, cancellationToken);
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
        _sslStream.WriteAsync(buffer, cancellationToken);

    protected override void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            try
            {
                // Properly shutdown the TLS connection
                _sslStream.ShutdownAsync().GetAwaiter().GetResult();
            }
            catch
            {
                // Ignore shutdown errors - client may have already disconnected
            }
            finally
            {
                _sslStream.Dispose();
                _disposed = true;
            }
        }
        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            try
            {
                // Properly shutdown the TLS connection
                await _sslStream.ShutdownAsync().ConfigureAwait(false);
            }
            catch
            {
                // Ignore shutdown errors - client may have already disconnected
            }
            finally
            {
                await _sslStream.DisposeAsync().ConfigureAwait(false);
                _disposed = true;
            }
        }
        await base.DisposeAsync().ConfigureAwait(false);
    }
}
