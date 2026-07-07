using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OdfKit.Core;

/// <summary>
/// Buffers writes into fixed-size pages and flushes them as gathered write batches when possible.
/// 將寫入資料緩衝為固定大小頁面，並在可行時以聚合寫入批次刷寫。
/// </summary>
public sealed class OdfPagedGatherWritableStream : Stream
{
    private const int DefaultPageSize = 4096;
    private const int DefaultPagesPerFlush = 16;

    private readonly Stream _underlyingStream;
    private readonly int _pageSize;
    private readonly int _pagesPerFlush;
    private readonly bool _leaveOpen;
    private readonly List<PageLease> _fullPages;
    private byte[] _activePage;
    private int _activeCount;
    private bool _isDisposed;

#if NET10_0_OR_GREATER
    private readonly FileStream? _fileStream;
    private long _fileOffset;
#endif

    internal static int LastFlushPageCountForTests;

    internal static int VectoredFlushCountForTests;

    internal static int SequentialFallbackFlushCountForTests;

    internal static int RentedPageCountForTests;

    internal static int ReturnedPageCountForTests;

    /// <summary>
    /// Initializes a new instance of the <see cref="OdfPagedGatherWritableStream"/> class.
    /// 初始化 <see cref="OdfPagedGatherWritableStream"/> 類別的新執行個體。
    /// </summary>
    /// <param name="underlyingStream">The stream that receives flushed bytes. / 接收刷寫位元組的底層資料流。</param>
    /// <param name="pageSize">The byte size of each page. / 每個頁面的位元組大小。</param>
    /// <param name="pagesPerFlush">The maximum number of pages gathered into one flush. / 每次聚合刷寫的最大頁面數。</param>
    /// <param name="leaveOpen">A value indicating whether the underlying stream remains open after disposal. / 指出處置後是否保持底層資料流開啟。</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="underlyingStream"/> is <see langword="null"/>. / 當 <paramref name="underlyingStream"/> 為 <see langword="null"/> 時擲出。</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="pageSize"/> or <paramref name="pagesPerFlush"/> is less than 1. / 當 <paramref name="pageSize"/> 或 <paramref name="pagesPerFlush"/> 小於 1 時擲出。</exception>
    public OdfPagedGatherWritableStream(
        Stream underlyingStream,
        int pageSize = DefaultPageSize,
        int pagesPerFlush = DefaultPagesPerFlush,
        bool leaveOpen = false)
    {
        _underlyingStream = underlyingStream ?? throw new ArgumentNullException(nameof(underlyingStream));
        if (pageSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize));
        }

        if (pagesPerFlush <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pagesPerFlush));
        }

        _pageSize = pageSize;
        _pagesPerFlush = pagesPerFlush;
        _leaveOpen = leaveOpen;
        _fullPages = new List<PageLease>(pagesPerFlush);
        _activePage = RentPage();

#if NET10_0_OR_GREATER
        if (underlyingStream is FileStream fileStream && fileStream.CanSeek)
        {
            _fileStream = fileStream;
            _fileOffset = fileStream.Position;
        }
