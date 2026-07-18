using System.Buffers.Binary;
using System.IO.Compression;
using OdfKit.WebFonts.OpenType;

namespace OdfKit.WebFonts.Tests;

public sealed class Woff2TransformDecoderTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void DecodeWoff2ReconstructsTransformedHmtx(byte transformFlags)
    {
        byte[] expected = CreateExpectedHmtx();
        byte[] woff2 = CreateWoff2(transformFlags, includeGlyf: true, appendHmtxByte: false);

        byte[] sfnt = ManagedOpenTypeWebFontVerifier.DecodeWoff2(woff2, 1024 * 1024);

        Assert.Equal(expected, ReadSfntTable(sfnt, "hmtx"));
    }

    [Fact]
    public void DecodeWoff2RejectsReservedHmtxFlags()
    {
        byte[] woff2 = CreateWoff2(0x04, includeGlyf: true, appendHmtxByte: false);

        Assert.Throws<InvalidDataException>(
            () => ManagedOpenTypeWebFontVerifier.DecodeWoff2(woff2, 1024 * 1024));
    }

    [Fact]
    public void DecodeWoff2RejectsTrailingTransformedHmtxData()
    {
        byte[] woff2 = CreateWoff2(3, includeGlyf: true, appendHmtxByte: true);

        Assert.Throws<InvalidDataException>(
            () => ManagedOpenTypeWebFontVerifier.DecodeWoff2(woff2, 1024 * 1024));
    }

    [Fact]
    public void DecodeWoff2RejectsTransformedHmtxWithoutGlyfDependencies()
    {
        byte[] woff2 = CreateWoff2(3, includeGlyf: false, appendHmtxByte: false);

        Assert.Throws<InvalidDataException>(
            () => ManagedOpenTypeWebFontVerifier.DecodeWoff2(woff2, 1024 * 1024));
    }

    [Fact]
    public void DecodeWoff2AcceptsMetadataPrivateDataAndAdvisorySfntSize()
    {
        byte[] woff2 = CreateWoff2(3, includeGlyf: true, appendHmtxByte: false);
        BinaryPrimitives.WriteUInt32BigEndian(woff2.AsSpan(16, 4), 1);
        woff2 = AppendOptionalBlocks(woff2, [1, 2, 3, 4, 5], [6, 7, 8]);

        byte[] sfnt = ManagedOpenTypeWebFontVerifier.DecodeWoff2(woff2, 1024 * 1024);

        Assert.Equal(CreateExpectedHmtx(), ReadSfntTable(sfnt, "hmtx"));
    }

    [Fact]
    public void DecodeWoff2CollectionSelectsRequestedFace()
    {
        byte[] woff2 = CreateWoff2Collection(duplicateSecondFaceTable: false);

        byte[] first = ManagedOpenTypeWebFontVerifier.DecodeWoff2(woff2, 1024 * 1024, 0);
        byte[] second = ManagedOpenTypeWebFontVerifier.DecodeWoff2(woff2, 1024 * 1024, 1);

        Assert.Equal([0, 1, 2, 3], ReadSfntTable(first, "TEST"));
        Assert.Equal([4, 5, 6, 7], ReadSfntTable(second, "TEST"));
    }

    [Fact]
    public void DecodeWoff2CollectionRejectsOutOfRangeFace()
    {
        byte[] woff2 = CreateWoff2Collection(duplicateSecondFaceTable: false);

        Assert.Throws<InvalidDataException>(
            () => ManagedOpenTypeWebFontVerifier.DecodeWoff2(woff2, 1024 * 1024, 2));
    }

    [Fact]
    public void DecodeWoff2CollectionRejectsDuplicateFaceTable()
    {
        byte[] woff2 = CreateWoff2Collection(duplicateSecondFaceTable: true);

        Assert.Throws<InvalidDataException>(
            () => ManagedOpenTypeWebFontVerifier.DecodeWoff2(woff2, 1024 * 1024, 1));
    }

    [Fact]
    public void ReconstructGlyfDecodesSimpleTripletsAndLongLoca()
    {
        byte[] transformed = CreateTransformedGlyf(
            [0, 1],
            indexFormat: 1,
            nPoints: [2],
            flags: [23, 20],
            glyphData: [0, 0, 0],
            compositeData: [],
            bboxData: [0, 0, 0, 0]);

        Woff2GlyfReconstruction result = Woff2GlyfReconstructor.Reconstruct(
            transformed,
            originalGlyfLength: 32,
            transformedLocaLength: 0,
            originalLocaLength: 12,
            CreateGlyfDependencies(glyphCount: 2, indexFormat: 1),
            maximumExpandedBytes: 1024 * 1024);

        Assert.Equal(12, result.Loca.Length);
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32BigEndian(result.Loca.AsSpan(0, 4)));
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32BigEndian(result.Loca.AsSpan(4, 4)));
        Assert.Equal((uint)result.Glyf.Length, BinaryPrimitives.ReadUInt32BigEndian(result.Loca.AsSpan(8, 4)));
        Assert.Equal(1, BinaryPrimitives.ReadInt16BigEndian(result.Glyf.AsSpan(0, 2)));
        Assert.Equal(0, BinaryPrimitives.ReadInt16BigEndian(result.Glyf.AsSpan(2, 2)));
        Assert.Equal(0, BinaryPrimitives.ReadInt16BigEndian(result.Glyf.AsSpan(4, 2)));
        Assert.Equal(1, BinaryPrimitives.ReadInt16BigEndian(result.Glyf.AsSpan(6, 2)));
        Assert.Equal(1, BinaryPrimitives.ReadInt16BigEndian(result.Glyf.AsSpan(8, 2)));
    }

    [Fact]
    public void ReconstructGlyfDecodesCompositeRecordAndShortLoca()
    {
        byte[] transformed = CreateTransformedGlyf(
            [0, -1],
            indexFormat: 0,
            nPoints: [],
            flags: [],
            glyphData: [],
            compositeData: [0, 0, 0, 0, 0, 0],
            bboxData: [0x40, 0, 0, 0, 0xFF, 0xFE, 0xFF, 0xFD, 0, 4, 0, 5]);

        Woff2GlyfReconstruction result = Woff2GlyfReconstructor.Reconstruct(
            transformed,
            originalGlyfLength: 16,
            transformedLocaLength: 0,
            originalLocaLength: 6,
            CreateGlyfDependencies(glyphCount: 2, indexFormat: 0),
            maximumExpandedBytes: 1024 * 1024);

        Assert.Equal(-1, BinaryPrimitives.ReadInt16BigEndian(result.Glyf.AsSpan(0, 2)));
        Assert.Equal(-2, BinaryPrimitives.ReadInt16BigEndian(result.Glyf.AsSpan(2, 2)));
        Assert.Equal(-3, BinaryPrimitives.ReadInt16BigEndian(result.Glyf.AsSpan(4, 2)));
        Assert.Equal(4, BinaryPrimitives.ReadInt16BigEndian(result.Glyf.AsSpan(6, 2)));
        Assert.Equal(5, BinaryPrimitives.ReadInt16BigEndian(result.Glyf.AsSpan(8, 2)));
        Assert.Equal((ushort)(result.Glyf.Length / 2), BinaryPrimitives.ReadUInt16BigEndian(result.Loca.AsSpan(4, 2)));
    }

    [Fact]
    public void ReconstructGlyfRejectsCompositeWithoutBoundingBox()
    {
        byte[] transformed = CreateTransformedGlyf(
            [-1],
            indexFormat: 0,
            nPoints: [],
            flags: [],
            glyphData: [],
            compositeData: [0, 0, 0, 0, 0, 0],
            bboxData: [0, 0, 0, 0]);

        Assert.Throws<InvalidDataException>(() => Woff2GlyfReconstructor.Reconstruct(
            transformed,
            originalGlyfLength: 16,
            transformedLocaLength: 0,
            originalLocaLength: 4,
            CreateGlyfDependencies(glyphCount: 1, indexFormat: 0),
            maximumExpandedBytes: 1024 * 1024));
    }

    private static byte[] CreateWoff2(
        byte transformFlags,
        bool includeGlyf,
        bool appendHmtxByte)
    {
        SortedDictionary<string, byte[]> tables = CreateTables(includeGlyf);
        byte[] originalHmtx = tables["hmtx"];
        byte[] transformedHmtx = CreateTransformedHmtx(transformFlags, appendHmtxByte);
        byte[] sfnt = WebFontWriters.WriteTrueType(new SfntSubset(0x00010000, tables));

        string[] order = includeGlyf
            ? ["head", "hhea", "maxp", "glyf", "loca", "hmtx"]
            : ["head", "hhea", "maxp", "hmtx"];
        using var directory = new MemoryStream();
        using var tableData = new MemoryStream();
        foreach (string tag in order)
        {
            int tagIndex = tag switch
            {
                "head" => 1,
                "hhea" => 2,
                "hmtx" => 3,
                "maxp" => 4,
                "glyf" => 10,
                "loca" => 11,
                _ => throw new InvalidOperationException()
            };
            int transformVersion = tag switch
            {
                "glyf" or "loca" => 3,
                "hmtx" => 1,
                _ => 0
            };
            directory.WriteByte(checked((byte)((transformVersion << 6) | tagIndex)));
            WriteUIntBase128(directory, checked((uint)tables[tag].Length));
            if (tag == "hmtx")
            {
                WriteUIntBase128(directory, checked((uint)transformedHmtx.Length));
                tableData.Write(transformedHmtx);
            }
            else
            {
                tableData.Write(tables[tag]);
            }
        }

        byte[] uncompressed = tableData.ToArray();
        var compressed = new byte[BrotliEncoder.GetMaxCompressedLength(uncompressed.Length)];
        Assert.True(BrotliEncoder.TryCompress(
            uncompressed,
            compressed,
            out int compressedLength,
            quality: 5,
            window: 18));

        byte[] directoryBytes = directory.ToArray();
        int contentLength = checked(48 + directoryBytes.Length + compressedLength);
        int outputLength = (contentLength + 3) & ~3;
        var output = new byte[outputLength];
        "wOF2"u8.CopyTo(output);
        BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(4, 4), 0x00010000);
        BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(8, 4), checked((uint)output.Length));
        BinaryPrimitives.WriteUInt16BigEndian(output.AsSpan(12, 2), checked((ushort)order.Length));
        BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(16, 4), checked((uint)sfnt.Length));
        BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(20, 4), checked((uint)compressedLength));
        directoryBytes.CopyTo(output, 48);
        compressed.AsSpan(0, compressedLength).CopyTo(output.AsSpan(48 + directoryBytes.Length));

        Assert.Equal(originalHmtx.Length, 10);
        return output;
    }

    private static byte[] CreateWoff2Collection(bool duplicateSecondFaceTable)
    {
        byte[][] tables =
        [
            new byte[54],
            [0, 1, 2, 3],
            [4, 5, 6, 7]
        ];
        using var directory = new MemoryStream();
        using var tableData = new MemoryStream();
        for (int index = 0; index < tables.Length; index++)
        {
            directory.WriteByte(63);
            directory.Write(index == 0 ? "head"u8 : "TEST"u8);
            WriteUIntBase128(directory, checked((uint)tables[index].Length));
            tableData.Write(tables[index]);
        }

        WriteUInt32(directory, 0x00010000);
        Write255UInt16(directory, 2);
        for (ushort face = 0; face < 2; face++)
        {
            Write255UInt16(directory, duplicateSecondFaceTable && face == 1 ? (ushort)3 : (ushort)2);
            WriteUInt32(directory, 0x00010000);
            Write255UInt16(directory, 0);
            Write255UInt16(directory, checked((ushort)(face + 1)));
            if (duplicateSecondFaceTable && face == 1)
            {
                Write255UInt16(directory, checked((ushort)(face + 1)));
            }
        }

        byte[] uncompressed = tableData.ToArray();
        var compressed = new byte[BrotliEncoder.GetMaxCompressedLength(uncompressed.Length)];
        Assert.True(BrotliEncoder.TryCompress(
            uncompressed,
            compressed,
            out int compressedLength,
            quality: 5,
            window: 18));

        byte[] directoryBytes = directory.ToArray();
        int contentLength = checked(48 + directoryBytes.Length + compressedLength);
        int outputLength = (contentLength + 3) & ~3;
        var output = new byte[outputLength];
        "wOF2"u8.CopyTo(output);
        BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(4, 4), 0x74746366);
        BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(8, 4), checked((uint)output.Length));
        BinaryPrimitives.WriteUInt16BigEndian(output.AsSpan(12, 2), checked((ushort)tables.Length));
        BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(16, 4), 1);
        BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(20, 4), checked((uint)compressedLength));
        directoryBytes.CopyTo(output, 48);
        compressed.AsSpan(0, compressedLength).CopyTo(output.AsSpan(48 + directoryBytes.Length));
        return output;
    }

    private static byte[] CreateTransformedGlyf(
        IReadOnlyList<short> contourCounts,
        ushort indexFormat,
        byte[] nPoints,
        byte[] flags,
        byte[] glyphData,
        byte[] compositeData,
        byte[] bboxData)
    {
        using var nContours = new MemoryStream();
        foreach (short count in contourCounts)
        {
            WriteInt16(nContours, count);
        }

        byte[][] streams =
        [
            nContours.ToArray(),
            nPoints,
            flags,
            glyphData,
            compositeData,
            bboxData,
            []
        ];
        using var output = new MemoryStream();
        WriteUInt16(output, 0);
        WriteUInt16(output, 0);
        WriteUInt16(output, checked((ushort)contourCounts.Count));
        WriteUInt16(output, indexFormat);
        foreach (byte[] stream in streams)
        {
            WriteUInt32(output, checked((uint)stream.Length));
        }
        foreach (byte[] stream in streams)
        {
            output.Write(stream);
        }

        return output.ToArray();
    }

    private static byte[] AppendOptionalBlocks(
        byte[] woff2,
        byte[] metadata,
        byte[] privateData)
    {
        int metadataOffset = woff2.Length;
        int privateOffset = (metadataOffset + metadata.Length + 3) & ~3;
        var result = new byte[privateOffset + privateData.Length];
        woff2.CopyTo(result, 0);
        metadata.CopyTo(result, metadataOffset);
        privateData.CopyTo(result, privateOffset);
        BinaryPrimitives.WriteUInt32BigEndian(result.AsSpan(8, 4), checked((uint)result.Length));
        BinaryPrimitives.WriteUInt32BigEndian(result.AsSpan(28, 4), checked((uint)metadataOffset));
        BinaryPrimitives.WriteUInt32BigEndian(result.AsSpan(32, 4), checked((uint)metadata.Length));
        BinaryPrimitives.WriteUInt32BigEndian(result.AsSpan(36, 4), 17);
        BinaryPrimitives.WriteUInt32BigEndian(result.AsSpan(40, 4), checked((uint)privateOffset));
        BinaryPrimitives.WriteUInt32BigEndian(result.AsSpan(44, 4), checked((uint)privateData.Length));
        return result;
    }

    private static IReadOnlyDictionary<string, byte[]> CreateGlyfDependencies(
        ushort glyphCount,
        short indexFormat)
    {
        var head = new byte[54];
        BinaryPrimitives.WriteInt16BigEndian(head.AsSpan(50, 2), indexFormat);
        var maxp = new byte[6];
        BinaryPrimitives.WriteUInt16BigEndian(maxp.AsSpan(4, 2), glyphCount);
        return new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["head"] = head,
            ["maxp"] = maxp
        };
    }

    private static SortedDictionary<string, byte[]> CreateTables(bool includeGlyf)
    {
        var head = new byte[54];
        BinaryPrimitives.WriteInt16BigEndian(head.AsSpan(50, 2), 1);
        var hhea = new byte[36];
        BinaryPrimitives.WriteUInt16BigEndian(hhea.AsSpan(34, 2), 2);
        var maxp = new byte[6];
        BinaryPrimitives.WriteUInt16BigEndian(maxp.AsSpan(4, 2), 3);

        var tables = new SortedDictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["head"] = head,
            ["hhea"] = hhea,
            ["hmtx"] = CreateExpectedHmtx(),
            ["maxp"] = maxp
        };
        if (includeGlyf)
        {
            var glyf = new byte[20];
            BinaryPrimitives.WriteInt16BigEndian(glyf.AsSpan(2, 2), -7);
            BinaryPrimitives.WriteInt16BigEndian(glyf.AsSpan(12, 2), 12);
            var loca = new byte[16];
            BinaryPrimitives.WriteUInt32BigEndian(loca.AsSpan(4, 4), 0);
            BinaryPrimitives.WriteUInt32BigEndian(loca.AsSpan(8, 4), 10);
            BinaryPrimitives.WriteUInt32BigEndian(loca.AsSpan(12, 4), 20);
            tables["glyf"] = glyf;
            tables["loca"] = loca;
        }

        return tables;
    }

    private static byte[] CreateExpectedHmtx()
    {
        var hmtx = new byte[10];
        BinaryPrimitives.WriteUInt16BigEndian(hmtx.AsSpan(0, 2), 500);
        BinaryPrimitives.WriteInt16BigEndian(hmtx.AsSpan(2, 2), 0);
        BinaryPrimitives.WriteUInt16BigEndian(hmtx.AsSpan(4, 2), 600);
        BinaryPrimitives.WriteInt16BigEndian(hmtx.AsSpan(6, 2), -7);
        BinaryPrimitives.WriteInt16BigEndian(hmtx.AsSpan(8, 2), 12);
        return hmtx;
    }

    private static byte[] CreateTransformedHmtx(byte flags, bool appendByte)
    {
        using var stream = new MemoryStream();
        stream.WriteByte(flags);
        WriteUInt16(stream, 500);
        WriteUInt16(stream, 600);
        if ((flags & 0x01) == 0)
        {
            WriteInt16(stream, 0);
            WriteInt16(stream, -7);
        }
        if ((flags & 0x02) == 0)
        {
            WriteInt16(stream, 12);
        }
        if (appendByte)
        {
            stream.WriteByte(0);
        }

        return stream.ToArray();
    }

    private static byte[] ReadSfntTable(byte[] sfnt, string tag)
    {
        ushort tableCount = BinaryPrimitives.ReadUInt16BigEndian(sfnt.AsSpan(4, 2));
        for (int index = 0; index < tableCount; index++)
        {
            int record = 12 + (index * 16);
            if (!sfnt.AsSpan(record, 4).SequenceEqual(System.Text.Encoding.ASCII.GetBytes(tag)))
            {
                continue;
            }

            int offset = checked((int)BinaryPrimitives.ReadUInt32BigEndian(sfnt.AsSpan(record + 8, 4)));
            int length = checked((int)BinaryPrimitives.ReadUInt32BigEndian(sfnt.AsSpan(record + 12, 4)));
            return sfnt.AsSpan(offset, length).ToArray();
        }

        throw new InvalidDataException($"Missing table: {tag}");
    }

    private static void WriteUIntBase128(Stream stream, uint value)
    {
        Span<byte> bytes = stackalloc byte[5];
        int index = bytes.Length;
        bytes[--index] = checked((byte)(value & 0x7F));
        while ((value >>= 7) != 0)
        {
            bytes[--index] = checked((byte)((value & 0x7F) | 0x80));
        }

        stream.Write(bytes[index..]);
    }

    private static void WriteUInt16(Stream stream, ushort value)
    {
        Span<byte> bytes = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(bytes, value);
        stream.Write(bytes);
    }

    private static void Write255UInt16(Stream stream, ushort value)
    {
        if (value < 253)
        {
            stream.WriteByte(checked((byte)value));
            return;
        }

        stream.WriteByte(253);
        WriteUInt16(stream, value);
    }

    private static void WriteInt16(Stream stream, short value)
    {
        Span<byte> bytes = stackalloc byte[2];
        BinaryPrimitives.WriteInt16BigEndian(bytes, value);
        stream.Write(bytes);
    }

    private static void WriteUInt32(Stream stream, uint value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
        stream.Write(bytes);
    }
}
