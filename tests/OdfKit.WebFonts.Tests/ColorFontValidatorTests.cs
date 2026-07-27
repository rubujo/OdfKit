using System.Buffers.Binary;
using OdfKit.WebFonts.OpenType;

namespace OdfKit.WebFonts.Tests;

public sealed class ColorFontValidatorTests
{
    [Fact]
    public void ValidateAcceptsBoundedColrVersionZero()
    {
        var tables = new Dictionary<string, byte[]>
        {
            ["CPAL"] = CreateCpal(),
            ["COLR"] = CreateColrVersionZero()
        };

        ColorGlyphClosure closure = ColorFontValidator.Validate(
            tables,
            glyphCount: 3,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(ColorFontTechnology.ColrV0, closure.Technologies);
    }

    [Fact]
    public void ValidateColrVersionZeroAddsOnlySelectedBaseGlyphLayers()
    {
        var tables = new Dictionary<string, byte[]>
        {
            ["CPAL"] = CreateCpal(),
            ["COLR"] = CreateColrVersionZero()
        };
        ColorGlyphClosure closure = ColorFontValidator.Validate(
            tables,
            glyphCount: 3,
            cancellationToken: TestContext.Current.CancellationToken);
        var glyphs = new HashSet<ushort> { 1 };

        closure.AddReferencedGlyphs(glyphs);

        Assert.Equal<ushort>([1, 2], glyphs.OrderBy(glyph => glyph));
    }

    [Fact]
    public void ValidateAcceptsColrVersionOnePaintOffsetRelativeToBaseGlyphList()
    {
        var tables = new Dictionary<string, byte[]>
        {
            ["CPAL"] = CreateCpal(),
            ["COLR"] = CreateColrVersionOne(paintOffset: 10)
        };

        ColorGlyphClosure closure = ColorFontValidator.Validate(
            tables,
            glyphCount: 3,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(ColorFontTechnology.ColrV1, closure.Technologies);
    }

    [Fact]
    public void ValidateRejectsColrVersionOnePaintOffsetAtTableEnd()
    {
        var tables = new Dictionary<string, byte[]>
        {
            ["CPAL"] = CreateCpal(),
            ["COLR"] = CreateColrVersionOne(paintOffset: 11)
        };

        Assert.Throws<InvalidDataException>(() => ColorFontValidator.Validate(
            tables,
            glyphCount: 3,
            cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ValidateColrVersionOneTraversesPaintGlyphClosure()
    {
        var tables = new Dictionary<string, byte[]>
        {
            ["CPAL"] = CreateCpal(),
            ["COLR"] = CreateColrVersionOnePaintGlyph()
        };
        ColorGlyphClosure closure = ColorFontValidator.Validate(
            tables,
            glyphCount: 4,
            cancellationToken: TestContext.Current.CancellationToken);
        var glyphs = new HashSet<ushort> { 1 };

        closure.AddReferencedGlyphs(glyphs);

        Assert.Equal<ushort>([1, 3], glyphs.OrderBy(glyph => glyph));
    }

    [Theory]
    [InlineData(11)]
    [InlineData(33)]
    public void ValidateRejectsCyclicOrUnknownColrVersionOnePaint(byte paintFormat)
    {
        var tables = new Dictionary<string, byte[]>
        {
            ["CPAL"] = CreateCpal(),
            ["COLR"] = CreateColrVersionOneLeaf(paintFormat)
        };

        Assert.Throws<InvalidDataException>(() => ColorFontValidator.Validate(
            tables,
            glyphCount: 3,
            cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ValidateRejectsColrWithoutCpal()
    {
        var tables = new Dictionary<string, byte[]> { ["COLR"] = CreateColrVersionZero() };

        Assert.Throws<InvalidDataException>(() => ColorFontValidator.Validate(
            tables,
            glyphCount: 3,
            cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ValidateAcceptsSvgWithOptionalCpal()
    {
        var tables = new Dictionary<string, byte[]>
        {
            ["CPAL"] = CreateCpal(),
            ["SVG "] = CreateSvg("<svg xmlns=\"http://www.w3.org/2000/svg\"><g id=\"glyph1\"/></svg>")
        };

        ColorGlyphClosure closure = ColorFontValidator.Validate(
            tables,
            glyphCount: 2,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(ColorFontTechnology.Svg, closure.Technologies);
    }

    [Theory]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\"><script/></svg>")]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\"><use href=\"https://example.test/x\"/></svg>")]
    [InlineData("<!DOCTYPE svg><svg xmlns=\"http://www.w3.org/2000/svg\"/>")]
    public void ValidateRejectsActiveSvgContent(string document)
    {
        var tables = new Dictionary<string, byte[]> { ["SVG "] = CreateSvg(document) };

        Assert.Throws<InvalidDataException>(() => ColorFontValidator.Validate(
            tables,
            glyphCount: 2,
            cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ValidateRejectsUnpairedBitmapTables()
    {
        var tables = new Dictionary<string, byte[]> { ["CBDT"] = [0, 3, 0, 0] };

        Assert.Throws<InvalidDataException>(() => ColorFontValidator.Validate(
            tables,
            glyphCount: 2,
            cancellationToken: TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("CBDT", "CBLC", 0x00030000u, 19)]
    [InlineData("EBDT", "EBLC", 0x00020000u, 1)]
    public void ValidateAcceptsVersionedBitmapIndexFormatOne(
        string dataTag,
        string locationTag,
        uint version,
        ushort imageFormat)
    {
        (byte[] data, byte[] location) = CreateBitmapPair(version, imageFormat, finalImageOffset: 0);
        var tables = new Dictionary<string, byte[]>
        {
            [dataTag] = data,
            [locationTag] = location
        };

        ColorGlyphClosure closure = ColorFontValidator.Validate(
            tables,
            glyphCount: 2,
            cancellationToken: TestContext.Current.CancellationToken);
        ColorFontTechnology expected = dataTag == "CBDT"
            ? ColorFontTechnology.Cbdt
            : ColorFontTechnology.Ebdt;

        Assert.Equal(expected, closure.Technologies);
    }

    [Fact]
    public void ValidateRejectsBitmapIndexPastDataTable()
    {
        (byte[] data, byte[] location) = CreateBitmapPair(0x00030000u, imageFormat: 19, finalImageOffset: 1);
        var tables = new Dictionary<string, byte[]>
        {
            ["CBDT"] = data,
            ["CBLC"] = location
        };

        Assert.Throws<InvalidDataException>(() => ColorFontValidator.Validate(
            tables,
            glyphCount: 2,
            cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ValidateSbixDupeAddsReferencedGlyph()
    {
        var tables = new Dictionary<string, byte[]> { ["sbix"] = CreateSbixDupe() };
        ColorGlyphClosure closure = ColorFontValidator.Validate(
            tables,
            glyphCount: 3,
            cancellationToken: TestContext.Current.CancellationToken);
        var glyphs = new HashSet<ushort> { 1 };

        closure.AddReferencedGlyphs(glyphs);

        Assert.Equal(ColorFontTechnology.Sbix, closure.Technologies);
        Assert.Equal<ushort>([1, 2], glyphs.OrderBy(glyph => glyph));
    }

    [Fact]
    public void ValidateRejectsSbixReservedFlags()
    {
        byte[] sbix = CreateSbixDupe();
        WriteUInt16(sbix, 2, 5);

        Assert.Throws<InvalidDataException>(
            () => ColorFontValidator.Validate(
                new Dictionary<string, byte[]> { ["sbix"] = sbix },
                glyphCount: 3,
                cancellationToken: TestContext.Current.CancellationToken));
    }

    private static byte[] CreateCpal()
    {
        var table = new byte[18];
        WriteUInt16(table, 2, 1);
        WriteUInt16(table, 4, 1);
        WriteUInt16(table, 6, 1);
        WriteUInt32(table, 8, 14);
        return table;
    }

    private static byte[] CreateColrVersionZero()
    {
        var table = new byte[24];
        WriteUInt16(table, 2, 1);
        WriteUInt32(table, 4, 14);
        WriteUInt32(table, 8, 20);
        WriteUInt16(table, 12, 1);
        WriteUInt16(table, 14, 1);
        WriteUInt16(table, 18, 1);
        WriteUInt16(table, 20, 2);
        return table;
    }

    private static byte[] CreateColrVersionOne(uint paintOffset)
    {
        const int baseGlyphListOffset = 34;
        var table = new byte[49];
        WriteUInt16(table, 0, 1);
        WriteUInt32(table, 14, baseGlyphListOffset);
        WriteUInt32(table, baseGlyphListOffset, 1);
        WriteUInt16(table, baseGlyphListOffset + 4, 1);
        WriteUInt32(table, baseGlyphListOffset + 6, paintOffset);
        table[baseGlyphListOffset + 10] = 2;
        WriteUInt16(table, baseGlyphListOffset + 11, 0);
        WriteUInt16(table, baseGlyphListOffset + 13, 0x4000);
        return table;
    }

    private static byte[] CreateColrVersionOnePaintGlyph()
    {
        const int baseGlyphListOffset = 34;
        const int paintOffset = baseGlyphListOffset + 10;
        const int fillOffset = paintOffset + 6;
        var table = new byte[fillOffset + 5];
        WriteUInt16(table, 0, 1);
        WriteUInt32(table, 14, baseGlyphListOffset);
        WriteUInt32(table, baseGlyphListOffset, 1);
        WriteUInt16(table, baseGlyphListOffset + 4, 1);
        WriteUInt32(table, baseGlyphListOffset + 6, 10);
        table[paintOffset] = 10;
        WriteUInt24(table, paintOffset + 1, 6);
        WriteUInt16(table, paintOffset + 4, 3);
        table[fillOffset] = 2;
        WriteUInt16(table, fillOffset + 1, 0);
        WriteUInt16(table, fillOffset + 3, 0x4000);
        return table;
    }

    private static byte[] CreateColrVersionOneLeaf(byte paintFormat)
    {
        const int baseGlyphListOffset = 34;
        const int paintOffset = baseGlyphListOffset + 10;
        var table = new byte[paintOffset + 5];
        WriteUInt16(table, 0, 1);
        WriteUInt32(table, 14, baseGlyphListOffset);
        WriteUInt32(table, baseGlyphListOffset, 1);
        WriteUInt16(table, baseGlyphListOffset + 4, 1);
        WriteUInt32(table, baseGlyphListOffset + 6, 10);
        table[paintOffset] = paintFormat;
        if (paintFormat == 11)
        {
            WriteUInt16(table, paintOffset + 1, 1);
        }

        return table;
    }

    private static byte[] CreateSbixDupe()
    {
        const int strikeOffset = 12;
        const int strikeHeaderLength = 20;
        var table = new byte[strikeOffset + strikeHeaderLength + 10];
        WriteUInt16(table, 0, 1);
        WriteUInt16(table, 2, 1);
        WriteUInt32(table, 4, 1);
        WriteUInt32(table, 8, strikeOffset);
        WriteUInt16(table, strikeOffset, 16);
        WriteUInt16(table, strikeOffset + 2, 96);
        WriteUInt32(table, strikeOffset + 4, strikeHeaderLength);
        WriteUInt32(table, strikeOffset + 8, strikeHeaderLength);
        WriteUInt32(table, strikeOffset + 12, strikeHeaderLength + 10);
        WriteUInt32(table, strikeOffset + 16, strikeHeaderLength + 10);
        int dataOffset = strikeOffset + strikeHeaderLength;
        System.Text.Encoding.ASCII.GetBytes("dupe").CopyTo(table, dataOffset + 4);
        WriteUInt16(table, dataOffset + 8, 2);
        return table;
    }

    private static (byte[] Data, byte[] Location) CreateBitmapPair(
        uint version,
        ushort imageFormat,
        uint finalImageOffset)
    {
        var data = new byte[4];
        WriteUInt32(data, 0, version);

        const int arrayOffset = 56;
        const int subtableOffset = 64;
        var location = new byte[84];
        WriteUInt32(location, 0, version);
        WriteUInt32(location, 4, 1);
        WriteUInt32(location, 8, arrayOffset);
        WriteUInt32(location, 12, 28);
        WriteUInt32(location, 16, 1);
        WriteUInt16(location, 48, 0);
        WriteUInt16(location, 50, 1);
        WriteUInt16(location, arrayOffset, 0);
        WriteUInt16(location, arrayOffset + 2, 1);
        WriteUInt32(location, arrayOffset + 4, 8);
        WriteUInt16(location, subtableOffset, 1);
        WriteUInt16(location, subtableOffset + 2, imageFormat);
        WriteUInt32(location, subtableOffset + 4, 4);
        WriteUInt32(location, subtableOffset + 8, 0);
        WriteUInt32(location, subtableOffset + 12, 0);
        WriteUInt32(location, subtableOffset + 16, finalImageOffset);
        return (data, location);
    }

    private static byte[] CreateSvg(string document)
    {
        byte[] documentBytes = System.Text.Encoding.UTF8.GetBytes(document);
        const int documentListOffset = 10;
        const int documentOffset = 14;
        var table = new byte[checked(documentListOffset + documentOffset + documentBytes.Length)];
        WriteUInt32(table, 2, documentListOffset);
        WriteUInt16(table, documentListOffset, 1);
        WriteUInt16(table, documentListOffset + 2, 1);
        WriteUInt16(table, documentListOffset + 4, 1);
        WriteUInt32(table, documentListOffset + 6, documentOffset);
        WriteUInt32(table, documentListOffset + 10, checked((uint)documentBytes.Length));
        documentBytes.CopyTo(table, documentListOffset + documentOffset);
        return table;
    }

    private static void WriteUInt16(byte[] bytes, int offset, ushort value)
        => BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(offset, 2), value);

    private static void WriteUInt32(byte[] bytes, int offset, uint value)
        => BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(offset, 4), value);

    private static void WriteUInt24(byte[] bytes, int offset, uint value)
    {
        bytes[offset] = checked((byte)((value >> 16) & 0xFF));
        bytes[offset + 1] = checked((byte)((value >> 8) & 0xFF));
        bytes[offset + 2] = checked((byte)(value & 0xFF));
    }
}
