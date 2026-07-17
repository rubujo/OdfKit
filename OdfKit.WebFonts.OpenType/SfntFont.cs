using System.Buffers.Binary;
using System.Text;
using OdfKit.Compliance;

namespace OdfKit.WebFonts.OpenType;

internal readonly struct UnicodeVariationSequence : IEquatable<UnicodeVariationSequence>
{
    internal UnicodeVariationSequence(int baseScalar, int selector)
    {
        BaseScalar = baseScalar;
        Selector = selector;
    }

    internal int BaseScalar { get; }

    internal int Selector { get; }

    public bool Equals(UnicodeVariationSequence other)
        => BaseScalar == other.BaseScalar && Selector == other.Selector;

    public override bool Equals(object? obj)
        => obj is UnicodeVariationSequence other && Equals(other);

    public override int GetHashCode()
        => (BaseScalar * 397) ^ Selector;
}

internal sealed class SfntSubset
{
    internal SfntSubset(uint flavor, SortedDictionary<string, byte[]> tables)
    {
        Flavor = flavor;
        Tables = tables;
    }

    internal uint Flavor { get; }

    internal SortedDictionary<string, byte[]> Tables { get; }
}

internal sealed class SfntFont
{
    private static readonly string[] CommonRequiredTables = ["OS/2", "cmap", "head", "hhea", "hmtx", "maxp", "name", "post"];
    private static readonly HashSet<string> RejectedTables = new(StringComparer.Ordinal)
    {
        "CFF2", "COLR", "CPAL", "CBDT", "CBLC", "EBDT", "EBLC", "EBSC", "SVG ", "sbix",
        "Silf", "Glat", "Gloc", "Feat", "Sill", "morx", "mort", "kerx"
    };

    private readonly SortedDictionary<string, byte[]> _tables;
    private readonly ushort _glyphCount;
    private readonly uint[] _locations;
    private readonly CmapMapping _cmap;

    private SfntFont(
        uint flavor,
        SortedDictionary<string, byte[]> tables,
        ushort glyphCount,
        uint[] locations,
        CmapMapping cmap)
    {
        Flavor = flavor;
        _tables = tables;
        _glyphCount = glyphCount;
        _locations = locations;
        _cmap = cmap;
    }

    private uint Flavor { get; }

    internal bool ContainsUnicodeScalar(int scalar)
        => _cmap.UnicodeMappings.TryGetValue(scalar, out ushort glyph) && glyph != 0;

    internal ushort GlyphCount => _glyphCount;

    internal ushort GetGlyphId(int scalar)
        => _cmap.UnicodeMappings.TryGetValue(scalar, out ushort glyph) ? glyph : (ushort)0;

    internal bool ContainsVariationSequence(int baseScalar, int selector)
        => _cmap.ContainsVariation(new UnicodeVariationSequence(baseScalar, selector));

