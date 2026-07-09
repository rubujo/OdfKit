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
/// Provides a read-only stream optimized for Direct I/O file access when the platform supports it.
/// 提供在平台支援時使用 Direct I/O 檔案存取最佳化的唯讀資料流。
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
    /// Initializes a new instance of the <see cref="OdfDirectIoReadableStream"/> class.
    /// 初始化 <see cref="OdfDirectIoReadableStream"/> 類別的新執行個體。
    /// </summary>
    /// <param name="filePath">The path of the file to read. / 要讀取的檔案路徑。</param>
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

    /// <summary>
    /// Provides the CanRead member.
    /// 提供 CanRead 成員。
    /// </summary>
    /// <inheritdoc />
    public override bool CanRead => !_isDisposed;

    /// <summary>
    /// Provides the CanSeek member.
    /// 提供 CanSeek 成員。
    /// </summary>
    /// <inheritdoc />
    public override bool CanSeek => true;

    /// <summary>
    /// Provides the CanWrite member.
    /// 提供 CanWrite 成員。
    /// </summary>
    /// <inheritdoc />
    public override bool CanWrite => false;

    /// <summary>
    /// Provides the Length member.
    /// 提供 Length 成員。
    /// </summary>
    /// <inheritdoc />
    public override long Length => _totalLength;

    /// <summary>
    /// Gets or sets the current stream position.
    /// 取得或設定目前資料流位置。
    /// </summary>
    /// <inheritdoc />
    public override long Position
    {
        get => _currentPosition;
        set => Seek(value, SeekOrigin.Begin);
    }

    /// <summary>
    /// Executes the Flush operation.
    /// 執行 Flush 作業。
    /// </summary>
    /// <inheritdoc />
    public override void Flush()
    {
    }

    /// <summary>
    /// Executes the Seek operation.
    /// 執行 Seek 作業。
    /// </summary>
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
            _ => throw new ArgumentException(
                OdfKit.Compliance.OdfLocalizer.GetMessage("Err_ArgumentOutOfRange_Origin"),
                nameof(origin))
        };

        if (target < 0 || target > _totalLength)
            throw new ArgumentOutOfRangeException(nameof(offset));

        lock (_lock)
        {
            if (_currentPosition != target)
            {
                _currentPosition = target;
                // 只作廢預讀結果，不清除 _prefetchTask 參考：背景工作可能仍在寫入
                // _backBuffer，必須保留參考讓後續 FillPrefetchBuffer／Dispose 能等待其完成，
                // 否則孤兒工作會與下一個排程至同一緩衝區的預讀形成資料競爭。
                _nextPrefetchStart = -1;
            }
        }

        return _currentPosition;
    }

    /// <summary>
    /// Executes the SetLength operation.
    /// 執行 SetLength 作業。
    /// </summary>
    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException(OdfKit.Compliance.OdfLocalizer.GetMessage("Err_StreamOperation_NotSupported"));

    /// <summary>
    /// Executes the Read operation.
    /// 執行 Read 作業。
    /// </summary>
    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count)
    {
        if (buffer is null)
            throw new ArgumentNullException(nameof(buffer));
        // 以減法形式檢查範圍，避免 offset + count 整數溢位繞過驗證
        if (offset < 0 || count < 0 || buffer.Length - offset < count)
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

        return ReadFromPrefetchLoop(buffer.AsSpan(offset, count));
    }

    /// <summary>
    /// 以預讀緩衝區為來源循序填滿目標範圍，緩衝區耗盡時同步補讀下一段資料。
    /// </summary>
    private int ReadFromPrefetchLoop(Span<byte> destination)
    {
        int totalBytesRead = 0;
        int remaining = (int)Math.Min(destination.Length, _totalLength - _currentPosition);

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
            ActiveSpan.Slice(bufferOffset, toCopy).CopyTo(destination.Slice(totalBytesRead, toCopy));

            _currentPosition += toCopy;
            totalBytesRead += toCopy;
            remaining -= toCopy;
        }

        return totalBytesRead;
    }

    /// <summary>
    /// 嘗試僅以預讀緩衝區內既有的資料完成讀取，不觸發任何磁碟 I/O。
    /// 命中時允許部分讀取（partial read），符合 Stream 讀取合約；
    /// 未命中（緩衝區無可用資料或處於後備模式）時傳回 false，由呼叫端改走執行緒集區路徑。
    /// </summary>
    private bool TryReadFromPrefetchedBuffer(Span<byte> destination, out int bytesRead)
    {
        bytesRead = 0;
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(OdfDirectIoReadableStream));

        if (_currentPosition >= _totalLength || destination.IsEmpty)
            return true;

        if (_isFallback)
            return false;

        lock (_lock)
        {
            if (_bufferStart == -1 || _currentPosition < _bufferStart || _currentPosition >= _bufferStart + _bufferLength)
                return false;

            int bufferOffset = (int)(_currentPosition - _bufferStart);
            int available = _bufferLength - bufferOffset;
            if (available <= 0)
                return false;

            int toCopy = Math.Min(destination.Length, available);
            ActiveSpan.Slice(bufferOffset, toCopy).CopyTo(destination);
            _currentPosition += toCopy;
            bytesRead = toCopy;
            return true;
        }
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

                    // 只接受實際讀到資料的預讀結果；背景工作失敗時會吞下例外並回傳 0，
                    // 若照單全收會形成「有效的空緩衝」，令 Read 回傳 0 造成提前 EOF 的
                    // 靜默資料截斷。空結果一律落回下方同步補讀，讓真正的 I/O 錯誤以例外浮現。
                    if (length > 0)
                    {
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
                }
                catch
                {
                    // 背景工作異常同樣落回同步補讀路徑；參考清理由下方 Drain 統一處理。
                }
            }

            // 丟棄不適用的預讀結果前必須先等待背景工作完成：
            // 孤兒工作（如 Seek 之後遺留者）仍可能在寫入 _backBuffer，
            // 若直接排程新預讀至同一緩衝區，將形成兩個背景寫入者的資料競爭；
            // 尾段分支的 EnsureFallbackStream 也會在其仍使用檔案控制代碼時將其釋放。
            DrainPendingPrefetchLocked();

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

    /// <summary>
    /// 等待仍在執行的背景預讀完成後捨棄其結果與參考。
    /// 必須在持有 <see cref="_lock"/> 時呼叫。
    /// </summary>
    private void DrainPendingPrefetchLocked()
    {
        if (_prefetchTask is not null)
        {
            try
            {
                _prefetchTask.GetAwaiter().GetResult();
            }
            catch
            {
                // 只需確保背景工作結束，孤兒預讀的失敗結果一律丟棄。
            }
        }

        _prefetchTask = null;
        _nextPrefetchStart = -1;
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

    /// <summary>
    /// Executes the Write operation.
    /// 執行 Write 作業。
    /// </summary>
    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException(OdfKit.Compliance.OdfLocalizer.GetMessage("Err_StreamOperation_NotSupported"));

    /// <summary>
    /// Executes the ReadAsync operation.
    /// 執行 ReadAsync 作業。
    /// </summary>
    /// <inheritdoc />
    /// <remarks>
    /// Completes synchronously when the requested data is already prefetched; otherwise the blocking read is dispatched to the thread pool so the caller thread is never blocked.
    /// 當要求的資料已在預讀緩衝區內時同步完成；否則將阻塞式讀取排入執行緒集區執行，避免以同步讀取偽裝非同步而阻塞呼叫端執行緒。
    /// </remarks>
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        if (buffer is null)
            throw new ArgumentNullException(nameof(buffer));
        // 以減法形式檢查範圍，避免 offset + count 整數溢位繞過驗證
        if (offset < 0 || count < 0 || buffer.Length - offset < count)
            throw new ArgumentOutOfRangeException(nameof(count));
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<int>(cancellationToken);
        }

        try
        {
            if (TryReadFromPrefetchedBuffer(buffer.AsSpan(offset, count), out int bytesRead))
            {
                return Task.FromResult(bytesRead);
            }
        }
        catch (Exception ex)
        {
            return Task.FromException<int>(ex);
        }

        return Task.Run(() => Read(buffer, offset, count), cancellationToken);
    }

