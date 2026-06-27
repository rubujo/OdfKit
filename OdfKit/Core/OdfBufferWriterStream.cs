using System;
using System.Buffers;
using System.IO;

namespace OdfKit.Core;

internal sealed class OdfBufferWriterStream(IBufferWriter<byte> writer) : Stream
{
    private readonly IBufferWriter<byte> _writer = writer ?? throw new ArgumentNullException(nameof(writer));

    public override bool CanRead => false;

    public override bool CanSeek => false;

    public override bool CanWrite => true;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush()
    {
    }

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count)
    {
        if (buffer is null)
            throw new ArgumentNullException(nameof(buffer));
        if (offset < 0 || count < 0 || buffer.Length - offset < count)
            throw new ArgumentOutOfRangeException(nameof(offset));

        buffer.AsSpan(offset, count).CopyTo(_writer.GetSpan(count));
        _writer.Advance(count);
    }

#if !NETSTANDARD2_0
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        buffer.CopyTo(_writer.GetSpan(buffer.Length));
        _writer.Advance(buffer.Length);
    }
#endif
}