    internal static SfntFont Parse(byte[] source, int faceIndex, int maxTableCount, bool validateChecksums)
    {
        if (source.Length < 12)
        {
            throw DataInvalid("sfnt-header");
        }

        ReadOnlySpan<byte> data = source;
        int faceOffset = 0;
        bool isCollection = data.Slice(0, 4).SequenceEqual("ttcf"u8);
        if (isCollection)
        {
            if (data.Length < 16)
            {
                throw DataInvalid("ttc-header");
            }

            uint count = ReadUInt32(data, 8, "ttc-count");
            if (count == 0 || count > 4096 || faceIndex < 0 || (uint)faceIndex >= count)
            {
                throw DataInvalid("ttc-face");
            }

            int offsetPosition = CheckedInt(12L + (faceIndex * 4L), "ttc-offset");
            faceOffset = CheckedInt(ReadUInt32(data, offsetPosition, "ttc-offset"), "ttc-offset");
        }
        else if (faceIndex != 0)
        {
            throw DataInvalid("sfnt-face");
        }

        EnsureRange(data, faceOffset, 12, "sfnt-header");
        uint flavor = ReadUInt32(data, faceOffset, "sfnt-flavor");
        bool isCff = flavor == 0x4F54544F;
        if (flavor != 0x00010000 && flavor != 0x74727565 && !isCff)
        {
            throw NotSupported("sfnt-flavor");
        }

        if (isCollection && isCff)
        {
            throw NotSupported("otc-face");
        }

        ushort tableCount = ReadUInt16(data, faceOffset + 4, "table-count");
        if (tableCount == 0 || tableCount > maxTableCount)
        {
            throw DataInvalid("table-count");
        }

        int directoryLength = checked(tableCount * 16);
        EnsureRange(data, faceOffset + 12, directoryLength, "table-directory");
        var tables = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
        var ranges = new List<(int Offset, int End, string Tag)>(tableCount);
        for (int index = 0; index < tableCount; index++)
        {
            int recordOffset = faceOffset + 12 + (index * 16);
            string tag = Encoding.ASCII.GetString(source, recordOffset, 4);
            if (!IsValidTag(tag) || tables.ContainsKey(tag))
            {
                throw DataInvalid("table-tag");
            }

            uint expectedChecksum = ReadUInt32(data, recordOffset + 4, $"{tag}-checksum");
            int offset = CheckedInt(ReadUInt32(data, recordOffset + 8, $"{tag}-offset"), $"{tag}-offset");
            int length = CheckedInt(ReadUInt32(data, recordOffset + 12, $"{tag}-length"), $"{tag}-length");
            EnsureRange(data, offset, length, tag);
            int end = checked(offset + length);
            foreach ((int existingOffset, int existingEnd, string existingTag) in ranges)
            {
                bool overlaps = offset < existingEnd && existingOffset < end;
                bool sameRange = offset == existingOffset && end == existingEnd;
                if (overlaps && !sameRange)
                {
                    throw DataInvalid($"{existingTag}/{tag}-overlap");
                }
            }

            ranges.Add((offset, end, tag));
            byte[] table = data.Slice(offset, length).ToArray();
            if (validateChecksums && CalculateTableChecksum(tag, table) != expectedChecksum)
            {
                throw DataInvalid($"{tag}-checksum");
            }

            tables.Add(tag, table);
        }

        string? rejectedTag = tables.Keys.FirstOrDefault(RejectedTables.Contains);
        if (rejectedTag is not null)
        {
            throw NotSupported(rejectedTag);
        }

        foreach (string tag in CommonRequiredTables)
        {
            if (!tables.ContainsKey(tag))
            {
                throw DataInvalid($"{tag}-missing");
            }
        }

        string[] outlineTables = isCff ? ["CFF "] : ["glyf", "loca"];
        foreach (string tag in outlineTables)
        {
            if (!tables.ContainsKey(tag))
            {
                throw DataInvalid($"{tag}-missing");
            }
        }

        if (isCff && (tables.ContainsKey("glyf") || tables.ContainsKey("loca")))
        {
            throw DataInvalid("outline-ambiguous");
        }

        ValidateEmbeddingRights(tables["OS/2"]);
        byte[] maxp = tables["maxp"];
        EnsureRange(maxp, 0, 6, "maxp");
        ushort glyphCount = ReadUInt16(maxp, 4, "maxp-numGlyphs");
        if (glyphCount == 0)
        {
            throw DataInvalid("maxp-numGlyphs");
        }

        byte[] head = tables["head"];
        EnsureRange(head, 0, 54, "head");
        uint[] locations = [];
        if (!isCff)
        {
            short locaFormat = ReadInt16(head, 50, "head-indexToLocFormat");
            locations = ReadLocations(tables["loca"], glyphCount, locaFormat, tables["glyf"].Length);
        }

        CmapMapping cmap = CmapMapping.Parse(tables["cmap"], glyphCount);
        return new SfntFont(flavor, tables, glyphCount, locations, cmap);
    }

    internal SfntSubset CreateSubset(
        IReadOnlyList<int> scalars,
        IReadOnlyList<UnicodeVariationSequence> variationSequences,
        int maxCompositeDepth)
    {
        var mappings = new SortedDictionary<int, ushort>();
        var selectedGlyphs = new HashSet<ushort> { 0 };
        foreach (int scalar in scalars)
        {
            if (!_cmap.UnicodeMappings.TryGetValue(scalar, out ushort glyph))
            {
                throw DataInvalid($"U+{scalar:X}-missing");
            }

            mappings.Add(scalar, glyph);
            selectedGlyphs.Add(glyph);
        }

        var selectedVariations = new List<CmapVariation>(variationSequences.Count);
        foreach (UnicodeVariationSequence sequence in variationSequences)
        {
            CmapVariation variation = _cmap.ResolveVariation(sequence);
            selectedVariations.Add(variation);
            selectedGlyphs.Add(variation.GlyphId);
        }

        if (_tables.TryGetValue("GSUB", out byte[]? gsub))
        {
            GsubGlyphClosure.Add(gsub, selectedGlyphs, _glyphCount);
        }

        bool retainFullLayoutGlyphSpace = scalars.Any(IsComplexShapingScalar)
            && (_tables.ContainsKey("GSUB") || _tables.ContainsKey("GPOS"));
        if (retainFullLayoutGlyphSpace)
        {
            for (int glyph = 1; glyph < _glyphCount; glyph++)
            {
                selectedGlyphs.Add((ushort)glyph);
            }
        }

        var tables = new SortedDictionary<string, byte[]>(_tables, StringComparer.Ordinal)
        {
            ["cmap"] = retainFullLayoutGlyphSpace
                ? (byte[])_tables["cmap"].Clone()
                : CmapMapping.Build(mappings, selectedVariations),
            ["head"] = (byte[])_tables["head"].Clone()
        };
        if (Flavor == 0x4F54544F)
        {
            tables["CFF "] = CffSubsetter.Build(_tables["CFF "], _glyphCount, selectedGlyphs);
        }
        else
        {
            AddCompositeClosure(selectedGlyphs, maxCompositeDepth);
            tables["glyf"] = BuildGlyf(selectedGlyphs, out byte[] subsetLoca);
            tables["loca"] = subsetLoca;
        }

        bool hasFvar = tables.TryGetValue("fvar", out byte[]? fvar);
        bool hasGvar = tables.TryGetValue("gvar", out byte[]? gvar);
        if (hasFvar != hasGvar || hasFvar && (fvar is null || gvar is null))
        {
            throw DataInvalid("variation-tables");
        }

        if (hasFvar)
        {
            tables["gvar"] = GvarSubsetter.Build(gvar!, fvar!, _glyphCount, selectedGlyphs);
        }

        tables.Remove("DSIG");
        if (Flavor != 0x4F54544F)
        {
            BinaryPrimitives.WriteInt16BigEndian(tables["head"].AsSpan(50, 2), 1);
            ushort headFlags = BinaryPrimitives.ReadUInt16BigEndian(tables["head"].AsSpan(16, 2));
            BinaryPrimitives.WriteUInt16BigEndian(tables["head"].AsSpan(16, 2), (ushort)(headFlags | 0x0800));
        }

        tables["head"].AsSpan(8, 4).Clear();
        return new SfntSubset(Flavor, tables);
    }