#if NET10_0_OR_GREATER
    /// <summary>
    /// Executes the Read operation.
    /// 執行 Read 作業。
    /// </summary>
    /// <inheritdoc />
    public override int Read(Span<byte> buffer)
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(OdfDirectIoReadableStream));

        if (_currentPosition >= _totalLength || buffer.IsEmpty)
            return 0;

        if (_isFallback)
        {
            lock (_lock)
            {
                _fileStream!.Seek(_currentPosition, SeekOrigin.Begin);
                int read = _fileStream.Read(buffer);
                _currentPosition += read;
                return read;
            }
        }

        return ReadFromPrefetchLoop(buffer);
    }

    /// <summary>
    /// Executes the ReadAsync operation.
    /// 執行 ReadAsync 作業。
    /// </summary>
    /// <inheritdoc />
    /// <remarks>
    /// Completes synchronously when the requested data is already prefetched; otherwise the blocking read is dispatched to the thread pool so the caller thread is never blocked.
    /// 當要求的資料已在預讀緩衝區內時同步完成；否則將阻塞式讀取排入執行緒集區執行，避免以同步讀取偽裝非同步而阻塞呼叫端執行緒。
    /// </remarks>
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return ValueTask.FromCanceled<int>(cancellationToken);
        }

        try
        {
            if (TryReadFromPrefetchedBuffer(buffer.Span, out int bytesRead))
            {
                return new ValueTask<int>(bytesRead);
            }
        }
        catch (Exception ex)
        {
            return ValueTask.FromException<int>(ex);
        }

        return new ValueTask<int>(Task.Run(() => Read(buffer.Span), cancellationToken));
    }
#endif

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
            // 先標記 _isDisposed 並取出可能仍在執行的背景預讀工作參考，
            // 再於鎖外等待其完成，才能釋放 _fileHandle／原生緩衝區；
            // 否則背景執行緒可能仍在對已釋放的原生控制代碼呼叫
            // RandomAccess.Read，屬未定義行為的資源生命週期競爭。
            Task<(long start, int length)>? pendingPrefetch;
            lock (_lock)
            {
                _isDisposed = true;
                pendingPrefetch = _prefetchTask;
                _prefetchTask = null;
            }

            if (pendingPrefetch is not null)
            {
                try
                {
                    pendingPrefetch.GetAwaiter().GetResult();
                }
                catch
                {
                    // 忽略釋放前尚未完成之背景預讀工作的例外，不影響 Dispose 流程。
                }
            }

#if NET10_0_OR_GREATER
            _fileHandle?.Dispose();
            ((IDisposable)_bufferA).Dispose();
            ((IDisposable)_bufferB).Dispose();
#endif
            _fileStream?.Dispose();
            _fileStream = null;
        }

        _isDisposed = true;
        base.Dispose(disposing);
    }
}

