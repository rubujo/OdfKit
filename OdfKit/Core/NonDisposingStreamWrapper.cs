using System;
using System.IO;

namespace OdfKit.Core;

internal class NonDisposingStreamWrapper(Stream baseStream) : Stream
{
    private readonly Stream _baseStream = baseStream ?? throw new ArgumentNullException(nameof(baseStream));

    public override bool CanRead => _baseStream.CanRead;
    public override bool CanSeek => _baseStream.CanSeek;
    public override bool CanWrite => _baseStream.CanWrite;
    public override long Length => _baseStream.Length;
    public override long Position
    {
        get => _baseStream.Position;
        set => _baseStream.Position = value;
    }

    public override void Flush() => _baseStream.Flush();

    public override int Read(byte[] buffer, int offset, int count) => _baseStream.Read(buffer, offset, count);

    public override long Seek(long offset, SeekOrigin origin) => _baseStream.Seek(offset, origin);

    public override void SetLength(long value) => _baseStream.SetLength(value);

    public override void Write(byte[] buffer, int offset, int count) => _baseStream.Write(buffer, offset, count);

    public override System.Threading.Tasks.Task WriteAsync(byte[] buffer, int offset, int count, System.Threading.CancellationToken cancellationToken)
    {
        return _baseStream.WriteAsync(buffer, offset, count, cancellationToken);
    }

    public override System.Threading.Tasks.Task<int> ReadAsync(byte[] buffer, int offset, int count, System.Threading.CancellationToken cancellationToken)
    {
        return _baseStream.ReadAsync(buffer, offset, count, cancellationToken);
    }

#if NETCOREAPP2_1_OR_GREATER
    public override System.Threading.Tasks.ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer,
        System.Threading.CancellationToken cancellationToken = default)
        => _baseStream.WriteAsync(buffer, cancellationToken);

    public override System.Threading.Tasks.ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        System.Threading.CancellationToken cancellationToken = default)
        => _baseStream.ReadAsync(buffer, cancellationToken);
#endif

    public override System.Threading.Tasks.Task FlushAsync(System.Threading.CancellationToken cancellationToken)
    {
        return _baseStream.FlushAsync(cancellationToken);
    }
}
