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
/// Provides the OdfDirectIoWritableStream API.
/// 實作作業系統 Direct I/O 的高效寫入資料流。
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
    /// Executes the OdfDirectIoWritableStream operation.
    /// 初始化 <see cref="OdfDirectIoWritableStream"/> 類別的新執行個體。
    /// </summary>
    /// <param name="filePath">檔案路徑。</param>
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
    /// Executes the Length operation.
    /// 執行 Length 作業。
    /// </summary>
    /// <inheritdoc />
    public override long Length => throw new NotSupportedException(OdfKit.Compliance.OdfLocalizer.GetMessage("Err_StreamOperation_NotSupported"));

    /// <summary>
    /// Provides the member member.
    /// 提供 member 成員。
    /// </summary>
    /// <inheritdoc />
    public override long Position
    {
        get => _totalAlignedWritten + _bufferOffset;
        set => throw new NotSupportedException(OdfKit.Compliance.OdfLocalizer.GetMessage("Err_StreamOperation_NotSupported"));
    }

    /// <summary>
    /// Executes the Flush operation.
    /// 執行 Flush 作業。
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
    /// Executes the Read operation.
    /// 執行 Read 作業。
    /// </summary>
    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException(OdfKit.Compliance.OdfLocalizer.GetMessage("Err_StreamOperation_NotSupported"));

    /// <summary>
    /// Executes the Seek operation.
    /// 執行 Seek 作業。
    /// </summary>
    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException(OdfKit.Compliance.OdfLocalizer.GetMessage("Err_StreamOperation_NotSupported"));

    /// <summary>
    /// Executes the SetLength operation.
    /// 執行 SetLength 作業。
    /// </summary>
    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException(OdfKit.Compliance.OdfLocalizer.GetMessage("Err_StreamOperation_NotSupported"));

    /// <summary>
    /// Executes the Write operation.
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

        int remaining = count;
        int currentOffset = offset;

        while (remaining > 0)
        {
            int space = SectorSize - _bufferOffset;
            int toCopy = Math.Min(space, remaining);
            buffer.AsSpan(currentOffset, toCopy).CopyTo(DirectSpan.Slice(_bufferOffset, toCopy));

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
    /// Executes the WriteAsync operation.
    /// 執行 WriteAsync 作業。
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

    /// <summary>
    /// Executes the Dispose operation.
    /// 執行 Dispose 作業。
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

