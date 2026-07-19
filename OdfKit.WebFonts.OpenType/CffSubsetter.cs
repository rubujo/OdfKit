using System.Buffers.Binary;

using System.Runtime.CompilerServices;

namespace OdfKit.WebFonts.OpenType;

internal static class CffSubsetter
{
    private static readonly ConditionalWeakTable<byte[], ParsedCff> ParsedFonts = new();

    private static readonly (ushort Sid, ushort Glyph)[] ExpertStandardGlyphs =
    [
        (1, 1), (13, 12), (14, 13), (15, 14), (99, 15),
        (27, 26), (28, 27), (109, 46), (110, 47)
    ];

    private static readonly (ushort Sid, ushort Glyph)[] ExpertSubsetStandardGlyphs =
    [
        (1, 1), (13, 8), (14, 9), (15, 10), (99, 11),
        (27, 22), (28, 23), (109, 41), (110, 42)
    ];

    private const int RosOperator = 0x0C1E;
    private const int FdArrayOperator = 0x0C24;
    private const int FdSelectOperator = 0x0C25;
    private const int CharStringsOperator = 17;
    private const int PrivateOperator = 18;
    private const int SubrsOperator = 19;
    private const int CharsetOperator = 15;

    internal static byte[] Build(byte[] source, ushort glyphCount, ISet<ushort> selectedGlyphs)
    {
        ParsedCff parsed = GetParsed(source, glyphCount);
        HashSet<ushort> retainedGlyphs = VerifyAndExpandSelectedGlyphs(
            source,
            glyphCount,
            selectedGlyphs,
            parsed);

        return CffTableCompactor.Build(source, glyphCount, retainedGlyphs);
    }

    internal static void Validate(byte[] source, ushort glyphCount, ISet<ushort> selectedGlyphs)
    {
        ParsedCff parsed = GetParsed(source, glyphCount);
        _ = VerifyAndExpandSelectedGlyphs(source, glyphCount, selectedGlyphs, parsed);
    }

    private static ParsedCff Parse(byte[] source, ushort glyphCount)
    {
        SfntFont.EnsureRange(source, 0, 4, "CFF-header");
        if (source[0] != 1 || source[2] < 4 || source[2] > source.Length || source[3] is < 1 or > 4)
        {
            throw SfntFont.DataInvalid("CFF-header");
        }

        CffIndex nameIndex = ReadIndex(source, source[2], "CFF-Name");
        CffIndex topDictIndex = ReadIndex(source, nameIndex.NextOffset, "CFF-TopDICT");
        CffIndex stringIndex = ReadIndex(source, topDictIndex.NextOffset, "CFF-String");
        CffIndex globalSubrs = ReadIndex(source, stringIndex.NextOffset, "CFF-GlobalSubrs");
        if (nameIndex.Objects.Count != 1 || topDictIndex.Objects.Count != 1)
        {
            throw SfntFont.DataInvalid("CFF-FontSet");
        }

        CffRange topRange = topDictIndex.Objects[0];
        Dictionary<int, long?[]> topDict = ReadDict(
            source.AsSpan(topRange.Offset, topRange.Length),
            "CFF-TopDICT");
        int charStringsOffset = GetOffset(topDict, CharStringsOperator, 1, "CFF-CharStrings");
        int charsetOffset = topDict.ContainsKey(CharsetOperator)
            ? GetOffset(topDict, CharsetOperator, 1, "CFF-charset")
            : 0;

        CffIndex charStrings = ReadIndex(source, charStringsOffset, "CFF-CharStrings");
        if (charStrings.Objects.Count != glyphCount || charStrings.Objects.Any(item => item.Length == 0))
        {
            throw SfntFont.DataInvalid("CFF-CharStrings-count");
        }

        bool isCidKeyed = topDict.ContainsKey(RosOperator);
        if (!isCidKeyed)
        {
            if (topDict.ContainsKey(FdArrayOperator) || topDict.ContainsKey(FdSelectOperator))
            {
                throw SfntFont.DataInvalid("CFF-name-FDArray");
            }

            IReadOnlyList<ReadOnlyMemory<byte>> localSubrs = topDict.ContainsKey(PrivateOperator)
                ? ValidatePrivateDict(source, topDict)
                : [];
            Dictionary<ushort, ushort> glyphBySid = ReadNameCharset(source, charsetOffset, glyphCount);
            return new ParsedCff(
                glyphCount,
                charStrings,
                globalSubrs.GetPrograms(source),
                [localSubrs],
                new byte[glyphCount],
                glyphBySid);
        }

        RequireOperands(topDict, RosOperator, 3, "CFF-ROS");
        int fdArrayOffset = GetOffset(topDict, FdArrayOperator, 1, "CFF-FDArray");
        int fdSelectOffset = GetOffset(topDict, FdSelectOperator, 1, "CFF-FDSelect");
        CffIndex fdArray = ReadIndex(source, fdArrayOffset, "CFF-FDArray");
        if (fdArray.Objects.Count == 0 || fdArray.Objects.Count > 256)
        {
            throw SfntFont.DataInvalid("CFF-FDArray-count");
        }

        var localSubrsByFontDict = new IReadOnlyList<ReadOnlyMemory<byte>>[fdArray.Objects.Count];
        for (int index = 0; index < fdArray.Objects.Count; index++)
        {
            CffRange fontDictRange = fdArray.Objects[index];
            Dictionary<int, long?[]> fontDict = ReadDict(
                source.AsSpan(fontDictRange.Offset, fontDictRange.Length),
                "CFF-FontDICT");
            localSubrsByFontDict[index] = ValidatePrivateDict(source, fontDict);
        }

        byte[] fontDictByGlyph = ValidateFdSelect(source, fdSelectOffset, glyphCount, fdArray.Objects.Count);
        ValidateCharset(source, charsetOffset, glyphCount, allowPredefined: false);
        if (topDict.TryGetValue(PrivateOperator, out long?[]? topPrivate))
        {
            _ = ValidatePrivateDict(source, new Dictionary<int, long?[]>
            {
                [PrivateOperator] = topPrivate
            });
        }

        return new ParsedCff(
            glyphCount,
            charStrings,
            globalSubrs.GetPrograms(source),
            localSubrsByFontDict,
            fontDictByGlyph,
            glyphBySid: null);
    }

