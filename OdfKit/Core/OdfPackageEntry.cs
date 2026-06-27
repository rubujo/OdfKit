using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using OdfKit.DOM;

using OdfKit.Compliance;
namespace OdfKit.Core;


internal class OdfPackageEntry : IDisposable
{
    public string Name { get; }
    private readonly ZipArchiveEntry? _zipEntry;
    private readonly OdfMmfEntryInfo? _mmfEntry;
    private readonly OdfPackage? _package;
    private byte[]? _bytes;
    private Stream? _stream;
    private bool _isModified;
    public bool IsCompressed { get; set; } = true;
    public OdfEncryptionInfo? EncryptionInfo { get; set; }
    private Task? _prefetchTask;
    internal OdfMmfEntryInfo? MmfEntry => _mmfEntry;

    private System.IO.MemoryMappedFiles.MemoryMappedViewAccessor? _viewAccessor;
    private unsafe byte* _viewPointer;

    private bool? _wasStoredInZip;
    public bool WasStoredInZip
    {
        get
        {
            if (_wasStoredInZip.HasValue)
                return _wasStoredInZip.Value;
            if (_zipEntry == null)
            {
                return !IsCompressed;
            }
            try
            {
#if NET10_0_OR_GREATER
                return _zipEntry.CompressedLength == _zipEntry.Length;
#else
                var fieldInfo = typeof(ZipArchiveEntry).GetField("_compressionMethod", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?? typeof(ZipArchiveEntry).GetField("m_compressionMethod", BindingFlags.NonPublic | BindingFlags.Instance);
                if (fieldInfo != null)
                {
                    var val = fieldInfo.GetValue(_zipEntry);
                    if (val != null)
                    {
                        int intVal = Convert.ToInt32(val);
                        return intVal == 0; // 0 is Stored
                    }
                }
                else
                {
                    OdfKitDiagnostics.Warn($"[OdfPackage] 無法反射取得 ZipArchiveEntry 壓縮方式欄位 ( .NET {Environment.Version} )；讀取時將以 CompressedLength == Length 作為判斷基準。");
                }
#endif
            }
            catch
            {
                // 後備方案
            }
            return _zipEntry.CompressedLength == _zipEntry.Length;
        }
        internal set => _wasStoredInZip = value;
    }

    public void SetContent(byte[] bytes)
    {
        _bytes = bytes;
        _stream = null;
        _isModified = true;
    }

    public long GetEstimatedSize()
    {
        if (_bytes != null)
            return _bytes.Length;
        if (_stream != null && _stream.CanSeek)
            return _stream.Length;
        if (_mmfEntry != null)
            return _mmfEntry.UncompressedSize;
        if (_zipEntry != null)
            return _zipEntry.Length;
        return 0;
    }

    public OdfPackageEntry(string name, ZipArchiveEntry zipEntry)
    {
        Name = name;
        _zipEntry = zipEntry;
    }

    public OdfPackageEntry(string name, byte[] bytes)
    {
        Name = name;
        _bytes = bytes;
        _isModified = true;
    }

    public OdfPackageEntry(string name, OdfMmfEntryInfo mmfEntry, OdfPackage package)
    {
        Name = name;
        _mmfEntry = mmfEntry;
        _package = package;
    }

    public OdfPackageEntry(string name, Stream stream)
    {
        Name = name;
        _stream = stream;
        _isModified = true;
    }

    internal bool IsModifiedForZipAppend => _isModified || _mmfEntry == null;

    internal bool IsModified => _isModified;

    /// <summary>
    /// 平行化啟動背景預載，讀取 entry 資料並快取至記憶體中。
    /// </summary>
    public void Prefetch()
    {
        if (!string.IsNullOrEmpty(Name) && (Name.EndsWith("/") || Name.EndsWith("\\")))
        {
            _bytes = Array.Empty<byte>();
            return;
        }

        if (_bytes != null || _stream != null)
        {
            return;
        }

        if (_prefetchTask == null)
        {
#if NET10_0_OR_GREATER
            if (_package?._prefetchChannel != null)
            {
                _prefetchTask = _package._prefetchChannel.Writer.WriteAsync(this).AsTask();
            }
            else
#endif
            {
                _prefetchTask = Task.Run(() =>
                {
                    try
                    {
                        EnsureBytesLoaded();
                    }
                    catch
                    {
                        // 預載階段異常留待讀取時拋出
                    }
                });
            }
        }
    }

    /// <summary>
    /// 非同步等待背景預載任務完成。
    /// </summary>
    public async Task PrefetchAsync(CancellationToken cancellationToken)
    {
        Prefetch();
        if (_prefetchTask != null)
        {
            if (cancellationToken.CanBeCanceled)
            {
                var tcs = new TaskCompletionSource<bool>();
                using (cancellationToken.Register(() => tcs.TrySetResult(true)))
                {
                    if (await Task.WhenAny(_prefetchTask, tcs.Task).ConfigureAwait(false) == tcs.Task)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                    }
                }
            }
            await _prefetchTask.ConfigureAwait(false);
        }
    }

