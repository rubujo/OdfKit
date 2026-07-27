using System.Buffers.Binary;

using System.Runtime.CompilerServices;

namespace OdfKit.WebFonts.OpenType;

internal static class Cff2Subsetter
{
    private static readonly ConditionalWeakTable<byte[], ParsedCff2CacheEntry> ParsedFonts = new();

    private const int CharStringsOperator = 17;
    private const int PrivateOperator = 18;
    private const int SubrsOperator = 19;
    private const int VsIndexOperator = 22;
    private const int BlendOperator = 23;
    private const int VariationStoreOperator = 24;
    private const int FontDictArrayOperator = 0x0C24;
    private const int FontDictSelectOperator = 0x0C25;
    private const int FontMatrixOperator = 0x0C07;
    private const int MaximumFontDictCount = 4096;
    private const int MaximumVariationDataCount = 4096;
    private const int MaximumVariationRegionCount = 4096;

    private static readonly HashSet<int> TopDictOperators =
    [CharStringsOperator, VariationStoreOperator, FontDictArrayOperator, FontDictSelectOperator, FontMatrixOperator];

    private static readonly HashSet<int> FontDictOperators = [PrivateOperator, FontMatrixOperator];

    private static readonly HashSet<int> PrivateDictOperators =
    [
        SubrsOperator, VsIndexOperator, BlendOperator, 6, 7, 8, 9, 10, 11,
        0x0C09, 0x0C0A, 0x0C0B, 0x0C0C, 0x0C0D, 0x0C11, 0x0C12
    ];

    internal static byte[] Build(
        byte[] source,
        byte[]? fvar,
        ushort glyphCount,
        ISet<ushort> selectedGlyphs,
        CancellationToken cancellationToken = default)
    {
        ParsedCff2 parsed = GetParsed(source, fvar, glyphCount);
        VerifySelectedGlyphs(source, glyphCount, selectedGlyphs, parsed, cancellationToken);

        return Cff2TableCompactor.Build(
            source,
            glyphCount,
            selectedGlyphs,
            parsed.VariationRegionCounts,
            cancellationToken);
    }

    internal static void Validate(
        byte[] source,
        byte[]? fvar,
        ushort glyphCount,
        ISet<ushort> selectedGlyphs,
        CancellationToken cancellationToken = default)
    {
        ParsedCff2 parsed = GetParsed(source, fvar, glyphCount);
        VerifySelectedGlyphs(source, glyphCount, selectedGlyphs, parsed, cancellationToken);
    }

