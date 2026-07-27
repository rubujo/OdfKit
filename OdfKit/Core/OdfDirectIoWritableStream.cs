using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
#if NET10_0_OR_GREATER
using Microsoft.Win32.SafeHandles;
#endif

namespace OdfKit.Core;

/// <summary>
/// Provides a write-only stream optimized for Direct I/O file access when the platform supports it.
/// 提供在平台支援時使用 Direct I/O 檔案存取最佳化的唯寫資料流。
/// </summary>
public sealed class OdfDirectIoWritableStream : Stream
{
    private const int SectorSize = 4096;
    private const FileOptions FileFlagNoBuffering = (FileOptions)0x20000000;

    private readonly string _filePath;
    private FileStream? _fileStream;
#if NET10_0_OR_GREATER
    private SafeFileHandle? _fileHandle;
    private readonly AlignedNativeBuffer _directBuffer;
#else
    private readonly byte[] _directBuffer;
#endif
    private int _bufferOffset;
    private long _totalAlignedWritten;
    private bool _isFallback;
    private bool _isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="OdfDirectIoWritableStream"/> class.
    /// 初始化 <see cref="OdfDirectIoWritableStream"/> 類別的新執行個體。
    /// </summary>
    /// <param name="filePath">The path of the file to write. / 要寫入的檔案路徑。</param>
    public OdfDirectIoWritableStream(string filePath)
    {
        _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
#if NET10_0_OR_GREATER
        _directBuffer = new AlignedNativeBuffer(SectorSize, SectorSize);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                _fileHandle = File.OpenHandle(
                    _filePath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.Read,
                    FileFlagNoBuffering | FileOptions.WriteThrough);
                _isFallback = false;
            }
            catch (Exception ex)
            {
                OdfKitDiagnostics.Warn($"[OdfDirectIo] 無法以 Direct I/O 模式開啟檔案，將退回常規寫入模式。原因: {ex.Message}");
                _isFallback = true;
            }
        }
        else
        {
            _isFallback = true;
        }
#else
        _directBuffer = new byte[SectorSize];
        _isFallback = true;
#endif

        if (_isFallback)
        {
            _fileStream = new FileStream(_filePath, FileMode.Create, FileAccess.Write, FileShare.Read, SectorSize);
        }
    }

    /// <summary>
    /// Provides the CanRead member.
    /// 提供 CanRead 成員。
    /// </summary>
    /// <inheritdoc />
    public override bool CanRead => false;

    /// <summary>
    /// Provides the CanSeek member.
    /// 提供 CanSeek 成員。
    /// </summary>
    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <summary>
    /// Provides the CanWrite member.
    /// 提供 CanWrite 成員。
    /// </summary>
    /// <inheritdoc />
    public override bool CanWrite => !_isDisposed;

    /// <summary>
    /// Performs length.
    /// 執行 Length。
    /// </summary>
    /// <inheritdoc />
    public override long Length => throw new NotSupportedException(OdfKit.Compliance.OdfLocalizer.GetMessage("Err_StreamOperation_NotSupported"));

    /// <summary>
    /// Gets the number of bytes accepted by the stream.
    /// 取得資料流已接受的位元組數。
    /// </summary>
    /// <inheritdoc />
    public override long Position
    {
        get => _totalAlignedWritten + _bufferOffset;
        set => throw new NotSupportedException(OdfKit.Compliance.OdfLocalizer.GetMessage("Err_StreamOperation_NotSupported"));
    }

    /// <summary>
    /// Performs flush.
    /// 執行 Flush。
    /// </summary>
    /// <inheritdoc />
    public override void Flush()
    {
        if (_isFallback)
        {
            _fileStream?.Flush();
        }
    }

    /// <summary>
    /// Performs the Read operation.
    /// 執行 Read 作業。
    /// </summary>
    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException(OdfKit.Compliance.OdfLocalizer.GetMessage("Err_StreamOperation_NotSupported"));

    /// <summary>
    /// Performs seek.
    /// 執行 Seek。
    /// </summary>
    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException(OdfKit.Compliance.OdfLocalizer.GetMessage("Err_StreamOperation_NotSupported"));

    /// <summary>
    /// Sets length.
    /// 設定 Length。
    /// </summary>
    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException(OdfKit.Compliance.OdfLocalizer.GetMessage("Err_StreamOperation_NotSupported"));

    /// <summary>
    /// Performs the Write operation.
    /// 執行 Write 作業。
    /// </summary>
    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count)
    {
        if (buffer is null)
            throw new ArgumentNullException(nameof(buffer));
        if (offset < 0 || count < 0 || offset + count > buffer.Length)
            throw new ArgumentOutOfRangeException(nameof(count));
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(OdfDirectIoWritableStream));

        if (_isFallback)
        {
            _fileStream!.Write(buffer, offset, count);
            return;
        }

        WriteDirect(buffer.AsSpan(offset, count));
    }