    internal void ValidateOutputFormats(IEnumerable<WebFontFormat> formats)
    {
        bool isCff = Flavor == 0x4F54544F;
        if (formats.Any(format => isCff
                ? format == WebFontFormat.TrueType
                : format == WebFontFormat.OpenType))
        {
            throw NotSupported("format-outline-mismatch");
        }
    }

    internal bool TryGetTable(string tag, out ReadOnlyMemory<byte> table)
    {
        if (_tables.TryGetValue(tag, out byte[]? value))
        {
            table = value;
            return true;
        }

        table = default;
        return false;
    }

    private static bool IsComplexShapingScalar(int scalar)
        => scalar is >= 0x0600 and <= 0x08FF
            or >= 0x0900 and <= 0x0DFF
            or >= 0x0F00 and <= 0x109F
            or >= 0x1780 and <= 0x17FF
            or >= 0x1A20 and <= 0x1AAF
            or >= 0xA8E0 and <= 0xA8FF
            or >= 0xA980 and <= 0xA9DF
            or >= 0xAA60 and <= 0xAA7F
            or >= 0xFB50 and <= 0xFDFF
            or >= 0xFE70 and <= 0xFEFF;

    private void AddCompositeClosure(HashSet<ushort> glyphs, int maxDepth)
    {
        var states = new byte[_glyphCount];
        foreach (ushort glyph in glyphs.ToArray())
        {
            VisitComposite(glyph, 0, maxDepth, glyphs, states);
        }
    }

    private void VisitComposite(
        ushort glyph,
        int depth,
        int maxDepth,
        HashSet<ushort> glyphs,
        byte[] states)
    {
        if (depth > maxDepth)
        {
            throw DataInvalid("composite-depth");
        }

        if (states[glyph] == 1)
        {
            throw DataInvalid("composite-cycle");
        }

        if (states[glyph] == 2)
        {
            return;
        }

        states[glyph] = 1;
        ReadOnlySpan<byte> data = GetGlyph(glyph);
        if (data.Length >= 10 && ReadInt16(data, 0, "glyf-contours") < 0)
        {
            int position = 10;
            ushort flags;
            do
            {
                EnsureRange(data, position, 4, "glyf-composite");
                flags = ReadUInt16(data, position, "glyf-flags");
                ushort component = ReadUInt16(data, position + 2, "glyf-component");
                if (component >= _glyphCount)
                {
                    throw DataInvalid("glyf-component");
                }

                glyphs.Add(component);
                VisitComposite(component, depth + 1, maxDepth, glyphs, states);
                position += 4;
                position += (flags & 0x0001) != 0 ? 4 : 2;
                if ((flags & 0x0008) != 0)
                {
                    position += 2;
                }
                else if ((flags & 0x0040) != 0)
                {
                    position += 4;
                }
                else if ((flags & 0x0080) != 0)
                {
                    position += 8;
                }

                EnsureRange(data, 0, position, "glyf-composite");
            }
            while ((flags & 0x0020) != 0);

            if ((flags & 0x0100) != 0)
            {
                EnsureRange(data, position, 2, "glyf-instructions");
                ushort instructionLength = ReadUInt16(data, position, "glyf-instructions");
                EnsureRange(data, position + 2, instructionLength, "glyf-instructions");
            }
        }

        states[glyph] = 2;
    }

    private byte[] BuildGlyf(HashSet<ushort> selectedGlyphs, out byte[] loca)
    {
        using var stream = new MemoryStream();
        loca = new byte[checked((_glyphCount + 1) * 4)];
        for (ushort glyph = 0; glyph < _glyphCount; glyph++)
        {
            BinaryPrimitives.WriteUInt32BigEndian(loca.AsSpan(glyph * 4, 4), checked((uint)stream.Length));
            if (selectedGlyphs.Contains(glyph))
            {
                ReadOnlySpan<byte> glyphData = GetGlyph(glyph);
                stream.Write(glyphData.ToArray(), 0, glyphData.Length);
                while ((stream.Length & 3) != 0)
                {
                    stream.WriteByte(0);
                }
            }
        }

        BinaryPrimitives.WriteUInt32BigEndian(loca.AsSpan(_glyphCount * 4, 4), checked((uint)stream.Length));
        return stream.ToArray();
    }