    private static ParsedCff2 Parse(byte[] source, ushort? variationAxisCount, ushort glyphCount)
    {
        SfntFont.EnsureRange(source, 0, 5, "CFF2-header");
        if (source[0] != 2 || source[1] != 0 || source[2] != 5)
        {
            throw SfntFont.DataInvalid("CFF2-header");
        }

        int topDictLength = SfntFont.ReadUInt16(source, 3, "CFF2-TopDICT-length");
        SfntFont.EnsureRange(source, source[2], topDictLength, "CFF2-TopDICT");
        Dictionary<int, double?[]> topDict = ReadDict(
            source.AsSpan(source[2], topDictLength),
            "CFF2-TopDICT",
            TopDictOperators,
            variationRegionCounts: null);
        int globalSubrOffset = checked(source[2] + topDictLength);
        Cff2Index globalSubrs = ReadIndex(source, globalSubrOffset, "CFF2-GlobalSubrs", 65_535);
        int charStringsOffset = GetOffset(topDict, CharStringsOperator, 1, "CFF2-CharStrings");
        int fontDictArrayOffset = GetOffset(topDict, FontDictArrayOperator, 1, "CFF2-FontDICTINDEX");

        int[] variationRegionCounts;
        if (topDict.TryGetValue(VariationStoreOperator, out double?[]? variationOperands))
        {
            if (variationAxisCount is null)
            {
                throw SfntFont.DataInvalid("CFF2-fvar-missing");
            }

            int variationStoreOffset = GetIntegerOperand(
                variationOperands,
                1,
                "CFF2-VariationStore");
            variationRegionCounts = ReadVariationStore(
                source,
                variationStoreOffset,
                variationAxisCount.Value);
        }
        else
        {
            variationRegionCounts = [];
        }

        Cff2Index charStrings = ReadIndex(source, charStringsOffset, "CFF2-CharStrings", glyphCount);
        if (charStrings.Objects.Count != glyphCount)
        {
            throw SfntFont.DataInvalid("CFF2-CharStrings-count");
        }

        Cff2Index fontDictArray = ReadIndex(
            source,
            fontDictArrayOffset,
            "CFF2-FontDICTINDEX",
            MaximumFontDictCount);
        if (fontDictArray.Objects.Count == 0)
        {
            throw SfntFont.DataInvalid("CFF2-FontDICTINDEX-count");
        }

        var localSubroutines = new IReadOnlyList<ReadOnlyMemory<byte>>[fontDictArray.Objects.Count];
        var defaultVariationIndexes = new int[fontDictArray.Objects.Count];
        for (int index = 0; index < fontDictArray.Objects.Count; index++)
        {
            Cff2Range range = fontDictArray.Objects[index];
            Dictionary<int, double?[]> fontDict = ReadDict(
                source.AsSpan(range.Offset, range.Length),
                "CFF2-FontDICT",
                FontDictOperators,
                variationRegionCounts: null);
            (localSubroutines[index], defaultVariationIndexes[index]) = ReadPrivateDict(
                source,
                fontDict,
                variationRegionCounts);
        }

        ushort[] fontDictByGlyph;
        if (topDict.TryGetValue(FontDictSelectOperator, out double?[]? selectOperands))
        {
            int selectOffset = GetIntegerOperand(selectOperands, 1, "CFF2-FontDICTSelect");
            fontDictByGlyph = ReadFontDictSelect(
                source,
                selectOffset,
                glyphCount,
                fontDictArray.Objects.Count);
        }
        else if (fontDictArray.Objects.Count == 1)
        {
            fontDictByGlyph = new ushort[glyphCount];
        }
        else
        {
            throw SfntFont.DataInvalid("CFF2-FontDICTSelect-missing");
        }

        return new ParsedCff2(
            charStrings,
            globalSubrs.GetPrograms(source),
            localSubroutines,
            fontDictByGlyph,
            defaultVariationIndexes,
            variationRegionCounts);
    }

    private static ParsedCff2 GetParsed(byte[] source, byte[]? fvar, ushort glyphCount)
    {
        ushort? variationAxisCount = fvar is null ? null : ValidateFvar(fvar);
        ParsedCff2CacheEntry cached = ParsedFonts.GetValue(
            source,
            value => new ParsedCff2CacheEntry(
                Parse(value, variationAxisCount, glyphCount),
                glyphCount,
                variationAxisCount));

        return cached.GlyphCount == glyphCount && cached.VariationAxisCount == variationAxisCount
            ? cached.Font
            : Parse(source, variationAxisCount, glyphCount);
    }

    private static (IReadOnlyList<ReadOnlyMemory<byte>> Subroutines, int DefaultVariationIndex) ReadPrivateDict(
        byte[] source,
        Dictionary<int, double?[]> fontDict,
        int[] variationRegionCounts)
    {
        if (!fontDict.TryGetValue(PrivateOperator, out double?[]? operands))
        {
            throw SfntFont.DataInvalid("CFF2-Private-missing");
        }

        if (operands.Length != 2)
        {
            throw SfntFont.DataInvalid("CFF2-Private");
        }

        int size = ToInt(operands[0], "CFF2-Private-size");
        int offset = ToInt(operands[1], "CFF2-Private-offset");
        SfntFont.EnsureRange(source, offset, size, "CFF2-Private");
        Dictionary<int, double?[]> privateDict = ReadDict(
            source.AsSpan(offset, size),
            "CFF2-Private",
            PrivateDictOperators,
            variationRegionCounts);
        int variationIndex = privateDict.TryGetValue(VsIndexOperator, out double?[]? vsOperands)
            ? GetVariationIndex(vsOperands, variationRegionCounts, "CFF2-Private-vsindex")
            : 0;
        if (!privateDict.TryGetValue(SubrsOperator, out double?[]? subrsOperands))
        {
            return ([], variationIndex);
        }

        int relativeOffset = GetIntegerOperand(subrsOperands, 1, "CFF2-LocalSubrs");
        int subrOffset = CheckedAdd(offset, relativeOffset, "CFF2-LocalSubrs");
        return (ReadIndex(source, subrOffset, "CFF2-LocalSubrs", 65_535).GetPrograms(source), variationIndex);
    }

