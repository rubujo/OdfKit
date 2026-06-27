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
/// 實作作業系統 Direct I/O 的高效唯讀資料流。
/// </summary>
public sealed class OdfDirectIoReadableStream : Stream
{
    private const int SectorSize = 4096;
    private const int PrefetchSize = 64 * 1024;
    private const FileOptions FileFlagNoBuffering = (FileOptions)0x20000000;

    private readonly string _filePath;
    internal string FilePath => _filePath;
    private FileStream? _fileStream;
#if NET10_0_OR_GREATER
    private SafeFileHandle? _fileHandle;
    private readonly AlignedNativeBuffer _bufferA;
    private readonly AlignedNativeBuffer _bufferB;
    private AlignedNativeBuffer _activeBuffer;
    private AlignedNativeBuffer _backBuffer;
#else
    private readonly byte[] _bufferA;
    private readonly byte[] _bufferB;
    private byte[] _activeBuffer;
    private byte[] _backBuffer;
#endif

    private long _bufferStart = -1;
    private int _bufferLength;
    private readonly long _totalLength;
    private readonly long _alignedLimit;
    private long _currentPosition;
    private bool _isFallback;
    private bool _isDisposed;

    private Task<(long start, int length)>? _prefetchTask;
    private long _nextPrefetchStart = -1;
    private readonly object _lock = new();

    /// <summary>
    /// 初始化 <see cref="OdfDirectIoReadableStream"/> 類別的新執行個體。
    /// </summary>
    /// <param name="filePath">檔案路徑。</param>
    public OdfDirectIoReadableStream(string filePath)
    {
        _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
#if NET10_0_OR_GREATER
        _bufferA = new AlignedNativeBuffer(PrefetchSize, SectorSize);
        _bufferB = new AlignedNativeBuffer(PrefetchSize, SectorSize);
#else
        _bufferA = new byte[PrefetchSize];
        _bufferB = new byte[PrefetchSize];
#endif
        _activeBuffer = _bufferA;
        _backBuffer = _bufferB;

        var fileInfo = new FileInfo(_filePath);
        _totalLength = fileInfo.Length;
        _alignedLimit = (_totalLength / SectorSize) * SectorSize;
        _currentPosition = 0;

#if NET10_0_OR_GREATER
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                _fileHandle = File.OpenHandle(
                    _filePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite,
                    FileFlagNoBuffering);
                _isFallback = false;
            }
            catch (Exception ex)
            {
                OdfKitDiagnostics.Warn($"[OdfDirectIo] 無法以 Direct I/O 模式開啟檔案讀取，將退回常規讀取模式。原因: {ex.Message}");
                _isFallback = true;
            }
        }
        else
        {
            _isFallback = true;
        }
#else
        _isFallback = true;
