using System;
using System.Buffers;
using System.IO;
using OdfKit.Compliance;

namespace OdfKit.Core;

/// <summary>
/// Partial: ZIP payload counting and pooled buffer streams for archive writing.
/// Partial：封存寫入用的計數與池化緩衝串流。
/// </summary>
internal static partial class OdfPackageArchiveWriter
{
    private sealed class CountingWriteStream(Stream inner) : Stream
    {
        public long BytesWritten { get; private set; }

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => inner.CanWrite;

        public override long Length => BytesWritten;

        public override long Position
        {
            get => BytesWritten;
            set => throw new NotSupportedException(OdfKit.Compliance.OdfLocalizer.GetMessage("Err_StreamOperation_NotSupported"));
        }

        public override void Flush() => inner.Flush();

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException(OdfKit.Compliance.OdfLocalizer.GetMessage("Err_StreamOperation_NotSupported"));

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException(OdfKit.Compliance.OdfLocalizer.GetMessage("Err_StreamOperation_NotSupported"));

        public override void SetLength(long value) => throw new NotSupportedException(OdfKit.Compliance.OdfLocalizer.GetMessage("Err_StreamOperation_NotSupported"));

        public override void Write(byte[] buffer, int offset, int count)
        {
            inner.Write(buffer, offset, count);
            BytesWritten += count;
        }

#if NETSTANDARD2_0
        public override void WriteByte(byte value)
        {
            inner.WriteByte(value);
            BytesWritten++;
        }
#else
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            inner.Write(buffer);
            BytesWritten += buffer.Length;
        }

        public override void WriteByte(byte value)
        {
            inner.WriteByte(value);
            BytesWritten++;
        }
#endif
    }

    private sealed class PooledZipPayloadStream : Stream
    {
        private byte[] _buffer;
        private int _length;
        private bool _disposed;

        public PooledZipPayloadStream(int initialCapacity)
        {
            _buffer = ArrayPool<byte>.Shared.Rent(Math.Max(1, initialCapacity));
        }

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => !_disposed;

        public override long Length => _length;

        public override long Position
        {
            get => _length;
            set => throw new NotSupportedException(OdfKit.Compliance.OdfLocalizer.GetMessage("Err_StreamOperation_NotSupported"));
        }

        public byte[] ToArray()
        {
            ThrowIfDisposed();

            if (_length == 0)
                return [];

            var result = new byte[_length];
            Buffer.BlockCopy(_buffer, 0, result, 0, _length);
            return result;
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException(OdfKit.Compliance.OdfLocalizer.GetMessage("Err_StreamOperation_NotSupported"));

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException(OdfKit.Compliance.OdfLocalizer.GetMessage("Err_StreamOperation_NotSupported"));

        public override void SetLength(long value) => throw new NotSupportedException(OdfKit.Compliance.OdfLocalizer.GetMessage("Err_StreamOperation_NotSupported"));

        public override void Write(byte[] buffer, int offset, int count)
        {
            ThrowIfDisposed();
            if (buffer is null)
                throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || count < 0 || offset > buffer.Length - count)
                throw new ArgumentOutOfRangeException(nameof(offset));

            EnsureCapacity(_length + count);
            Buffer.BlockCopy(buffer, offset, _buffer, _length, count);
            _length += count;
        }

#if !NETSTANDARD2_0
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            ThrowIfDisposed();
            EnsureCapacity(_length + buffer.Length);
            buffer.CopyTo(_buffer.AsSpan(_length));
            _length += buffer.Length;
        }
#endif

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                ArrayPool<byte>.Shared.Return(_buffer);
                _buffer = Array.Empty<byte>();
                _length = 0;
                _disposed = true;
            }

            base.Dispose(disposing);
        }

        private void EnsureCapacity(int required)
        {
            if (required <= _buffer.Length)
                return;

            int newSize = _buffer.Length;
            while (newSize < required)
            {
                newSize = checked(newSize * 2);
            }

            byte[] next = ArrayPool<byte>.Shared.Rent(newSize);
            Buffer.BlockCopy(_buffer, 0, next, 0, _length);
            ArrayPool<byte>.Shared.Return(_buffer);
            _buffer = next;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(PooledZipPayloadStream));
            }
        }
    }

    private sealed record RawZipCentralDirectoryEntry(
        string Name,
        ushort Method,
        uint Crc32,
        uint CompressedSize,
        uint UncompressedSize,
        uint LocalHeaderOffset,
        ushort Flags,
        uint TimeDate,
        byte[] NameBytes);

    private sealed record PreparedZipEntry(
        string Name,
        ushort Method,
        uint Crc32,
        byte[] Payload,
        int UncompressedSize,
        ushort Flags,
        uint TimeDate,
        byte[] NameBytes);

    private static Task CopyEntryContentAsync(Stream source, Stream destination, CancellationToken cancellationToken = default)
    {
        return source.CopyToAsync(destination, 81920, cancellationToken);
    }

}
