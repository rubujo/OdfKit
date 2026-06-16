using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace OdfKit.Styles;

internal static class TtfFontNameReader
{
    public static List<string> GetFontNames(string filePath)
    {
        List<string> names = [];
        try
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var reader = new BinaryReader(fs);

            uint sfntVersion = ReadUInt32BE(reader);
            if (sfntVersion != 0x00010000 && sfntVersion != 0x74727565 && sfntVersion != 0x4F54544F) // 1.0, true, OTTO
            {
                // 檢查是否為 TrueType Collection (TTC)
                if (sfntVersion == 0x74746366) // 'ttcf'
                {
                    fs.Position = 8; // 跳過版本
                    uint numFonts = ReadUInt32BE(reader);
                    if (numFonts > 256)
                        return names; // 防禦性上限：真實 TTC 最多不超過 256 字型
                    List<uint> offsets = [];
                    for (int f = 0; f < numFonts; f++)
                    {
                        offsets.Add(ReadUInt32BE(reader));
                    }
                    foreach (var offset in offsets)
                    {
                        fs.Position = offset;
                        names.AddRange(ReadSingleTtfNames(reader));
                    }
                }
                return names;
            }

            fs.Position = 0;
            names.AddRange(ReadSingleTtfNames(reader));
        }
        catch
        {
            // 壓制損毀檔案的錯誤
        }
        return names;
    }

    private static List<string> ReadSingleTtfNames(BinaryReader reader)
    {
        List<string> names = [];
        var fs = reader.BaseStream;
        long fontStart = fs.Position;

        fs.Position = fontStart + 4;
        ushort numTables = ReadUInt16BE(reader);
        fs.Position = fontStart + 12;

        uint nameTableOffset = 0;
        uint nameTableLength = 0;

        for (int i = 0; i < numTables; i++)
        {
            uint tag = reader.ReadUInt32();
            uint checkSum = reader.ReadUInt32();
            uint offset = ReadUInt32BE(reader);
            uint length = ReadUInt32BE(reader);

            byte[] tagBytes = BitConverter.GetBytes(tag);
            string tagStr = Encoding.ASCII.GetString(tagBytes);
            if (tagStr == "eman" || tagStr == "name")
            {
                nameTableOffset = offset;
                nameTableLength = length;
                break;
            }
        }

        if (nameTableOffset == 0 || nameTableLength == 0)
            return names;

        fs.Position = nameTableOffset;
        ushort format = ReadUInt16BE(reader);
        ushort count = ReadUInt16BE(reader);
        ushort stringOffset = ReadUInt16BE(reader);

        long stringStorageOffset = nameTableOffset + stringOffset;

        for (int i = 0; i < count; i++)
        {
            ushort platformId = ReadUInt16BE(reader);
            ushort encodingId = ReadUInt16BE(reader);
            ushort languageId = ReadUInt16BE(reader);
            ushort nameId = ReadUInt16BE(reader);
            ushort length = ReadUInt16BE(reader);
            ushort offset = ReadUInt16BE(reader);

            // Name ID 1 = 字型家族名稱，Name ID 4 = 完整字型名稱
            if (nameId == 1 || nameId == 4)
            {
                long returnPos = fs.Position;
                fs.Position = stringStorageOffset + offset;
                byte[] bytes = reader.ReadBytes(length);
                fs.Position = returnPos;

                string fontName;
                if (platformId == 3 || platformId == 0) // Windows 或 Unicode (UTF-16BE)
                {
                    fontName = Encoding.BigEndianUnicode.GetString(bytes);
                }
                else // Macintosh (MacRoman/ASCII)
                {
                    fontName = Encoding.ASCII.GetString(bytes);
                }

                fontName = fontName.Trim('\0', ' ', '\t', '\r', '\n');
                if (!string.IsNullOrEmpty(fontName) && !names.Contains(fontName))
                {
                    names.Add(fontName);
                }
            }
        }

        return names;
    }

    private static ushort ReadUInt16BE(BinaryReader reader)
    {
        byte[] bytes = reader.ReadBytes(2);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        return BitConverter.ToUInt16(bytes, 0);
    }

    private static uint ReadUInt32BE(BinaryReader reader)
    {
        byte[] bytes = reader.ReadBytes(4);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        return BitConverter.ToUInt32(bytes, 0);
    }
}