#endif

        if (_isFallback)
        {
            _fileStream = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, SectorSize);
        }
    }

    /// <inheritdoc />
    public override bool CanRead => !_isDisposed;

    /// <inheritdoc />
    public override bool CanSeek => true;

    /// <inheritdoc />
    public override bool CanWrite => false;

    /// <inheritdoc />
    public override long Length => _totalLength;

    /// <inheritdoc />
    public override long Position
    {
        get => _currentPosition;
        set => Seek(value, SeekOrigin.Begin);
    }

    /// <inheritdoc />
    public override void Flush()
    {
    }

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin)
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(OdfDirectIoReadableStream));

        long target = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _currentPosition + offset,
            SeekOrigin.End => _totalLength + offset,
            _ => throw new ArgumentException(null, nameof(origin))
        };

        if (target < 0 || target > _totalLength)
            throw new ArgumentOutOfRangeException(nameof(offset));

        lock (_lock)
        {
            if (_currentPosition != target)
            {
                _currentPosition = target;
                _prefetchTask = null;
                _nextPrefetchStart = -1;
            }
        }

        return _currentPosition;
    }

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count)
    {
        if (buffer is null)
            throw new ArgumentNullException(nameof(buffer));
        if (offset < 0 || count < 0 || offset + count > buffer.Length)
            throw new ArgumentOutOfRangeException(nameof(count));
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(OdfDirectIoReadableStream));

        if (_currentPosition >= _totalLength || count == 0)
            return 0;

        if (_isFallback)
        {
            lock (_lock)
            {
                _fileStream!.Seek(_currentPosition, SeekOrigin.Begin);
                int read = _fileStream.Read(buffer, offset, count);
                _currentPosition += read;
                return read;
            }
        }

        int totalBytesRead = 0;
        int remaining = (int)Math.Min(count, _totalLength - _currentPosition);

        while (remaining > 0)
        {
            if (_bufferStart == -1 || _currentPosition < _bufferStart || _currentPosition >= _bufferStart + _bufferLength)
            {
                FillPrefetchBuffer();
            }

            int bufferOffset = (int)(_currentPosition - _bufferStart);
            int available = _bufferLength - bufferOffset;
            if (available <= 0)
                break;

            int toCopy = Math.Min(remaining, available);
            ActiveSpan.Slice(bufferOffset, toCopy).CopyTo(buffer.AsSpan(offset + totalBytesRead, toCopy));

            _currentPosition += toCopy;
            totalBytesRead += toCopy;
            remaining -= toCopy;
        }

        return totalBytesRead;
    }

    private Span<byte> ActiveSpan
    {
        get
        {
#if NET10_0_OR_GREATER
            return _activeBuffer.GetSpan();
#else
            return _activeBuffer;
#endif
        }
    }

    private void FillPrefetchBuffer()
    {
        lock (_lock)
        {
            if (_prefetchTask is not null && _nextPrefetchStart == _currentPosition)
            {
                try
                {
                    var (start, length) = _prefetchTask.GetAwaiter().GetResult();
                    _bufferStart = start;
                    _bufferLength = length;

                    var temp = _activeBuffer;
                    _activeBuffer = _backBuffer;
                    _backBuffer = temp;

                    _prefetchTask = null;
                    _nextPrefetchStart = -1;
                    TriggerNextPrefetch();
                    return;
                }
                catch
                {
                    _prefetchTask = null;
                    _nextPrefetchStart = -1;
                }
            }

            _prefetchTask = null;
            _nextPrefetchStart = -1;

            if (_currentPosition < _alignedLimit)
            {
                long readStart = (_currentPosition / SectorSize) * SectorSize;
                int readSize = (int)Math.Min(PrefetchSize, _alignedLimit - readStart);

                if (readSize > 0)
                {
                    int read = ReadIntoBuffer(_activeBuffer, readStart, readSize);
                    _bufferStart = readStart;
                    _bufferLength = read;
                }
            }
            else
            {
                EnsureFallbackStream();

                long readStart = _alignedLimit;
                int readSize = (int)(_totalLength - _alignedLimit);

                if (readSize > 0)
                {
                    _fileStream!.Seek(readStart, SeekOrigin.Begin);
                    int read = ReadFallback(_activeBuffer, readSize);
                    _bufferStart = readStart;
                    _bufferLength = read;
                }
            }

            TriggerNextPrefetch();
        }
    }

    private void EnsureFallbackStream()
    {
        if (_isFallback)
            return;

#if NET10_0_OR_GREATER
        _fileHandle?.Dispose();
        _fileHandle = null;
#endif
        _fileStream = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, SectorSize);
        _isFallback = true;
    }

#if NET10_0_OR_GREATER
    private int ReadIntoBuffer(AlignedNativeBuffer buffer, long fileOffset, int count)
    {
        if (!_isFallback && _fileHandle is not null)
            return RandomAccess.Read(_fileHandle, buffer.GetSpan().Slice(0, count), fileOffset);

        _fileStream!.Seek(fileOffset, SeekOrigin.Begin);
        return _fileStream.Read(buffer.GetSpan().Slice(0, count));
    }

    private int ReadFallback(AlignedNativeBuffer buffer, int count)
    {
        return _fileStream!.Read(buffer.GetSpan().Slice(0, count));
    }
#else
    private int ReadIntoBuffer(byte[] buffer, long fileOffset, int count)
    {
        _fileStream!.Seek(fileOffset, SeekOrigin.Begin);
        return _fileStream.Read(buffer, 0, count);
    }

    private int ReadFallback(byte[] buffer, int count) => _fileStream!.Read(buffer, 0, count);
#endif

    private void TriggerNextPrefetch()
    {
        long nextStart = _bufferStart + _bufferLength;
        if (nextStart < _alignedLimit && !_isFallback)
        {
            int nextSize = (int)Math.Min(PrefetchSize, _alignedLimit - nextStart);
            if (nextSize > 0)
            {
                _nextPrefetchStart = nextStart;
                var targetBackBuffer = _backBuffer;
                _prefetchTask = Task.Run(() =>
                {
                    if (_isDisposed || _isFallback)
                        return (nextStart, 0);

                    try
                    {
                        int read = ReadIntoBuffer(targetBackBuffer, nextStart, nextSize);
                        return (nextStart, read);
                    }
                    catch
                    {
                        return (nextStart, 0);
                    }
                });
            }
        }
    }

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc />
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<int>(cancellationToken);
        }

        try
        {
            return Task.FromResult(Read(buffer, offset, count));
        }
        catch (Exception ex)
        {
            return Task.FromException<int>(ex);
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (_isDisposed)
            return;

        if (disposing)
        {
            lock (_lock)
            {
                _prefetchTask = null;
#if NET10_0_OR_GREATER
                _fileHandle?.Dispose();
                ((IDisposable)_bufferA).Dispose();
                ((IDisposable)_bufferB).Dispose();
#endif
                _fileStream?.Dispose();
                _fileStream = null;
            }
        }

        _isDisposed = true;
        base.Dispose(disposing);
    }
}
