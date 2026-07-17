using System.Buffers.Binary;

namespace OdfKit.WebFonts.OpenType;

internal static class CffSubsetter
{
    private const int RosOperator = 0x0C1E;
    private const int FdArrayOperator = 0x0C24;
    private const int FdSelectOperator = 0x0C25;
    private const int CharStringsOperator = 17;
    private const int PrivateOperator = 18;
    private const int SubrsOperator = 19;
    private const int CharsetOperator = 15;

    internal static byte[] Build(byte[] source, ushort glyphCount, ISet<ushort> selectedGlyphs)
    {
        SfntFont.EnsureRange(source, 0, 4, "CFF-header");
        if (source[0] != 1 || source[2] < 4 || source[2] > source.Length || source[3] is < 1 or > 4)
        {
            throw SfntFont.DataInvalid("CFF-header");
        }

        CffIndex nameIndex = ReadIndex(source, source[2], "CFF-Name");
        CffIndex topDictIndex = ReadIndex(source, nameIndex.NextOffset, "CFF-TopDICT");
        CffIndex stringIndex = ReadIndex(source, topDictIndex.NextOffset, "CFF-String");
        _ = ReadIndex(source, stringIndex.NextOffset, "CFF-GlobalSubrs");
        if (nameIndex.Objects.Count != 1 || topDictIndex.Objects.Count != 1)
        {
            throw SfntFont.DataInvalid("CFF-FontSet");
        }

        CffRange topRange = topDictIndex.Objects[0];
        Dictionary<int, long?[]> topDict = ReadDict(
            source.AsSpan(topRange.Offset, topRange.Length),
            "CFF-TopDICT");
        RequireOperands(topDict, RosOperator, 3, "CFF-ROS");
        int charStringsOffset = GetOffset(topDict, CharStringsOperator, 1, "CFF-CharStrings");
        int fdArrayOffset = GetOffset(topDict, FdArrayOperator, 1, "CFF-FDArray");
        int fdSelectOffset = GetOffset(topDict, FdSelectOperator, 1, "CFF-FDSelect");
        int charsetOffset = GetOffset(topDict, CharsetOperator, 1, "CFF-charset");

        CffIndex charStrings = ReadIndex(source, charStringsOffset, "CFF-CharStrings");
        if (charStrings.Objects.Count != glyphCount || charStrings.Objects.Any(item => item.Length == 0))
        {
            throw SfntFont.DataInvalid("CFF-CharStrings-count");
        }

        CffIndex fdArray = ReadIndex(source, fdArrayOffset, "CFF-FDArray");
        if (fdArray.Objects.Count == 0 || fdArray.Objects.Count > 256)
        {
            throw SfntFont.DataInvalid("CFF-FDArray-count");
        }

        foreach (CffRange fontDictRange in fdArray.Objects)
        {
            Dictionary<int, long?[]> fontDict = ReadDict(
                source.AsSpan(fontDictRange.Offset, fontDictRange.Length),
                "CFF-FontDICT");
            ValidatePrivateDict(source, fontDict);
        }

        ValidateFdSelect(source, fdSelectOffset, glyphCount, fdArray.Objects.Count);
        ValidateCharset(source, charsetOffset, glyphCount);
        if (topDict.TryGetValue(PrivateOperator, out long?[]? topPrivate))
        {
            ValidatePrivateDict(source, new Dictionary<int, long?[]>
            {
                [PrivateOperator] = topPrivate
            });
        }

        var output = (byte[])source.Clone();
        for (ushort glyph = 0; glyph < glyphCount; glyph++)
        {
            if (!selectedGlyphs.Contains(glyph))
            {
                CffRange range = charStrings.Objects[glyph];
                WriteBlankCharString(output.AsSpan(range.Offset, range.Length));
            }
        }

        return output;
    }

