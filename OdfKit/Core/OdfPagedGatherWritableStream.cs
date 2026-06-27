using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OdfKit.Core;

/// <summary>
/// 將寫入資料分割為固定大小頁面，並在檔案目標上以向量化寫入一次提交多個頁面。
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
    /// 初始化 <see cref="OdfPagedGatherWritableStream"/> 類別的新執行個體。
    /// </summary>
    /// <param name="underlyingStream">接收寫入結果的底層串流</param>
    /// <param name="pageSize">每個分頁的位元組大小，預設為 4096</param>
    /// <param name="pagesPerFlush">每次聚合刷寫的最大分頁數</param>
    /// <param name="leaveOpen">是否在處置後保持底層串流開啟</param>
    /// <exception cref="ArgumentNullException">當 <paramref name="underlyingStream"/> 為 <see langword="null"/> 時擲出</exception>
    /// <exception cref="ArgumentOutOfRangeException">當 <paramref name="pageSize"/> 或 <paramref name="pagesPerFlush"/> 小於 1 時擲出</exception>
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

    /// <inheritdoc />
    public override bool CanRead => false;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override bool CanWrite => !_isDisposed && _underlyingStream.CanWrite;

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
        ThrowIfDisposed();
        FlushPages(includeActivePage: true);
        _underlyingStream.Flush();
    }

    /// <inheritdoc />
    public override async Task FlushAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        await FlushPagesAsync(includeActivePage: true, cancellationToken).ConfigureAwait(false);
        await _underlyingStream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

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
            throw new ArgumentException(null, nameof(count));
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