#endif
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
    public override bool CanWrite => !_isDisposed && _underlyingStream.CanWrite;

    /// <summary>
    /// Executes the Length operation.
    /// 執行 Length 作業。
    /// </summary>
    /// <inheritdoc />
    public override long Length => throw new NotSupportedException(OdfKit.Compliance.OdfLocalizer.GetMessage("Err_StreamOperation_NotSupported"));

    /// <summary>
    /// Gets or sets the stream position.
    /// 取得或設定資料流位置。
    /// </summary>
    /// <inheritdoc />
    public override long Position
    {
        get => throw new NotSupportedException(OdfKit.Compliance.OdfLocalizer.GetMessage("Err_StreamOperation_NotSupported"));
        set => throw new NotSupportedException(OdfKit.Compliance.OdfLocalizer.GetMessage("Err_StreamOperation_NotSupported"));
    }

    /// <summary>
    /// Executes the Flush operation.
    /// 執行 Flush 作業。
    /// </summary>
    /// <inheritdoc />
    public override void Flush()
    {
        ThrowIfDisposed();
        FlushPages(includeActivePage: true);
        _underlyingStream.Flush();
    }

    /// <summary>
    /// Executes the FlushAsync operation.
    /// 執行 FlushAsync 作業。
    /// </summary>
    /// <inheritdoc />
    public override async Task FlushAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        await FlushPagesAsync(includeActivePage: true, cancellationToken).ConfigureAwait(false);
        await _underlyingStream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

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
    /// Executes the Read operation.
    /// 執行 Read 作業。
    /// </summary>
    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException(OdfKit.Compliance.OdfLocalizer.GetMessage("Err_StreamOperation_NotSupported"));

    /// <summary>
    /// Executes the Write operation.
    /// 執行 Write 作業。
    /// </summary>
    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count)
    {
        ValidateWrite(buffer, offset, count);
        int written = 0;
        while (written < count)
        {
            int toCopy = Math.Min(_pageSize - _activeCount, count - written);
            Buffer.BlockCopy(buffer, offset + written, _activePage, _activeCount, toCopy);
            _activeCount += toCopy;
            written += toCopy;

            if (_activeCount == _pageSize)
            {
                CommitActivePage();
                if (_fullPages.Count == _pagesPerFlush)
                {
                    FlushPages(includeActivePage: false);
                }
            }
        }
    }

    /// <summary>
    /// Executes the WriteAsync operation.
    /// 執行 WriteAsync 作業。
    /// </summary>
    /// <inheritdoc />
    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        ValidateWrite(buffer, offset, count);
        int written = 0;
        while (written < count)
        {
            int toCopy = Math.Min(_pageSize - _activeCount, count - written);
            Buffer.BlockCopy(buffer, offset + written, _activePage, _activeCount, toCopy);
            _activeCount += toCopy;
            written += toCopy;

            if (_activeCount == _pageSize)
            {
                CommitActivePage();
                if (_fullPages.Count == _pagesPerFlush)
                {
                    await FlushPagesAsync(includeActivePage: false, cancellationToken).ConfigureAwait(false);
                }
            }
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
        {
            base.Dispose(disposing);
            return;
        }

        if (disposing)
        {
            try
            {
                FlushPages(includeActivePage: true);
            }
            finally
            {
                ReturnActivePage();
                ReturnPages(_fullPages);
                if (!_leaveOpen)
                {
                    _underlyingStream.Dispose();
                }
            }
        }

        _isDisposed = true;
        base.Dispose(disposing);
    }

    private void ValidateWrite(byte[] buffer, int offset, int count)
    {
        ThrowIfDisposed();
        if (buffer is null)
        {
            throw new ArgumentNullException(nameof(buffer));
        }

        if (offset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        if (buffer.Length - offset < count)
        {
            throw new ArgumentException(
                OdfKit.Compliance.OdfLocalizer.GetMessage("Err_ArgumentOutOfRange_Count"),
                nameof(count));
        }
    }

    private void CommitActivePage()
    {
        _fullPages.Add(new PageLease(_activePage, _activeCount));
        _activePage = RentPage();
        _activeCount = 0;
    }

    private void FlushPages(bool includeActivePage)
    {
        if (includeActivePage && _activeCount > 0)
        {
            CommitActivePage();
        }

        if (_fullPages.Count == 0)
        {
            return;
        }

        LastFlushPageCountForTests = _fullPages.Count;
#if NET10_0_OR_GREATER
        if (_fileStream is not null)
        {
            var buffers = new List<ReadOnlyMemory<byte>>(_fullPages.Count);
            foreach (PageLease page in _fullPages)
            {
                buffers.Add(page.Buffer.AsMemory(0, page.Count));
            }

            RandomAccess.Write(_fileStream.SafeFileHandle, buffers, _fileOffset);
            AdvanceFileStreamPosition();
            Interlocked.Increment(ref VectoredFlushCountForTests);
            ReturnPages(_fullPages);
            return;
        }
#endif

        foreach (PageLease page in _fullPages)
        {
            _underlyingStream.Write(page.Buffer, 0, page.Count);
        }

        Interlocked.Increment(ref SequentialFallbackFlushCountForTests);
        ReturnPages(_fullPages);
    }

    private async Task FlushPagesAsync(bool includeActivePage, CancellationToken cancellationToken)
    {
        if (includeActivePage && _activeCount > 0)
        {
            CommitActivePage();
        }

        if (_fullPages.Count == 0)
        {
            return;
        }

        LastFlushPageCountForTests = _fullPages.Count;
#if NET10_0_OR_GREATER
        if (_fileStream is not null)
        {
            var buffers = new List<ReadOnlyMemory<byte>>(_fullPages.Count);
            foreach (PageLease page in _fullPages)
            {
                buffers.Add(page.Buffer.AsMemory(0, page.Count));
            }

            await RandomAccess.WriteAsync(_fileStream.SafeFileHandle, buffers, _fileOffset, cancellationToken).ConfigureAwait(false);
            AdvanceFileStreamPosition();
            Interlocked.Increment(ref VectoredFlushCountForTests);
            ReturnPages(_fullPages);
            return;
        }
#endif

        foreach (PageLease page in _fullPages)
        {
            await _underlyingStream.WriteAsync(page.Buffer, 0, page.Count, cancellationToken).ConfigureAwait(false);
        }

        Interlocked.Increment(ref SequentialFallbackFlushCountForTests);
        ReturnPages(_fullPages);
    }

    private byte[] RentPage()
    {
        Interlocked.Increment(ref RentedPageCountForTests);
        return ArrayPool<byte>.Shared.Rent(_pageSize);
    }

    private static void ReturnPages(List<PageLease> pages)
    {
        foreach (PageLease page in pages)
        {
            ArrayPool<byte>.Shared.Return(page.Buffer);
            Interlocked.Increment(ref ReturnedPageCountForTests);
        }

        pages.Clear();
    }

    private void ReturnActivePage()
    {
        if (_activePage.Length == 0)
        {
            return;
        }

        ArrayPool<byte>.Shared.Return(_activePage);
        Interlocked.Increment(ref ReturnedPageCountForTests);
        _activePage = [];
        _activeCount = 0;
    }

#if NET10_0_OR_GREATER
    private void AdvanceFileStreamPosition()
    {
        foreach (PageLease page in _fullPages)
        {
            _fileOffset += page.Count;
        }

        _fileStream!.Position = _fileOffset;
    }
#endif

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(OdfPagedGatherWritableStream));
        }
    }

    private readonly struct PageLease(byte[] buffer, int count)
    {
        public byte[] Buffer { get; } = buffer;

        public int Count { get; } = count;
    }
}