    internal void EnsureBytesLoaded()
    {
        if (_bytes != null)
            return;

        if (!string.IsNullOrEmpty(Name) && (Name.EndsWith("/") || Name.EndsWith("\\")))
        {
            _bytes = Array.Empty<byte>();
            return;
        }

        if (_stream != null)
        {
            if (_stream.CanSeek)
            {
                _stream.Position = 0;
            }
            using var ms = new MemoryStream();
            _stream.CopyTo(ms);
            _bytes = ms.ToArray();
            return;
        }

        if (_mmfEntry != null && _package != null && _package.Mmf != null)
        {
            try
            {
                using var stream = _mmfEntry.OpenStream(_package.Mmf);
                using var ms = new MemoryStream();
                stream.CopyTo(ms);
                _bytes = ms.ToArray();
            }
            catch (Exception ex)
            {
                throw new InvalidDataException($"[{Name}] {ex.Message}", ex);
            }
            return;
        }

        if (_zipEntry != null)
        {
            lock (_zipEntry.Archive)
            {
#if NET10_0_OR_GREATER
                uint expectedCrc = (uint)_zipEntry.Crc32;
#else
                uint expectedCrc = 0;
                var crcField = typeof(ZipArchiveEntry).GetField("_crc32", BindingFlags.NonPublic | BindingFlags.Instance)
                               ?? typeof(ZipArchiveEntry).GetField("crc32", BindingFlags.NonPublic | BindingFlags.Instance);
                if (crcField != null)
                {
                    expectedCrc = Convert.ToUInt32(crcField.GetValue(_zipEntry));
                }
#endif
                try
                {
                    using var zipStream = new OdfCrc32Stream(_zipEntry.Open(), expectedCrc);
                    using var ms = new MemoryStream();
                    zipStream.CopyTo(ms);
                    _bytes = ms.ToArray();
                }
                catch (InvalidDataException ex)
                {
                    throw new InvalidDataException($"[{Name}] {ex.Message}", ex);
                }
            }
            return;
        }

        throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_OdfPackageEntry_InvalidOdfpackageentryState"));
    }

    public Stream OpenReader()
    {
        _prefetchTask?.GetAwaiter().GetResult();

        if (_bytes != null)
        {
            return new MemoryStream(_bytes, false);
        }

        if (!string.IsNullOrEmpty(Name) && (Name.EndsWith("/") || Name.EndsWith("\\")))
        {
            _bytes = Array.Empty<byte>();
            return new MemoryStream(_bytes, false);
        }

        if (_stream != null)
        {
            if (_stream.CanSeek)
            {
                _stream.Position = 0;
            }
            return _stream;
        }

        if (_mmfEntry != null && _package != null && _package.Mmf != null)
        {
            try
            {
                return _mmfEntry.OpenStream(_package.Mmf);
            }
            catch (Exception ex)
            {
                throw new InvalidDataException($"[{Name}] {ex.Message}", ex);
            }
        }

        if (_zipEntry != null)
        {
            EnsureBytesLoaded();
            if (_bytes != null)
            {
                return new MemoryStream(_bytes, false);
            }
        }

        throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_OdfPackageEntry_InvalidOdfpackageentryState"));
    }

