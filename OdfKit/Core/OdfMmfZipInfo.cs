using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.IO.MemoryMappedFiles;
using System.Security;
using System.Text;

using OdfKit.Compliance;

namespace OdfKit.Core;

/// <summary>
/// 表示記憶體對應檔案（Memory-Mapped File）中 ZIP 項目的二進位區段與描述資訊。
/// </summary>
internal sealed class OdfMmfEntryInfo
{
    /// <summary>
    /// 取得專案名稱。
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 取得 Local File Header 在實體檔案中的二進位偏移量。
    /// </summary>
    public long LocalHeaderOffset { get; }

    /// <summary>
    /// 取得壓縮資料在實體檔案中的二進位偏移量。
    /// </summary>
    public long CompressedDataOffset { get; }

    /// <summary>
    /// 取得壓縮後資料的大小。
    /// </summary>
    public long CompressedSize { get; }

    /// <summary>
    /// 取得解壓縮後資料的大小。
    /// </summary>
    public long UncompressedSize { get; }

    /// <summary>
    /// 取得壓縮方法。
    /// </summary>
    public ushort CompressionMethod { get; }

    /// <summary>
    /// 取得專案的 CRC-32 校驗值。
    /// </summary>
    public uint Crc32 { get; }

    /// <summary>
    /// 取得檔名與屬性旗標 (General Purpose Bit Flag)。
    /// </summary>
    public ushort Flags { get; }

    /// <summary>
    /// 取得原本的 MS-DOS 時間與日期戳記。
    /// </summary>
    public uint TimeDate { get; }

    /// <summary>
    /// 初始化 <see cref="OdfMmfEntryInfo"/> 類別的新執行個體。
    /// </summary>
    public OdfMmfEntryInfo(string name, long dataOffset, long compSize, long uncompSize, ushort method, uint crc, long localHeaderOffset, ushort flags, uint timeDate)
    {
        Name = name;
        CompressedDataOffset = dataOffset;
        CompressedSize = compSize;
        UncompressedSize = uncompSize;
        CompressionMethod = method;
        Crc32 = crc;
        LocalHeaderOffset = localHeaderOffset;
        Flags = flags;
        TimeDate = timeDate;
    }

    /// <summary>
    /// 開啟唯讀的解壓縮資料流，並自動套用 CRC - 32 實時校驗。
    /// </summary>
    public Stream OpenStream(MemoryMappedFile mmf)
    {
        // 大小為 0 的項目不能建立檢視：CreateViewStream 的 size 為 0 代表「從位移到檔案結尾」，
        // 讀取空項目會讀到後面不相干的位元組。
        if (CompressedSize == 0)
        {
            return new OdfCrc32Stream(new MemoryStream([], writable: false), Crc32);
        }

        if (CompressionMethod == 0)
        {
            var viewStream = mmf.CreateViewStream(CompressedDataOffset, CompressedSize, MemoryMappedFileAccess.Read);
            return new OdfCrc32Stream(viewStream, Crc32);
        }
        else
        {
            var viewStream = mmf.CreateViewStream(CompressedDataOffset, CompressedSize, MemoryMappedFileAccess.Read);
            var deflateStream = new DeflateStream(viewStream, CompressionMode.Decompress);
            // 標頭宣告的大小不可信：以宣告值限制實際解壓量，避免謊報大小的解壓縮炸彈。
            var boundedStream = new OdfSizeLimitedReadStream(deflateStream, UncompressedSize, Name);
            return new OdfCrc32Stream(boundedStream, Crc32);
        }
    }
}

/// <summary>
/// 限制唯讀資料流可讀出的位元組數；超過限制時擲出 <see cref="SecurityException"/>（內部協作者）。
/// </summary>
internal sealed class OdfSizeLimitedReadStream(Stream inner, long limit, string entryName) : Stream
{
    private readonly Stream _inner = inner;
    private readonly long _limit = limit;
    private readonly string _entryName = entryName;
    private long _total;

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        long remaining = _limit - _total;
        if (remaining <= 0)
        {
            // 已達宣告大小：若底層仍有資料，表示標頭謊報大小。
            byte[] probe = new byte[1];
            if (_inner.Read(probe, 0, 1) > 0)
            {
                throw new SecurityException(
                    OdfLocalizer.GetMessage("Err_OdfPackage_ZipEntrySizeLimitExceeded", _entryName, _total + 1, _limit));
            }

            return 0;
        }

        int read = _inner.Read(buffer, offset, (int)Math.Min(count, remaining));
        _total += read;
        return read;
    }

    public override void Flush() { }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _inner.Dispose();
        }

        base.Dispose(disposing);
    }
}

internal sealed class OdfMmfZipDirectoryInfo(
    Dictionary<string, OdfMmfEntryInfo> entries,
    int entryCount,
    List<string> duplicateEntryNames)
{
    public Dictionary<string, OdfMmfEntryInfo> Entries { get; } = entries;

    public int EntryCount { get; } = entryCount;

    public List<string> DuplicateEntryNames { get; } = duplicateEntryNames;
}

