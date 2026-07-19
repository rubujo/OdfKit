using System.Buffers.Binary;

using OdfKit.WebFonts.OpenType;

namespace OdfKit.WebFonts.Tests;

public sealed class CffSubsetterTests
{
    [Fact]
    public void Build_AcceptsNameKeyedFontWithPredefinedCharset()
    {
        byte[] source = BuildNameKeyedCff([14], [139, 22, 14]);

        CffSubsetter.Validate(source, glyphCount: 2, new HashSet<ushort> { 0, 1 });
        byte[] subset = CffSubsetter.Build(source, glyphCount: 2, new HashSet<ushort> { 0 });
        CffSubsetter.Validate(subset, glyphCount: 2, new HashSet<ushort> { 0, 1 });

        Assert.NotEqual(source, subset);
        Assert.Equal(source.Length, subset.Length);
    }

    [Fact]
    public void Validate_DoesNotReuseCachedParseAcrossGlyphCounts()
    {
        byte[] source = BuildNameKeyedCff([14], [139, 22, 14]);
        CffSubsetter.Validate(source, glyphCount: 2, new HashSet<ushort> { 0 });

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            CffSubsetter.Validate(source, glyphCount: 1, new HashSet<ushort> { 0 }));

        Assert.Contains("CharStrings-count", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_RejectsPredefinedCharsetBeyondItsBound()
    {
        byte[][] charStrings = Enumerable.Repeat(new byte[] { 14 }, 230).ToArray();
        byte[] source = BuildNameKeyedCff(charStrings);

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            CffSubsetter.Validate(source, glyphCount: 230, new HashSet<ushort> { 0 }));

        Assert.Contains("charset-predefined", exception.Message, StringComparison.Ordinal);
    }

    private static byte[] BuildNameKeyedCff(params byte[][] charStrings)
    {
        byte[] nameIndex = [0, 1, 1, 1, 2, (byte)'A'];
        const int topDictLength = 6;
        int topDictIndexLength = 5 + topDictLength;
        int charStringsOffset = 4 + nameIndex.Length + topDictIndexLength + 2 + 2;

        var topDict = new byte[topDictLength];
        topDict[0] = 29;
        BinaryPrimitives.WriteInt32BigEndian(topDict.AsSpan(1, 4), charStringsOffset);
        topDict[5] = 17;

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

    private static void AppendIndex(List<byte> bytes, IReadOnlyList<byte[]> objects)
    {
        Span<byte> count = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(count, checked((ushort)objects.Count));
        bytes.AddRange(count.ToArray());
        if (objects.Count == 0)
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
