using System;
using System.IO;
using System.IO.Compression;
using OdfKit.DOM;

namespace OdfKit.Core;


internal class OdfPackageEntry : IDisposable
{
    public string Name { get; }
    private readonly ZipArchiveEntry? _zipEntry;
    private byte[]? _bytes;
    private Stream? _stream;
    public bool IsCompressed { get; set; } = true;
    public OdfEncryptionInfo? EncryptionInfo { get; set; }

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
                var fieldInfo = typeof(ZipArchiveEntry).GetField("_compressionMethod", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?? typeof(ZipArchiveEntry).GetField("m_compressionMethod", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
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
            }
            catch
            {
                // Fallback
            }
            return _zipEntry.CompressedLength == _zipEntry.Length;
        }
        internal set => _wasStoredInZip = value;
    }

    public void SetContent(byte[] bytes)
    {
        _bytes = bytes;
        _stream = null;
    }

    public long GetEstimatedSize()
    {
        if (_bytes != null)
            return _bytes.Length;
        if (_stream != null && _stream.CanSeek)
            return _stream.Length;
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
    }

    public OdfPackageEntry(string name, Stream stream)
    {
        Name = name;
        _stream = stream;
    }

    public Stream OpenReader()
    {
        if (_bytes != null)
        {
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

        if (_zipEntry != null)
        {
            // In ZipArchiveMode.Read, we can call Open() multiple times but it will return a new stream.
            // We cache it in memory to support multiple reads or if archive gets closed.
            using var zipStream = _zipEntry.Open();
            var ms = new MemoryStream();
            zipStream.CopyTo(ms);
            _bytes = ms.ToArray();
            return new MemoryStream(_bytes, false);
        }

        throw new InvalidOperationException("OdfPackageEntry is in an invalid state.");
    }

    public void Dispose()
    {
        _stream?.Dispose();
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

