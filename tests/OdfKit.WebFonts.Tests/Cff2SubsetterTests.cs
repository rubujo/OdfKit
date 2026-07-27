using System.Buffers.Binary;

using OdfKit.WebFonts.OpenType;

namespace OdfKit.WebFonts.Tests;

public sealed class Cff2SubsetterTests
{
    [Fact]
    public void BuildAcceptsNonVariableFontWithoutVariationStoreOrFvar()
    {
        byte[] source = BuildCff2([]);
        var selectedGlyphs = new HashSet<ushort> { 0 };

        Cff2Subsetter.Validate(
            source,
            fvar: null,
            glyphCount: 1,
            selectedGlyphs,
            cancellationToken: TestContext.Current.CancellationToken);
        byte[] subset = Cff2Subsetter.Build(
            source,
            fvar: null,
            glyphCount: 1,
            selectedGlyphs,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(source, subset);
    }

    [Fact]
    public void ValidateRejectsVariationIndexWithoutVariationStore()
    {
        byte[] source = BuildCff2([139, 15]);

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            Cff2Subsetter.Validate(
                source,
                fvar: null,
                glyphCount: 1,
                new HashSet<ushort> { 0 },
                cancellationToken: TestContext.Current.CancellationToken));

        Assert.Contains("vsindex", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateRejectsBlendWithoutVariationStore()
    {
        byte[] source = BuildCff2([139, 16]);

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            Cff2Subsetter.Validate(
                source,
                fvar: null,
                glyphCount: 1,
                new HashSet<ushort> { 0 },
                cancellationToken: TestContext.Current.CancellationToken));

        Assert.Contains("blend-without-vstore", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateDoesNotReuseCachedParseAcrossGlyphCounts()
    {
        byte[] source = BuildCff2([]);
        var selectedGlyphs = new HashSet<ushort> { 0 };
        Cff2Subsetter.Validate(
            source,
            fvar: null,
            glyphCount: 1,
            selectedGlyphs,
            cancellationToken: TestContext.Current.CancellationToken);

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            Cff2Subsetter.Validate(
                source,
                fvar: null,
                glyphCount: 2,
                selectedGlyphs,
                cancellationToken: TestContext.Current.CancellationToken));

        Assert.Contains("CharStrings-count", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateDoesNotReuseCachedParseAcrossVariationContexts()
    {
        byte[] source = BuildCff2([]);
        var selectedGlyphs = new HashSet<ushort> { 0 };
        Cff2Subsetter.Validate(
            source,
            fvar: null,
            glyphCount: 1,
            selectedGlyphs,
            cancellationToken: TestContext.Current.CancellationToken);

        Cff2Subsetter.Validate(
            source,
            BuildFvar(),
            glyphCount: 1,
            selectedGlyphs,
            cancellationToken: TestContext.Current.CancellationToken);

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            Cff2Subsetter.Validate(
                source,
                new byte[16],
                glyphCount: 1,
                selectedGlyphs,
                cancellationToken: TestContext.Current.CancellationToken));
        Assert.Contains("fvar-header", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildCompactsCharStringsAndRewritesPrivateSubrsOffsets()
    {
        byte[] source = BuildCff2WithCharStrings(
            includeLocalSubrs: true,
            [],
            [32, 10],
            [139, 139, 21]);
        var retainedGlyphs = new HashSet<ushort> { 0, 1 };

        byte[] subset = Cff2Subsetter.Build(
            source,
            fvar: null,
            glyphCount: 3,
            retainedGlyphs,
            cancellationToken: TestContext.Current.CancellationToken);
        Cff2Subsetter.Validate(
            subset,
            fvar: null,
            glyphCount: 3,
            new HashSet<ushort> { 0, 1, 2 },
            cancellationToken: TestContext.Current.CancellationToken);
        byte[] rebuilt = Cff2Subsetter.Build(
            subset,
            fvar: null,
            glyphCount: 3,
            retainedGlyphs,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(subset.Length < source.Length);
        Assert.Equal(subset, rebuilt);
    }

    private static byte[] BuildCff2(byte[] charString)
        => BuildCff2WithCharStrings(includeLocalSubrs: false, charString);

    private static byte[] BuildCff2WithCharStrings(
        bool includeLocalSubrs,
        params byte[][] charStrings)
    {
        const int headerSize = 5;
        const int topDictSize = 13;
        const int globalSubrIndexSize = 4;
        int charStringsOffset = headerSize + topDictSize + globalSubrIndexSize;
        var charStringsIndex = new List<byte>();
        AppendIndex(charStringsIndex, charStrings);
        int charStringsSize = charStringsIndex.Count;
        int fontDictArrayOffset = charStringsOffset + charStringsSize;
        const int fontDictArraySize = 18;
        int privateDictOffset = fontDictArrayOffset + fontDictArraySize;
        int privateDictLength = includeLocalSubrs ? 6 : 0;

        var bytes = new List<byte>()
        {
            2, 0, headerSize, 0, topDictSize
        };

        AppendDictInteger(bytes, charStringsOffset);
        bytes.Add(17);
        AppendDictInteger(bytes, fontDictArrayOffset);
        bytes.AddRange([12, 36]);

        bytes.AddRange([0, 0, 0, 0]);
        bytes.AddRange(charStringsIndex);

        bytes.AddRange([0, 0, 0, 1, 1, 1, 12]);
        AppendDictInteger(bytes, privateDictLength);
        AppendDictInteger(bytes, privateDictOffset);
        bytes.Add(18);

        if (includeLocalSubrs)
        {
            AppendDictInteger(bytes, privateDictLength);
            bytes.Add(19);
            AppendIndex(bytes, [new byte[] { 139, 22 }]);
        }

        return bytes.ToArray();
    }

    private static void AppendIndex(List<byte> bytes, byte[][] objects)
    {
        Span<byte> count = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(count, checked((uint)objects.Length));
        bytes.AddRange(count.ToArray());
        if (objects.Length == 0)
        {
            return;
        }

        bytes.Add(1);
        int offset = 1;
        foreach (byte[] value in objects)
        {
            bytes.Add(checked((byte)offset));
            offset = checked(offset + value.Length);
        }

        bytes.Add(checked((byte)offset));
        foreach (byte[] value in objects)
        {
            bytes.AddRange(value);
        }
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