    private static ParsedCff GetParsed(byte[] source, ushort glyphCount)
    {
        ParsedCff parsed = ParsedFonts.GetValue(source, value => Parse(value, glyphCount));
        return parsed.GlyphCount == glyphCount
            ? parsed
            : throw SfntFont.DataInvalid("CFF-CharStrings-count");
    }

    private static HashSet<ushort> VerifyAndExpandSelectedGlyphs(
        byte[] source,
        ushort glyphCount,
        ISet<ushort> selectedGlyphs,
        ParsedCff parsed)
    {
        var retainedGlyphs = new HashSet<ushort>(selectedGlyphs);
        foreach (ushort glyph in selectedGlyphs)
        {
            if (glyph >= glyphCount)
            {
                throw SfntFont.DataInvalid("CFF-selected-glyph");
            }

            CffRange range = parsed.CharStrings.Objects[glyph];
            Type2SeacComponents? components = Type2CharStringVerifier.Verify(
                new ReadOnlyMemory<byte>(source, range.Offset, range.Length),
                parsed.GlobalSubroutines,
                parsed.LocalSubroutinesByFontDict[parsed.FontDictByGlyph[glyph]]);
            if (components is not Type2SeacComponents seac)
            {
                continue;
            }

            if (parsed.GlyphBySid is null)
            {
                throw SfntFont.DataInvalid("CFF-seac-CID");
            }

            AddSeacComponent(source, parsed, retainedGlyphs, seac.BaseCode);
            AddSeacComponent(source, parsed, retainedGlyphs, seac.AccentCode);
        }

        return retainedGlyphs;
    }

    private static void AddSeacComponent(
        byte[] source,
        ParsedCff parsed,
        ISet<ushort> retainedGlyphs,
        byte standardEncodingCode)
    {
        ushort sid = GetStandardEncodingSid(standardEncodingCode);
        if (!parsed.GlyphBySid!.TryGetValue(sid, out ushort glyph))
        {
            throw SfntFont.DataInvalid("CFF-seac-component");
        }

        CffRange range = parsed.CharStrings.Objects[glyph];
        Type2SeacComponents? nested = Type2CharStringVerifier.Verify(
            new ReadOnlyMemory<byte>(source, range.Offset, range.Length),
            parsed.GlobalSubroutines,
            parsed.LocalSubroutinesByFontDict[parsed.FontDictByGlyph[glyph]]);
        if (nested is not null)
        {
            throw SfntFont.DataInvalid("CFF-seac-nested");
        }

        retainedGlyphs.Add(glyph);
    }

