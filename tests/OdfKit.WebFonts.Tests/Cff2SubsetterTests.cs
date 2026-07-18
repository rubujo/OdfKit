using System.Buffers.Binary;

using OdfKit.WebFonts.OpenType;

namespace OdfKit.WebFonts.Tests;

public sealed class Cff2SubsetterTests
{
    [Fact]
    public void Build_AcceptsNonVariableFontWithoutVariationStoreOrFvar()
    {
        byte[] source = BuildCff2([]);
        var selectedGlyphs = new HashSet<ushort> { 0 };

        Cff2Subsetter.Validate(source, fvar: null, glyphCount: 1, selectedGlyphs);
        byte[] subset = Cff2Subsetter.Build(source, fvar: null, glyphCount: 1, selectedGlyphs);

        Assert.Equal(source, subset);
    }

    [Fact]
    public void Validate_RejectsVariationIndexWithoutVariationStore()
    {
        byte[] source = BuildCff2([139, 15]);

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            Cff2Subsetter.Validate(source, fvar: null, glyphCount: 1, new HashSet<ushort> { 0 }));

        Assert.Contains("vsindex", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_RejectsBlendWithoutVariationStore()
    {
        byte[] source = BuildCff2([139, 16]);

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            Cff2Subsetter.Validate(source, fvar: null, glyphCount: 1, new HashSet<ushort> { 0 }));

        Assert.Contains("blend-without-vstore", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_DoesNotReuseCachedParseAcrossGlyphCounts()
    {
        byte[] source = BuildCff2([]);
        var selectedGlyphs = new HashSet<ushort> { 0 };
        Cff2Subsetter.Validate(source, fvar: null, glyphCount: 1, selectedGlyphs);

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            Cff2Subsetter.Validate(source, fvar: null, glyphCount: 2, selectedGlyphs));

        Assert.Contains("CharStrings-count", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_DoesNotReuseCachedParseAcrossVariationContexts()
    {
        byte[] source = BuildCff2([]);
        var selectedGlyphs = new HashSet<ushort> { 0 };
        Cff2Subsetter.Validate(source, fvar: null, glyphCount: 1, selectedGlyphs);

        Cff2Subsetter.Validate(source, BuildFvar(), glyphCount: 1, selectedGlyphs);

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            Cff2Subsetter.Validate(source, new byte[16], glyphCount: 1, selectedGlyphs));
        Assert.Contains("fvar-header", exception.Message, StringComparison.Ordinal);
    }

    private static byte[] BuildCff2(byte[] charString)
    {
        const int headerSize = 5;
        const int topDictSize = 13;
        const int globalSubrIndexSize = 4;
        int charStringsOffset = headerSize + topDictSize + globalSubrIndexSize;
        int charStringsSize = 7 + charString.Length;
        int fontDictArrayOffset = charStringsOffset + charStringsSize;
        const int fontDictArraySize = 18;
        int privateDictOffset = fontDictArrayOffset + fontDictArraySize;

        var bytes = new List<byte>(privateDictOffset)
        {
            2, 0, headerSize, 0, topDictSize
        };

        AppendDictInteger(bytes, charStringsOffset);
        bytes.Add(17);
        AppendDictInteger(bytes, fontDictArrayOffset);
        bytes.AddRange([12, 36]);

        bytes.AddRange([0, 0, 0, 0]);
        bytes.AddRange([0, 0, 0, 1, 1, 1, checked((byte)(charString.Length + 1))]);
        bytes.AddRange(charString);

        bytes.AddRange([0, 0, 0, 1, 1, 1, 12]);
        AppendDictInteger(bytes, 0);
        AppendDictInteger(bytes, privateDictOffset);
        bytes.Add(18);

        return bytes.ToArray();
    }

    private static void AppendDictInteger(List<byte> bytes, int value)
    {
        Span<byte> encoded = stackalloc byte[5];
        encoded[0] = 29;
        BinaryPrimitives.WriteInt32BigEndian(encoded[1..], value);
        bytes.AddRange(encoded.ToArray());
    }

    private static byte[] BuildFvar()
    {
        var fvar = new byte[36];
        BinaryPrimitives.WriteUInt16BigEndian(fvar.AsSpan(0, 2), 1);
        BinaryPrimitives.WriteUInt16BigEndian(fvar.AsSpan(4, 2), 16);
        BinaryPrimitives.WriteUInt16BigEndian(fvar.AsSpan(6, 2), 2);
        BinaryPrimitives.WriteUInt16BigEndian(fvar.AsSpan(8, 2), 1);
        BinaryPrimitives.WriteUInt16BigEndian(fvar.AsSpan(10, 2), 20);
        BinaryPrimitives.WriteUInt16BigEndian(fvar.AsSpan(14, 2), 8);
        return fvar;
    }
}
