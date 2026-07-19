using System.Buffers.Binary;

namespace OdfKit.WebFonts.OpenType;

internal static class Cff2TableCompactor
{
    private static readonly byte[] BlankCharString = [19];

    private const int CharStringsOperator = 17;
    private const int PrivateOperator = 18;
    private const int SubrsOperator = 19;
    private const int VsIndexOperator = 22;
    private const int BlendOperator = 23;
    private const int VariationStoreOperator = 24;
    private const int FontDictArrayOperator = 0x0C24;
    private const int FontDictSelectOperator = 0x0C25;

    internal static byte[] Build(
        byte[] source,
        ushort glyphCount,
        ISet<ushort> retainedGlyphs,
        int[] variationRegionCounts)
    {
        try
        {
            return BuildCore(source, glyphCount, retainedGlyphs, variationRegionCounts);
        }
        catch (OverflowException)
        {
            throw SfntFont.DataInvalid("CFF2-compact-overflow");
        }
    }

    private static byte[] BuildCore(
        byte[] source,
        ushort glyphCount,
        ISet<ushort> retainedGlyphs,
        int[] variationRegionCounts)
    {
        Layout layout = Parse(source, glyphCount, variationRegionCounts);
        var replacements = new List<CffTableCompactor.Replacement>
        {
            new(layout.TopDictStart, layout.TopDictLength, Array.Empty<byte>()),
            new(
                layout.CharStrings.Start,
                layout.CharStrings.Length,
                BuildCharStrings(source, layout.CharStrings, retainedGlyphs)),
            new(layout.FontDictIndex.Start, layout.FontDictIndex.Length, Array.Empty<byte>())
        };

        var privateLengths = new Dictionary<int, int>();
        foreach (PrivateLayout privateLayout in layout.PrivateDicts)
        {
            byte[] bytes = RewritePrivateDict(privateLayout, offsetMap: null);
            if (privateLengths.ContainsKey(privateLayout.Start))
            {
                throw SfntFont.DataInvalid("CFF2-compact-Private-duplicate");
            }

            privateLengths.Add(privateLayout.Start, bytes.Length);
            replacements.Add(new CffTableCompactor.Replacement(
                privateLayout.Start,
                privateLayout.Length,
                bytes));
        }

        replacements[0].Bytes = RewriteTopDict(layout, offsetMap: null);
        replacements[2].Bytes = BuildIndex(layout.FontDicts
            .Select(item => RewriteFontDict(item, privateLengths, offsetMap: null))
            .ToArray());

        CffTableCompactor.RelocationMap offsetMap =
            CffTableCompactor.RelocationMap.Create(source.Length, replacements);
        replacements[0].Bytes = RewriteTopDict(layout, offsetMap);
        replacements[2].Bytes = BuildIndex(layout.FontDicts
            .Select(item => RewriteFontDict(item, privateLengths, offsetMap))
            .ToArray());
        foreach (PrivateLayout privateLayout in layout.PrivateDicts)
        {
            CffTableCompactor.Replacement replacement = replacements.Single(
                item => item.Start == privateLayout.Start);
            replacement.Bytes = RewritePrivateDict(privateLayout, offsetMap);
        }

        offsetMap.VerifyLengths(replacements);
        byte[] output = offsetMap.Apply(source, replacements);
        if (replacements[0].Bytes.Length > ushort.MaxValue)
        {
            throw SfntFont.DataInvalid("CFF2-compact-TopDICT-length");
        }

        BinaryPrimitives.WriteUInt16BigEndian(
            output.AsSpan(3, 2),
            checked((ushort)replacements[0].Bytes.Length));
        return output;
    }

