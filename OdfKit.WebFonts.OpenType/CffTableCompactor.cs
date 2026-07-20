using System.Buffers.Binary;

namespace OdfKit.WebFonts.OpenType;

internal static class CffTableCompactor
{
    private static readonly byte[] BlankCharString = [14];

    private const int CharsetOperator = 15;
    private const int EncodingOperator = 16;
    private const int CharStringsOperator = 17;
    private const int PrivateOperator = 18;
    private const int SubrsOperator = 19;
    private const int RosOperator = 0x0C1E;
    private const int FdArrayOperator = 0x0C24;
    private const int FdSelectOperator = 0x0C25;

    internal static byte[] Build(
        byte[] source,
        ushort glyphCount,
        ISet<ushort> retainedGlyphs,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return BuildCore(source, glyphCount, retainedGlyphs, cancellationToken);
        }
        catch (OverflowException)
        {
            throw SfntFont.DataInvalid("CFF-compact-overflow");
        }
    }

    private static byte[] BuildCore(
        byte[] source,
        ushort glyphCount,
        ISet<ushort> retainedGlyphs,
        CancellationToken cancellationToken)
    {
        Layout layout = Parse(source, glyphCount);
        byte[] compactCharStrings = BuildCharStrings(source, layout.CharStrings, retainedGlyphs, cancellationToken);
        var replacements = new List<Replacement>
        {
            new(layout.TopDictIndex.Start, layout.TopDictIndex.Length, Array.Empty<byte>()),
            new(layout.CharStrings.Start, layout.CharStrings.Length, compactCharStrings)
        };

        if (layout.FontDictIndex is not null)
        {
            replacements.Add(new Replacement(
                layout.FontDictIndex.Start,
                layout.FontDictIndex.Length,
                Array.Empty<byte>()));
        }

        var privateLengths = new Dictionary<int, int>();
        foreach (PrivateLayout privateLayout in layout.PrivateDicts)
        {
            byte[] bytes = RewritePrivateDict(privateLayout, offsetMap: null);
            if (privateLengths.ContainsKey(privateLayout.Start))
            {
                throw SfntFont.DataInvalid("CFF-compact-Private-duplicate");
            }

            privateLengths.Add(privateLayout.Start, bytes.Length);
            replacements.Add(new Replacement(
                privateLayout.Start,
                privateLayout.Length,
                bytes));
        }

        replacements[0].Bytes = BuildIndex(
            [RewriteTopDict(layout, privateLengths, offsetMap: null)]);
        if (layout.FontDictIndex is not null)
        {
            Replacement fontDictReplacement = replacements.Single(
                item => item.Start == layout.FontDictIndex.Start);
            fontDictReplacement.Bytes = BuildIndex(layout.FontDicts
                .Select(item => RewriteFontDict(item, privateLengths, offsetMap: null))
                .ToArray());
        }

        RelocationMap offsetMap = RelocationMap.Create(source.Length, replacements);
        replacements[0].Bytes = BuildIndex(
            [RewriteTopDict(layout, privateLengths, offsetMap)]);
        if (layout.FontDictIndex is not null)
        {
            Replacement fontDictReplacement = replacements.Single(
                item => item.Start == layout.FontDictIndex.Start);
            fontDictReplacement.Bytes = BuildIndex(layout.FontDicts
                .Select(item => RewriteFontDict(item, privateLengths, offsetMap))
                .ToArray());
        }

        foreach (PrivateLayout privateLayout in layout.PrivateDicts)
        {
            Replacement replacement = replacements.Single(item => item.Start == privateLayout.Start);
            replacement.Bytes = RewritePrivateDict(privateLayout, offsetMap);
        }

        offsetMap.VerifyLengths(replacements);
        return offsetMap.Apply(source, replacements);
    }

    private static Layout Parse(byte[] source, ushort glyphCount)
    {
        SfntFont.EnsureRange(source, 0, 4, "CFF-compact-header");
        IndexLayout nameIndex = ReadIndex(source, source[2], "CFF-compact-Name");
        IndexLayout topDictIndex = ReadIndex(source, nameIndex.End, "CFF-compact-TopDICT");
        IndexLayout stringIndex = ReadIndex(source, topDictIndex.End, "CFF-compact-String");
        _ = ReadIndex(source, stringIndex.End, "CFF-compact-GlobalSubrs");
        if (topDictIndex.Objects.Count != 1)
        {
            throw SfntFont.DataInvalid("CFF-compact-FontSet");
        }

        Range topRange = topDictIndex.Objects[0];
        IReadOnlyList<DictEntry> topDict = ReadDict(
            source.AsSpan(topRange.Start, topRange.Length),
            "CFF-compact-TopDICT");
        int charStringsOffset = GetOffset(topDict, CharStringsOperator, 1, "CFF-compact-CharStrings");
        IndexLayout charStrings = ReadIndex(source, charStringsOffset, "CFF-compact-CharStrings");
        if (charStrings.Objects.Count != glyphCount)
        {
            throw SfntFont.DataInvalid("CFF-compact-CharStrings-count");
        }

        var privateDicts = new List<PrivateLayout>();
        PrivateLayout? topPrivate = ReadPrivateLayout(source, topDict, "CFF-compact-Private");
        if (topPrivate is not null)
        {
            privateDicts.Add(topPrivate);
        }

        IndexLayout? fontDictIndex = null;
        IReadOnlyList<FontDictLayout> fontDicts = [];
        if (Contains(topDict, RosOperator))
        {
            int fdArrayOffset = GetOffset(topDict, FdArrayOperator, 1, "CFF-compact-FDArray");
            fontDictIndex = ReadIndex(source, fdArrayOffset, "CFF-compact-FDArray");
            var parsedFontDicts = new List<FontDictLayout>(fontDictIndex.Objects.Count);
            foreach (Range range in fontDictIndex.Objects)
            {
                IReadOnlyList<DictEntry> entries = ReadDict(
                    source.AsSpan(range.Start, range.Length),
                    "CFF-compact-FontDICT");
                PrivateLayout? privateLayout = ReadPrivateLayout(
                    source,
                    entries,
                    "CFF-compact-FontPrivate");
                if (privateLayout is not null
                    && privateDicts.All(item => item.Start != privateLayout.Start))
                {
                    privateDicts.Add(privateLayout);
                }

                parsedFontDicts.Add(new FontDictLayout(entries, privateLayout));
            }

            fontDicts = parsedFontDicts;
        }

        return new Layout(
            topDictIndex,
            topDict,
            topPrivate,
            charStrings,
            fontDictIndex,
            fontDicts,
            privateDicts);
    }

    private static PrivateLayout? ReadPrivateLayout(
        byte[] source,
        IReadOnlyList<DictEntry> parentDict,
        string detail)
    {
        DictEntry? entry = Find(parentDict, PrivateOperator);
        if (entry is null)
        {
            return null;
        }

        RequireOperands(entry, 2, detail);
        int length = ToInt(entry.Operands[0], $"{detail}-length");
        int offset = ToInt(entry.Operands[1], $"{detail}-offset");
        SfntFont.EnsureRange(source, offset, length, detail);
        IReadOnlyList<DictEntry> privateDict = ReadDict(
            source.AsSpan(offset, length),
            detail);
        DictEntry? subrs = Find(privateDict, SubrsOperator);
        int? subrsOffset = null;
        if (subrs is not null)
        {
            RequireOperands(subrs, 1, $"{detail}-Subrs");
            long relativeOffset = ToInt(subrs.Operands[0], $"{detail}-Subrs");
            subrsOffset = ToInt((long)offset + relativeOffset, $"{detail}-Subrs");
            _ = ReadIndex(source, subrsOffset.Value, $"{detail}-Subrs");
        }

        return new PrivateLayout(offset, length, privateDict, subrsOffset);
    }

    private static byte[] RewriteTopDict(
        Layout layout,
        IReadOnlyDictionary<int, int> privateLengths,
        RelocationMap? offsetMap)
    {
        var values = new Dictionary<int, int[]>();
        AddMappedOffset(values, layout.TopDict, CharsetOperator, offsetMap, predefinedMaximum: 2);
        AddMappedOffset(values, layout.TopDict, EncodingOperator, offsetMap, predefinedMaximum: 1);
        AddMappedOffset(values, layout.TopDict, CharStringsOperator, offsetMap);
        AddMappedOffset(values, layout.TopDict, FdArrayOperator, offsetMap);
        AddMappedOffset(values, layout.TopDict, FdSelectOperator, offsetMap);
        if (layout.TopPrivate is not null)
        {
            values[PrivateOperator] =
            [
                privateLengths[layout.TopPrivate.Start],
                offsetMap?.Map(layout.TopPrivate.Start) ?? 0
            ];
        }

        return RewriteDict(layout.TopDict, values);
    }

    private static byte[] RewriteFontDict(
        FontDictLayout fontDict,
        IReadOnlyDictionary<int, int> privateLengths,
        RelocationMap? offsetMap)
    {
        var values = new Dictionary<int, int[]>();
        if (fontDict.PrivateDict is not null)
        {
            values[PrivateOperator] =
            [
                privateLengths[fontDict.PrivateDict.Start],
                offsetMap?.Map(fontDict.PrivateDict.Start) ?? 0
            ];
        }

        return RewriteDict(fontDict.Entries, values);
    }

    private static byte[] RewritePrivateDict(PrivateLayout layout, RelocationMap? offsetMap)
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
        RelocationMap? offsetMap,
        int predefinedMaximum = -1)
    {
        DictEntry? entry = Find(entries, operation);
        if (entry is null)
        {
            return;
        }

        RequireOperands(entry, 1, "CFF-compact-offset");
        int original = ToInt(entry.Operands[0], "CFF-compact-offset");
        if (original <= predefinedMaximum)
        {
            return;
        }

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
        ISet<ushort> retainedGlyphs,
        CancellationToken cancellationToken)
    {
        var objects = new byte[charStrings.Objects.Count][];
        for (ushort glyph = 0; glyph < objects.Length; glyph++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Range range = charStrings.Objects[glyph];
            objects[glyph] = retainedGlyphs.Contains(glyph)
                ? source.AsSpan(range.Start, range.Length).ToArray()
                : BlankCharString;
        }

        return BuildIndex(objects);
    }

    private static byte[] BuildIndex(IReadOnlyList<byte[]> objects)
    {
        if (objects.Count == 0)
        {
            return [0, 0];
        }

        if (objects.Count > ushort.MaxValue)
        {
            throw SfntFont.DataInvalid("CFF-compact-INDEX-count");
        }

        int dataLength = 0;
        foreach (byte[] value in objects)
        {
            dataLength = checked(dataLength + value.Length);
        }

        uint maximumOffset = checked((uint)dataLength + 1);
        int offSize = maximumOffset <= byte.MaxValue
            ? 1
            : maximumOffset <= ushort.MaxValue
                ? 2
                : maximumOffset <= 0x00FF_FFFF
                    ? 3
                    : 4;
        int headerLength = checked(3 + ((objects.Count + 1) * offSize));
        var output = new byte[checked(headerLength + dataLength)];
        BinaryPrimitives.WriteUInt16BigEndian(output, checked((ushort)objects.Count));
        output[2] = checked((byte)offSize);
        int offset = 1;
        int dataPosition = headerLength;
        for (int index = 0; index < objects.Count; index++)
        {
            WriteOffset(output, 3 + (index * offSize), offSize, checked((uint)offset));
            byte[] value = objects[index];
            value.CopyTo(output, dataPosition);
            offset = checked(offset + value.Length);
            dataPosition = checked(dataPosition + value.Length);
        }

        WriteOffset(output, 3 + (objects.Count * offSize), offSize, checked((uint)offset));
        return output;
    }

    private static IndexLayout ReadIndex(byte[] source, int offset, string detail)
    {
        SfntFont.EnsureRange(source, offset, 2, detail);
        int count = SfntFont.ReadUInt16(source, offset, detail);
        if (count == 0)
        {
            return new IndexLayout(offset, 2, []);
        }

        SfntFont.EnsureRange(source, offset + 2, 1, detail);
        int offSize = source[offset + 2];
        if (offSize is < 1 or > 4)
        {
            throw SfntFont.DataInvalid($"{detail}-offSize");
        }

        int offsetsStart = checked(offset + 3);
        int dataStart = checked(offsetsStart + ((count + 1) * offSize));
        SfntFont.EnsureRange(source, offsetsStart, checked((count + 1) * offSize), detail);
        var offsets = new int[count + 1];
        int previous = 0;
        for (int index = 0; index <= count; index++)
        {
            int value = ToInt(ReadOffset(source, offsetsStart + (index * offSize), offSize), detail);
            if (index == 0 && value != 1 || value < previous)
            {
                throw SfntFont.DataInvalid($"{detail}-offset");
            }

            offsets[index] = value;
            previous = value;
        }

        int dataLength = checked(offsets[count] - 1);
        SfntFont.EnsureRange(source, dataStart, dataLength, detail);
        var objects = new Range[count];
        for (int index = 0; index < count; index++)
        {
            objects[index] = new Range(
                checked(dataStart + offsets[index] - 1),
                checked(offsets[index + 1] - offsets[index]));
        }

        int length = checked((dataStart + dataLength) - offset);
        return new IndexLayout(offset, length, objects);
    }

    private static IReadOnlyList<DictEntry> ReadDict(ReadOnlySpan<byte> data, string detail)
    {
        var entries = new List<DictEntry>();
        var operands = new List<long?>(48);
        int position = 0;
        int entryStart = 0;
        while (position < data.Length)
        {
            byte value = data[position++];
            if (value >= 32 || value is 28 or 29 or 30)
            {
                if (operands.Count >= 48)
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
            else if (value <= 21)
            {
                operation = value;
            }
            else
            {
                throw SfntFont.DataInvalid($"{detail}-operator");
            }

            if (entries.Any(item => item.Operation == operation))
            {
                throw SfntFont.DataInvalid($"{detail}-duplicate");
            }

            entries.Add(new DictEntry(
                operation,
                operands.ToArray(),
                data.Slice(entryStart, position - entryStart).ToArray()));
            operands.Clear();
            entryStart = position;
        }

        if (operands.Count != 0)
        {
            throw SfntFont.DataInvalid($"{detail}-trailing");
        }

        return entries;
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

    private static bool Contains(IReadOnlyList<DictEntry> entries, int operation)
        => Find(entries, operation) is not null;

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
            throw SfntFont.DataInvalid("CFF-compact-INDEX-offset");
        }
    }

    internal sealed class RelocationMap
    {
        private readonly int _sourceLength;
        private readonly ReplacementInfo[] _replacements;

        private RelocationMap(int sourceLength, ReplacementInfo[] replacements)
        {
            _sourceLength = sourceLength;
            _replacements = replacements;
        }

        internal static RelocationMap Create(int sourceLength, IReadOnlyList<Replacement> replacements)
        {
            Replacement[] ordered = replacements.OrderBy(item => item.Start).ToArray();
            var result = new ReplacementInfo[ordered.Length];
            int previousEnd = 0;
            int delta = 0;
            for (int index = 0; index < ordered.Length; index++)
            {
                Replacement replacement = ordered[index];
                if (replacement.Start < previousEnd
                    || replacement.Start < 0
                    || replacement.Length < 0
                    || replacement.Start > sourceLength - replacement.Length)
                {
                    throw SfntFont.DataInvalid("CFF-compact-overlap");
                }

                result[index] = new ReplacementInfo(
                    replacement.Start,
                    replacement.Length,
                    replacement.Bytes.Length,
                    delta);
                delta = checked(delta + replacement.Bytes.Length - replacement.Length);
                previousEnd = checked(replacement.Start + replacement.Length);
            }

            _ = checked(sourceLength + delta);
            return new RelocationMap(sourceLength, result);
        }

        internal int Map(int originalOffset)
        {
            if ((uint)originalOffset > (uint)_sourceLength)
            {
                throw SfntFont.DataInvalid("CFF-compact-offset");
            }

            int delta = 0;
            foreach (ReplacementInfo replacement in _replacements)
            {
                if (originalOffset < replacement.Start)
                {
                    break;
                }

                if (originalOffset == replacement.Start)
                {
                    return checked(originalOffset + replacement.DeltaBefore);
                }

                int end = checked(replacement.Start + replacement.OldLength);
                if (originalOffset < end)
                {
                    throw SfntFont.DataInvalid("CFF-compact-interior-offset");
                }

                delta = checked(replacement.DeltaBefore + replacement.NewLength - replacement.OldLength);
            }

            return checked(originalOffset + delta);
        }

        internal void VerifyLengths(IReadOnlyList<Replacement> replacements)
        {
            foreach (Replacement replacement in replacements)
            {
                ReplacementInfo expected = _replacements.Single(item => item.Start == replacement.Start);
                if (replacement.Bytes.Length != expected.NewLength)
                {
                    throw SfntFont.DataInvalid("CFF-compact-layout-changed");
                }
            }
        }

        internal byte[] Apply(byte[] source, IReadOnlyList<Replacement> replacements)
        {
            Replacement[] ordered = replacements.OrderBy(item => item.Start).ToArray();
            int outputLength = source.Length;
            foreach (Replacement replacement in ordered)
            {
                outputLength = checked(outputLength + replacement.Bytes.Length - replacement.Length);
            }

            var output = new byte[outputLength];
            int sourcePosition = 0;
            int outputPosition = 0;
            foreach (Replacement replacement in ordered)
            {
                int unchangedLength = checked(replacement.Start - sourcePosition);
                source.AsSpan(sourcePosition, unchangedLength)
                    .CopyTo(output.AsSpan(outputPosition, unchangedLength));
                outputPosition = checked(outputPosition + unchangedLength);
                replacement.Bytes.CopyTo(output, outputPosition);
                outputPosition = checked(outputPosition + replacement.Bytes.Length);
                sourcePosition = checked(replacement.Start + replacement.Length);
            }

            source.AsSpan(sourcePosition).CopyTo(output.AsSpan(outputPosition));
            return output;
        }
    }

    internal sealed class Replacement(int start, int length, byte[] bytes)
    {
        internal int Start { get; } = start;

        internal int Length { get; } = length;

        internal byte[] Bytes { get; set; } = bytes;
    }

    private sealed class DictEntry(int operation, long?[] operands, byte[] raw)
    {
        internal int Operation { get; } = operation;

        internal long?[] Operands { get; } = operands;

        internal byte[] Raw { get; } = raw;
    }

    private sealed class Layout(
        IndexLayout topDictIndex,
        IReadOnlyList<DictEntry> topDict,
        PrivateLayout? topPrivate,
        IndexLayout charStrings,
        IndexLayout? fontDictIndex,
        IReadOnlyList<FontDictLayout> fontDicts,
        IReadOnlyList<PrivateLayout> privateDicts)
    {
        internal IndexLayout TopDictIndex { get; } = topDictIndex;

        internal IReadOnlyList<DictEntry> TopDict { get; } = topDict;

        internal PrivateLayout? TopPrivate { get; } = topPrivate;

        internal IndexLayout CharStrings { get; } = charStrings;

        internal IndexLayout? FontDictIndex { get; } = fontDictIndex;

        internal IReadOnlyList<FontDictLayout> FontDicts { get; } = fontDicts;

        internal IReadOnlyList<PrivateLayout> PrivateDicts { get; } = privateDicts;
    }

    private sealed class FontDictLayout(
        IReadOnlyList<DictEntry> entries,
        PrivateLayout? privateDict)
    {
        internal IReadOnlyList<DictEntry> Entries { get; } = entries;

        internal PrivateLayout? PrivateDict { get; } = privateDict;
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

    private sealed class IndexLayout(int start, int length, IReadOnlyList<Range> objects)
    {
        internal int Start { get; } = start;

        internal int Length { get; } = length;

        internal int End { get; } = checked(start + length);

        internal IReadOnlyList<Range> Objects { get; } = objects;
    }

    private readonly struct Range(int start, int length)
    {
        internal int Start { get; } = start;

        internal int Length { get; } = length;
    }

    private readonly struct ReplacementInfo(
        int start,
        int oldLength,
        int newLength,
        int deltaBefore)
    {
        internal int Start { get; } = start;

        internal int OldLength { get; } = oldLength;

        internal int NewLength { get; } = newLength;

        internal int DeltaBefore { get; } = deltaBefore;
    }
}