/// <summary>
/// 用於解析 ZIP 檔案中央目錄（Central Directory）以取得各專案偏移量與大小的快速二進位解析器。
/// </summary>
internal static class OdfZipDirectoryParser
{
    /// <summary>
    /// 解析指定資料流中的中央目錄，並傳回各專案的映射資訊。
    /// </summary>
    public static OdfMmfZipDirectoryInfo? ParseCentralDirectory(Stream stream)
    {
        try
        {
            long eocdOffset = FindEocdOffset(stream);
            if (eocdOffset < 0)
                return null;

            var entries = new Dictionary<string, OdfMmfEntryInfo>(StringComparer.Ordinal);
            var duplicateEntryNames = new List<string>();
            using (var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true))
            {
                stream.Position = eocdOffset + 10;
                ushort totalRecords = reader.ReadUInt16();
                uint cdSize = reader.ReadUInt32();
                uint cdOffset = reader.ReadUInt32();

                // ZIP64（APPNOTE 4.4.1.4）：EOCD 欄位為 sentinel 時，真實值位於 ZIP64 EOCD；
                // 本快速解析器不支援，交由 ZipArchive 處理，避免誤讀。
                if (totalRecords == 0xFFFF || cdSize == 0xFFFFFFFF || cdOffset == 0xFFFFFFFF)
                    return null;

                // 前往中央目錄起點
                stream.Position = cdOffset;
                for (int i = 0; i < totalRecords; i++)
                {
                    // 任何無法完整解析的記錄都不得無聲略過：回傳 null 讓呼叫端退回 ZipArchive，
                    // 由其驗證並擲出明確例外，避免僅載入部分項目而在儲存時造成資料遺失。
                    if (stream.Position + 46 > stream.Length)
                        return null;

                    uint signature = reader.ReadUInt32();
                    if (signature != 0x02014b50)
                        return null;

                    stream.Position += 4; // 跳過版本
                    ushort flags = reader.ReadUInt16();
                    ushort compressionMethod = reader.ReadUInt16();
                    uint timeDate = reader.ReadUInt32();
                    uint crc32 = reader.ReadUInt32();
                    uint compressedSize = reader.ReadUInt32();
                    uint uncompressedSize = reader.ReadUInt32();
                    ushort fileNameLength = reader.ReadUInt16();
                    ushort extraFieldLength = reader.ReadUInt16();
                    ushort commentLength = reader.ReadUInt16();
                    stream.Position += 8; // 跳過磁碟與屬性
                    uint localHeaderOffset = reader.ReadUInt32();

                    // ZIP64 sentinel：真實大小與偏移位於 0x0001 extra field，本解析器不支援。
                    if (compressedSize == 0xFFFFFFFF || uncompressedSize == 0xFFFFFFFF || localHeaderOffset == 0xFFFFFFFF)
                        return null;

                    if (stream.Position + fileNameLength + extraFieldLength + commentLength > stream.Length)
                        return null;

                    byte[] fileNameBytes = reader.ReadBytes(fileNameLength);
                    string fileName = Encoding.UTF8.GetString(fileNameBytes);

                    stream.Position += extraFieldLength + commentLength;

                    // 解析 Local File Header 以決定實際的壓縮資料起始偏移量
                    long savedPos = stream.Position;
                    stream.Position = localHeaderOffset;
                    if (stream.Position + 30 > stream.Length)
                        return null;

                    uint lfhSig = reader.ReadUInt32();
                    if (lfhSig != 0x04034b50)
                        return null;

                    stream.Position += 22; // 跳過屬性欄位
                    ushort lfhNameLen = reader.ReadUInt16();
                    ushort lfhExtraLen = reader.ReadUInt16();
                    long dataOffset = localHeaderOffset + 30 + lfhNameLen + lfhExtraLen;

                    if (dataOffset < 0 || dataOffset + compressedSize > stream.Length)
                        return null;

                    string sanitized = OdfPackage.SanitizeEntryName(fileName);
                    if (entries.ContainsKey(sanitized))
                    {
                        duplicateEntryNames.Add(sanitized);
                    }

                    entries[sanitized] = new OdfMmfEntryInfo(sanitized, dataOffset, compressedSize, uncompressedSize, compressionMethod, crc32, localHeaderOffset, flags, timeDate);
                    stream.Position = savedPos;
                }

                return new OdfMmfZipDirectoryInfo(entries, totalRecords, duplicateEntryNames);
            }
        }
        catch
        {
            return null;
        }
    }

    internal static long FindEocdOffset(Stream stream)
    {
        long length = stream.Length;
        if (length < 22)
            return -1;

        int searchLength = (int)Math.Min(length, 65557);
        byte[] buffer = new byte[searchLength];
        stream.Position = length - searchLength;
        int read = stream.Read(buffer, 0, searchLength);

        for (int i = read - 22; i >= 0; i--)
        {
            if (buffer[i] == 0x50 && buffer[i + 1] == 0x4B && buffer[i + 2] == 0x05 && buffer[i + 3] == 0x06)
            {
                return length - searchLength + i;
            }
        }
        return -1;
    }
}