#if NETCOREAPP2_1_OR_GREATER
    /// <summary>
    /// Writes a span directly to the underlying stream.
    /// 將唯讀範圍直接寫入底層資料流。
    /// </summary>
    /// <inheritdoc />
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(OdfDirectIoWritableStream));

        if (_isFallback)
        {
            _fileStream!.Write(buffer);
            return;
        }

        WriteDirect(buffer);
    }
#endif

    private void WriteDirect(ReadOnlySpan<byte> buffer)
    {
        int remaining = buffer.Length;
        int currentOffset = 0;

        while (remaining > 0)
        {
            int space = SectorSize - _bufferOffset;
            int toCopy = Math.Min(space, remaining);
            buffer.Slice(currentOffset, toCopy).CopyTo(DirectSpan.Slice(_bufferOffset, toCopy));

            _bufferOffset += toCopy;
            currentOffset += toCopy;
            remaining -= toCopy;

            if (_bufferOffset == SectorSize)
            {
                WriteAlignedBuffer();
                _bufferOffset = 0;
            }
        }
    }

    private Span<byte> DirectSpan
    {
        get
        {
#if NET10_0_OR_GREATER
            return _directBuffer.GetSpan();
#else
            return _directBuffer;
#endif
        }
    }

    private void WriteAlignedBuffer()
    {
#if NET10_0_OR_GREATER
        RandomAccess.Write(_fileHandle!, _directBuffer.GetSpan().Slice(0, SectorSize), _totalAlignedWritten);
#else
        _fileStream!.Write(_directBuffer, 0, SectorSize);
#endif
        _totalAlignedWritten += SectorSize;
    }

    /// <summary>
    /// Writes async.
    /// 寫入 Async。
    /// </summary>
    /// <inheritdoc />
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled(cancellationToken);
        }

        try
        {
            Write(buffer, offset, count);
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            return Task.FromException(ex);
        }
    }

#if NETCOREAPP2_1_OR_GREATER
    /// <summary>
    /// Writes the buffer asynchronously without cancellation.
    /// 在不可取消的情況下非同步寫入緩衝區。
    /// </summary>
    /// <param name="buffer">The buffer to write. / 要寫入的緩衝區。</param>
    /// <returns>A value task representing the write operation. / 代表寫入作業的值工作。</returns>
    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer)
        => WriteAsync(buffer, CancellationToken.None);

    /// <summary>
    /// Writes a memory buffer asynchronously.
    /// 非同步寫入記憶體緩衝區。
    /// </summary>
    /// <inheritdoc />
    public override ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return ValueTask.FromCanceled(cancellationToken);

        try
        {
            Write(buffer.Span);
            return ValueTask.CompletedTask;
        }
        catch (Exception ex)
        {
            return ValueTask.FromException(ex);
        }
    }
#endif

    /// <summary>
    /// Releases unmanaged resources.
    /// 釋放非受控資源。
    /// </summary>
    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (_isDisposed)
            return;

        if (disposing)
        {
            if (!_isFallback)
            {
#if NET10_0_OR_GREATER
                _fileHandle?.Dispose();
                _fileHandle = null;
#endif
                if (_bufferOffset > 0)
                {
                    using var fallbackStream = new FileStream(
                        _filePath,
                        FileMode.Open,
                        FileAccess.Write,
                        FileShare.ReadWrite);
                    fallbackStream.Seek(_totalAlignedWritten, SeekOrigin.Begin);
#if NET10_0_OR_GREATER
                    fallbackStream.Write(DirectSpan.Slice(0, _bufferOffset));
                    fallbackStream.SetLength(_totalAlignedWritten + _bufferOffset);
                    fallbackStream.Flush(true);
#else
                    fallbackStream.Write(_directBuffer, 0, _bufferOffset);
                    fallbackStream.SetLength(_totalAlignedWritten + _bufferOffset);
                    fallbackStream.Flush();
#endif
                }
            }
            else
            {
                _fileStream?.Dispose();
                _fileStream = null;
            }

#if NET10_0_OR_GREATER
            ((IDisposable)_directBuffer).Dispose();
#endif
        }

        _isDisposed = true;
        base.Dispose(disposing);
    }
}