    private ReadOnlySpan<byte> GetGlyph(ushort glyph)
    {
        uint start = _locations[glyph];
        uint end = _locations[glyph + 1];
        if (end < start || end > _tables["glyf"].Length)
        {
            throw DataInvalid("loca-order");
        }

        return _tables["glyf"].AsSpan(checked((int)start), checked((int)(end - start)));
    }

    private static uint[] ReadLocations(byte[] loca, ushort glyphCount, short format, int glyfLength)
    {
        var result = new uint[glyphCount + 1];
        if (format == 0)
        {
            EnsureRange(loca, 0, checked((glyphCount + 1) * 2), "loca");
            for (int index = 0; index <= glyphCount; index++)
            {
                result[index] = checked((uint)(ReadUInt16(loca, index * 2, "loca") * 2));
            }
        }
        else if (format == 1)
        {
            EnsureRange(loca, 0, checked((glyphCount + 1) * 4), "loca");
            for (int index = 0; index <= glyphCount; index++)
            {
                result[index] = ReadUInt32(loca, index * 4, "loca");
            }
        }
        else
        {
            throw DataInvalid("head-indexToLocFormat");
        }

        uint previous = 0;
        foreach (uint offset in result)
        {
            if (offset < previous || offset > glyfLength)
            {
                throw DataInvalid("loca-order");
            }

            previous = offset;
        }

        return result;
    }

    private static void ValidateEmbeddingRights(byte[] os2)
    {
        EnsureRange(os2, 0, 10, "OS/2");
        ushort fsType = ReadUInt16(os2, 8, "OS/2-fsType");
        if ((fsType & 0x0002) != 0 || (fsType & 0x0100) != 0 || (fsType & 0x0200) != 0)
        {
            throw NotSupported("OS/2-fsType");
        }
    }

    internal static uint CalculateTableChecksum(string tag, ReadOnlySpan<byte> data)
    {
        uint sum = 0;
        int paddedLength = checked((data.Length + 3) & ~3);
        Span<byte> word = stackalloc byte[4];
        for (int offset = 0; offset < paddedLength; offset += 4)
        {
            word.Clear();
            int count = Math.Min(4, data.Length - offset);
            if (count > 0)
            {
                data.Slice(offset, count).CopyTo(word);
            }

            if (tag == "head" && offset == 8)
            {
                word.Clear();
            }

            sum = unchecked(sum + BinaryPrimitives.ReadUInt32BigEndian(word));
        }

        return sum;
    }

    private static bool IsValidTag(string tag)
        => tag.Length == 4 && tag.All(character => character is >= ' ' and <= '~');

    internal static ushort ReadUInt16(ReadOnlySpan<byte> data, int offset, string detail)
    {
        EnsureRange(data, offset, 2, detail);
        return BinaryPrimitives.ReadUInt16BigEndian(data.Slice(offset, 2));
    }

    internal static short ReadInt16(ReadOnlySpan<byte> data, int offset, string detail)
    {
        EnsureRange(data, offset, 2, detail);
        return BinaryPrimitives.ReadInt16BigEndian(data.Slice(offset, 2));
    }

    internal static uint ReadUInt32(ReadOnlySpan<byte> data, int offset, string detail)
    {
        EnsureRange(data, offset, 4, detail);
        return BinaryPrimitives.ReadUInt32BigEndian(data.Slice(offset, 4));
    }

    internal static void EnsureRange(ReadOnlySpan<byte> data, int offset, int length, string detail)
    {
        if (offset < 0 || length < 0 || offset > data.Length - length)
        {
            throw DataInvalid(detail);
        }
    }

    private static int CheckedInt(long value, string detail)
    {
        if (value < 0 || value > int.MaxValue)
        {
            throw DataInvalid(detail);
        }

        return (int)value;
    }

    internal static InvalidDataException DataInvalid(string detail)
        => new($"{OdfLocalizer.GetMessage("Err_WebFont_DataInvalid")} [{detail}]");

    private static NotSupportedException NotSupported(string detail)
        => new($"{OdfLocalizer.GetMessage("Err_WebFont_DataInvalid")} [{detail}]");
}

internal sealed class CmapVariation
{
    internal CmapVariation(int baseScalar, int selector, ushort glyphId, bool usesDefaultGlyph)
    {
        BaseScalar = baseScalar;
        Selector = selector;
        GlyphId = glyphId;
        UsesDefaultGlyph = usesDefaultGlyph;
    }

    internal int BaseScalar { get; }

    internal int Selector { get; }

    internal ushort GlyphId { get; }

    internal bool UsesDefaultGlyph { get; }
}

internal sealed class CmapMapping
{
    private readonly Dictionary<UnicodeVariationSequence, CmapVariation> _variations;

