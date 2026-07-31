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

    public override long Length => throw new NotSupportedException(OdfKit.Compliance.OdfLocalizer.GetMessage("Err_StreamOperation_NotSupported"));

    public override long Position
    {
        get => throw new NotSupportedException(OdfKit.Compliance.OdfLocalizer.GetMessage("Err_StreamOperation_NotSupported"));
        set => throw new NotSupportedException(OdfKit.Compliance.OdfLocalizer.GetMessage("Err_StreamOperation_NotSupported"));
    }

    public override void Flush()
    {
    }

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException(OdfKit.Compliance.OdfLocalizer.GetMessage("Err_StreamOperation_NotSupported"));

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException(OdfKit.Compliance.OdfLocalizer.GetMessage("Err_StreamOperation_NotSupported"));

    public override void SetLength(long value) => throw new NotSupportedException(OdfKit.Compliance.OdfLocalizer.GetMessage("Err_StreamOperation_NotSupported"));

    public override void Write(byte[] buffer, int offset, int count)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(buffer, nameof(buffer));
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

    /// <summary>
    /// 直接寫入底層 <see cref="System.Buffers.IBufferWriter{T}"/>，不經基底類別的陣列轉接。
    /// </summary>
    /// <remarks>
    /// 基底 <see cref="Stream.WriteAsync(ReadOnlyMemory{byte}, CancellationToken)"/> 會嘗試取出底層陣列，
    /// 取不到時（例如來源為 <see cref="System.Buffers.MemoryManager{T}"/>）會另行配置並複製。寫入此串流
    /// 本身是同步的記憶體複製，因此直接覆寫可省去該配置與 Task 包裝。
    /// </remarks>
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return ValueTask.FromCanceled(cancellationToken);

        buffer.Span.CopyTo(_writer.GetSpan(buffer.Length));
        _writer.Advance(buffer.Length);
        return ValueTask.CompletedTask;
    }
#endif
}

