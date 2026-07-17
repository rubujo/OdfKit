using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using OdfKit.Compliance;

namespace OdfKit.WebFonts.OpenType;

internal static class WebFontWriters
{
    private static readonly string[] Woff2KnownTags =
    [
        "cmap", "head", "hhea", "hmtx", "maxp", "name", "OS/2", "post",
        "cvt ", "fpgm", "glyf", "loca", "prep", "CFF ", "VORG", "EBDT",
        "EBLC", "gasp", "hdmx", "kern", "LTSH", "PCLT", "VDMX", "vhea",
        "vmtx", "BASE", "GDEF", "GPOS", "GSUB", "EBSC", "JSTF", "MATH",
        "CBDT", "CBLC", "COLR", "CPAL", "SVG ", "sbix", "acnt", "avar",
        "bdat", "bloc", "bsln", "cvar", "fdsc", "feat", "fmtx", "fvar",
        "gvar", "hsty", "just", "lcar", "mort", "morx", "opbd", "prop",
        "trak", "Zapf", "Silf", "Glat", "Gloc", "Feat", "Sill"
    ];

    internal static byte[] Write(SfntSubset subset, WebFontFormat format)
        => format switch
        {
            WebFontFormat.TrueType or WebFontFormat.OpenType => WriteTrueType(subset),
            WebFontFormat.Woff => WriteWoff(subset),
#if NET10_0_OR_GREATER
            WebFontFormat.Woff2 => WriteWoff2(subset),
#else
            WebFontFormat.Woff2 => throw new NotSupportedException(OdfLocalizer.GetMessage("Err_WebFont_DataInvalid")),
#endif
            _ => throw new NotSupportedException(OdfLocalizer.GetMessage("Err_WebFont_DataInvalid"))
        };