    private static int[] ReadVariationStore(byte[] source, int offset, ushort expectedAxisCount)
    {
        SfntFont.EnsureRange(source, offset, 2, "CFF2-VariationStore");
        int length = SfntFont.ReadUInt16(source, offset, "CFF2-VariationStore-length");
        int storeOffset = checked(offset + 2);
        SfntFont.EnsureRange(source, storeOffset, length, "CFF2-ItemVariationStore");
        ReadOnlySpan<byte> store = source.AsSpan(storeOffset, length);
        SfntFont.EnsureRange(store, 0, 8, "CFF2-ItemVariationStore-header");
        if (SfntFont.ReadUInt16(store, 0, "CFF2-ItemVariationStore-format") != 1)
        {
            throw SfntFont.DataInvalid("CFF2-ItemVariationStore-format");
        }

        int regionListOffset = ToInt(
            SfntFont.ReadUInt32(store, 2, "CFF2-VariationRegionList-offset"),
            "CFF2-VariationRegionList-offset");
        int dataCount = SfntFont.ReadUInt16(store, 6, "CFF2-ItemVariationData-count");
        if (dataCount == 0 || dataCount > MaximumVariationDataCount)
        {
            throw SfntFont.DataInvalid("CFF2-ItemVariationData-count");
        }

        SfntFont.EnsureRange(store, 8, checked(dataCount * 4), "CFF2-ItemVariationData-offsets");
        SfntFont.EnsureRange(store, regionListOffset, 4, "CFF2-VariationRegionList");
        int axisCount = SfntFont.ReadUInt16(store, regionListOffset, "CFF2-axisCount");
        int regionCount = SfntFont.ReadUInt16(store, regionListOffset + 2, "CFF2-regionCount");
        if (axisCount != expectedAxisCount || regionCount > MaximumVariationRegionCount)
        {
            throw SfntFont.DataInvalid("CFF2-VariationRegionList-count");
        }

        int coordinatesOffset = checked(regionListOffset + 4);
        int coordinatesLength = checked(regionCount * axisCount * 6);
        SfntFont.EnsureRange(store, coordinatesOffset, coordinatesLength, "CFF2-VariationRegions");
        for (int region = 0; region < regionCount; region++)
        {
            for (int axis = 0; axis < axisCount; axis++)
            {
                int coordinateOffset = coordinatesOffset + (((region * axisCount) + axis) * 6);
                short start = SfntFont.ReadInt16(store, coordinateOffset, "CFF2-region-start");
                short peak = SfntFont.ReadInt16(store, coordinateOffset + 2, "CFF2-region-peak");
                short end = SfntFont.ReadInt16(store, coordinateOffset + 4, "CFF2-region-end");
                if (start < -16_384 || end > 16_384 || start > peak || peak > end)
                {
                    throw SfntFont.DataInvalid("CFF2-VariationRegion-coordinate");
                }
            }
        }

        var result = new int[dataCount];
        int previousOffset = 0;
        for (int index = 0; index < dataCount; index++)
        {
            int dataOffset = ToInt(
                SfntFont.ReadUInt32(store, 8 + (index * 4), "CFF2-ItemVariationData-offset"),
                "CFF2-ItemVariationData-offset");
            int nextOffset = index + 1 < dataCount
                ? ToInt(
                    SfntFont.ReadUInt32(store, 8 + ((index + 1) * 4), "CFF2-ItemVariationData-offset"),
                    "CFF2-ItemVariationData-offset")
                : store.Length;
            if (dataOffset < 8 + (dataCount * 4)
                || dataOffset < previousOffset
                || nextOffset < dataOffset)
            {
                throw SfntFont.DataInvalid("CFF2-ItemVariationData-offset-order");
            }

            SfntFont.EnsureRange(store, dataOffset, 6, "CFF2-ItemVariationData");
            int itemCount = SfntFont.ReadUInt16(store, dataOffset, "CFF2-ItemVariationData-itemCount");
            int wordDeltaCount = SfntFont.ReadUInt16(store, dataOffset + 2, "CFF2-ItemVariationData-wordCount");
            int regionIndexCount = SfntFont.ReadUInt16(store, dataOffset + 4, "CFF2-ItemVariationData-regionCount");
            int dataLength = checked(6 + (regionIndexCount * 2));
            if (itemCount != 0
                || wordDeltaCount != 0
                || regionIndexCount > regionCount
                || dataOffset + dataLength != nextOffset)
            {
                throw SfntFont.DataInvalid("CFF2-ItemVariationData");
            }

            SfntFont.EnsureRange(store, dataOffset + 6, regionIndexCount * 2, "CFF2-regionIndexes");
            for (int regionIndex = 0; regionIndex < regionIndexCount; regionIndex++)
            {
                if (SfntFont.ReadUInt16(
                        store,
                        dataOffset + 6 + (regionIndex * 2),
                        "CFF2-regionIndex") >= regionCount)
                {
                    throw SfntFont.DataInvalid("CFF2-regionIndex");
                }
            }

            result[index] = regionIndexCount;
            previousOffset = dataOffset;
        }

        return result;
    }