    private static Layout Parse(
        byte[] source,
        ushort glyphCount,
        int[] variationRegionCounts)
    {
        SfntFont.EnsureRange(source, 0, 5, "CFF2-compact-header");
        int topDictStart = source[2];
        int topDictLength = SfntFont.ReadUInt16(source, 3, "CFF2-compact-TopDICT-length");
        SfntFont.EnsureRange(source, topDictStart, topDictLength, "CFF2-compact-TopDICT");
        IReadOnlyList<DictEntry> topDict = ReadDict(
            source.AsSpan(topDictStart, topDictLength),
            "CFF2-compact-TopDICT",
            variationRegionCounts: null);
        int charStringsOffset = GetOffset(
            topDict,
            CharStringsOperator,
            1,
            "CFF2-compact-CharStrings");
        IndexLayout charStrings = ReadIndex(
            source,
            charStringsOffset,
            "CFF2-compact-CharStrings",
            glyphCount);
        if (charStrings.Objects.Count != glyphCount)
        {
            throw SfntFont.DataInvalid("CFF2-compact-CharStrings-count");
        }

        int fontDictArrayOffset = GetOffset(
            topDict,
            FontDictArrayOperator,
            1,
            "CFF2-compact-FontDICTINDEX");
        IndexLayout fontDictIndex = ReadIndex(
            source,
            fontDictArrayOffset,
            "CFF2-compact-FontDICTINDEX",
            maximumCount: 4096);
        if (fontDictIndex.Objects.Count == 0)
        {
            throw SfntFont.DataInvalid("CFF2-compact-FontDICTINDEX-count");
        }

        var fontDicts = new List<FontDictLayout>(fontDictIndex.Objects.Count);
        var privateDicts = new List<PrivateLayout>(fontDictIndex.Objects.Count);
        foreach (DataRange range in fontDictIndex.Objects)
        {
            IReadOnlyList<DictEntry> entries = ReadDict(
                source.AsSpan(range.Start, range.Length),
                "CFF2-compact-FontDICT",
                variationRegionCounts: null);
            PrivateLayout privateLayout = ReadPrivateLayout(
                source,
                entries,
                variationRegionCounts);
            if (privateDicts.All(item => item.Start != privateLayout.Start))
            {
                privateDicts.Add(privateLayout);
            }

            fontDicts.Add(new FontDictLayout(entries, privateLayout));
        }

        return new Layout(
            topDictStart,
            topDictLength,
            topDict,
            charStrings,
            fontDictIndex,
            fontDicts,
            privateDicts);
    }

    private static PrivateLayout ReadPrivateLayout(
        byte[] source,
        IReadOnlyList<DictEntry> fontDict,
        int[] variationRegionCounts)
    {
        DictEntry entry = Find(fontDict, PrivateOperator)
            ?? throw SfntFont.DataInvalid("CFF2-compact-Private-missing");
        RequireOperands(entry, 2, "CFF2-compact-Private");
        int length = ToInt(entry.Operands[0], "CFF2-compact-Private-length");
        int offset = ToInt(entry.Operands[1], "CFF2-compact-Private-offset");
        SfntFont.EnsureRange(source, offset, length, "CFF2-compact-Private");
        IReadOnlyList<DictEntry> privateDict = ReadDict(
            source.AsSpan(offset, length),
            "CFF2-compact-Private",
            variationRegionCounts);
        DictEntry? subrs = Find(privateDict, SubrsOperator);
        int? subrsOffset = null;
        if (subrs is not null)
        {
            RequireOperands(subrs, 1, "CFF2-compact-Subrs");
            long relativeOffset = ToInt(subrs.Operands[0], "CFF2-compact-Subrs");
            subrsOffset = ToInt((long)offset + relativeOffset, "CFF2-compact-Subrs");
            _ = ReadIndex(source, subrsOffset.Value, "CFF2-compact-Subrs", 65_535);
        }

        return new PrivateLayout(offset, length, privateDict, subrsOffset);
    }

    private static byte[] RewriteTopDict(
        Layout layout,
        CffTableCompactor.RelocationMap? offsetMap)
    {
        var values = new Dictionary<int, int[]>();
        AddMappedOffset(values, layout.TopDict, CharStringsOperator, offsetMap);
        AddMappedOffset(values, layout.TopDict, VariationStoreOperator, offsetMap);
        AddMappedOffset(values, layout.TopDict, FontDictArrayOperator, offsetMap);
        AddMappedOffset(values, layout.TopDict, FontDictSelectOperator, offsetMap);
        return RewriteDict(layout.TopDict, values);
    }

    private static byte[] RewriteFontDict(
        FontDictLayout layout,
        IReadOnlyDictionary<int, int> privateLengths,
        CffTableCompactor.RelocationMap? offsetMap)
    {
        var values = new Dictionary<int, int[]>
        {
            [PrivateOperator] =
            [
                privateLengths[layout.PrivateDict.Start],
                offsetMap?.Map(layout.PrivateDict.Start) ?? 0
            ]
        };
        return RewriteDict(layout.Entries, values);
    }

    private static byte[] RewritePrivateDict(
        PrivateLayout layout,
        CffTableCompactor.RelocationMap? offsetMap)
    {
        var values = new Dictionary<int, int[]>();
        if (layout.SubrsOffset is int subrsOffset)
        {
            int relativeOffset = offsetMap is null
                ? 0
                : checked(offsetMap.Map(subrsOffset) - offsetMap.Map(layout.Start));
            values[SubrsOperator] = [relativeOffset];
        }

        return RewriteDict(layout.Entries, values);
    }