    private CmapMapping(
        Dictionary<int, ushort> unicodeMappings,
        Dictionary<UnicodeVariationSequence, CmapVariation> variations)
    {
        UnicodeMappings = unicodeMappings;
        _variations = variations;
    }

    internal Dictionary<int, ushort> UnicodeMappings { get; }

    internal static CmapMapping Parse(byte[] cmap, ushort glyphCount)
    {
        SfntFont.EnsureRange(cmap, 0, 4, "cmap");
        ushort count = SfntFont.ReadUInt16(cmap, 2, "cmap-count");
        SfntFont.EnsureRange(cmap, 4, checked(count * 8), "cmap-records");
        var mappings = new Dictionary<int, ushort>();
        var variations = new Dictionary<UnicodeVariationSequence, CmapVariation>();
        var offsets = new HashSet<uint>();
        for (int index = 0; index < count; index++)
        {
            int record = 4 + (index * 8);
            uint offset = SfntFont.ReadUInt32(cmap, record + 4, "cmap-offset");
            if (!offsets.Add(offset) || offset > int.MaxValue)
            {
                continue;
            }

            int subtable = (int)offset;
            ushort format = SfntFont.ReadUInt16(cmap, subtable, "cmap-format");
            if (format == 4)
            {
                ParseFormat4(cmap, subtable, glyphCount, mappings);
            }
            else if (format == 12)
            {
                ParseFormat12(cmap, subtable, glyphCount, mappings);
            }
            else if (format == 14)
            {
                ParseFormat14(cmap, subtable, glyphCount, mappings, variations);
            }
        }

        if (mappings.Count == 0)
        {
            throw SfntFont.DataInvalid("cmap-unicode");
        }

        return new CmapMapping(mappings, variations);
    }

    internal CmapVariation ResolveVariation(UnicodeVariationSequence sequence)
    {
        if (!_variations.TryGetValue(sequence, out CmapVariation? variation))
        {
            throw SfntFont.DataInvalid($"U+{sequence.BaseScalar:X}/U+{sequence.Selector:X}-missing");
        }

        return variation;
    }

    internal bool ContainsVariation(UnicodeVariationSequence sequence)
        => _variations.ContainsKey(sequence);

    internal static byte[] Build(
        SortedDictionary<int, ushort> mappings,
        IReadOnlyList<CmapVariation> variations)
    {
        byte[] format4 = BuildFormat4(mappings.Where(item => item.Key <= 0xFFFF));
        byte[] format12 = BuildFormat12(mappings);
        byte[]? format14 = variations.Count == 0 ? null : BuildFormat14(variations);
        ushort recordCount = checked((ushort)(format14 is null ? 3 : 4));
        int directoryLength = 4 + (recordCount * 8);
        int format4Offset = directoryLength;
        int format12Offset = checked(format4Offset + format4.Length);
        int format14Offset = checked(format12Offset + format12.Length);
        var output = new byte[checked(directoryLength + format4.Length + format12.Length + (format14?.Length ?? 0))];
        BinaryPrimitives.WriteUInt16BigEndian(output.AsSpan(2, 2), recordCount);
        WriteEncodingRecord(output, 4, 0, 3, format4Offset);
        WriteEncodingRecord(output, 12, 3, 1, format4Offset);
        WriteEncodingRecord(output, 20, 3, 10, format12Offset);
        if (format14 is not null)
        {
            WriteEncodingRecord(output, 28, 0, 5, format14Offset);
        }

        format4.CopyTo(output, format4Offset);
        format12.CopyTo(output, format12Offset);
        format14?.CopyTo(output, format14Offset);
        return output;
    }

    private static void ParseFormat4(
        byte[] cmap,
        int start,
        ushort glyphCount,
        Dictionary<int, ushort> mappings)
    {
        ushort length = SfntFont.ReadUInt16(cmap, start + 2, "cmap4-length");
        SfntFont.EnsureRange(cmap, start, length, "cmap4");
        ushort segCountX2 = SfntFont.ReadUInt16(cmap, start + 6, "cmap4-segments");
        if (segCountX2 == 0 || (segCountX2 & 1) != 0)
        {
            throw SfntFont.DataInvalid("cmap4-segments");
        }

        int segCount = segCountX2 / 2;
        int endCodes = start + 14;
        int startCodes = checked(endCodes + (segCount * 2) + 2);
        int deltas = checked(startCodes + (segCount * 2));
        int rangeOffsets = checked(deltas + (segCount * 2));
        SfntFont.EnsureRange(cmap, rangeOffsets, segCount * 2, "cmap4-arrays");
        for (int segment = 0; segment < segCount; segment++)
        {
            ushort end = SfntFont.ReadUInt16(cmap, endCodes + (segment * 2), "cmap4-end");
            ushort first = SfntFont.ReadUInt16(cmap, startCodes + (segment * 2), "cmap4-start");
            short delta = SfntFont.ReadInt16(cmap, deltas + (segment * 2), "cmap4-delta");
            ushort rangeOffset = SfntFont.ReadUInt16(cmap, rangeOffsets + (segment * 2), "cmap4-range");
            if (first > end)
            {
                throw SfntFont.DataInvalid("cmap4-order");
            }

            for (int code = first; code <= end && code != 0xFFFF; code++)
            {
                ushort glyph;
                if (rangeOffset == 0)
                {
                    glyph = unchecked((ushort)(code + delta));
                }
                else
                {
                    int glyphPosition = checked(rangeOffsets + (segment * 2) + rangeOffset + ((code - first) * 2));
                    glyph = SfntFont.ReadUInt16(cmap, glyphPosition, "cmap4-glyph");
                    if (glyph != 0)
                    {
                        glyph = unchecked((ushort)(glyph + delta));
                    }
                }

                AddMapping(mappings, code, glyph, glyphCount);
            }
        }
    }