    private static ushort ValidateFvar(byte[] fvar)
    {
        SfntFont.EnsureRange(fvar, 0, 16, "CFF2-fvar-header");
        if (SfntFont.ReadUInt16(fvar, 0, "CFF2-fvar-version") != 1
            || SfntFont.ReadUInt16(fvar, 2, "CFF2-fvar-version") != 0
            || SfntFont.ReadUInt16(fvar, 6, "CFF2-fvar-reserved") != 2)
        {
            throw SfntFont.DataInvalid("CFF2-fvar-header");
        }

        int axesOffset = SfntFont.ReadUInt16(fvar, 4, "CFF2-fvar-axesOffset");
        ushort axisCount = SfntFont.ReadUInt16(fvar, 8, "CFF2-fvar-axisCount");
        int axisSize = SfntFont.ReadUInt16(fvar, 10, "CFF2-fvar-axisSize");
        int instanceCount = SfntFont.ReadUInt16(fvar, 12, "CFF2-fvar-instanceCount");
        int instanceSize = SfntFont.ReadUInt16(fvar, 14, "CFF2-fvar-instanceSize");
        int minimumInstanceSize = checked(4 + (axisCount * 4));
        if (axisCount == 0
            || axisCount > 64
            || axesOffset < 16
            || axisSize != 20
            || instanceSize != minimumInstanceSize && instanceSize != minimumInstanceSize + 2)
        {
            throw SfntFont.DataInvalid("CFF2-fvar-counts");
        }

        int recordsLength = checked((axisCount * axisSize) + (instanceCount * instanceSize));
        SfntFont.EnsureRange(fvar, axesOffset, recordsLength, "CFF2-fvar-records");
        return axisCount;
    }