    private static void AddMappedOffset(
        IDictionary<int, int[]> values,
        IReadOnlyList<DictEntry> entries,
        int operation,
        CffTableCompactor.RelocationMap? offsetMap)
    {
        DictEntry? entry = Find(entries, operation);
        if (entry is null)
        {
            return;
        }

        RequireOperands(entry, 1, "CFF2-compact-offset");
        int original = ToInt(entry.Operands[0], "CFF2-compact-offset");
        values[operation] = [offsetMap?.Map(original) ?? 0];
    }

    private static byte[] RewriteDict(
        IReadOnlyList<DictEntry> entries,
        IReadOnlyDictionary<int, int[]> replacementValues)
    {
        var output = new List<byte>();
        var encoded = new byte[4];
        foreach (DictEntry entry in entries)
        {
            if (!replacementValues.TryGetValue(entry.Operation, out int[]? values))
            {
                output.AddRange(entry.Raw);
                continue;
            }

            foreach (int value in values)
            {
                output.Add(29);
                BinaryPrimitives.WriteInt32BigEndian(encoded, value);
                output.AddRange(encoded);
            }

            if ((entry.Operation & 0x0C00) != 0)
            {
                output.Add(12);
                output.Add(checked((byte)(entry.Operation & 0xFF)));
            }
            else
            {
                output.Add(checked((byte)entry.Operation));
            }
        }

        return output.ToArray();
    }

    private static byte[] BuildCharStrings(
        byte[] source,
        IndexLayout charStrings,
        ISet<ushort> retainedGlyphs)
    {
        var objects = new byte[charStrings.Objects.Count][];
        for (ushort glyph = 0; glyph < objects.Length; glyph++)
        {
            DataRange range = charStrings.Objects[glyph];
            objects[glyph] = retainedGlyphs.Contains(glyph)
                ? source.AsSpan(range.Start, range.Length).ToArray()
                : BlankCharString;
        }

        return BuildIndex(objects);
    }

    private static byte[] BuildIndex(IReadOnlyList<byte[]> objects)
    {
        int dataLength = 0;
        foreach (byte[] value in objects)
        {
            dataLength = checked(dataLength + value.Length);
        }

        if (objects.Count == 0)
        {
            return [0, 0, 0, 0];
        }

        uint maximumOffset = checked((uint)dataLength + 1);
        int offSize = maximumOffset <= byte.MaxValue
            ? 1
            : maximumOffset <= ushort.MaxValue
                ? 2
                : maximumOffset <= 0x00FF_FFFF
                    ? 3
                    : 4;
        int headerLength = checked(5 + ((objects.Count + 1) * offSize));
        var output = new byte[checked(headerLength + dataLength)];
        BinaryPrimitives.WriteUInt32BigEndian(output, checked((uint)objects.Count));
        output[4] = checked((byte)offSize);
        int offset = 1;
        int dataPosition = headerLength;
        for (int index = 0; index < objects.Count; index++)
        {
            WriteOffset(output, 5 + (index * offSize), offSize, checked((uint)offset));
            byte[] value = objects[index];
            value.CopyTo(output, dataPosition);
            offset = checked(offset + value.Length);
            dataPosition = checked(dataPosition + value.Length);
        }

        WriteOffset(output, 5 + (objects.Count * offSize), offSize, checked((uint)offset));
        return output;
    }

    private static IndexLayout ReadIndex(
        byte[] source,
        int offset,
        string detail,
        int maximumCount)
    {
        SfntFont.EnsureRange(source, offset, 4, detail);
        int count = ToInt(SfntFont.ReadUInt32(source, offset, detail), detail);
        if (count > maximumCount)
        {
            throw SfntFont.DataInvalid($"{detail}-count");
        }

        if (count == 0)
        {
            return new IndexLayout(offset, 4, []);
        }

        SfntFont.EnsureRange(source, offset + 4, 1, detail);
        int offSize = source[offset + 4];
        if (offSize is < 1 or > 4)
        {
            throw SfntFont.DataInvalid($"{detail}-offSize");
        }

        int offsetsStart = checked(offset + 5);
        int offsetBytes = checked((count + 1) * offSize);
        SfntFont.EnsureRange(source, offsetsStart, offsetBytes, detail);
        int dataStart = checked(offsetsStart + offsetBytes);
        var offsets = new int[count + 1];
        int previous = 0;
        for (int index = 0; index <= count; index++)
        {
            int value = ToInt(
                ReadOffset(source, offsetsStart + (index * offSize), offSize),
                detail);
            if (index == 0 && value != 1 || value < previous)
            {
                throw SfntFont.DataInvalid($"{detail}-offset");
            }

            offsets[index] = value;
            previous = value;
        }

        int dataLength = checked(offsets[count] - 1);
        SfntFont.EnsureRange(source, dataStart, dataLength, detail);
        var objects = new DataRange[count];
        for (int index = 0; index < count; index++)
        {
            objects[index] = new DataRange(
                checked(dataStart + offsets[index] - 1),
                checked(offsets[index + 1] - offsets[index]));
        }

        int length = checked((dataStart + dataLength) - offset);
        return new IndexLayout(offset, length, objects);
    }