    private static void ParseFormat12(
        byte[] cmap,
        int start,
        ushort glyphCount,
        Dictionary<int, ushort> mappings)
    {
        uint length = SfntFont.ReadUInt32(cmap, start + 4, "cmap12-length");
        if (length > int.MaxValue)
        {
            throw SfntFont.DataInvalid("cmap12-length");
        }

        SfntFont.EnsureRange(cmap, start, (int)length, "cmap12");
        uint groupCount = SfntFont.ReadUInt32(cmap, start + 12, "cmap12-groups");
        if (groupCount > 1_000_000)
        {
            throw SfntFont.DataInvalid("cmap12-groups");
        }

        SfntFont.EnsureRange(cmap, start + 16, checked((int)groupCount * 12), "cmap12-groups");
        uint previousEnd = 0;
        for (int index = 0; index < groupCount; index++)
        {
            int group = start + 16 + (index * 12);
            uint first = SfntFont.ReadUInt32(cmap, group, "cmap12-start");
            uint end = SfntFont.ReadUInt32(cmap, group + 4, "cmap12-end");
            uint firstGlyph = SfntFont.ReadUInt32(cmap, group + 8, "cmap12-glyph");
            if (first > end || end > 0x10FFFF || (index > 0 && first <= previousEnd))
            {
                throw SfntFont.DataInvalid("cmap12-order");
            }

            for (uint code = first; code <= end; code++)
            {
                uint glyph = checked(firstGlyph + (code - first));
                if (glyph > ushort.MaxValue)
                {
                    throw SfntFont.DataInvalid("cmap12-glyph");
                }

                AddMapping(mappings, checked((int)code), (ushort)glyph, glyphCount);
            }

            previousEnd = end;
        }
    }

    private static void ParseFormat14(
        byte[] cmap,
        int start,
        ushort glyphCount,
        Dictionary<int, ushort> mappings,
        Dictionary<UnicodeVariationSequence, CmapVariation> variations)
    {
        uint length = SfntFont.ReadUInt32(cmap, start + 2, "cmap14-length");
        if (length > int.MaxValue)
        {
            throw SfntFont.DataInvalid("cmap14-length");
        }

        SfntFont.EnsureRange(cmap, start, (int)length, "cmap14");
        uint selectorCount = SfntFont.ReadUInt32(cmap, start + 6, "cmap14-selectors");
        if (selectorCount > 512)
        {
            throw SfntFont.DataInvalid("cmap14-selectors");
        }

        SfntFont.EnsureRange(cmap, start + 10, checked((int)selectorCount * 11), "cmap14-records");
        for (int index = 0; index < selectorCount; index++)
        {
            int record = start + 10 + (index * 11);
            int selector = ReadUInt24(cmap, record, "cmap14-selector");
            uint defaultOffset = SfntFont.ReadUInt32(cmap, record + 3, "cmap14-default");
            uint nonDefaultOffset = SfntFont.ReadUInt32(cmap, record + 7, "cmap14-nondefault");
            if (defaultOffset != 0)
            {
                ParseDefaultUvs(cmap, start, defaultOffset, selector, mappings, variations);
            }

            if (nonDefaultOffset != 0)
            {
                ParseNonDefaultUvs(cmap, start, nonDefaultOffset, selector, glyphCount, variations);
            }
        }
    }

    private static void ParseDefaultUvs(
        byte[] cmap,
        int start,
        uint relativeOffset,
        int selector,
        Dictionary<int, ushort> mappings,
        Dictionary<UnicodeVariationSequence, CmapVariation> variations)
    {
        int offset = checked(start + checked((int)relativeOffset));
        uint count = SfntFont.ReadUInt32(cmap, offset, "cmap14-default-count");
        if (count > 1_000_000)
        {
            throw SfntFont.DataInvalid("cmap14-default-count");
        }

        SfntFont.EnsureRange(cmap, offset + 4, checked((int)count * 4), "cmap14-default-ranges");
        for (int index = 0; index < count; index++)
        {
            int range = offset + 4 + (index * 4);
            int first = ReadUInt24(cmap, range, "cmap14-default-start");
            int additional = cmap[range + 3];
            for (int scalar = first; scalar <= first + additional; scalar++)
            {
                if (mappings.TryGetValue(scalar, out ushort glyph))
                {
                    variations[new UnicodeVariationSequence(scalar, selector)] =
                        new CmapVariation(scalar, selector, glyph, usesDefaultGlyph: true);
                }
            }
        }
    }