    private static ushort[] ReadFontDictSelect(byte[] source, int offset, ushort glyphCount, int dictCount)
    {
        SfntFont.EnsureRange(source, offset, 1, "CFF2-FontDICTSelect");
        var result = new ushort[glyphCount];
        byte format = source[offset];
        if (format == 0)
        {
            SfntFont.EnsureRange(source, offset + 1, glyphCount, "CFF2-FontDICTSelect-format0");
            for (int glyph = 0; glyph < glyphCount; glyph++)
            {
                int value = source[offset + 1 + glyph];
                if (value >= dictCount)
                {
                    throw SfntFont.DataInvalid("CFF2-FontDICTSelect-id");
                }

                result[glyph] = (ushort)value;
            }

            return result;
        }

        if (format is not (3 or 4))
        {
            throw SfntFont.DataInvalid("CFF2-FontDICTSelect-format");
        }

        int rangeCount;
        int position;
        if (format == 3)
        {
            SfntFont.EnsureRange(source, offset + 1, 2, "CFF2-FontDICTSelect-ranges");
            rangeCount = SfntFont.ReadUInt16(source, offset + 1, "CFF2-FontDICTSelect-ranges");
            position = offset + 3;
        }
        else
        {
            SfntFont.EnsureRange(source, offset + 1, 4, "CFF2-FontDICTSelect-ranges");
            rangeCount = ToInt(
                SfntFont.ReadUInt32(source, offset + 1, "CFF2-FontDICTSelect-ranges"),
                "CFF2-FontDICTSelect-ranges");
            position = offset + 5;
        }

        if (rangeCount == 0 || rangeCount > glyphCount)
        {
            throw SfntFont.DataInvalid("CFF2-FontDICTSelect-ranges");
        }

        int previousGlyph = -1;
        int previousDict = -1;
        for (int index = 0; index < rangeCount; index++)
        {
            int firstGlyph;
            int dict;
            if (format == 3)
            {
                SfntFont.EnsureRange(source, position, 3, "CFF2-FontDICTSelect-range3");
                firstGlyph = SfntFont.ReadUInt16(source, position, "CFF2-FontDICTSelect-first");
                dict = source[position + 2];
                position += 3;
            }
            else
            {
                SfntFont.EnsureRange(source, position, 6, "CFF2-FontDICTSelect-range4");
                firstGlyph = ToInt(
                    SfntFont.ReadUInt32(source, position, "CFF2-FontDICTSelect-first"),
                    "CFF2-FontDICTSelect-first");
                dict = SfntFont.ReadUInt16(source, position + 4, "CFF2-FontDICTSelect-id");
                position += 6;
            }

            if (index == 0 && firstGlyph != 0
                || firstGlyph <= previousGlyph
                || firstGlyph >= glyphCount
                || dict >= dictCount)
            {
                throw SfntFont.DataInvalid("CFF2-FontDICTSelect-range");
            }

            if (previousGlyph >= 0)
            {
                result.AsSpan(previousGlyph, firstGlyph - previousGlyph).Fill((ushort)previousDict);
            }

            previousGlyph = firstGlyph;
            previousDict = dict;
        }

        int sentinel;
        if (format == 3)
        {
            SfntFont.EnsureRange(source, position, 2, "CFF2-FontDICTSelect-sentinel");
            sentinel = SfntFont.ReadUInt16(source, position, "CFF2-FontDICTSelect-sentinel");
        }
        else
        {
            SfntFont.EnsureRange(source, position, 4, "CFF2-FontDICTSelect-sentinel");
            sentinel = ToInt(
                SfntFont.ReadUInt32(source, position, "CFF2-FontDICTSelect-sentinel"),
                "CFF2-FontDICTSelect-sentinel");
        }

        if (sentinel != glyphCount)
        {
            throw SfntFont.DataInvalid("CFF2-FontDICTSelect-sentinel");
        }

        result.AsSpan(previousGlyph, sentinel - previousGlyph).Fill((ushort)previousDict);
        return result;
    }

    private static Cff2Index ReadIndex(byte[] source, int offset, string detail, int maximumCount)
    {
        SfntFont.EnsureRange(source, offset, 4, detail);
        int count = ToInt(SfntFont.ReadUInt32(source, offset, detail), detail);
        if (count > maximumCount)
        {
            throw SfntFont.DataInvalid($"{detail}-count");
        }

        if (count == 0)
        {
            return new Cff2Index([], checked(offset + 4));
        }

        SfntFont.EnsureRange(source, offset + 4, 1, detail);
        int offSize = source[offset + 4];
        if (offSize is < 1 or > 4)
        {
            throw SfntFont.DataInvalid($"{detail}-offSize");
        }

        int offsetsOffset = checked(offset + 5);
        int offsetBytes = checked((count + 1) * offSize);
        SfntFont.EnsureRange(source, offsetsOffset, offsetBytes, detail);
        int dataOffset = checked(offsetsOffset + offsetBytes);
        var offsets = new int[count + 1];
        int previous = 0;
        for (int index = 0; index <= count; index++)
        {
            int value = ReadOffset(source, offsetsOffset + (index * offSize), offSize, detail);
            if (index == 0 && value != 1 || value < previous)
            {
                throw SfntFont.DataInvalid($"{detail}-offset");
            }

            offsets[index] = value;
            previous = value;
        }

        int dataLength = checked(offsets[count] - 1);
        SfntFont.EnsureRange(source, dataOffset, dataLength, detail);
        var objects = new Cff2Range[count];
        for (int index = 0; index < count; index++)
        {
            objects[index] = new Cff2Range(
                checked(dataOffset + offsets[index] - 1),
                checked(offsets[index + 1] - offsets[index]));
        }

        return new Cff2Index(objects, checked(dataOffset + dataLength));
    }