    private static void ValidatePrivateDict(byte[] source, IReadOnlyDictionary<int, long?[]> dict)
    {
        if (!dict.TryGetValue(PrivateOperator, out long?[]? operands))
        {
            throw SfntFont.DataInvalid("CFF-Private-missing");
        }

        if (operands.Length != 2 || operands[0] is not long sizeValue || operands[1] is not long offsetValue)
        {
            throw SfntFont.DataInvalid("CFF-Private");
        }

        int size = ToInt(sizeValue, "CFF-Private");
        int offset = ToInt(offsetValue, "CFF-Private");
        SfntFont.EnsureRange(source, offset, size, "CFF-Private");
        if (size == 0)
        {
            return;
        }

        Dictionary<int, long?[]> privateDict = ReadDict(source.AsSpan(offset, size), "CFF-Private");
        if (privateDict.TryGetValue(SubrsOperator, out long?[]? subrsOperands))
        {
            if (subrsOperands.Length != 1 || subrsOperands[0] is not long relativeValue)
            {
                throw SfntFont.DataInvalid("CFF-Subrs");
            }

            int subrsOffset = ToInt((long)offset + relativeValue, "CFF-Subrs");
            _ = ReadIndex(source, subrsOffset, "CFF-LocalSubrs");
        }
    }

    private static void ValidateFdSelect(byte[] source, int offset, ushort glyphCount, int fdCount)
    {
        SfntFont.EnsureRange(source, offset, 1, "CFF-FDSelect");
        byte format = source[offset];
        if (format == 0)
        {
            SfntFont.EnsureRange(source, offset + 1, glyphCount, "CFF-FDSelect");
            for (int glyph = 0; glyph < glyphCount; glyph++)
            {
                if (source[offset + 1 + glyph] >= fdCount)
                {
                    throw SfntFont.DataInvalid("CFF-FDSelect-fd");
                }
            }

            return;
        }

        if (format != 3)
        {
            throw SfntFont.DataInvalid("CFF-FDSelect-format");
        }

        SfntFont.EnsureRange(source, offset + 1, 2, "CFF-FDSelect");
        int rangeCount = SfntFont.ReadUInt16(source, offset + 1, "CFF-FDSelect-ranges");
        if (rangeCount == 0 || rangeCount > glyphCount)
        {
            throw SfntFont.DataInvalid("CFF-FDSelect-ranges");
        }

        int recordsOffset = offset + 3;
        SfntFont.EnsureRange(source, recordsOffset, checked((rangeCount * 3) + 2), "CFF-FDSelect");
        int previous = -1;
        for (int index = 0; index < rangeCount; index++)
        {
            int recordOffset = recordsOffset + (index * 3);
            int firstGlyph = SfntFont.ReadUInt16(source, recordOffset, "CFF-FDSelect-first");
            int fd = source[recordOffset + 2];
            if (index == 0 && firstGlyph != 0 || firstGlyph <= previous || firstGlyph >= glyphCount || fd >= fdCount)
            {
                throw SfntFont.DataInvalid("CFF-FDSelect-range");
            }

            previous = firstGlyph;
        }

        int sentinel = SfntFont.ReadUInt16(source, recordsOffset + (rangeCount * 3), "CFF-FDSelect-sentinel");
        if (sentinel != glyphCount)
        {
            throw SfntFont.DataInvalid("CFF-FDSelect-sentinel");
        }
    }

    private static void ValidateCharset(byte[] source, int offset, ushort glyphCount)
    {
        if (offset is 0 or 1 or 2)
        {
            throw SfntFont.DataInvalid("CFF-CID-charset");
        }

        SfntFont.EnsureRange(source, offset, 1, "CFF-charset");
        byte format = source[offset];
        int remaining = glyphCount - 1;
        int position = offset + 1;
        if (format == 0)
        {
            SfntFont.EnsureRange(source, position, checked(remaining * 2), "CFF-charset");
            return;
        }

        if (format is not (1 or 2))
        {
            throw SfntFont.DataInvalid("CFF-charset-format");
        }

        while (remaining > 0)
        {
            int rangeLength = format == 1 ? 3 : 4;
            SfntFont.EnsureRange(source, position, rangeLength, "CFF-charset-range");
            int left = format == 1
                ? source[position + 2]
                : SfntFont.ReadUInt16(source, position + 2, "CFF-charset-left");
            int count = checked(left + 1);
            if (count > remaining)
            {
                throw SfntFont.DataInvalid("CFF-charset-count");
            }

            remaining -= count;
            position += rangeLength;
        }
    }