    private static void ParseNonDefaultUvs(
        byte[] cmap,
        int start,
        uint relativeOffset,
        int selector,
        ushort glyphCount,
        Dictionary<UnicodeVariationSequence, CmapVariation> variations)
    {
        int offset = checked(start + checked((int)relativeOffset));
        uint count = SfntFont.ReadUInt32(cmap, offset, "cmap14-nondefault-count");
        if (count > 1_000_000)
        {
            throw SfntFont.DataInvalid("cmap14-nondefault-count");
        }

        SfntFont.EnsureRange(cmap, offset + 4, checked((int)count * 5), "cmap14-nondefault-records");
        for (int index = 0; index < count; index++)
        {
            int record = offset + 4 + (index * 5);
            int scalar = ReadUInt24(cmap, record, "cmap14-scalar");
            ushort glyph = SfntFont.ReadUInt16(cmap, record + 3, "cmap14-glyph");
            if (glyph == 0 || glyph >= glyphCount)
            {
                throw SfntFont.DataInvalid("cmap14-glyph");
            }

            variations[new UnicodeVariationSequence(scalar, selector)] =
                new CmapVariation(scalar, selector, glyph, usesDefaultGlyph: false);
        }
    }

    private static byte[] BuildFormat4(IEnumerable<KeyValuePair<int, ushort>> values)
    {
        KeyValuePair<int, ushort>[] mappings = values.OrderBy(item => item.Key).ToArray();
        int segmentCount = checked(mappings.Length + 1);
        int length = checked(16 + (segmentCount * 8));
        if (length > ushort.MaxValue)
        {
            throw SfntFont.DataInvalid("cmap4-size");
        }

        var output = new byte[length];
        BinaryPrimitives.WriteUInt16BigEndian(output.AsSpan(0, 2), 4);
        BinaryPrimitives.WriteUInt16BigEndian(output.AsSpan(2, 2), (ushort)length);
        BinaryPrimitives.WriteUInt16BigEndian(output.AsSpan(6, 2), checked((ushort)(segmentCount * 2)));
        ushort maximumPower = HighestPowerOfTwo((ushort)segmentCount);
        BinaryPrimitives.WriteUInt16BigEndian(output.AsSpan(8, 2), checked((ushort)(maximumPower * 2)));
        BinaryPrimitives.WriteUInt16BigEndian(output.AsSpan(10, 2), Log2(maximumPower));
        BinaryPrimitives.WriteUInt16BigEndian(output.AsSpan(12, 2), checked((ushort)((segmentCount * 2) - (maximumPower * 2))));
        int endCodes = 14;
        int startCodes = endCodes + (segmentCount * 2) + 2;
        int deltas = startCodes + (segmentCount * 2);
        for (int index = 0; index < mappings.Length; index++)
        {
            ushort code = checked((ushort)mappings[index].Key);
            BinaryPrimitives.WriteUInt16BigEndian(output.AsSpan(endCodes + (index * 2), 2), code);
            BinaryPrimitives.WriteUInt16BigEndian(output.AsSpan(startCodes + (index * 2), 2), code);
            ushort delta = unchecked((ushort)(mappings[index].Value - code));
            BinaryPrimitives.WriteUInt16BigEndian(output.AsSpan(deltas + (index * 2), 2), delta);
        }

        BinaryPrimitives.WriteUInt16BigEndian(output.AsSpan(endCodes + (mappings.Length * 2), 2), 0xFFFF);
        BinaryPrimitives.WriteUInt16BigEndian(output.AsSpan(startCodes + (mappings.Length * 2), 2), 0xFFFF);
        BinaryPrimitives.WriteUInt16BigEndian(output.AsSpan(deltas + (mappings.Length * 2), 2), 1);
        return output;
    }