    internal static byte[] WriteTrueType(SfntSubset subset)
    {
        KeyValuePair<string, byte[]>[] tables = subset.Tables.ToArray();
        int directoryLength = checked(12 + (tables.Length * 16));
        int offset = Align4(directoryLength);
        var records = new List<TableOutputRecord>(tables.Length);
        foreach (KeyValuePair<string, byte[]> table in tables)
        {
            records.Add(new TableOutputRecord(
                table.Key,
                SfntFont.CalculateTableChecksum(table.Key, table.Value),
                offset,
                table.Value));
            offset = checked(offset + Align4(table.Value.Length));
        }

        var output = new byte[offset];
        WriteSfntHeader(output, subset.Flavor, checked((ushort)tables.Length));
        for (int index = 0; index < records.Count; index++)
        {
            int recordOffset = 12 + (index * 16);
            WriteTag(output, recordOffset, records[index].Tag);
            BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(recordOffset + 4, 4), records[index].Checksum);
            BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(recordOffset + 8, 4), checked((uint)records[index].Offset));
            BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(recordOffset + 12, 4), checked((uint)records[index].Data.Length));
            records[index].Data.CopyTo(output, records[index].Offset);
        }

        TableOutputRecord head = records.Single(record => record.Tag == "head");
        output.AsSpan(head.Offset + 8, 4).Clear();
        uint checksum = CalculateFontChecksum(output);
        uint adjustment = unchecked(0xB1B0AFBAu - checksum);
        BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(head.Offset + 8, 4), adjustment);
        return output;
    }

    internal static byte[] WriteWoff(SfntSubset subset)
    {
        KeyValuePair<string, byte[]>[] tables = subset.Tables.ToArray();
        byte[] sfnt = WriteTrueType(subset);
        int offset = Align4(checked(44 + (tables.Length * 20)));
        var records = new List<TableOutputRecord>(tables.Length);
        foreach (KeyValuePair<string, byte[]> table in tables)
        {
            byte[] compressed = CompressZlib(table.Value);
            byte[] stored = compressed.Length < table.Value.Length ? compressed : table.Value;
            records.Add(new TableOutputRecord(
                table.Key,
                SfntFont.CalculateTableChecksum(table.Key, table.Value),
                offset,
                stored,
                table.Value.Length));
            offset = checked(offset + Align4(stored.Length));
        }

        var output = new byte[offset];
        WriteTag(output, 0, "wOFF");
        BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(4, 4), subset.Flavor);
        BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(8, 4), checked((uint)output.Length));
        BinaryPrimitives.WriteUInt16BigEndian(output.AsSpan(12, 2), checked((ushort)tables.Length));
        BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(16, 4), checked((uint)sfnt.Length));
        for (int index = 0; index < records.Count; index++)
        {
            int recordOffset = 44 + (index * 20);
            WriteTag(output, recordOffset, records[index].Tag);
            BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(recordOffset + 4, 4), checked((uint)records[index].Offset));
            BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(recordOffset + 8, 4), checked((uint)records[index].Data.Length));
            BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(recordOffset + 12, 4), checked((uint)records[index].OriginalLength));
            BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(recordOffset + 16, 4), records[index].Checksum);
            records[index].Data.CopyTo(output, records[index].Offset);
        }

        return output;
    }

    internal static byte[] CompressZlib(ReadOnlySpan<byte> input)
    {
        using var output = new MemoryStream();
        output.WriteByte(0x78);
        output.WriteByte(0x9C);
        using (var compressor = new DeflateStream(output, CompressionLevel.Optimal, leaveOpen: true))
        {
#if NET10_0_OR_GREATER
            compressor.Write(input);
#else
            byte[] buffer = input.ToArray();
            compressor.Write(buffer, 0, buffer.Length);
#endif
        }

        uint adler32 = CalculateAdler32(input);
        output.WriteByte(unchecked((byte)(adler32 >> 24)));
        output.WriteByte(unchecked((byte)(adler32 >> 16)));
        output.WriteByte(unchecked((byte)(adler32 >> 8)));
        output.WriteByte(unchecked((byte)adler32));
        return output.ToArray();
    }

    internal static byte[] DecompressZlib(ReadOnlySpan<byte> input, int expectedLength)
    {
        if (input.Length < 6
            || expectedLength < 0
            || (input[0] & 0x0F) != 8
            || ((input[0] << 8) | input[1]) % 31 != 0
            || (input[1] & 0x20) != 0)
        {
            throw SfntFont.DataInvalid("zlib-header");
        }

        byte[] payload = input.Slice(2, input.Length - 6).ToArray();
        using var compressed = new MemoryStream(payload, writable: false);
        using var decompressor = new DeflateStream(compressed, CompressionMode.Decompress);
        var result = new byte[expectedLength];
        int read = 0;
        while (read < result.Length)
        {
            int count = decompressor.Read(result, read, result.Length - read);
            if (count == 0)
            {
                throw SfntFont.DataInvalid("zlib-truncated");
            }

            read += count;
        }

        if (decompressor.ReadByte() != -1)
        {
            throw SfntFont.DataInvalid("zlib-expanded-size");
        }

        uint expectedAdler32 = BinaryPrimitives.ReadUInt32BigEndian(input.Slice(input.Length - 4, 4));
        if (CalculateAdler32(result) != expectedAdler32)
        {
            throw SfntFont.DataInvalid("zlib-adler32");
        }

        return result;
    }

    private static uint CalculateAdler32(ReadOnlySpan<byte> input)
    {
        const uint modulus = 65521;
        uint first = 1;
        uint second = 0;
        foreach (byte value in input)
        {
            first = (first + value) % modulus;
            second = (second + first) % modulus;
        }

        return (second << 16) | first;
    }

#if NET10_0_OR_GREATER
    internal static byte[] WriteWoff2(SfntSubset subset)
    {
        KeyValuePair<string, byte[]>[] tables = OrderWoff2Tables(subset.Tables);
        byte[] sfnt = WriteTrueType(subset);
        using var directoryStream = new MemoryStream();
        using var tableStream = new MemoryStream();
        foreach (KeyValuePair<string, byte[]> table in tables)
        {
            int tagIndex = Array.IndexOf(Woff2KnownTags, table.Key);
            bool customTag = tagIndex < 0;
            int transformVersion = table.Key is "glyf" or "loca" ? 3 : 0;
            int flags = (transformVersion << 6) | (customTag ? 63 : tagIndex);
            directoryStream.WriteByte(checked((byte)flags));
            if (customTag)
            {
                byte[] tag = Encoding.ASCII.GetBytes(table.Key);
                directoryStream.Write(tag, 0, tag.Length);
            }

            WriteUIntBase128(directoryStream, checked((uint)table.Value.Length));
            tableStream.Write(table.Value, 0, table.Value.Length);
        }

        byte[] uncompressed = tableStream.ToArray();
        int maximumLength = BrotliEncoder.GetMaxCompressedLength(uncompressed.Length);
        var compressedBuffer = new byte[maximumLength];
        if (!BrotliEncoder.TryCompress(
                uncompressed,
                compressedBuffer,
                out int compressedLength,
                quality: 11,
                window: 22))
        {
            throw new InvalidDataException(OdfLocalizer.GetMessage("Err_WebFont_DataInvalid"));
        }

        byte[] directory = directoryStream.ToArray();
        int compressedEnd = checked(48 + directory.Length + compressedLength);
        int length = Align4(compressedEnd);
        var output = new byte[length];
        WriteTag(output, 0, "wOF2");
        BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(4, 4), subset.Flavor);
        BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(8, 4), checked((uint)length));
        BinaryPrimitives.WriteUInt16BigEndian(output.AsSpan(12, 2), checked((ushort)tables.Length));
        BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(16, 4), checked((uint)sfnt.Length));
        BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(20, 4), checked((uint)compressedLength));
        directory.CopyTo(output, 48);
        compressedBuffer.AsSpan(0, compressedLength).CopyTo(output.AsSpan(48 + directory.Length));
        return output;
    }

    private static KeyValuePair<string, byte[]>[] OrderWoff2Tables(
        IReadOnlyDictionary<string, byte[]> tables)
    {
        var ordered = new List<KeyValuePair<string, byte[]>>(tables.Count);
        foreach (KeyValuePair<string, byte[]> table in tables)
        {
            if (table.Key == "loca")
            {
                continue;
            }

            ordered.Add(table);
            if (table.Key == "glyf" && tables.TryGetValue("loca", out byte[]? loca))
            {
                ordered.Add(new KeyValuePair<string, byte[]>("loca", loca));
            }
        }

        return ordered.ToArray();
    }