    internal ReadOnlySpan<byte> GetBytesSpan()
    {
        _prefetchTask?.GetAwaiter().GetResult();
        EnsureBytesLoaded();
        return _bytes ?? [];
    }

    internal byte[]? GetCachedBytes()
    {
        _prefetchTask?.GetAwaiter().GetResult();
        EnsureBytesLoaded();
        return _bytes;
    }

    public bool CanExposeMmfPointer => _mmfEntry != null && _mmfEntry.CompressionMethod == 0 && _package != null && _package.Mmf != null;

    /// <summary>
    /// 取得 entry 在記憶體映射檔案 (MMF) 中的直接唯讀記憶體指標。
    /// </summary>
    public unsafe IntPtr GetMmfPointer(out int length)
    {
        length = 0;
        var mmfEntry = _mmfEntry;
        var package = _package;
        if (mmfEntry == null || mmfEntry.CompressionMethod != 0 || package == null || package.Mmf == null)
            return IntPtr.Zero;

        if (_viewAccessor == null)
        {
            _viewAccessor = package.Mmf.CreateViewAccessor(mmfEntry.CompressedDataOffset, mmfEntry.UncompressedSize, System.IO.MemoryMappedFiles.MemoryMappedFileAccess.Read);
            _viewAccessor.SafeMemoryMappedViewHandle.AcquirePointer(ref _viewPointer);
        }

        length = (int)mmfEntry.UncompressedSize;
        return (IntPtr)(_viewPointer + _viewAccessor.PointerOffset);
    }

    internal void ReleaseMmfView()
    {
        if (_viewAccessor != null)
        {
            _viewAccessor.SafeMemoryMappedViewHandle.ReleasePointer();
            _viewAccessor.Dispose();
            _viewAccessor = null;
        }
    }

    public void Dispose()
    {
        _stream?.Dispose();
        if (_viewAccessor != null)
        {
            _viewAccessor.SafeMemoryMappedViewHandle.ReleasePointer();
            _viewAccessor.Dispose();
            _viewAccessor = null;
        }
    }
}

internal class PeekableStream : Stream
{
    private readonly Stream _underlying;
    private readonly byte[] _peekBuffer;
    private readonly int _peekedCount;
    private readonly bool _leaveOpen;
    private int _peekPosition;

    public PeekableStream(Stream underlying, byte[] peekBuffer, int peekedCount, bool leaveOpen)
    {
        _underlying = underlying ?? throw new ArgumentNullException(nameof(underlying));
        _peekBuffer = peekBuffer ?? throw new ArgumentNullException(nameof(peekBuffer));
        _peekedCount = peekedCount;
        _leaveOpen = leaveOpen;
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush() => _underlying.Flush();

    public override int Read(byte[] buffer, int offset, int count)
    {
        int bytesRead = 0;
        if (_peekPosition < _peekedCount)
        {
            int available = _peekedCount - _peekPosition;
            int toCopy = Math.Min(available, count);
            Array.Copy(_peekBuffer, _peekPosition, buffer, offset, toCopy);
            _peekPosition += toCopy;
            offset += toCopy;
            count -= toCopy;
            bytesRead += toCopy;
        }

        if (count > 0)
        {
            bytesRead += _underlying.Read(buffer, offset, count);
        }

        return bytesRead;
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_leaveOpen)
        {
            _underlying.Dispose();
        }
        base.Dispose(disposing);
    }
}

