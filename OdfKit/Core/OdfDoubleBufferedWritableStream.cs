using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OdfKit.Core;

/// <summary>
/// 唯寫的雙緩衝非同步資料流，在非同步寫入緩衝區 A 的同時，允許主執行緒繼續寫入緩衝區 B，實現 CPU 與磁碟 I/O 的流水線重疊。
/// </summary>
public sealed class OdfDoubleBufferedWritableStream : Stream
{
    private readonly Stream _underlyingStream;
    private readonly byte[] _bufferA;
    private readonly byte[] _bufferB;
    private readonly int _bufferSize;
    private byte[] _activeBuffer;
    private byte[] _backBuffer;
    private int _activeCount;
    private Task _writeTask = Task.CompletedTask;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private bool _isDisposed;

    private readonly bool _leaveOpen;

    internal static int RentedBufferCountForTests;

    internal static int ReturnedBufferCountForTests;

    /// <summary>
    /// 初始化 <see cref="OdfDoubleBufferedWritableStream"/> 類別的新執行個體。
    /// </summary>
    public OdfDoubleBufferedWritableStream(Stream underlyingStream, int bufferSize = 64 * 1024, bool leaveOpen = false)
    {
        _underlyingStream = underlyingStream ?? throw new ArgumentNullException(nameof(underlyingStream));
        if (bufferSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(bufferSize));

        _bufferSize = bufferSize;
        _bufferA = ArrayPool<byte>.Shared.Rent(bufferSize);
        _bufferB = ArrayPool<byte>.Shared.Rent(bufferSize);
        Interlocked.Add(ref RentedBufferCountForTests, 2);
        _activeBuffer = _bufferA;
        _backBuffer = _bufferB;
        _leaveOpen = leaveOpen;
    }

    /// <inheritdoc />
    public override bool CanRead => false;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override bool CanWrite => !_isDisposed;

    /// <inheritdoc />
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <inheritdoc />
    public override void Flush()
    {
        _writeTask.GetAwaiter().GetResult();
        if (_activeCount > 0)
        {
            _underlyingStream.Write(_activeBuffer, 0, _activeCount);
            _activeCount = 0;
        }
        _underlyingStream.Flush();
    }

    /// <inheritdoc />
    public override async Task FlushAsync(CancellationToken cancellationToken)
    {
        await _writeTask.ConfigureAwait(false);
        if (_activeCount > 0)
        {
            await _underlyingStream.WriteAsync(_activeBuffer, 0, _activeCount, cancellationToken).ConfigureAwait(false);
            _activeCount = 0;
        }
        await _underlyingStream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc />
    /// <remarks>
    /// Uses a fully synchronous path: buffered bytes are flushed with a direct synchronous write instead of blocking on the asynchronous pipeline.
    /// 採用純同步路徑：緩衝區滿時直接以同步寫入刷寫，不以阻塞等待非同步管線的方式偽裝同步。
    /// </remarks>
    public override void Write(byte[] buffer, int offset, int count)
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(OdfDoubleBufferedWritableStream));

        int bytesWritten = 0;
        while (bytesWritten < count)
        {
            int space = _bufferSize - _activeCount;
            int toCopy = Math.Min(space, count - bytesWritten);
            Buffer.BlockCopy(buffer, offset + bytesWritten, _activeBuffer, _activeCount, toCopy);
            _activeCount += toCopy;
            bytesWritten += toCopy;

            if (_activeCount == _bufferSize)
            {
                // 先等待仍在執行的背景寫入完成，再直接同步刷寫目前緩衝區；
                // 同步呼叫端不需要雙緩衝流水線，也避免 sync-over-async 的執行緒阻塞成本。
                _writeTask.GetAwaiter().GetResult();
                _underlyingStream.Write(_activeBuffer, 0, _activeCount);
                _activeCount = 0;
            }
        }
    }

    /// <inheritdoc />
    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(OdfDoubleBufferedWritableStream));

        int bytesWritten = 0;
        while (bytesWritten < count)
        {
            int space = _bufferSize - _activeCount;
            int toCopy = Math.Min(space, count - bytesWritten);
            Buffer.BlockCopy(buffer, offset + bytesWritten, _activeBuffer, _activeCount, toCopy);
            _activeCount += toCopy;
            bytesWritten += toCopy;

            if (_activeCount == _bufferSize)
            {
                await _writeTask.ConfigureAwait(false);
                await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

                var temp = _activeBuffer;
                _activeBuffer = _backBuffer;
                _backBuffer = temp;

                int lengthToWrite = _activeCount;
                _activeCount = 0;

                // 不將 cancellationToken 傳給 Task.Run：若權杖在背景工作排程前即被取消，
                // 工作主體（含 finally 的 Release）將永不執行，semaphore 會被永久持有，
                // 造成後續 WriteAsync 死結。取消語意仍由主體內的 WriteAsync 履行。
                _writeTask = Task.Run(async () =>
                {
                    try
                    {
                        await _underlyingStream.WriteAsync(_backBuffer, 0, lengthToWrite, cancellationToken).ConfigureAwait(false);
                    }
                    finally
                    {
                        _semaphore.Release();
                    }
                });
            }
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (!_isDisposed)
        {
            if (disposing)
            {
                try
                {
                    Flush();
                }
                catch
                {
                    // 忽略處置期間的刷寫異常
                }
                _semaphore.Dispose();
                if (!_leaveOpen)
                {
                    _underlyingStream.Dispose();
                }

                // 歸還前抹除緩衝區內容，避免文件明文殘留於共用集區被其他租用者讀到。
                ArrayPool<byte>.Shared.Return(_bufferA, clearArray: true);
                ArrayPool<byte>.Shared.Return(_bufferB, clearArray: true);
                Interlocked.Add(ref ReturnedBufferCountForTests, 2);
            }
            _isDisposed = true;
        }
        base.Dispose(disposing);
    }
}
