using System.Buffers.Binary;
using OdfKit.WebFonts.OpenType;

namespace OdfKit.WebFonts.Tests;

public sealed class ColorFontValidatorTests
{
    [Fact]
    public void Validate_AcceptsBoundedColrVersionZero()
    {
        var tables = new Dictionary<string, byte[]>
        {
            ["CPAL"] = CreateCpal(),
            ["COLR"] = CreateColrVersionZero()
        };

        Assert.True(ColorFontValidator.Validate(tables, glyphCount: 3));
    }

    [Fact]
    public void Validate_AcceptsColrVersionOnePaintOffsetRelativeToBaseGlyphList()
    {
        var tables = new Dictionary<string, byte[]>
        {
            ["CPAL"] = CreateCpal(),
            ["COLR"] = CreateColrVersionOne(paintOffset: 10)
        };

        Assert.True(ColorFontValidator.Validate(tables, glyphCount: 3));
    }

    [Fact]
    public void Validate_RejectsColrVersionOnePaintOffsetAtTableEnd()
    {
        var tables = new Dictionary<string, byte[]>
        {
            ["CPAL"] = CreateCpal(),
            ["COLR"] = CreateColrVersionOne(paintOffset: 11)
        };

        Assert.Throws<InvalidDataException>(() => ColorFontValidator.Validate(tables, glyphCount: 3));
    }

    [Fact]
    public void Validate_RejectsColrWithoutCpal()
    {
        var tables = new Dictionary<string, byte[]> { ["COLR"] = CreateColrVersionZero() };

        Assert.Throws<InvalidDataException>(() => ColorFontValidator.Validate(tables, glyphCount: 3));
    }

    [Fact]
    public void Validate_AcceptsSvgWithOptionalCpal()
    {
        var tables = new Dictionary<string, byte[]>
        {
            ["CPAL"] = CreateCpal(),
            ["SVG "] = CreateSvg("<svg xmlns=\"http://www.w3.org/2000/svg\"><g id=\"glyph1\"/></svg>")
        };

        Assert.True(ColorFontValidator.Validate(tables, glyphCount: 2));
    }

    [Theory]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\"><script/></svg>")]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\"><use href=\"https://example.test/x\"/></svg>")]
    [InlineData("<!DOCTYPE svg><svg xmlns=\"http://www.w3.org/2000/svg\"/>")]
    public void Validate_RejectsActiveSvgContent(string document)
    {
        var tables = new Dictionary<string, byte[]> { ["SVG "] = CreateSvg(document) };

        Assert.Throws<InvalidDataException>(() => ColorFontValidator.Validate(tables, glyphCount: 2));
    }

    [Fact]
    public void Validate_RejectsUnpairedBitmapTables()
    {
        var tables = new Dictionary<string, byte[]> { ["CBDT"] = [0, 3, 0, 0] };

        Assert.Throws<InvalidDataException>(() => ColorFontValidator.Validate(tables, glyphCount: 2));
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
        var table = new byte[45];
        WriteUInt16(table, 0, 1);
        WriteUInt32(table, 14, baseGlyphListOffset);
        WriteUInt32(table, baseGlyphListOffset, 1);
        WriteUInt16(table, baseGlyphListOffset + 4, 1);
        WriteUInt32(table, baseGlyphListOffset + 6, paintOffset);
        table[baseGlyphListOffset + 10] = 2;
        return table;
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
}
