using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using System.Xml;

namespace OdfKit.WebFonts.OpenType;

[Flags]
internal enum ColorFontTechnology
{
    None = 0,
    ColrV0 = 1,
    ColrV1 = 2,
    Cbdt = 4,
    Ebdt = 8,
    Svg = 16,
    Sbix = 32
}

internal sealed class ColorGlyphClosure
{
    private readonly IReadOnlyDictionary<ushort, ushort[]> _references;

    internal ColorGlyphClosure(ColorFontTechnology technologies, IReadOnlyDictionary<ushort, ushort[]> references)
    {
        Technologies = technologies;
        _references = references;
    }

    internal bool HasColorTables => Technologies != ColorFontTechnology.None;

    internal ColorFontTechnology Technologies { get; }

    internal void AddReferencedGlyphs(HashSet<ushort> glyphs)
    {
        if (!HasColorTables || _references.Count == 0)
        {
            return;
        }

        var pending = new Queue<ushort>(glyphs);
        var visited = new HashSet<ushort>();
        while (pending.Count != 0)
        {
            ushort glyph = pending.Dequeue();
            if (!visited.Add(glyph) || !_references.TryGetValue(glyph, out ushort[]? references))
            {
                continue;
            }

            foreach (ushort reference in references)
            {
                if (glyphs.Add(reference))
                {
                    pending.Enqueue(reference);
                }
            }
        }
    }
}