    private static CffIndex ReadIndex(byte[] source, int offset, string detail)
    {
        SfntFont.EnsureRange(source, offset, 2, detail);
        int count = SfntFont.ReadUInt16(source, offset, detail);
        if (count == 0)
        {
            return new CffIndex([], offset + 2);
        }

        SfntFont.EnsureRange(source, offset + 2, 1, detail);
        int offSize = source[offset + 2];
        if (offSize is < 1 or > 4)
        {
            throw SfntFont.DataInvalid($"{detail}-offSize");
        }

        int offsetsOffset = offset + 3;
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
        var objects = new List<CffRange>(count);
        for (int index = 0; index < count; index++)
        {
            objects.Add(new CffRange(
                checked(dataOffset + offsets[index] - 1),
                checked(offsets[index + 1] - offsets[index])));
        }

        return new CffIndex(objects, checked(dataOffset + dataLength));
    }

    private static Dictionary<int, long?[]> ReadDict(ReadOnlySpan<byte> data, string detail)
    {
        var result = new Dictionary<int, long?[]>();
        var operands = new List<long?>(48);
        int position = 0;
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

            if (result.ContainsKey(operation))
            {
                throw SfntFont.DataInvalid($"{detail}-duplicate");
            }

            result.Add(operation, operands.ToArray());

            operands.Clear();
        }

        if (operands.Count != 0)
        {
            throw SfntFont.DataInvalid($"{detail}-trailing");
        }

        return result;
    }

    private static long? ReadDictNumber(ReadOnlySpan<byte> data, ref int position, byte first, string detail)
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

        if (!terminated)
        {
            throw SfntFont.DataInvalid($"{detail}-real");
        }

        return null;
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

    private static int GetOffset(
        IReadOnlyDictionary<int, long?[]> dict,
        int operation,
        int operandCount,
        string detail)
    {
        RequireOperands(dict, operation, operandCount, detail);
        long? value = dict[operation][0];
        return value is long integer ? ToInt(integer, detail) : throw SfntFont.DataInvalid(detail);
    }

    private static void RequireOperands(
        IReadOnlyDictionary<int, long?[]> dict,
        int operation,
        int count,
        string detail)
    {
        if (!dict.TryGetValue(operation, out long?[]? operands) || operands.Length != count)
        {
            throw SfntFont.DataInvalid(detail);
        }
    }

    private static void WriteBlankCharString(Span<byte> output)
    {
        int position = 0;
        while (output.Length - position > 6)
        {
            output[position++] = 139;
            output[position++] = 12;
            output[position++] = 18;
        }

        Span<byte> tail = output.Slice(position);
        switch (tail.Length)
        {
            case 1:
                tail[0] = 14;
                break;
            case 2:
                tail[0] = 139;
                tail[1] = 14;
                break;
            case 3:
                tail[0] = 247;
                tail[1] = 0;
                tail[2] = 14;
                break;
            case 4:
                tail[0] = 139;
                tail[1] = 139;
                tail[2] = 1;
                tail[3] = 14;
                break;
            case 5:
                tail[0] = 139;
                tail[1] = 139;
                tail[2] = 139;
                tail[3] = 1;
                tail[4] = 14;
                break;
            case 6:
                tail[0] = 255;
                tail[1] = 0;
                tail[2] = 0;
                tail[3] = 0;
                tail[4] = 0;
                tail[5] = 14;
                break;
            default:
                throw SfntFont.DataInvalid("CFF-CharString-length");
        }
    }

    private static int ToInt(long value, string detail)
    {
        if (value < 0 || value > int.MaxValue)
        {
            throw SfntFont.DataInvalid(detail);
        }

        return (int)value;
    }

    private sealed class CffIndex
    {
        internal CffIndex(IReadOnlyList<CffRange> objects, int nextOffset)
        {
            Objects = objects;
            NextOffset = nextOffset;
        }

        internal IReadOnlyList<CffRange> Objects { get; }

        internal int NextOffset { get; }
    }

    private readonly struct CffRange
    {
        internal CffRange(int offset, int length)
        {
            Offset = offset;
            Length = length;
        }

        internal int Offset { get; }

        internal int Length { get; }
    }
}