    private static byte[] BuildFormat12(SortedDictionary<int, ushort> mappings)
    {
        var groups = new List<(uint Start, uint End, uint Glyph)>();
        foreach (KeyValuePair<int, ushort> item in mappings)
        {
            if (groups.Count > 0)
            {
                (uint start, uint end, uint glyph) = groups[groups.Count - 1];
                if (item.Key == end + 1 && item.Value == glyph + (item.Key - start))
                {
                    groups[groups.Count - 1] = (start, (uint)item.Key, glyph);
                    continue;
                }
            }

            groups.Add(((uint)item.Key, (uint)item.Key, item.Value));
        }

        var output = new byte[checked(16 + (groups.Count * 12))];
        BinaryPrimitives.WriteUInt16BigEndian(output.AsSpan(0, 2), 12);
        BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(4, 4), checked((uint)output.Length));
        BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(12, 4), checked((uint)groups.Count));
        for (int index = 0; index < groups.Count; index++)
        {
            int offset = 16 + (index * 12);
            BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(offset, 4), groups[index].Start);
            BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(offset + 4, 4), groups[index].End);
            BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(offset + 8, 4), groups[index].Glyph);
        }

        return output;
    }

    private static byte[] BuildFormat14(IReadOnlyList<CmapVariation> variations)
    {
        IGrouping<int, CmapVariation>[] groups = variations
            .GroupBy(item => item.Selector)
            .OrderBy(group => group.Key)
            .ToArray();
        int headerLength = checked(10 + (groups.Length * 11));
        var blocks = new List<(byte[]? Default, byte[]? NonDefault)>(groups.Length);
        int totalLength = headerLength;
        foreach (IGrouping<int, CmapVariation> group in groups)
        {
            CmapVariation[] defaults = group.Where(item => item.UsesDefaultGlyph).OrderBy(item => item.BaseScalar).ToArray();
            CmapVariation[] nonDefaults = group.Where(item => !item.UsesDefaultGlyph).OrderBy(item => item.BaseScalar).ToArray();
            byte[]? defaultBlock = defaults.Length == 0 ? null : BuildDefaultUvs(defaults);
            byte[]? nonDefaultBlock = nonDefaults.Length == 0 ? null : BuildNonDefaultUvs(nonDefaults);
            blocks.Add((defaultBlock, nonDefaultBlock));
            totalLength = checked(totalLength + (defaultBlock?.Length ?? 0) + (nonDefaultBlock?.Length ?? 0));
        }

        var output = new byte[totalLength];
        BinaryPrimitives.WriteUInt16BigEndian(output.AsSpan(0, 2), 14);
        BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(2, 4), checked((uint)totalLength));
        BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(6, 4), checked((uint)groups.Length));
        int dataOffset = headerLength;
        for (int index = 0; index < groups.Length; index++)
        {
            int record = 10 + (index * 11);
            WriteUInt24(output, record, groups[index].Key);
            if (blocks[index].Default is not null)
            {
                BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(record + 3, 4), checked((uint)dataOffset));
                blocks[index].Default!.CopyTo(output, dataOffset);
                dataOffset += blocks[index].Default!.Length;
            }

            if (blocks[index].NonDefault is not null)
            {
                BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(record + 7, 4), checked((uint)dataOffset));
                blocks[index].NonDefault!.CopyTo(output, dataOffset);
                dataOffset += blocks[index].NonDefault!.Length;
            }
        }

        return output;
    }

    private static byte[] BuildDefaultUvs(IReadOnlyList<CmapVariation> values)
    {
        var output = new byte[checked(4 + (values.Count * 4))];
        BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(0, 4), checked((uint)values.Count));
        for (int index = 0; index < values.Count; index++)
        {
            WriteUInt24(output, 4 + (index * 4), values[index].BaseScalar);
        }

        return output;
    }

    private static byte[] BuildNonDefaultUvs(IReadOnlyList<CmapVariation> values)
    {
        var output = new byte[checked(4 + (values.Count * 5))];
        BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(0, 4), checked((uint)values.Count));
        for (int index = 0; index < values.Count; index++)
        {
            int offset = 4 + (index * 5);
            WriteUInt24(output, offset, values[index].BaseScalar);
            BinaryPrimitives.WriteUInt16BigEndian(output.AsSpan(offset + 3, 2), values[index].GlyphId);
        }

        return output;
    }

    private static void WriteEncodingRecord(byte[] output, int offset, ushort platform, ushort encoding, int subtableOffset)
    {
        BinaryPrimitives.WriteUInt16BigEndian(output.AsSpan(offset, 2), platform);
        BinaryPrimitives.WriteUInt16BigEndian(output.AsSpan(offset + 2, 2), encoding);
        BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(offset + 4, 4), checked((uint)subtableOffset));
    }

    private static void AddMapping(Dictionary<int, ushort> mappings, int scalar, ushort glyph, ushort glyphCount)
    {
        if (glyph >= glyphCount)
        {
            throw SfntFont.DataInvalid("cmap-glyph");
        }

        if (glyph != 0 && (!mappings.TryGetValue(scalar, out ushort existing) || existing == 0))
        {
            mappings[scalar] = glyph;
        }
    }

    private static ushort HighestPowerOfTwo(ushort value)
    {
        ushort result = 1;
        while (result <= value / 2)
        {
            result *= 2;
        }

        return result;
    }

    private static ushort Log2(ushort value)
    {
        ushort result = 0;
        while (value > 1)
        {
            value /= 2;
            result++;
        }

        return result;
    }

    private static int ReadUInt24(ReadOnlySpan<byte> data, int offset, string detail)
    {
        SfntFont.EnsureRange(data, offset, 3, detail);
        return (data[offset] << 16) | (data[offset + 1] << 8) | data[offset + 2];
    }

    private static void WriteUInt24(Span<byte> data, int offset, int value)
    {
        if (value is < 0 or > 0xFFFFFF)
        {
            throw SfntFont.DataInvalid("uint24");
        }

        data[offset] = (byte)(value >> 16);
        data[offset + 1] = (byte)(value >> 8);
        data[offset + 2] = (byte)value;
    }
}