internal static class ColorFontValidator
{
    /// <remarks>
    /// 取消權杖只在各技術階段之間檢查，不逐 glyph 傳遞。依據是所有圖狀巡訪都有
    /// 記憶化，而非僅靠位元組範圍檢查：COLR paint 的 <c>_visitStates</c> 與
    /// <c>_dependencyCache</c>、<c>sbix dupe</c> 的 <c>states</c>，以及
    /// <c>ColorGlyphClosure</c> 的 <c>visited</c>，都讓每個節點至多處理一次，
    /// 因此不存在 CharString 直譯器那種 breadth^depth 的展開。
    /// 若日後移除任一記憶化，本註解的前提即不再成立，必須改為逐 glyph 傳遞權杖。
    /// </remarks>
    internal static ColorGlyphClosure Validate(
        IReadOnlyDictionary<string, byte[]> tables,
        ushort glyphCount,
        CancellationToken cancellationToken = default)
    {
        ColorFontTechnology technologies = ColorFontTechnology.None;
        var references = new Dictionary<ushort, HashSet<ushort>>();
        bool hasCpal = tables.TryGetValue("CPAL", out byte[]? cpal);
        ushort paletteEntryCount = 0;
        if (hasCpal)
        {
            paletteEntryCount = ValidateCpal(cpal!);
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (tables.TryGetValue("COLR", out byte[]? colr))
        {
            if (!hasCpal)
            {
                throw SfntFont.DataInvalid("COLR-CPAL-pair");
            }

            technologies |= ValidateColr(colr, glyphCount, paletteEntryCount, references);
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (ValidateBitmapPair(tables, "CBDT", "CBLC", glyphCount))
        {
            technologies |= ColorFontTechnology.Cbdt;
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (ValidateBitmapPair(tables, "EBDT", "EBLC", glyphCount))
        {
            technologies |= ColorFontTechnology.Ebdt;
        }
        if (tables.TryGetValue("EBSC", out byte[]? ebsc))
        {
            if (!tables.ContainsKey("EBDT") || !tables.ContainsKey("EBLC"))
            {
                throw SfntFont.DataInvalid("EBSC-EBDT-EBLC-pair");
            }

            ValidateVersionAndCount(ebsc, "EBSC", 0x00020000u, 8, 28);
            technologies |= ColorFontTechnology.Ebdt;
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (tables.TryGetValue("SVG ", out byte[]? svg))
        {
            ValidateSvg(svg, glyphCount);
            technologies |= ColorFontTechnology.Svg;
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (tables.TryGetValue("sbix", out byte[]? sbix))
        {
            var sbixReferences = new Dictionary<ushort, HashSet<ushort>>();
            ValidateSbix(sbix, glyphCount, sbixReferences);
            foreach (KeyValuePair<ushort, HashSet<ushort>> reference in sbixReferences)
            {
                AddReferences(references, reference.Key, reference.Value);
            }

            technologies |= ColorFontTechnology.Sbix;
        }

        return new ColorGlyphClosure(
            technologies,
            references.ToDictionary(
                pair => pair.Key,
                pair => pair.Value.OrderBy(glyph => glyph).ToArray()));
    }

    private static ushort ValidateCpal(byte[] table)
    {
        SfntFont.EnsureRange(table, 0, 12, "CPAL-header");
        ushort version = ReadUInt16(table, 0);
        if (version > 1)
        {
            throw SfntFont.DataInvalid("CPAL-version");
        }

        ushort entriesPerPalette = ReadUInt16(table, 2);
        ushort paletteCount = ReadUInt16(table, 4);
        ushort colorCount = ReadUInt16(table, 6);
        uint colorOffset = ReadUInt32(table, 8);
        if (entriesPerPalette == 0 || paletteCount == 0 || colorCount == 0)
        {
            throw SfntFont.DataInvalid("CPAL-count");
        }

        SfntFont.EnsureRange(table, 12, checked(paletteCount * 2), "CPAL-indices");
        for (int index = 0; index < paletteCount; index++)
        {
            ushort firstColor = ReadUInt16(table, 12 + (index * 2));
            if (firstColor > colorCount || entriesPerPalette > colorCount - firstColor)
            {
                throw SfntFont.DataInvalid("CPAL-index");
            }
        }

        EnsureOffsetRange(table, colorOffset, checked((uint)colorCount * 4), "CPAL-colors");
        if (version == 1)
        {
            int extension = checked(12 + (paletteCount * 2));
            SfntFont.EnsureRange(table, extension, 12, "CPAL-v1");
            ValidateOptionalArray(table, ReadUInt32(table, extension), paletteCount, 4, "CPAL-paletteTypes");
            ValidateOptionalArray(table, ReadUInt32(table, extension + 4), paletteCount, 2, "CPAL-paletteLabels");
            ValidateOptionalArray(table, ReadUInt32(table, extension + 8), entriesPerPalette, 2, "CPAL-entryLabels");
        }

        return entriesPerPalette;
    }

    private static ColorFontTechnology ValidateColr(
        byte[] table,
        ushort glyphCount,
        ushort paletteEntryCount,
        Dictionary<ushort, HashSet<ushort>> references)
    {
        SfntFont.EnsureRange(table, 0, 14, "COLR-header");
        ushort version = ReadUInt16(table, 0);
        if (version > 1)
        {
            throw SfntFont.DataInvalid("COLR-version");
        }

        ushort baseCount = ReadUInt16(table, 2);
        uint baseOffset = ReadUInt32(table, 4);
        uint layerOffset = ReadUInt32(table, 8);
        ushort layerCount = ReadUInt16(table, 12);
        EnsureOffsetRange(table, baseOffset, checked((uint)baseCount * 6), "COLR-baseRecords");
        EnsureOffsetRange(table, layerOffset, checked((uint)layerCount * 4), "COLR-layerRecords");
        var versionZeroReferences = new Dictionary<ushort, HashSet<ushort>>();
        int previousBaseGlyph = -1;
        for (int index = 0; index < baseCount; index++)
        {
            int record = checked((int)baseOffset + (index * 6));
            ushort baseGlyph = ReadUInt16(table, record);
            ValidateGlyph(baseGlyph, glyphCount, "COLR-baseGlyph");
            if (baseGlyph <= previousBaseGlyph)
            {
                throw SfntFont.DataInvalid("COLR-baseGlyphOrder");
            }

            previousBaseGlyph = baseGlyph;
            ushort firstLayer = ReadUInt16(table, record + 2);
            ushort count = ReadUInt16(table, record + 4);
            if (firstLayer > layerCount || count > layerCount - firstLayer)
            {
                throw SfntFont.DataInvalid("COLR-layerRange");
            }

            var layerGlyphs = new HashSet<ushort>();
            for (int layer = firstLayer; layer < firstLayer + count; layer++)
            {
                layerGlyphs.Add(ReadUInt16(table, checked((int)layerOffset + (layer * 4))));
            }

            versionZeroReferences.Add(baseGlyph, layerGlyphs);
        }

        for (int index = 0; index < layerCount; index++)
        {
            int record = checked((int)layerOffset + (index * 4));
            ValidateGlyph(ReadUInt16(table, record), glyphCount, "COLR-layerGlyph");
            ValidatePaletteIndex(ReadUInt16(table, record + 2), paletteEntryCount, "COLR-layerPalette");
        }

        if (version == 1)
        {
            SfntFont.EnsureRange(table, 14, 20, "COLR-v1-header");
            uint baseGlyphListOffset = ReadUInt32(table, 14);
            uint layerListOffset = ReadUInt32(table, 18);
            ValidateColrV1ListOffset(table, baseGlyphListOffset, 4, "COLR-baseGlyphList");
            ValidateColrV1ListOffset(table, layerListOffset, 4, "COLR-layerList");
            uint clipListOffset = ReadUInt32(table, 22);
            ValidateOptionalOffset(table, ReadUInt32(table, 26), "COLR-varIndexMap");
            uint itemVariationStoreOffset = ReadUInt32(table, 30);
            ValidateOptionalOffset(table, itemVariationStoreOffset, "COLR-itemVariationStore");

            if (baseGlyphListOffset == 0)
            {
                throw SfntFont.DataInvalid("COLR-baseGlyphList");
            }

            var graph = new ColrV1GraphValidator(
                table,
                glyphCount,
                paletteEntryCount,
                baseGlyphListOffset,
                layerListOffset,
                clipListOffset,
                itemVariationStoreOffset != 0);
            graph.Validate(references);
            foreach (KeyValuePair<ushort, HashSet<ushort>> reference in versionZeroReferences)
            {
                if (!graph.ContainsBaseGlyph(reference.Key))
                {
                    AddReferences(references, reference.Key, reference.Value);
                }
            }

            return ColorFontTechnology.ColrV1;
        }

        foreach (KeyValuePair<ushort, HashSet<ushort>> reference in versionZeroReferences)
        {
            AddReferences(references, reference.Key, reference.Value);
        }

        return ColorFontTechnology.ColrV0;
    }

    private sealed class ColrV1GraphValidator
    {
        private const int MaximumPaintDepth = 128;
        private readonly byte[] _table;
        private readonly ushort _glyphCount;
        private readonly ushort _paletteEntryCount;
        private readonly uint _baseGlyphListOffset;
        private readonly uint _layerListOffset;
        private readonly bool _hasVariationStore;
        private readonly Dictionary<ushort, int> _basePaints = [];
        private readonly List<int> _layerPaints = [];
        private readonly Dictionary<int, PaintNode> _nodes = [];
        private readonly Dictionary<int, byte> _visitStates = [];
        private readonly Dictionary<int, ushort[]> _dependencyCache = [];

        internal ColrV1GraphValidator(
            byte[] table,
            ushort glyphCount,
            ushort paletteEntryCount,
            uint baseGlyphListOffset,
            uint layerListOffset,
            uint clipListOffset,
            bool hasVariationStore)
        {
            _table = table;
            _glyphCount = glyphCount;
            _paletteEntryCount = paletteEntryCount;
            _baseGlyphListOffset = baseGlyphListOffset;
            _layerListOffset = layerListOffset;
            _hasVariationStore = hasVariationStore;
            ParseBaseGlyphList();
            ParseLayerList();
            ValidateClipList(clipListOffset);
        }

        internal bool ContainsBaseGlyph(ushort glyph) => _basePaints.ContainsKey(glyph);

        internal void Validate(Dictionary<ushort, HashSet<ushort>> references)
        {
            foreach (int root in _basePaints.Values)
            {
                Visit(root, 0);
            }

            foreach (KeyValuePair<ushort, int> basePaint in _basePaints)
            {
                AddReferences(references, basePaint.Key, CollectDependencies(basePaint.Value, 0));
            }
        }

        private void ParseBaseGlyphList()
        {
            int offset = checked((int)_baseGlyphListOffset);
            uint count = ReadUInt32(_table, offset);
            if (count == 0 || count > _glyphCount)
            {
                throw SfntFont.DataInvalid("COLR-baseGlyphPaintCount");
            }

            EnsureOffsetRange(
                _table,
                checked(_baseGlyphListOffset + 4),
                checked(count * 6),
                "COLR-baseGlyphPaintRecords");
            int previousGlyph = -1;
            for (int index = 0; index < count; index++)
            {
                int record = checked(offset + 4 + (index * 6));
                ushort glyph = ReadUInt16(_table, record);
                ValidateGlyph(glyph, _glyphCount, "COLR-v1-baseGlyph");
                if (glyph <= previousGlyph)
                {
                    throw SfntFont.DataInvalid("COLR-v1-baseGlyphOrder");
                }

                previousGlyph = glyph;
                int paint = ResolveRelativeOffset(
                    _baseGlyphListOffset,
                    ReadUInt32(_table, record + 2),
                    "COLR-paint");
                _basePaints.Add(glyph, paint);
            }
        }

        private void ParseLayerList()
        {
            if (_layerListOffset == 0)
            {
                return;
            }

            int offset = checked((int)_layerListOffset);
            uint count = ReadUInt32(_table, offset);
            EnsureOffsetRange(_table, checked(_layerListOffset + 4), checked(count * 4), "COLR-layerPaintOffsets");
            for (int index = 0; index < count; index++)
            {
                _layerPaints.Add(ResolveRelativeOffset(
                    _layerListOffset,
                    ReadUInt32(_table, checked(offset + 4 + (index * 4))),
                    "COLR-layerPaint"));
            }
        }

        private void ValidateClipList(uint offsetValue)
        {
            if (offsetValue == 0)
            {
                return;
            }

            EnsureOffsetRange(_table, offsetValue, 5, "COLR-clipList");
            int offset = checked((int)offsetValue);
            if (_table[offset] != 1)
            {
                throw SfntFont.DataInvalid("COLR-clipListFormat");
            }

            uint count = ReadUInt32(_table, offset + 1);
            EnsureOffsetRange(_table, checked(offsetValue + 5), checked(count * 7), "COLR-clips");
            int previousEnd = -1;
            for (int index = 0; index < count; index++)
            {
                int record = checked(offset + 5 + (index * 7));
                ushort first = ReadUInt16(_table, record);
                ushort last = ReadUInt16(_table, record + 2);
                if (first > last || last >= _glyphCount || first <= previousEnd)
                {
                    throw SfntFont.DataInvalid("COLR-clipGlyphRange");
                }

                previousEnd = last;
                int clipBox = ResolveRelativeOffset(offsetValue, ReadUInt24(_table, record + 4), "COLR-clipBox");
                byte format = _table[clipBox];
                int length = format switch
                {
                    1 => 9,
                    2 when _hasVariationStore => 13,
                    2 => throw SfntFont.DataInvalid("COLR-variablePaintStore"),
                    _ => throw SfntFont.DataInvalid("COLR-clipBoxFormat")
                };
                SfntFont.EnsureRange(_table, clipBox, length, "COLR-clipBox");
            }
        }

        private void Visit(int offset, int depth)
        {
            if (depth > MaximumPaintDepth)
            {
                throw SfntFont.DataInvalid("COLR-paintDepth");
            }

            if (_visitStates.TryGetValue(offset, out byte state))
            {
                if (state == 1)
                {
                    throw SfntFont.DataInvalid("COLR-paintCycle");
                }

                return;
            }

            _visitStates[offset] = 1;
            PaintNode node = ParsePaint(offset);
            _nodes[offset] = node;
            foreach (int child in node.Children)
            {
                Visit(child, checked(depth + 1));
            }

            _visitStates[offset] = 2;
        }

        private PaintNode ParsePaint(int offset)
        {
            SfntFont.EnsureRange(_table, offset, 1, "COLR-paint");
            byte format = _table[offset];
            if (IsVariablePaint(format) && !_hasVariationStore)
            {
                throw SfntFont.DataInvalid("COLR-variablePaintStore");
            }

            return format switch
            {
                1 => ParseLayers(offset),
                2 => ParseSolid(offset, 5),
                3 => ParseSolid(offset, 9),
                4 => ParseGradient(offset, 16, false),
                5 => ParseGradient(offset, 20, true),
                6 => ParseGradient(offset, 16, false),
                7 => ParseGradient(offset, 20, true),
                8 => ParseGradient(offset, 12, false),
                9 => ParseGradient(offset, 16, true),
                10 => ParseGlyph(offset),
                11 => ParseColrGlyph(offset),
                12 => ParseTransform(offset, 7, 24),
                13 => ParseTransform(offset, 7, 28),
                14 => ParseSingleChild(offset, 8),
                15 => ParseSingleChild(offset, 12),
                16 => ParseSingleChild(offset, 8),
                17 => ParseSingleChild(offset, 12),
                18 => ParseSingleChild(offset, 12),
                19 => ParseSingleChild(offset, 16),
                20 => ParseSingleChild(offset, 6),
                21 => ParseSingleChild(offset, 10),
                22 => ParseSingleChild(offset, 10),
                23 => ParseSingleChild(offset, 14),
                24 => ParseSingleChild(offset, 6),
                25 => ParseSingleChild(offset, 10),
                26 => ParseSingleChild(offset, 10),
                27 => ParseSingleChild(offset, 14),
                28 => ParseSingleChild(offset, 8),
                29 => ParseSingleChild(offset, 12),
                30 => ParseSingleChild(offset, 12),
                31 => ParseSingleChild(offset, 16),
                32 => ParseComposite(offset),
                _ => throw SfntFont.DataInvalid("COLR-paintFormat")
            };
        }

        private PaintNode ParseLayers(int offset)
        {
            SfntFont.EnsureRange(_table, offset, 6, "COLR-paintLayers");
            int count = _table[offset + 1];
            uint first = ReadUInt32(_table, offset + 2);
            if (_layerListOffset == 0
                || first > (uint)_layerPaints.Count
                || count > _layerPaints.Count - checked((int)first))
            {
                throw SfntFont.DataInvalid("COLR-paintLayerRange");
            }

            return new PaintNode(_layerPaints.Skip(checked((int)first)).Take(count).ToArray(), []);
        }

        private PaintNode ParseSolid(int offset, int length)
        {
            SfntFont.EnsureRange(_table, offset, length, "COLR-paintSolid");
            ValidatePaletteIndex(ReadUInt16(_table, offset + 1), _paletteEntryCount, "COLR-paintPalette");
            return PaintNode.Empty;
        }

        private PaintNode ParseGradient(int offset, int length, bool variable)
        {
            SfntFont.EnsureRange(_table, offset, length, "COLR-paintGradient");
            int colorLine = ResolveRelativeOffset((uint)offset, ReadUInt24(_table, offset + 1), "COLR-colorLine");
            ValidateColorLine(colorLine, variable);
            return PaintNode.Empty;
        }

        private void ValidateColorLine(int offset, bool variable)
        {
            SfntFont.EnsureRange(_table, offset, 3, "COLR-colorLine");
            ushort count = ReadUInt16(_table, offset + 1);
            int recordLength = variable ? 10 : 6;
            SfntFont.EnsureRange(_table, offset + 3, checked(count * recordLength), "COLR-colorStops");
            for (int index = 0; index < count; index++)
            {
                int record = checked(offset + 3 + (index * recordLength));
                ValidatePaletteIndex(ReadUInt16(_table, record + 2), _paletteEntryCount, "COLR-colorStopPalette");
            }
        }

        private PaintNode ParseGlyph(int offset)
        {
            SfntFont.EnsureRange(_table, offset, 6, "COLR-paintGlyph");
            ushort glyph = ReadUInt16(_table, offset + 4);
            ValidateGlyph(glyph, _glyphCount, "COLR-paintGlyphId");
            return new PaintNode([ResolvePaintOffset(offset, 1)], [glyph]);
        }

        private PaintNode ParseColrGlyph(int offset)
        {
            SfntFont.EnsureRange(_table, offset, 3, "COLR-paintColrGlyph");
            ushort glyph = ReadUInt16(_table, offset + 1);
            if (!_basePaints.TryGetValue(glyph, out int paint))
            {
                throw SfntFont.DataInvalid("COLR-paintColrGlyphId");
            }

            return new PaintNode([paint], [glyph], stopDependencyTraversal: true);
        }

        private PaintNode ParseTransform(int offset, int length, int transformLength)
        {
            SfntFont.EnsureRange(_table, offset, length, "COLR-paintTransform");
            int transform = ResolveRelativeOffset((uint)offset, ReadUInt24(_table, offset + 4), "COLR-transform");
            SfntFont.EnsureRange(_table, transform, transformLength, "COLR-transform");
            return new PaintNode([ResolvePaintOffset(offset, 1)], []);
        }

        private PaintNode ParseSingleChild(int offset, int length)
        {
            SfntFont.EnsureRange(_table, offset, length, "COLR-paintTransform");
            return new PaintNode([ResolvePaintOffset(offset, 1)], []);
        }

        private PaintNode ParseComposite(int offset)
        {
            SfntFont.EnsureRange(_table, offset, 8, "COLR-paintComposite");
            return new PaintNode([ResolvePaintOffset(offset, 1), ResolvePaintOffset(offset, 5)], []);
        }

        private int ResolvePaintOffset(int origin, int fieldOffset)
            => ResolveRelativeOffset((uint)origin, ReadUInt24(_table, origin + fieldOffset), "COLR-paint");

        private int ResolveRelativeOffset(uint origin, uint relative, string detail)
        {
            ValidateRelativeOffset(_table, origin, relative, detail);
            int result = checked((int)(origin + relative));
            if (result <= origin)
            {
                throw SfntFont.DataInvalid(detail);
            }

            return result;
        }

        private ushort[] CollectDependencies(int offset, int depth)
        {
            if (depth > MaximumPaintDepth)
            {
                throw SfntFont.DataInvalid("COLR-paintDepth");
            }

            if (_dependencyCache.TryGetValue(offset, out ushort[]? cached))
            {
                return cached;
            }

            PaintNode node = _nodes[offset];
            var dependencies = new HashSet<ushort>(node.Glyphs);
            if (!node.StopDependencyTraversal)
            {
                foreach (int child in node.Children)
                {
                    dependencies.UnionWith(CollectDependencies(child, checked(depth + 1)));
                }
            }

            ushort[] result = dependencies.OrderBy(glyph => glyph).ToArray();
            _dependencyCache.Add(offset, result);
            return result;
        }

        private static bool IsVariablePaint(byte format)
            => format is 3 or 5 or 7 or 9 or 13 or 15 or 17 or 19 or 21 or 23 or 25 or 27 or 29 or 31;

        private sealed class PaintNode(
            int[] children,
            ushort[] glyphs,
            bool stopDependencyTraversal = false)
        {
            internal static PaintNode Empty { get; } = new([], []);

            internal int[] Children { get; } = children;

            internal ushort[] Glyphs { get; } = glyphs;

            internal bool StopDependencyTraversal { get; } = stopDependencyTraversal;
        }
    }

    private static bool ValidateBitmapPair(
        IReadOnlyDictionary<string, byte[]> tables,
        string dataTag,
        string locationTag,
        ushort glyphCount)
    {
        bool hasData = tables.TryGetValue(dataTag, out byte[]? data);
        bool hasLocation = tables.TryGetValue(locationTag, out byte[]? location);
        if (hasData != hasLocation)
        {
            throw SfntFont.DataInvalid($"{dataTag}-{locationTag}-pair");
        }

        if (!hasData)
        {
            return false;
        }

        SfntFont.EnsureRange(data!, 0, 4, $"{dataTag}-header");
        SfntFont.EnsureRange(location!, 0, 8, $"{locationTag}-header");
        uint expectedVersion = dataTag == "CBDT" ? 0x00030000u : 0x00020000u;
        if (ReadUInt32(data!, 0) != expectedVersion || ReadUInt32(location!, 0) != expectedVersion)
        {
            throw SfntFont.DataInvalid($"{dataTag}-version");
        }

        uint strikeCount = ReadUInt32(location!, 4);
        if (strikeCount > 4096)
        {
            throw SfntFont.DataInvalid($"{locationTag}-strikeCount");
        }

        EnsureOffsetRange(location!, 8, checked(strikeCount * 48), $"{locationTag}-strikes");
        var strikeRanges = new List<(uint Start, uint End)>();
        for (int strike = 0; strike < strikeCount; strike++)
        {
            int record = checked(8 + (strike * 48));
            uint arrayOffset = ReadUInt32(location!, record);
            uint arraySize = ReadUInt32(location!, record + 4);
            uint subtableCount = ReadUInt32(location!, record + 8);
            ushort firstGlyph = ReadUInt16(location!, record + 40);
            ushort lastGlyph = ReadUInt16(location!, record + 42);
            if (firstGlyph > lastGlyph || lastGlyph >= glyphCount || subtableCount > glyphCount)
            {
                throw SfntFont.DataInvalid($"{locationTag}-glyphRange");
            }

            EnsureOffsetRange(location!, arrayOffset, arraySize, $"{locationTag}-indexTables");
            if (arraySize < checked(subtableCount * 8))
            {
                throw SfntFont.DataInvalid($"{locationTag}-indexArray");
            }

            uint arrayEnd = checked(arrayOffset + arraySize);
            foreach ((uint start, uint end) in strikeRanges)
            {
                if (arrayOffset < end && start < arrayEnd)
                {
                    throw SfntFont.DataInvalid($"{locationTag}-strikeOverlap");
                }
            }

            strikeRanges.Add((arrayOffset, arrayEnd));

            int previousLastGlyph = -1;
            for (int index = 0; index < subtableCount; index++)
            {
                int entry = checked((int)arrayOffset + (index * 8));
                ushort first = ReadUInt16(location!, entry);
                ushort last = ReadUInt16(location!, entry + 2);
                uint additionalOffset = ReadUInt32(location!, entry + 4);
                if (first > last || last >= glyphCount || first < firstGlyph || last > lastGlyph
                    || first <= previousLastGlyph)
                {
                    throw SfntFont.DataInvalid($"{locationTag}-subtableRange");
                }

                previousLastGlyph = last;

                uint subtableOffset = checked(arrayOffset + additionalOffset);
                EnsureOffsetRange(location!, subtableOffset, 8, $"{locationTag}-subtable");
                if (subtableOffset < checked(arrayOffset + (subtableCount * 8)) || subtableOffset >= arrayEnd)
                {
                    throw SfntFont.DataInvalid($"{locationTag}-subtableOffset");
                }


                ValidateBitmapIndexSubtable(
                    data!,
                    location!,
                    dataTag,
                    locationTag,
                    subtableOffset,
                    arrayEnd,
                    first,
                    last);
            }
        }

        return true;
    }

    private static void ValidateBitmapIndexSubtable(
        byte[] data,
        byte[] location,
        string dataTag,
        string locationTag,
        uint subtableOffset,
        uint subtableLimit,
        ushort firstGlyph,
        ushort lastGlyph)
    {
        int offset = checked((int)subtableOffset);
        ushort indexFormat = ReadUInt16(location, offset);
        ushort imageFormat = ReadUInt16(location, offset + 2);
        uint imageDataOffset = ReadUInt32(location, offset + 4);
        if (imageDataOffset < 4 || imageDataOffset > data.Length || !IsBitmapImageFormatSupported(dataTag, imageFormat))
        {
            throw SfntFont.DataInvalid($"{dataTag}-imageFormatOffset");
        }

        uint glyphsInRange = checked((uint)lastGlyph - firstGlyph + 1);
        switch (indexFormat)
        {
            case 1:
                ValidateBitmapOffsetArray(
                    data,
                    location,
                    dataTag,
                    locationTag,
                    subtableOffset,
                    subtableLimit,
                    imageDataOffset,
                    checked(glyphsInRange + 1),
                    elementSize: 4);
                break;
            case 2:
                EnsureSubtableRange(location, subtableOffset, 20, subtableLimit, $"{locationTag}-format2");
                ValidateBitmapDataRange(
                    data,
                    imageDataOffset,
                    checked(ReadUInt32(location, offset + 8) * glyphsInRange),
                    dataTag);
                break;
            case 3:
                ValidateBitmapOffsetArray(
                    data,
                    location,
                    dataTag,
                    locationTag,
                    subtableOffset,
                    subtableLimit,
                    imageDataOffset,
                    checked(glyphsInRange + 1),
                    elementSize: 2);
                break;
            case 4:
                ValidateSparseBitmapOffsets(
                    data,
                    location,
                    dataTag,
                    locationTag,
                    subtableOffset,
                    subtableLimit,
                    imageDataOffset,
                    firstGlyph,
                    lastGlyph);
                break;
            case 5:
                ValidateConstantSparseBitmaps(
                    data,
                    location,
                    dataTag,
                    locationTag,
                    subtableOffset,
                    subtableLimit,
                    imageDataOffset,
                    firstGlyph,
                    lastGlyph);
                break;
            default:
                throw SfntFont.DataInvalid($"{locationTag}-indexFormat");
        }
    }

    private static void ValidateBitmapOffsetArray(
        byte[] data,
        byte[] location,
        string dataTag,
        string locationTag,
        uint subtableOffset,
        uint subtableLimit,
        uint imageDataOffset,
        uint count,
        int elementSize)
    {
        uint length = checked(8 + (count * (uint)elementSize));
        EnsureSubtableRange(location, subtableOffset, length, subtableLimit, $"{locationTag}-offsetArray");
        int offsets = checked((int)subtableOffset + 8);
        uint previous = 0;
        for (int index = 0; index < count; index++)
        {
            uint current = elementSize == 4
                ? ReadUInt32(location, checked(offsets + (index * 4)))
                : ReadUInt16(location, checked(offsets + (index * 2)));
            if (current < previous)
            {
                throw SfntFont.DataInvalid($"{locationTag}-imageOffsets");
            }

            previous = current;
        }

        ValidateBitmapDataRange(data, imageDataOffset, previous, dataTag);
    }

    private static void ValidateSparseBitmapOffsets(
        byte[] data,
        byte[] location,
        string dataTag,
        string locationTag,
        uint subtableOffset,
        uint subtableLimit,
        uint imageDataOffset,
        ushort firstGlyph,
        ushort lastGlyph)
    {
        EnsureSubtableRange(location, subtableOffset, 12, subtableLimit, $"{locationTag}-format4");
        int offset = checked((int)subtableOffset);
        uint count = ReadUInt32(location, offset + 8);
        if (count == 0 || count > checked((uint)lastGlyph - firstGlyph + 1))
        {
            throw SfntFont.DataInvalid($"{locationTag}-format4Count");
        }

        EnsureSubtableRange(
            location,
            subtableOffset,
            checked(12 + ((count + 1) * 4)),
            subtableLimit,
            $"{locationTag}-format4Glyphs");
        int previousGlyph = firstGlyph - 1;
        uint previousOffset = 0;
        for (int index = 0; index <= count; index++)
        {
            int record = checked(offset + 12 + (index * 4));
            ushort glyph = ReadUInt16(location, record);
            uint currentOffset = ReadUInt16(location, record + 2);
            if (index < count && (glyph <= previousGlyph || glyph < firstGlyph || glyph > lastGlyph)
                || currentOffset < previousOffset)
            {
                throw SfntFont.DataInvalid($"{locationTag}-format4Order");
            }

            previousGlyph = glyph;
            previousOffset = currentOffset;
        }

        ValidateBitmapDataRange(data, imageDataOffset, previousOffset, dataTag);
    }

    private static void ValidateConstantSparseBitmaps(
        byte[] data,
        byte[] location,
        string dataTag,
        string locationTag,
        uint subtableOffset,
        uint subtableLimit,
        uint imageDataOffset,
        ushort firstGlyph,
        ushort lastGlyph)
    {
        EnsureSubtableRange(location, subtableOffset, 24, subtableLimit, $"{locationTag}-format5");
        int offset = checked((int)subtableOffset);
        uint imageSize = ReadUInt32(location, offset + 8);
        uint count = ReadUInt32(location, offset + 20);
        if (count == 0 || count > checked((uint)lastGlyph - firstGlyph + 1))
        {
            throw SfntFont.DataInvalid($"{locationTag}-format5Count");
        }

        EnsureSubtableRange(
            location,
            subtableOffset,
            checked(24 + (count * 2)),
            subtableLimit,
            $"{locationTag}-format5Glyphs");
        int previousGlyph = firstGlyph - 1;
        for (int index = 0; index < count; index++)
        {
            ushort glyph = ReadUInt16(location, checked(offset + 24 + (index * 2)));
            if (glyph <= previousGlyph || glyph < firstGlyph || glyph > lastGlyph)
            {
                throw SfntFont.DataInvalid($"{locationTag}-format5Order");
            }

            previousGlyph = glyph;
        }

        ValidateBitmapDataRange(data, imageDataOffset, checked(imageSize * count), dataTag);
    }

    private static void ValidateBitmapDataRange(byte[] data, uint offset, uint length, string dataTag)
    {
        EnsureOffsetRange(data, offset, length, $"{dataTag}-imageData");
    }

    private static void EnsureSubtableRange(
        byte[] table,
        uint offset,
        uint length,
        uint limit,
        string detail)
    {
        if (offset > limit || length > limit - offset)
        {
            throw SfntFont.DataInvalid(detail);
        }

        EnsureOffsetRange(table, offset, length, detail);
    }

    private static bool IsBitmapImageFormatSupported(string dataTag, ushort imageFormat)
        => imageFormat is >= 1 and <= 9 || dataTag == "CBDT" && imageFormat is >= 17 and <= 19;

    private static void ValidateSvg(byte[] table, ushort glyphCount)
    {
        SfntFont.EnsureRange(table, 0, 10, "SVG-header");
        if (ReadUInt16(table, 0) != 0)
        {
            throw SfntFont.DataInvalid("SVG-version");
        }

        uint indexOffset = ReadUInt32(table, 2);
        EnsureOffsetRange(table, indexOffset, 2, "SVG-index");
        int index = checked((int)indexOffset);
        ushort count = ReadUInt16(table, index);
        if (count == 0)
        {
            throw SfntFont.DataInvalid("SVG-entryCount");
        }

        EnsureOffsetRange(table, indexOffset + 2, checked((uint)count * 12), "SVG-entries");
        int previousEnd = -1;
        for (int entryIndex = 0; entryIndex < count; entryIndex++)
        {
            int entry = checked(index + 2 + (entryIndex * 12));
            ushort first = ReadUInt16(table, entry);
            ushort last = ReadUInt16(table, entry + 2);
            if (first > last || last >= glyphCount || first <= previousEnd)
            {
                throw SfntFont.DataInvalid("SVG-glyphRange");
            }

            previousEnd = last;

            uint documentOffset = ReadUInt32(table, entry + 4);
            uint documentLength = ReadUInt32(table, entry + 8);
            if (documentOffset == 0 || documentLength == 0)
            {
                throw SfntFont.DataInvalid("SVG-documentRange");
            }

            EnsureOffsetRange(table, checked(indexOffset + documentOffset), documentLength, "SVG-document");
            try
            {
                ValidateSvgDocument(table.AsSpan(
                    checked((int)(indexOffset + documentOffset)),
                    checked((int)documentLength)));
            }
            catch (Exception exception) when (exception is IOException or XmlException)
            {
                throw SfntFont.DataInvalid("SVG-document");
            }
        }
    }

    private static void ValidateSvgDocument(ReadOnlySpan<byte> encoded)
    {
        const int maximumDocumentBytes = 4 * 1024 * 1024;
        byte[] document;
        if (encoded.Length >= 3 && encoded[0] == 0x1F && encoded[1] == 0x8B && encoded[2] == 0x08)
        {
            using var input = new MemoryStream(encoded.ToArray(), writable: false);
            using var gzip = new GZipStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            var buffer = new byte[81920];
            int total = 0;
            int read;
            while ((read = gzip.Read(buffer, 0, Math.Min(buffer.Length, maximumDocumentBytes + 1 - total))) != 0)
            {
                total = checked(total + read);
                if (total > maximumDocumentBytes)
                {
                    throw SfntFont.DataInvalid("SVG-expandedSize");
                }

                output.Write(buffer, 0, read);
            }

            document = output.ToArray();
        }
        else
        {
            if (encoded.Length > maximumDocumentBytes)
            {
                throw SfntFont.DataInvalid("SVG-documentSize");
            }

            document = encoded.ToArray();
        }

        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = maximumDocumentBytes,
            IgnoreComments = true,
            IgnoreProcessingInstructions = false
        };
        using var stream = new MemoryStream(document, writable: false);
        using XmlReader reader = XmlReader.Create(stream, settings);
        bool sawRoot = false;
        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.ProcessingInstruction)
            {
                throw SfntFont.DataInvalid("SVG-processingInstruction");
            }

            if (reader.NodeType != XmlNodeType.Element)
            {
                continue;
            }

            if (!sawRoot)
            {
                if (reader.LocalName != "svg" || reader.NamespaceURI != "http://www.w3.org/2000/svg")
                {
                    throw SfntFont.DataInvalid("SVG-root");
                }

                sawRoot = true;
            }

            if (reader.NamespaceURI != "http://www.w3.org/2000/svg")
            {
                throw SfntFont.DataInvalid("SVG-elementNamespace");
            }

            if (reader.LocalName is "script" or "text" or "font" or "foreignObject" or "switch" or "a"
                or "view" or "image" or "style")
            {
                throw SfntFont.DataInvalid("SVG-restrictedElement");
            }

            if (!reader.HasAttributes)
            {
                continue;
            }

            while (reader.MoveToNextAttribute())
            {
                string value = reader.Value;
                bool isHref = reader.LocalName == "href";
                if (isHref && !global::OdfKit.Internal.OdfStringHelper.StartsWith(value, '#')
                    || value.Contains("javascript:", StringComparison.OrdinalIgnoreCase)
                    || value.Contains("@import", StringComparison.OrdinalIgnoreCase)
                    || value.Contains("url(", StringComparison.OrdinalIgnoreCase)
                       && !value.Contains("url(#", StringComparison.OrdinalIgnoreCase))
                {
                    throw SfntFont.DataInvalid("SVG-externalContent");
                }
            }

            reader.MoveToElement();
        }

        if (!sawRoot)
        {
            throw SfntFont.DataInvalid("SVG-root");
        }
    }

    private static void ValidateSbix(
        byte[] table,
        ushort glyphCount,
        Dictionary<ushort, HashSet<ushort>> references)
    {
        SfntFont.EnsureRange(table, 0, 8, "sbix-header");
        if (ReadUInt16(table, 0) != 1)
        {
            throw SfntFont.DataInvalid("sbix-version");
        }

        ushort flags = ReadUInt16(table, 2);
        if ((flags & 1) == 0 || (flags & 0xFFFC) != 0)
        {
            throw SfntFont.DataInvalid("sbix-flags");
        }

        uint strikeCount = ReadUInt32(table, 4);
        if (strikeCount == 0 || strikeCount > 4096)
        {
            throw SfntFont.DataInvalid("sbix-strikeCount");
        }

        EnsureOffsetRange(table, 8, checked(strikeCount * 4), "sbix-strikeOffsets");
        for (int index = 0; index < strikeCount; index++)
        {
            uint strikeOffset = ReadUInt32(table, checked(8 + (index * 4)));
            uint offsetCount = checked((uint)glyphCount + 1);
            uint strikeHeaderLength = checked(4 + (offsetCount * 4));
            EnsureOffsetRange(table, strikeOffset, strikeHeaderLength, "sbix-strike");
            uint previous = strikeHeaderLength;
            for (int glyph = 0; glyph <= glyphCount; glyph++)
            {
                uint current = ReadUInt32(table, checked((int)strikeOffset + 4 + (glyph * 4)));
                if (current < previous || strikeOffset + current > table.Length)
                {
                    throw SfntFont.DataInvalid("sbix-glyphOffset");
                }

                previous = current;
            }

            for (ushort glyph = 0; glyph < glyphCount; glyph++)
            {
                uint start = ReadUInt32(table, checked((int)strikeOffset + 4 + (glyph * 4)));
                uint end = ReadUInt32(table, checked((int)strikeOffset + 8 + (glyph * 4)));
                uint length = end - start;
                if (length == 0)
                {
                    continue;
                }

                if (length < 8)
                {
                    throw SfntFont.DataInvalid("sbix-glyphData");
                }

                int dataOffset = checked((int)(strikeOffset + start));
                string graphicType = Encoding.ASCII.GetString(table, dataOffset + 4, 4);
                if (graphicType == "dupe")
                {
                    if (length != 10)
                    {
                        throw SfntFont.DataInvalid("sbix-dupeLength");
                    }

                    ushort referencedGlyph = ReadUInt16(table, dataOffset + 8);
                    ValidateGlyph(referencedGlyph, glyphCount, "sbix-dupeGlyph");
                    if (referencedGlyph == glyph)
                    {
                        throw SfntFont.DataInvalid("sbix-dupeCycle");
                    }

                    AddReferences(references, glyph, [referencedGlyph]);
                }
                else if (graphicType is not ("jpg " or "png " or "tiff"))
                {
                    throw SfntFont.DataInvalid("sbix-graphicType");
                }
            }
        }

        ValidateReferenceCycles(references, "sbix-dupeCycle");
    }

    private static void ValidateVersionAndCount(
        byte[] table,
        string tag,
        uint version,
        int headerLength,
        int recordLength)
    {
        SfntFont.EnsureRange(table, 0, headerLength, $"{tag}-header");
        if (ReadUInt32(table, 0) != version)
        {
            throw SfntFont.DataInvalid($"{tag}-version");
        }

        uint count = ReadUInt32(table, 4);
        EnsureOffsetRange(table, (uint)headerLength, checked(count * (uint)recordLength), $"{tag}-records");
    }

    private static void ValidateColrV1ListOffset(byte[] table, uint offset, int minimum, string detail)
    {
        if (offset != 0)
        {
            EnsureOffsetRange(table, offset, (uint)minimum, detail);
        }
    }

    private static void ValidateOptionalArray(
        byte[] table,
        uint offset,
        int count,
        int elementSize,
        string detail)
    {
        if (offset != 0)
        {
            EnsureOffsetRange(table, offset, checked((uint)(count * elementSize)), detail);
        }
    }

    private static void ValidateOptionalOffset(byte[] table, uint offset, string detail)
    {
        if (offset != 0)
        {
            EnsureOffsetRange(table, offset, 1, detail);
        }
    }

    private static void ValidateRelativeOffset(byte[] table, uint origin, uint offset, string detail)
    {
        if (origin > int.MaxValue || offset == 0 || offset > int.MaxValue
            || origin >= table.Length || offset >= table.Length - origin)
        {
            throw SfntFont.DataInvalid(detail);
        }
    }

    private static void ValidatePaletteIndex(ushort paletteIndex, ushort paletteEntryCount, string detail)
    {
        if (paletteIndex != ushort.MaxValue && paletteIndex >= paletteEntryCount)
        {
            throw SfntFont.DataInvalid(detail);
        }
    }

    private static void AddReferences(
        Dictionary<ushort, HashSet<ushort>> references,
        ushort glyph,
        IEnumerable<ushort> added)
    {
        if (!references.TryGetValue(glyph, out HashSet<ushort>? current))
        {
            current = [];
            references.Add(glyph, current);
        }

        current.UnionWith(added);
    }

    private static void ValidateReferenceCycles(
        IReadOnlyDictionary<ushort, HashSet<ushort>> references,
        string detail)
    {
        var states = new Dictionary<ushort, byte>();
        foreach (ushort glyph in references.Keys)
        {
            Visit(glyph);
        }

        void Visit(ushort glyph)
        {
            if (states.TryGetValue(glyph, out byte state))
            {
                if (state == 1)
                {
                    throw SfntFont.DataInvalid(detail);
                }

                return;
            }

            states[glyph] = 1;
            if (references.TryGetValue(glyph, out HashSet<ushort>? targets))
            {
                foreach (ushort target in targets)
                {
                    Visit(target);
                }
            }

            states[glyph] = 2;
        }
    }

    private static void ValidateGlyph(ushort glyph, ushort glyphCount, string detail)
    {
        if (glyph >= glyphCount)
        {
            throw SfntFont.DataInvalid(detail);
        }
    }

    private static void EnsureOffsetRange(byte[] table, uint offset, uint length, string detail)
    {
        if (offset > int.MaxValue || length > int.MaxValue)
        {
            throw SfntFont.DataInvalid(detail);
        }

        SfntFont.EnsureRange(table, (int)offset, (int)length, detail);
    }

    private static ushort ReadUInt16(byte[] table, int offset)
        => BinaryPrimitives.ReadUInt16BigEndian(table.AsSpan(offset, 2));

    private static uint ReadUInt32(byte[] table, int offset)
        => BinaryPrimitives.ReadUInt32BigEndian(table.AsSpan(offset, 4));

    private static uint ReadUInt24(byte[] table, int offset)
    {
        SfntFont.EnsureRange(table, offset, 3, "COLR-offset24");
        return (uint)(table[offset] << 16 | table[offset + 1] << 8 | table[offset + 2]);
    }
}