    private static IReadOnlyList<ReadOnlyMemory<byte>> ValidatePrivateDict(
        byte[] source,
        IReadOnlyDictionary<int, long?[]> dict)
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
            return [];
        }

        Dictionary<int, long?[]> privateDict = ReadDict(source.AsSpan(offset, size), "CFF-Private");
        if (privateDict.TryGetValue(SubrsOperator, out long?[]? subrsOperands))
        {
            if (subrsOperands.Length != 1 || subrsOperands[0] is not long relativeValue)
            {
                throw SfntFont.DataInvalid("CFF-Subrs");
            }

            int subrsOffset = ToInt((long)offset + relativeValue, "CFF-Subrs");
            return ReadIndex(source, subrsOffset, "CFF-LocalSubrs").GetPrograms(source);
        }

        return [];
    }

    private static byte[] ValidateFdSelect(byte[] source, int offset, ushort glyphCount, int fdCount)
    {
        var fontDictByGlyph = new byte[glyphCount];
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

                fontDictByGlyph[glyph] = source[offset + 1 + glyph];
            }

            return fontDictByGlyph;
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
        int previousFd = -1;
        for (int index = 0; index < rangeCount; index++)
        {
            int recordOffset = recordsOffset + (index * 3);
            int firstGlyph = SfntFont.ReadUInt16(source, recordOffset, "CFF-FDSelect-first");
            int fd = source[recordOffset + 2];
            if (index == 0 && firstGlyph != 0 || firstGlyph <= previous || firstGlyph >= glyphCount || fd >= fdCount)
            {
                throw SfntFont.DataInvalid("CFF-FDSelect-range");
            }

            if (previous >= 0)
            {
                fontDictByGlyph.AsSpan(previous, firstGlyph - previous).Fill(checked((byte)previousFd));
            }

            previous = firstGlyph;
            previousFd = fd;
        }

        int sentinel = SfntFont.ReadUInt16(source, recordsOffset + (rangeCount * 3), "CFF-FDSelect-sentinel");
        if (sentinel != glyphCount)
        {
            throw SfntFont.DataInvalid("CFF-FDSelect-sentinel");
        }
        fontDictByGlyph.AsSpan(previous, sentinel - previous).Fill(checked((byte)previousFd));
        return fontDictByGlyph;
    }

    private static void ValidateCharset(
        byte[] source,
        int offset,
        ushort glyphCount,
        bool allowPredefined)
    {
        if (offset is 0 or 1 or 2)
        {
            int maximumGlyphCount = offset switch
            {
                0 => 229,
                1 => 166,
                _ => 87
            };
            if (!allowPredefined || glyphCount > maximumGlyphCount)
            {
                throw SfntFont.DataInvalid("CFF-charset-predefined");
            }

            return;
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

    private static Dictionary<ushort, ushort> ReadNameCharset(
        byte[] source,
        int offset,
        ushort glyphCount)
    {
        var glyphBySid = new Dictionary<ushort, ushort>(glyphCount)
        {
            [0] = 0
        };
        if (offset is 0 or 1 or 2)
        {
            int maximumGlyphCount = offset switch
            {
                0 => 229,
                1 => 166,
                _ => 87
            };
            if (glyphCount > maximumGlyphCount)
            {
                throw SfntFont.DataInvalid("CFF-charset-predefined");
            }

            if (offset == 0)
            {
                for (ushort glyph = 1; glyph < glyphCount; glyph++)
                {
                    glyphBySid.Add(glyph, glyph);
                }
            }
            else
            {
                (ushort Sid, ushort Glyph)[] entries = offset == 1
                    ? ExpertStandardGlyphs
                    : ExpertSubsetStandardGlyphs;
                foreach ((ushort sid, ushort glyph) in entries)
                {
                    if (glyph < glyphCount)
                    {
                        glyphBySid.Add(sid, glyph);
                    }
                }
            }

            return glyphBySid;
        }

        SfntFont.EnsureRange(source, offset, 1, "CFF-charset");
        byte format = source[offset];
        int remaining = glyphCount - 1;
        int position = offset + 1;
        ushort glyphIndex = 1;
        if (format == 0)
        {
            SfntFont.EnsureRange(source, position, checked(remaining * 2), "CFF-charset");
            for (int index = 0; index < remaining; index++)
            {
                ushort sid = SfntFont.ReadUInt16(source, position, "CFF-charset-SID");
                AddCharsetEntry(glyphBySid, sid, glyphIndex++);
                position += 2;
            }

            return glyphBySid;
        }

        if (format is not (1 or 2))
        {
            throw SfntFont.DataInvalid("CFF-charset-format");
        }

        while (remaining > 0)
        {
            int rangeLength = format == 1 ? 3 : 4;
            SfntFont.EnsureRange(source, position, rangeLength, "CFF-charset-range");
            ushort firstSid = SfntFont.ReadUInt16(source, position, "CFF-charset-SID");
            int left = format == 1
                ? source[position + 2]
                : SfntFont.ReadUInt16(source, position + 2, "CFF-charset-left");
            int count = checked(left + 1);
            if (count > remaining || firstSid > ushort.MaxValue - left)
            {
                throw SfntFont.DataInvalid("CFF-charset-count");
            }

            for (int index = 0; index < count; index++)
            {
                AddCharsetEntry(glyphBySid, checked((ushort)(firstSid + index)), glyphIndex++);
            }

            remaining -= count;
            position += rangeLength;
        }

        return glyphBySid;
    }

    private static void AddCharsetEntry(
        IDictionary<ushort, ushort> glyphBySid,
        ushort sid,
        ushort glyph)
    {
        if (sid == 0 || glyphBySid.ContainsKey(sid))
        {
            throw SfntFont.DataInvalid("CFF-charset-duplicate");
        }

        glyphBySid.Add(sid, glyph);
    }

    private static ushort GetStandardEncodingSid(byte code)
    {
        if (code is >= 32 and <= 126)
        {
            return checked((ushort)(code - 31));
        }

        return code switch
        {
            161 => 96,
            162 => 97,
            163 => 98,
            164 => 99,
            165 => 100,
            166 => 101,
            167 => 102,
            168 => 103,
            169 => 104,
            170 => 105,
            171 => 106,
            172 => 107,
            173 => 108,
            174 => 109,
            175 => 110,
            177 => 111,
            178 => 112,
            179 => 113,
            180 => 114,
            182 => 115,
            183 => 116,
            184 => 117,
            185 => 118,
            186 => 119,
            187 => 120,
            188 => 121,
            189 => 122,
            191 => 123,
            193 => 124,
            194 => 125,
            195 => 126,
            196 => 127,
            197 => 128,
            198 => 129,
            199 => 130,
            200 => 131,
            202 => 132,
            203 => 133,
            205 => 134,
            206 => 135,
            207 => 136,
            208 => 137,
            225 => 138,
            227 => 139,
            232 => 140,
            233 => 141,
            234 => 142,
            235 => 143,
            241 => 144,
            245 => 145,
            248 => 146,
            249 => 147,
            250 => 148,
            251 => 149,
            _ => 0
        };
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

        internal IReadOnlyList<ReadOnlyMemory<byte>> GetPrograms(byte[] source)
        {
            var programs = new ReadOnlyMemory<byte>[Objects.Count];
            for (int index = 0; index < Objects.Count; index++)
            {
                CffRange range = Objects[index];
                if (range.Length > 65_535)
                {
                    throw SfntFont.DataInvalid("CFF-CharString-length");
                }

                programs[index] = new ReadOnlyMemory<byte>(source, range.Offset, range.Length);
            }

            return programs;
        }
    }

    private sealed class ParsedCff(
        ushort glyphCount,
        CffIndex charStrings,
        IReadOnlyList<ReadOnlyMemory<byte>> globalSubroutines,
        IReadOnlyList<ReadOnlyMemory<byte>>[] localSubroutinesByFontDict,
        byte[] fontDictByGlyph,
        Dictionary<ushort, ushort>? glyphBySid)
    {
        internal ushort GlyphCount { get; } = glyphCount;

        internal CffIndex CharStrings { get; } = charStrings;

        internal IReadOnlyList<ReadOnlyMemory<byte>> GlobalSubroutines { get; } = globalSubroutines;

        internal IReadOnlyList<ReadOnlyMemory<byte>>[] LocalSubroutinesByFontDict { get; }
            = localSubroutinesByFontDict;

        internal byte[] FontDictByGlyph { get; } = fontDictByGlyph;

        internal Dictionary<ushort, ushort>? GlyphBySid { get; } = glyphBySid;
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
