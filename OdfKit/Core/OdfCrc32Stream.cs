using System;
using System.IO;
using OdfKit.Compliance;

namespace OdfKit.Core;

/// <summary>
/// Wraps a stream and computes a running CRC-32 checksum while bytes are read or written.
/// 包裝資料流，並在讀取或寫入位元組時即時計算累積的 CRC-32 校驗碼。
/// </summary>
public sealed class OdfCrc32Stream : Stream
{
    private readonly Stream _underlyingStream;
    private readonly uint? _expectedCrc;
    private bool _verified;

#if NET10_0_OR_GREATER
    private readonly System.IO.Hashing.Crc32 _crcInstance = new();
#else
    private uint _currentCrc = 0xFFFFFFFF;
#endif
    /// <summary>
    /// Convenience overload that uses default values for remaining parameters.
    /// 便利多載：其餘參數使用預設值並轉呼叫最長多載。
    /// </summary>
    public OdfCrc32Stream(Stream underlyingStream) : this(underlyingStream, null) { }


    /// <summary>
    /// Initializes a new instance of the <see cref="OdfCrc32Stream"/> class.
    /// 初始化 <see cref="OdfCrc32Stream"/> 類別的新執行個體。
    /// </summary>
    /// <param name="underlyingStream">The stream to wrap. / 要包裝的資料流。</param>
    /// <param name="expectedCrc">The optional expected CRC-32 value to verify at end of stream. / 選用的預期 CRC-32 值，會在讀取至結尾時驗證。</param>
    public OdfCrc32Stream(Stream underlyingStream, uint? expectedCrc)
    {
        _underlyingStream = underlyingStream ?? throw new ArgumentNullException(nameof(underlyingStream));
        _expectedCrc = expectedCrc;
    }


#if NET10_0_OR_GREATER
    /// <summary>
    /// Gets the current CRC-32 value computed so far.
    /// 取得目前為止計算出的 CRC-32 值。
    /// </summary>
    public uint Crc32 => _crcInstance.GetCurrentHashAsUInt32();
#else
    /// <summary>
    /// Gets the current CRC-32 value computed so far.
    /// 取得目前為止計算出的 CRC-32 值。
    /// </summary>
    public uint Crc32 => _currentCrc ^ 0xFFFFFFFF;
#endif

    /// <summary>
    /// Provides the CanRead member.
    /// 提供 CanRead 成員。
    /// </summary>
    /// <inheritdoc />
    public override bool CanRead => _underlyingStream.CanRead;

    /// <summary>
    /// Provides the CanSeek member.
    /// 提供 CanSeek 成員。
    /// </summary>
    /// <inheritdoc />
    public override bool CanSeek => _underlyingStream.CanSeek;

    /// <summary>
    /// Provides the CanWrite member.
    /// 提供 CanWrite 成員。
    /// </summary>
    /// <inheritdoc />
    public override bool CanWrite => _underlyingStream.CanWrite;

    /// <summary>
    /// Provides the Length member.
    /// 提供 Length 成員。
    /// </summary>
    /// <inheritdoc />
    public override long Length => _underlyingStream.Length;

    /// <summary>
    /// Gets or sets the current position of the underlying stream.
    /// 取得或設定底層資料流的目前位置。
    /// </summary>
    /// <inheritdoc />
    public override long Position
    {
        get => _underlyingStream.Position;
        set => _underlyingStream.Position = value;
    }

    /// <summary>
    /// Executes the Flush operation.
    /// 執行 Flush 作業。
    /// </summary>
    /// <inheritdoc />
    public override void Flush() => _underlyingStream.Flush();

    /// <summary>
    /// Executes the Seek operation.
    /// 執行 Seek 作業。
    /// </summary>
    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => _underlyingStream.Seek(offset, origin);

    /// <summary>
    /// Executes the SetLength operation.
    /// 執行 SetLength 作業。
    /// </summary>
    /// <inheritdoc />
    public override void SetLength(long value) => _underlyingStream.SetLength(value);

    /// <summary>
    /// Executes the Read operation.
    /// 執行 Read 作業。
    /// </summary>
    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count)
    {
        int bytesRead = _underlyingStream.Read(buffer, offset, count);
        if (bytesRead > 0)
        {
#if NET10_0_OR_GREATER
            _crcInstance.Append(new ReadOnlySpan<byte>(buffer, offset, bytesRead));
#else
            _currentCrc = OdfCrc32.Compute(_currentCrc, new ReadOnlySpan<byte>(buffer, offset, bytesRead));
#endif
        }
        else if (bytesRead == 0 && _expectedCrc.HasValue && !_verified)
        {
            VerifyCrc();
        }
        return bytesRead;
    }

    /// <summary>
    /// Executes the Write operation.
    /// 執行 Write 作業。
    /// </summary>
    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count)
    {
        _underlyingStream.Write(buffer, offset, count);
        if (count > 0)
        {
#if NET10_0_OR_GREATER
            _crcInstance.Append(new ReadOnlySpan<byte>(buffer, offset, count));
#else
            _currentCrc = OdfCrc32.Compute(_currentCrc, new ReadOnlySpan<byte>(buffer, offset, count));
#endif
        }
    }

    private void VerifyCrc()
    {
        _verified = true;
        uint finalCrc = Crc32;
        if (finalCrc != _expectedCrc!.Value)
        {
            throw new InvalidDataException(OdfLocalizer.GetMessage("Err_OdfPackage_CrcMismatch", _expectedCrc.Value.ToString("X8"), finalCrc.ToString("X8")));
        }
    }

    /// <summary>
    /// Executes the Dispose operation.
    /// 執行 Dispose 作業。
    /// </summary>
    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_expectedCrc.HasValue && !_verified)
            {
                try
                {
                    byte[] temp = new byte[4096];
                    while (Read(temp, 0, temp.Length) > 0)
                    { }
                }
                catch
                {
                    // 忽略處置期間的讀取錯誤，避免掩蓋主要異常
                }
            }
            _underlyingStream.Dispose();
        }
        base.Dispose(disposing);
    }
}