#endif

    private static void WriteSfntHeader(Span<byte> output, uint flavor, ushort tableCount)
    {
        BinaryPrimitives.WriteUInt32BigEndian(output.Slice(0, 4), flavor);
        BinaryPrimitives.WriteUInt16BigEndian(output.Slice(4, 2), tableCount);
        ushort maximumPower = HighestPowerOfTwo(tableCount);
        BinaryPrimitives.WriteUInt16BigEndian(output.Slice(6, 2), checked((ushort)(maximumPower * 16)));
        BinaryPrimitives.WriteUInt16BigEndian(output.Slice(8, 2), Log2(maximumPower));
        BinaryPrimitives.WriteUInt16BigEndian(output.Slice(10, 2), checked((ushort)((tableCount * 16) - (maximumPower * 16))));
    }

    private static uint CalculateFontChecksum(ReadOnlySpan<byte> bytes)
    {
        uint sum = 0;
        Span<byte> word = stackalloc byte[4];
        for (int offset = 0; offset < bytes.Length; offset += 4)
        {
            word.Clear();
            int count = Math.Min(4, bytes.Length - offset);
            bytes.Slice(offset, count).CopyTo(word);
            sum = unchecked(sum + BinaryPrimitives.ReadUInt32BigEndian(word));
        }

        return sum;
    }

#if NET10_0_OR_GREATER
    private static void WriteUIntBase128(Stream stream, uint value)
    {
        Span<byte> buffer = stackalloc byte[5];
        int count = 0;
        do
        {
            buffer[count++] = (byte)(value & 0x7F);
            value >>= 7;
        }
        while (value != 0);

        for (int index = count - 1; index >= 0; index--)
        {
            byte current = buffer[index];
            if (index != 0)
            {
                current |= 0x80;
            }

            stream.WriteByte(current);
        }
    }
#endif

    private static void WriteTag(Span<byte> output, int offset, string tag)
    {
        if (tag.Length != 4)
        {
            throw new InvalidDataException(OdfLocalizer.GetMessage("Err_WebFont_DataInvalid"));
        }

        for (int index = 0; index < 4; index++)
        {
            output[offset + index] = checked((byte)tag[index]);
        }
    }

    private static int Align4(int value)
        => checked((value + 3) & ~3);

    private static ushort HighestPowerOfTwo(ushort value)
    {
        ushort result = 1;
        while (result <= value / 2)
        {
            result *= 2;
        }

        return result;
    }

    private static ushort Log2(ushort value)
    {
        ushort result = 0;
        while (value > 1)
        {
            value /= 2;
            result++;
        }

        return result;
    }

    private sealed class TableOutputRecord
    {
        internal TableOutputRecord(
            string tag,
            uint checksum,
            int offset,
            byte[] data,
            int? originalLength = null)
        {
            Tag = tag;
            Checksum = checksum;
            Offset = offset;
            Data = data;
            OriginalLength = originalLength ?? data.Length;
        }

        internal string Tag { get; }

        internal uint Checksum { get; }

        internal int Offset { get; }

        internal byte[] Data { get; }

        internal int OriginalLength { get; }
    }
}