    private static IReadOnlyList<DictEntry> ReadDict(
        ReadOnlySpan<byte> data,
        string detail,
        int[]? variationRegionCounts)
    {
        var entries = new List<DictEntry>();
        var operands = new List<long?>(513);
        var operations = new HashSet<int>();
        int activeVariationIndex = 0;
        int position = 0;
        int entryStart = 0;
        while (position < data.Length)
        {
            byte value = data[position++];
            if (value >= 32 || value is 28 or 29 or 30)
            {
                if (operands.Count >= 513)
                {
                    throw SfntFont.DataInvalid($"{detail}-stack");
                }

                operands.Add(ReadDictNumber(data, ref position, value, detail));
                continue;
            }

            int operation;
            if (value == 12)
            {
                SfntFont.EnsureRange(data, position, 1, detail);
                operation = 0x0C00 | data[position++];
            }
            else
            {
                operation = value;
            }

            if (operation == BlendOperator)
            {
                ProcessBlend(operands, variationRegionCounts, activeVariationIndex, detail);
                continue;
            }

            if (!operations.Add(operation))
            {
                throw SfntFont.DataInvalid($"{detail}-duplicate");
            }

            long?[] values = operands.ToArray();
            entries.Add(new DictEntry(
                operation,
                values,
                data.Slice(entryStart, position - entryStart).ToArray()));
            operands.Clear();
            entryStart = position;
            if (operation == VsIndexOperator)
            {
                activeVariationIndex = GetVariationIndex(
                    values,
                    variationRegionCounts,
                    detail);
            }
        }

        if (operands.Count != 0)
        {
            throw SfntFont.DataInvalid($"{detail}-trailing");
        }

        return entries;
    }

    private static void ProcessBlend(
        List<long?> operands,
        int[]? variationRegionCounts,
        int variationIndex,
        string detail)
    {
        if (variationRegionCounts is null
            || (uint)variationIndex >= (uint)variationRegionCounts.Length
            || operands.Count == 0)
        {
            throw SfntFont.DataInvalid($"{detail}-blend");
        }

        int valueCount = ToInt(operands[operands.Count - 1], $"{detail}-blend-count");
        int regionCount = variationRegionCounts[variationIndex];
        int consumed = checked(1 + valueCount + (valueCount * regionCount));
        if (valueCount <= 0 || consumed > operands.Count)
        {
            throw SfntFont.DataInvalid($"{detail}-blend-stack");
        }

        int start = operands.Count - consumed;
        operands.RemoveRange(start, consumed);
        for (int index = 0; index < valueCount; index++)
        {
            operands.Add(null);
        }
    }

    private static int GetVariationIndex(
        long?[] operands,
        int[]? variationRegionCounts,
        string detail)
    {
        if (variationRegionCounts is null || operands.Length != 1)
        {
            throw SfntFont.DataInvalid($"{detail}-vsindex");
        }

        int index = ToInt(operands[0], $"{detail}-vsindex");
        return (uint)index < (uint)variationRegionCounts.Length
            ? index
            : throw SfntFont.DataInvalid($"{detail}-vsindex");
    }

