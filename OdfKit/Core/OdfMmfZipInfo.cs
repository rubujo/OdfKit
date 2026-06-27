using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.IO.MemoryMappedFiles;
using System.Text;

namespace OdfKit.Core;

/// <summary>
/// 表示記憶體映射檔案（Memory - Mapped File）中 ZIP 專案的二進位區段與描述資訊。
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
        if (CompressionMethod == 0)
        {
            var viewStream = mmf.CreateViewStream(CompressedDataOffset, CompressedSize, MemoryMappedFileAccess.Read);
            return new OdfCrc32Stream(viewStream, Crc32);
        }
        else
        {
            var viewStream = mmf.CreateViewStream(CompressedDataOffset, CompressedSize, MemoryMappedFileAccess.Read);
            var deflateStream = new DeflateStream(viewStream, CompressionMode.Decompress);
            return new OdfCrc32Stream(deflateStream, Crc32);
        }
    }
}

/// <summary>
/// 用於解析 ZIP 檔案中央目錄（Central Directory）以取得各專案偏移量與大小的快速二進位解析器。
/// </summary>
internal static class OdfZipDirectoryParser
{
    /// <summary>
    /// 解析指定資料流中的中央目錄，並傳回各專案的映射資訊。
    /// </summary>
    public static Dictionary<string, OdfMmfEntryInfo>? ParseCentralDirectory(Stream stream)
    {
        try
        {
            long eocdOffset = FindEocdOffset(stream);
            if (eocdOffset < 0)
                return null;

            var entries = new Dictionary<string, OdfMmfEntryInfo>(StringComparer.Ordinal);
            using (var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true))
            {
                stream.Position = eocdOffset + 10;
                ushort totalRecords = reader.ReadUInt16();
                uint cdSize = reader.ReadUInt32();
                uint cdOffset = reader.ReadUInt32();

                // 前往中央目錄起點
                stream.Position = cdOffset;
                for (int i = 0; i < totalRecords; i++)
                {
                    if (stream.Position + 46 > stream.Length)
                        break;

                    uint signature = reader.ReadUInt32();
                    if (signature != 0x02014b50)
                        break;

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

                    if (stream.Position + fileNameLength + extraFieldLength + commentLength > stream.Length)
                        break;

                    byte[] fileNameBytes = reader.ReadBytes(fileNameLength);
                    string fileName = Encoding.UTF8.GetString(fileNameBytes);

                    stream.Position += extraFieldLength + commentLength;

                    // 解析 Local File Header 以決定實際的壓縮資料起始偏移量
                    long savedPos = stream.Position;
                    stream.Position = localHeaderOffset;
                    if (stream.Position + 30 <= stream.Length)
                    {
                        uint lfhSig = reader.ReadUInt32();
                        if (lfhSig == 0x04034b50)
                        {
                            stream.Position += 22; // 跳過屬性欄位
                            ushort lfhNameLen = reader.ReadUInt16();
                            ushort lfhExtraLen = reader.ReadUInt16();
                            long dataOffset = localHeaderOffset + 30 + lfhNameLen + lfhExtraLen;

                            string sanitized = OdfPackage.SanitizeEntryName(fileName);
                            entries[sanitized] = new OdfMmfEntryInfo(sanitized, dataOffset, compressedSize, uncompressedSize, compressionMethod, crc32, localHeaderOffset, flags, timeDate);
                        }
                    }
                    stream.Position = savedPos;
                }
            }
            return entries;
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