    private static Dictionary<int, double?[]> ReadDict(
        ReadOnlySpan<byte> data,
        string detail,
        HashSet<int> allowedOperators,
        int[]? variationRegionCounts)
    {
        var result = new Dictionary<int, double?[]>();
        var operands = new List<double?>(513);
        int activeVariationIndex = 0;
        int position = 0;
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

            if (!allowedOperators.Contains(operation))
            {
                throw SfntFont.DataInvalid($"{detail}-operator-{operation:X}");
            }

            if (operation == BlendOperator)
            {
                if (variationRegionCounts is null)
                {
                    throw SfntFont.DataInvalid($"{detail}-blend");
                }

                ProcessBlend(operands, variationRegionCounts, activeVariationIndex, detail);
                continue;
            }

            if (result.ContainsKey(operation))
            {
                throw SfntFont.DataInvalid($"{detail}-duplicate");
            }

            double?[] values = operands.ToArray();
            result.Add(operation, values);
            operands.Clear();
            if (operation == VsIndexOperator)
            {
                activeVariationIndex = GetVariationIndex(values, variationRegionCounts!, detail);
            }
        }

        if (operands.Count != 0)
        {
            throw SfntFont.DataInvalid($"{detail}-trailing");
        }

        return result;
    }

    private static void ProcessBlend(
        List<double?> operands,
        int[] variationRegionCounts,
        int variationIndex,
        string detail)
    {
        if (operands.Count == 0 || variationIndex < 0 || variationIndex >= variationRegionCounts.Length)
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

    private static double? ReadDictNumber(ReadOnlySpan<byte> data, ref int position, byte first, string detail)
    {
        if (first is >= 32 and <= 246)
        {
            return first - 139;
        }

        if (first is >= 247 and <= 250)
        {
            SfntFont.EnsureRange(data, position, 1, detail);
            return ((first - 247) * 256d) + data[position++] + 108;
        }

        if (first is >= 251 and <= 254)
        {
            SfntFont.EnsureRange(data, position, 1, detail);
            return -((first - 251) * 256d) - data[position++] - 108;
        }

        if (first == 28)
        {
            SfntFont.EnsureRange(data, position, 2, detail);
            double result = BinaryPrimitives.ReadInt16BigEndian(data.Slice(position, 2));
            position += 2;
            return result;
        }

        if (first == 29)
        {
            SfntFont.EnsureRange(data, position, 4, detail);
            double result = BinaryPrimitives.ReadInt32BigEndian(data.Slice(position, 4));
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

        if (!terminated)
        {
            throw SfntFont.DataInvalid($"{detail}-real");
        }

        return null;
    }

    private static void VerifySelectedGlyphs(
        byte[] source,
        ushort glyphCount,
        ISet<ushort> selectedGlyphs,
        ParsedCff2 parsed,
        CancellationToken cancellationToken)
    {
        foreach (ushort glyph in selectedGlyphs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (glyph >= glyphCount)
            {
                throw SfntFont.DataInvalid("CFF2-selected-glyph");
            }

            int fontDict = parsed.FontDictByGlyph[glyph];
            Cff2Range range = parsed.CharStrings.Objects[glyph];
            Cff2CharStringVerifier.Verify(
                new ReadOnlyMemory<byte>(source, range.Offset, range.Length),
                parsed.GlobalSubroutines,
                parsed.LocalSubroutinesByFontDict[fontDict],
                parsed.VariationRegionCounts,
                parsed.DefaultVariationIndexes[fontDict],
                cancellationToken);
        }
    }

    private static int GetOffset(
        Dictionary<int, double?[]> dict,
        int operation,
        int operandCount,
        string detail)
        => dict.TryGetValue(operation, out double?[]? operands)
            ? GetIntegerOperand(operands, operandCount, detail)
            : throw SfntFont.DataInvalid(detail);

    private static int GetIntegerOperand(double?[] operands, int count, string detail)
    {
        if (operands.Length != count)
        {
            throw SfntFont.DataInvalid(detail);
        }

        return ToInt(operands[0], detail);
    }

    private static int GetVariationIndex(double?[] operands, int[] regionCounts, string detail)
    {
        int index = GetIntegerOperand(operands, 1, detail);
        if ((uint)index >= (uint)regionCounts.Length)
        {
            throw SfntFont.DataInvalid(detail);
        }

        return index;
    }

    private static int ReadOffset(byte[] source, int offset, int size, string detail)
    {
        uint value = 0;
        for (int index = 0; index < size; index++)
        {
            value = (value << 8) | source[offset + index];
        }

        return ToInt(value, detail);
    }

    private static int CheckedAdd(int left, int right, string detail)
    {
        long result = (long)left + right;
        return result <= int.MaxValue ? (int)result : throw SfntFont.DataInvalid(detail);
    }

    private static int ToInt(double? value, string detail)
    {
        if (value is not double known
            || known != Math.Truncate(known)
            || known < 0
            || known > int.MaxValue)
        {
            throw SfntFont.DataInvalid(detail);
        }

        return (int)known;
    }

    private static int ToInt(uint value, string detail)
        => value <= int.MaxValue ? (int)value : throw SfntFont.DataInvalid(detail);

    private sealed class Cff2Index(IReadOnlyList<Cff2Range> objects, int nextOffset)
    {
        internal IReadOnlyList<Cff2Range> Objects { get; } = objects;

        internal int NextOffset { get; } = nextOffset;

        internal ReadOnlyMemory<byte>[] GetPrograms(byte[] source)
        {
            var result = new ReadOnlyMemory<byte>[Objects.Count];
            for (int index = 0; index < Objects.Count; index++)
            {
                Cff2Range range = Objects[index];
                result[index] = new ReadOnlyMemory<byte>(source, range.Offset, range.Length);
            }

            return result;
        }
    }

    private readonly struct Cff2Range(int offset, int length)
    {
        internal int Offset { get; } = offset;

        internal int Length { get; } = length;
    }

    private sealed class ParsedCff2(
        Cff2Index charStrings,
        IReadOnlyList<ReadOnlyMemory<byte>> globalSubroutines,
        IReadOnlyList<ReadOnlyMemory<byte>>[] localSubroutinesByFontDict,
        ushort[] fontDictByGlyph,
        int[] defaultVariationIndexes,
        int[] variationRegionCounts)
    {
        internal Cff2Index CharStrings { get; } = charStrings;

        internal IReadOnlyList<ReadOnlyMemory<byte>> GlobalSubroutines { get; } = globalSubroutines;

        internal IReadOnlyList<ReadOnlyMemory<byte>>[] LocalSubroutinesByFontDict { get; }
            = localSubroutinesByFontDict;

        internal ushort[] FontDictByGlyph { get; } = fontDictByGlyph;

        internal int[] DefaultVariationIndexes { get; } = defaultVariationIndexes;

        internal int[] VariationRegionCounts { get; } = variationRegionCounts;
    }

    private sealed class ParsedCff2CacheEntry(
        ParsedCff2 font,
        ushort glyphCount,
        ushort? variationAxisCount)
    {
        internal ParsedCff2 Font { get; } = font;

        internal ushort GlyphCount { get; } = glyphCount;

        internal ushort? VariationAxisCount { get; } = variationAxisCount;
    }
}