    private static long? ReadDictNumber(
        ReadOnlySpan<byte> data,
        ref int position,
        byte first,
        string detail)
    {
        if (first is >= 32 and <= 246)
        {
            return first - 139;
        }

        if (first is >= 247 and <= 250)
        {
            SfntFont.EnsureRange(data, position, 1, detail);
            return ((first - 247) * 256L) + data[position++] + 108;
        }

        if (first is >= 251 and <= 254)
        {
            SfntFont.EnsureRange(data, position, 1, detail);
            return -((first - 251) * 256L) - data[position++] - 108;
        }

        if (first == 28)
        {
            SfntFont.EnsureRange(data, position, 2, detail);
            long result = BinaryPrimitives.ReadInt16BigEndian(data.Slice(position, 2));
            position += 2;
            return result;
        }

        if (first == 29)
        {
            SfntFont.EnsureRange(data, position, 4, detail);
            long result = BinaryPrimitives.ReadInt32BigEndian(data.Slice(position, 4));
            position += 4;
            return result;
        }

        if (first != 30)
        {
            throw SfntFont.DataInvalid($"{detail}-number");
        }

        bool terminated = false;
        while (position < data.Length && !terminated)
        {
            byte pair = data[position++];
            terminated = (pair >> 4) == 0x0F || (pair & 0x0F) == 0x0F;
        }

        return terminated ? null : throw SfntFont.DataInvalid($"{detail}-real");
    }

    private static DictEntry? Find(IReadOnlyList<DictEntry> entries, int operation)
        => entries.FirstOrDefault(item => item.Operation == operation);

    private static int GetOffset(
        IReadOnlyList<DictEntry> entries,
        int operation,
        int operandCount,
        string detail)
    {
        DictEntry? entry = Find(entries, operation);
        if (entry is null)
        {
            throw SfntFont.DataInvalid(detail);
        }

        RequireOperands(entry, operandCount, detail);
        return ToInt(entry.Operands[0], detail);
    }

    private static void RequireOperands(DictEntry entry, int count, string detail)
    {
        if (entry.Operands.Length != count)
        {
            throw SfntFont.DataInvalid(detail);
        }
    }

    private static int ToInt(long? value, string detail)
        => value is long known && known >= 0 && known <= int.MaxValue
            ? (int)known
            : throw SfntFont.DataInvalid(detail);

    private static int ToInt(uint value, string detail)
        => value <= int.MaxValue ? (int)value : throw SfntFont.DataInvalid(detail);

    private static uint ReadOffset(byte[] source, int offset, int size)
    {
        uint value = 0;
        for (int index = 0; index < size; index++)
        {
            value = (value << 8) | source[offset + index];
        }

        return value;
    }

    private static void WriteOffset(byte[] output, int offset, int size, uint value)
    {
        for (int index = size - 1; index >= 0; index--)
        {
            output[offset + index] = checked((byte)(value & 0xFF));
            value >>= 8;
        }

        if (value != 0)
        {
            throw SfntFont.DataInvalid("CFF2-compact-INDEX-offset");
        }
    }

    private sealed class Layout(
        int topDictStart,
        int topDictLength,
        IReadOnlyList<DictEntry> topDict,
        IndexLayout charStrings,
        IndexLayout fontDictIndex,
        IReadOnlyList<FontDictLayout> fontDicts,
        IReadOnlyList<PrivateLayout> privateDicts)
    {
        internal int TopDictStart { get; } = topDictStart;

        internal int TopDictLength { get; } = topDictLength;

        internal IReadOnlyList<DictEntry> TopDict { get; } = topDict;

        internal IndexLayout CharStrings { get; } = charStrings;

        internal IndexLayout FontDictIndex { get; } = fontDictIndex;

        internal IReadOnlyList<FontDictLayout> FontDicts { get; } = fontDicts;

        internal IReadOnlyList<PrivateLayout> PrivateDicts { get; } = privateDicts;
    }

    private sealed class FontDictLayout(
        IReadOnlyList<DictEntry> entries,
        PrivateLayout privateDict)
    {
        internal IReadOnlyList<DictEntry> Entries { get; } = entries;

        internal PrivateLayout PrivateDict { get; } = privateDict;
    }

    private sealed class PrivateLayout(
        int start,
        int length,
        IReadOnlyList<DictEntry> entries,
        int? subrsOffset)
    {
        internal int Start { get; } = start;

        internal int Length { get; } = length;

        internal IReadOnlyList<DictEntry> Entries { get; } = entries;

        internal int? SubrsOffset { get; } = subrsOffset;
    }

    private sealed class IndexLayout(
        int start,
        int length,
        IReadOnlyList<DataRange> objects)
    {
        internal int Start { get; } = start;

        internal int Length { get; } = length;

        internal IReadOnlyList<DataRange> Objects { get; } = objects;
    }

    private sealed class DictEntry(int operation, long?[] operands, byte[] raw)
    {
        internal int Operation { get; } = operation;

        internal long?[] Operands { get; } = operands;

        internal byte[] Raw { get; } = raw;
    }

    private readonly struct DataRange(int start, int length)
    {
        internal int Start { get; } = start;

        internal int Length { get; } = length;
    }
}
