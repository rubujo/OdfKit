using System.Buffers.Binary;

using OdfKit.WebFonts.OpenType;

namespace OdfKit.WebFonts.Tests;

public sealed class CffSubsetterTests
{
    [Fact]
    public void BuildAcceptsNameKeyedFontWithPredefinedCharset()
    {
        byte[] source = BuildNameKeyedCff([14], [139, 22, 14]);

        CffSubsetter.Validate(
            source,
            glyphCount: 2,
            new HashSet<ushort> { 0, 1 },
            cancellationToken: TestContext.Current.CancellationToken);
        byte[] subset = CffSubsetter.Build(
            source,
            glyphCount: 2,
            new HashSet<ushort> { 0 },
            cancellationToken: TestContext.Current.CancellationToken);
        CffSubsetter.Validate(
            subset,
            glyphCount: 2,
            new HashSet<ushort> { 0, 1 },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotEqual(source, subset);
        Assert.True(subset.Length < source.Length);
    }

    [Fact]
    public void ValidateDoesNotReuseCachedParseAcrossGlyphCounts()
    {
        byte[] source = BuildNameKeyedCff([14], [139, 22, 14]);
        CffSubsetter.Validate(
            source,
            glyphCount: 2,
            new HashSet<ushort> { 0 },
            cancellationToken: TestContext.Current.CancellationToken);

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            CffSubsetter.Validate(
                source,
                glyphCount: 1,
                new HashSet<ushort> { 0 },
                cancellationToken: TestContext.Current.CancellationToken));

        Assert.Contains("CharStrings-count", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateRejectsPredefinedCharsetBeyondItsBound()
    {
        byte[][] charStrings = Enumerable.Repeat(new byte[] { 14 }, 230).ToArray();
        byte[] source = BuildNameKeyedCff(charStrings);

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            CffSubsetter.Validate(
                source,
                glyphCount: 230,
                new HashSet<ushort> { 0 },
                cancellationToken: TestContext.Current.CancellationToken));

        Assert.Contains("charset-predefined", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildRetainsIsoAdobeSeacComponents()
    {
        byte[][] charStrings = Enumerable.Range(0, 127)
            .Select(_ => new byte[] { 14 })
            .ToArray();
        charStrings[34] = [139, 22, 14];
        charStrings[125] = [139, 4, 14];
        charStrings[126] = [139, 139, 204, 247, 86, 14];
        byte[] source = BuildNameKeyedCff(charStrings);
        int baseOffset = source.AsSpan().IndexOf(charStrings[34]);
        int accentOffset = source.AsSpan().IndexOf(charStrings[125]);

        byte[] subset = CffSubsetter.Build(
            source,
            glyphCount: 127,
            new HashSet<ushort> { 0, 126 },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(charStrings[34], subset.AsSpan(baseOffset, charStrings[34].Length).ToArray());
        Assert.Equal(charStrings[125], subset.AsSpan(accentOffset, charStrings[125].Length).ToArray());
    }

    [Theory]
    [InlineData(1, 12)]
    [InlineData(2, 8)]
    public void BuildRetainsPredefinedExpertSeacComponents(int charset, int commaGlyph)
    {
        int glyphCount = commaGlyph + 1;
        byte[][] charStrings = Enumerable.Range(0, glyphCount)
            .Select(_ => new byte[] { 14 })
            .ToArray();
        charStrings[1] = [139, 22, 14];
        charStrings[2] = [139, 139, 171, 183, 14];
        charStrings[commaGlyph] = [139, 4, 14];
        byte[] source = BuildNameKeyedCffWithPredefinedCharset(charset, charStrings);
        int spaceOffset = source.AsSpan().IndexOf(charStrings[1]);
        int commaOffset = source.AsSpan().IndexOf(charStrings[commaGlyph]);

        byte[] subset = CffSubsetter.Build(
            source,
            checked((ushort)glyphCount),
            new HashSet<ushort> { 0, 2 },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(charStrings[1], subset.AsSpan(spaceOffset, charStrings[1].Length).ToArray());
        Assert.Equal(
            charStrings[commaGlyph],
            subset.AsSpan(commaOffset, charStrings[commaGlyph].Length).ToArray());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void BuildRetainsCustomCharsetSeacComponents(int charsetFormat)
    {
        byte[][] charStrings =
        [
            [14],
            [139, 22, 14],
            [139, 4, 14],
            [139, 139, 204, 247, 86, 14]
        ];
        byte[] source = BuildNameKeyedCffWithCustomCharset(
            [34, 125, 171],
            charsetFormat,
            charStrings);
        int baseOffset = source.AsSpan().IndexOf(charStrings[1]);
        int accentOffset = source.AsSpan().IndexOf(charStrings[2]);

        byte[] subset = CffSubsetter.Build(
            source,
            glyphCount: 4,
            new HashSet<ushort> { 0, 3 },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(charStrings[1], subset.AsSpan(baseOffset, charStrings[1].Length).ToArray());
        Assert.Equal(charStrings[2], subset.AsSpan(accentOffset, charStrings[2].Length).ToArray());
    }

    [Fact]
    public void BuildRejectsMissingSeacComponent()
    {
        byte[][] charStrings = Enumerable.Range(0, 125)
            .Select(_ => new byte[] { 14 })
            .ToArray();
        charStrings[34] = [139, 22, 14];
        charStrings[124] = [139, 139, 204, 247, 86, 14];
        byte[] source = BuildNameKeyedCff(charStrings);

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            CffSubsetter.Build(
                source,
                glyphCount: 125,
                new HashSet<ushort> { 0, 124 },
                cancellationToken: TestContext.Current.CancellationToken));

        Assert.Contains("seac-component", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildRejectsNestedSeacComponent()
    {
        byte[][] charStrings = Enumerable.Range(0, 127)
            .Select(_ => new byte[] { 14 })
            .ToArray();
        charStrings[34] = [139, 139, 204, 247, 86, 14];
        charStrings[125] = [139, 4, 14];
        charStrings[126] = [139, 139, 204, 247, 86, 14];
        byte[] source = BuildNameKeyedCff(charStrings);

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            CffSubsetter.Build(
                source,
                glyphCount: 127,
                new HashSet<ushort> { 0, 126 },
                cancellationToken: TestContext.Current.CancellationToken));

        Assert.Contains("seac-nested", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateRejectsDuplicateCustomCharsetSid()
    {
        byte[] source = BuildNameKeyedCffWithCustomCharset(
            [34, 34],
            0,
            [14],
            [139, 22, 14],
            [139, 4, 14]);

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            CffSubsetter.Validate(
                source,
                glyphCount: 3,
                new HashSet<ushort> { 0 },
                cancellationToken: TestContext.Current.CancellationToken));

        Assert.Contains("charset-duplicate", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildRewritesPrivateSubrsOffsetsAndIsIdempotent()
    {
        byte[] source = BuildNameKeyedCffWithPrivateSubrs(
            [14],
            [32, 10, 14],
            [139, 139, 139, 1, 14]);
        var retainedGlyphs = new HashSet<ushort> { 0, 1 };

        byte[] subset = CffSubsetter.Build(
            source,
            glyphCount: 3,
            retainedGlyphs,
            cancellationToken: TestContext.Current.CancellationToken);
        CffSubsetter.Validate(
            subset,
            glyphCount: 3,
            new HashSet<ushort> { 0, 1, 2 },
            cancellationToken: TestContext.Current.CancellationToken);
        byte[] rebuilt = CffSubsetter.Build(
            subset,
            glyphCount: 3,
            retainedGlyphs,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(subset.Length < source.Length);
        Assert.Equal(subset, rebuilt);
    }

    private static byte[] BuildNameKeyedCff(params byte[][] charStrings)
        => BuildNameKeyedCffWithPredefinedCharset(0, charStrings);

    private static byte[] BuildNameKeyedCffWithPredefinedCharset(
        int charset,
        params byte[][] charStrings)
    {
        byte[] nameIndex = [0, 1, 1, 1, 2, (byte)'A'];
        int topDictLength = charset == 0 ? 6 : 8;
        int topDictIndexLength = 5 + topDictLength;
        int charStringsOffset = 4 + nameIndex.Length + topDictIndexLength + 2 + 2;

        var topDict = new List<byte>(topDictLength);
        if (charset != 0)
        {
            topDict.Add(checked((byte)(139 + charset)));
            topDict.Add(15);
        }
        AppendDictOffset(topDict, charStringsOffset, 17);

        var bytes = new List<byte>
        {
            1, 0, 4, 1
        };
        bytes.AddRange(nameIndex);
        bytes.AddRange([0, 1, 1, 1, checked((byte)(topDictLength + 1))]);
        bytes.AddRange(topDict);
        bytes.AddRange([0, 0]);
        bytes.AddRange([0, 0]);
        AppendIndex(bytes, charStrings);
        return bytes.ToArray();
    }

    private static byte[] BuildNameKeyedCffWithCustomCharset(
        IReadOnlyList<ushort> charsetSids,
        int charsetFormat = 0,
        params byte[][] charStrings)
    {
        Assert.Equal(charStrings.Length - 1, charsetSids.Count);
        byte[] nameIndex = [0, 1, 1, 1, 2, (byte)'A'];
        const int topDictLength = 12;
        int topDictIndexLength = 5 + topDictLength;
        int charStringsOffset = 4 + nameIndex.Length + topDictIndexLength + 2 + 2;
        var charStringsIndex = new List<byte>();
        AppendIndex(charStringsIndex, charStrings);
        int charsetOffset = charStringsOffset + charStringsIndex.Count;
        var topDict = new List<byte>(topDictLength);
        AppendDictOffset(topDict, charsetOffset, 15);
        AppendDictOffset(topDict, charStringsOffset, 17);

        var bytes = new List<byte>
        {
            1, 0, 4, 1
        };
        bytes.AddRange(nameIndex);
        bytes.AddRange([0, 1, 1, 1, checked((byte)(topDictLength + 1))]);
        bytes.AddRange(topDict);
        bytes.AddRange([0, 0]);
        bytes.AddRange([0, 0]);
        bytes.AddRange(charStringsIndex);
        bytes.Add(checked((byte)charsetFormat));
        Span<byte> encoded = stackalloc byte[2];
        foreach (ushort sid in charsetSids)
        {
            BinaryPrimitives.WriteUInt16BigEndian(encoded, sid);
            bytes.AddRange(encoded.ToArray());
            if (charsetFormat == 1)
            {
                bytes.Add(0);
            }
            else if (charsetFormat == 2)
            {
                bytes.Add(0);
                bytes.Add(0);
            }
        }

        return bytes.ToArray();
    }

    private static byte[] BuildNameKeyedCffWithPrivateSubrs(params byte[][] charStrings)
    {
        byte[] nameIndex = [0, 1, 1, 1, 2, (byte)'A'];
        const int topDictLength = 17;
        int topDictIndexLength = 5 + topDictLength;
        int charStringsOffset = 4 + nameIndex.Length + topDictIndexLength + 2 + 2;
        var charStringsIndex = new List<byte>();
        AppendIndex(charStringsIndex, charStrings);
        int privateOffset = charStringsOffset + charStringsIndex.Count;
        const int privateLength = 6;
        var topDict = new List<byte>(topDictLength);
        AppendDictOffset(topDict, charStringsOffset, 17);
        AppendDictInteger(topDict, privateLength);
        AppendDictInteger(topDict, privateOffset);
        topDict.Add(18);

        var bytes = new List<byte>
        {
            1, 0, 4, 1
        };
        bytes.AddRange(nameIndex);
        bytes.AddRange([0, 1, 1, 1, checked((byte)(topDictLength + 1))]);
        bytes.AddRange(topDict);
        bytes.AddRange([0, 0]);
        bytes.AddRange([0, 0]);
        bytes.AddRange(charStringsIndex);
        AppendDictInteger(bytes, privateLength);
        bytes.Add(19);
        AppendIndex(bytes, [new byte[] { 139, 22, 11 }]);
        return bytes.ToArray();
    }

    private static void AppendDictOffset(List<byte> bytes, int offset, byte operation)
    {
        AppendDictInteger(bytes, offset);
        bytes.Add(operation);
    }

    private static void AppendDictInteger(List<byte> bytes, int value)
    {
        bytes.Add(29);
        Span<byte> encoded = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(encoded, value);
        bytes.AddRange(encoded.ToArray());
    }

    private static void AppendIndex(List<byte> bytes, byte[][] objects)
    {
        Span<byte> count = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(count, checked((ushort)objects.Length));
        bytes.AddRange(count.ToArray());
        if (objects.Length == 0)
        {
            return;
        }

        int dataLength = objects.Sum(item => item.Length);
        int offSize = dataLength + 1 <= byte.MaxValue ? 1 : 2;
        bytes.Add(checked((byte)offSize));
        int offset = 1;
        foreach (byte[] item in objects)
        {
            AppendOffset(bytes, offset, offSize);
            offset = checked(offset + item.Length);
        }
        AppendOffset(bytes, offset, offSize);
        foreach (byte[] item in objects)
        {
            bytes.AddRange(item);
        }
    }

    private static void AppendOffset(List<byte> bytes, int value, int size)
    {
        if (size == 1)
        {
            bytes.Add(checked((byte)value));
            return;
        }

        Span<byte> encoded = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(encoded, checked((ushort)value));
        bytes.AddRange(encoded.ToArray());
    }
}
