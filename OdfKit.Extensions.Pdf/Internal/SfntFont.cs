using System;
using System.Collections.Generic;
using System.Text;

namespace OdfKit.Export.PdfInternal;

/// <summary>
/// 讀取 TrueType／OpenType（含 TrueType Collection 的指定字型面）的度量、字元對應與字形資料，
/// 並能為 TrueType 輪廓字型產生保留字形編號的子集。
/// Reads metrics, character mapping and glyph data of TrueType/OpenType fonts (including a face
/// of a TrueType Collection) and builds glyph-id-preserving subsets of TrueType-outline fonts.
/// </summary>
internal sealed class SfntFont
{
    private readonly byte[] _data;
    private readonly Dictionary<string, (int Offset, int Length)> _tables;
    private readonly int _sfntVersion;
    private readonly bool _isCollection;
    private readonly List<CmapSubtable> _cmaps = [];
    private readonly Dictionary<int, int> _glyphCache = [];
    private readonly int _hmtxOffset;
    private readonly int _locaOffset;
    private readonly int _locaLength;
    private readonly int _glyfOffset;
    private readonly int _glyfLength;

    private SfntFont(byte[] data, Dictionary<string, (int, int)> tables, int sfntVersion, bool isCollection)
    {
        _data = data;
        _tables = tables;
        _sfntVersion = sfntVersion;
        _isCollection = isCollection;
        (int headOffset, int headLength) = tables["head"];
        if (headLength < 54)
        {
            throw new FormatException("head table is truncated.");
        }

        UnitsPerEm = U16(headOffset + 18);
        if (UnitsPerEm < 16 || UnitsPerEm > 16384)
        {
            throw new FormatException("Invalid unitsPerEm.");
        }

        XMin = S16(headOffset + 36);
        YMin = S16(headOffset + 38);
        XMax = S16(headOffset + 40);
        YMax = S16(headOffset + 42);
        IndexToLocFormat = S16(headOffset + 50);

        (int maxpOffset, int maxpLength) = tables["maxp"];
        if (maxpLength < 6)
        {
            throw new FormatException("maxp table is truncated.");
        }

        NumGlyphs = U16(maxpOffset + 4);

        (int hheaOffset, int hheaLength) = tables["hhea"];
        if (hheaLength < 36)
        {
            throw new FormatException("hhea table is truncated.");
        }

        Ascent = S16(hheaOffset + 4);
        Descent = S16(hheaOffset + 6);
        LineGap = S16(hheaOffset + 8);
        NumberOfHMetrics = Math.Max(1, U16(hheaOffset + 34));

        (_hmtxOffset, int hmtxLength) = tables["hmtx"];
        if (hmtxLength < NumberOfHMetrics * 4)
        {
            throw new FormatException("hmtx table is truncated.");
        }

        if (tables.TryGetValue("loca", out (int Offset, int Length) loca) && tables.TryGetValue("glyf", out (int Offset, int Length) glyf))
        {
            _locaOffset = loca.Offset;
            _locaLength = loca.Length;
            _glyfOffset = glyf.Offset;
            _glyfLength = glyf.Length;
        }

        if (tables.TryGetValue("post", out (int Offset, int Length) post) && post.Length >= 8)
        {
            // Fixed 16.16：整數部分有號，小數部分為無號。
            ItalicAngle = S16(post.Offset + 4) + (U16(post.Offset + 6) / 65536.0);
        }

        if (tables.TryGetValue("OS/2", out (int Offset, int Length) os2) && os2.Length >= 90)
        {
            int version = U16(os2.Offset);
            CapHeight = version >= 2 && os2.Length >= 90 ? S16(os2.Offset + 88) : 0;
            WeightClass = U16(os2.Offset + 4);
        }

        if (CapHeight <= 0)
        {
            CapHeight = (int)(UnitsPerEm * 0.7);
        }

        PostScriptName = ReadPostScriptName();
        ParseCmaps();
    }

    internal int UnitsPerEm { get; }

    internal int NumGlyphs { get; }

    internal int NumberOfHMetrics { get; }

    internal int IndexToLocFormat { get; }

    internal int Ascent { get; }

    internal int Descent { get; }

    internal int LineGap { get; }

    internal int XMin { get; }

    internal int YMin { get; }

    internal int XMax { get; }

    internal int YMax { get; }

    internal int CapHeight { get; }

    internal int WeightClass { get; }

    internal double ItalicAngle { get; }

    internal string PostScriptName { get; }

    /// <summary>
    /// 字型是否為 CFF 輪廓（由 <see cref="CffFont"/> 子集化，無法解析時整份內嵌）。
    /// </summary>
    internal bool IsCff => _sfntVersion == 0x4F54544F;

    internal bool CanSubset => !IsCff && _glyfLength > 0 && _locaLength > 0;

    /// <summary>
    /// 載入字型；格式不支援或損毀時回傳 <see langword="null"/>。
    /// </summary>
    internal static SfntFont? TryLoad(byte[] data, int faceIndex = 0)
    {
        try
        {
            if (data.Length < 12)
            {
                return null;
            }

            int directoryOffset = 0;
            bool collection = false;
            if (data[0] == 't' && data[1] == 't' && data[2] == 'c' && data[3] == 'f')
            {
                collection = true;
                int count = (int)ReadU32(data, 8);
                if (faceIndex < 0 || faceIndex >= count || 12 + (4 * (faceIndex + 1)) > data.Length)
                {
                    return null;
                }

                directoryOffset = checked((int)ReadU32(data, 12 + (4 * faceIndex)));
            }

            if (directoryOffset < 0 || directoryOffset + 12 > data.Length)
            {
                return null;
            }

            int version = (int)ReadU32(data, directoryOffset);
            if (version != 0x00010000 && version != 0x74727565 && version != 0x4F54544F)
            {
                return null;
            }

            int tableCount = (data[directoryOffset + 4] << 8) | data[directoryOffset + 5];
            var tables = new Dictionary<string, (int, int)>(StringComparer.Ordinal);
            for (int i = 0; i < tableCount; i++)
            {
                int record = directoryOffset + 12 + (16 * i);
                if (record + 16 > data.Length)
                {
                    return null;
                }

                string tag = Encoding.ASCII.GetString(data, record, 4);
                long offset = ReadU32(data, record + 8);
                long length = ReadU32(data, record + 12);
                if (offset + length > data.Length)
                {
                    return null;
                }

                tables[tag] = ((int)offset, (int)length);
            }

            foreach (string required in new[] { "head", "maxp", "hhea", "hmtx" })
            {
                if (!tables.ContainsKey(required))
                {
                    return null;
                }
            }

            return new SfntFont(data, tables, version, collection);
        }
        catch (Exception ex) when (ex is FormatException or OverflowException or ArgumentException or KeyNotFoundException)
        {
            return null;
        }
    }

    /// <summary>
    /// 取得碼位對應的字形編號；找不到時回傳 0（.notdef）。
    /// </summary>
    internal int GetGlyph(int codePoint)
    {
        if (_glyphCache.TryGetValue(codePoint, out int cached))
        {
            return cached;
        }

        int glyph = 0;
        foreach (CmapSubtable subtable in _cmaps)
        {
            glyph = subtable.Lookup(this, codePoint);
            if (glyph != 0)
            {
                break;
            }
        }

        if (glyph >= NumGlyphs)
        {
            glyph = 0;
        }

        _glyphCache[codePoint] = glyph;
        return glyph;
    }

    internal bool HasGlyph(int codePoint) => GetGlyph(codePoint) != 0;

    /// <summary>
    /// 字形前進寬度（字型單位）。
    /// </summary>
    internal int GetAdvance(int glyph)
    {
        int index = glyph < NumberOfHMetrics ? Math.Max(0, glyph) : NumberOfHMetrics - 1;
        return U16(_hmtxOffset + (4 * index));
    }

    /// <summary>
    /// 為 TrueType 輪廓字型產生子集：保留原字形編號，只保留指定字形（含複合字形的元件），
    /// 其餘字形資料清空，並截掉最大使用編號之後的字形，使輸出遠小於原字型。
    /// </summary>
    internal byte[] BuildSubset(ICollection<int> usedGlyphs)
    {
        if (!CanSubset)
        {
            throw new InvalidOperationException("The font cannot be subset.");
        }

        var keep = new SortedSet<int> { 0 };
        var pending = new Stack<int>();
        foreach (int glyph in usedGlyphs)
        {
            if (glyph >= 0 && glyph < NumGlyphs && keep.Add(glyph))
            {
                pending.Push(glyph);
            }
        }

        pending.Push(0);
        while (pending.Count > 0)
        {
            int glyph = pending.Pop();
            foreach (int component in EnumerateComponents(glyph))
            {
                if (component >= 0 && component < NumGlyphs && keep.Add(component))
                {
                    pending.Push(component);
                }
            }
        }

        int newGlyphCount = keep.Max + 1;
        var glyf = new List<byte>();
        var loca = new byte[(newGlyphCount + 1) * 4];
        for (int glyph = 0; glyph < newGlyphCount; glyph++)
        {
            WriteU32(loca, glyph * 4, (uint)glyf.Count);
            if (keep.Contains(glyph))
            {
                (int start, int end) = GetGlyphRange(glyph);
                for (int i = start; i < end; i++)
                {
                    glyf.Add(_data[_glyfOffset + i]);
                }

                while ((glyf.Count & 3) != 0)
                {
                    glyf.Add(0);
                }
            }
        }

        WriteU32(loca, newGlyphCount * 4, (uint)glyf.Count);

        int hMetricCount = Math.Min(NumberOfHMetrics, newGlyphCount);
        int sideBearingCount = Math.Max(0, newGlyphCount - hMetricCount);
        var hmtx = new byte[(hMetricCount * 4) + (sideBearingCount * 2)];
        for (int i = 0; i < hMetricCount; i++)
        {
            Buffer.BlockCopy(_data, _hmtxOffset + (4 * i), hmtx, 4 * i, 4);
        }

        int sideBearingSource = _hmtxOffset + (NumberOfHMetrics * 4);
        for (int i = 0; i < sideBearingCount; i++)
        {
            int glyph = hMetricCount + i;
            int source = sideBearingSource + (2 * (glyph - NumberOfHMetrics));
            if (glyph >= NumberOfHMetrics && source + 2 <= _data.Length)
            {
                hmtx[(hMetricCount * 4) + (2 * i)] = _data[source];
                hmtx[(hMetricCount * 4) + (2 * i) + 1] = _data[source + 1];
            }
        }

        var tables = new List<(string Tag, byte[] Data)>
        {
            ("head", CopyTable("head", head => { head[8] = head[9] = head[10] = head[11] = 0; head[50] = 0; head[51] = 1; })),
            ("hhea", CopyTable("hhea", hhea => { hhea[34] = (byte)(hMetricCount >> 8); hhea[35] = (byte)hMetricCount; })),
            ("maxp", CopyTable("maxp", maxp => { maxp[4] = (byte)(newGlyphCount >> 8); maxp[5] = (byte)newGlyphCount; })),
            ("hmtx", hmtx),
            ("loca", loca),
            ("glyf", glyf.ToArray()),
        };
        foreach (string optional in new[] { "cvt ", "fpgm", "prep" })
        {
            if (_tables.TryGetValue(optional, out (int Offset, int Length) table))
            {
                tables.Add((optional, Slice(table.Offset, table.Length)));
            }
        }

        return AssembleSfnt(0x00010000, tables);
    }

    /// <summary>
    /// 回傳可整份內嵌的字型位元組：單一字型面的檔案原樣回傳，TrueType Collection 則重組為獨立字型。
    /// </summary>
    internal byte[] BuildStandalone()
    {
        if (!_isCollection)
        {
            return _data;
        }

        var tables = new List<(string Tag, byte[] Data)>();
        foreach (KeyValuePair<string, (int Offset, int Length)> table in _tables)
        {
            tables.Add((table.Key, Slice(table.Value.Offset, table.Value.Length)));
        }

        return AssembleSfnt(_sfntVersion, tables);
    }

    /// <summary>
    /// 取得 OpenType 的 <c>CFF </c> 資料表原始位元組；沒有時回傳 <see langword="null"/>。
    /// </summary>
    internal byte[]? GetCffTable() =>
        _tables.TryGetValue("CFF ", out (int Offset, int Length) table) ? Slice(table.Offset, table.Length) : null;

    private byte[] CopyTable(string tag, Action<byte[]> patch)
    {
        (int offset, int length) = _tables[tag];
        byte[] copy = Slice(offset, length);
        patch(copy);
        return copy;
    }

    private byte[] Slice(int offset, int length)
    {
        var copy = new byte[length];
        Buffer.BlockCopy(_data, offset, copy, 0, length);
        return copy;
    }

    private (int Start, int End) GetGlyphRange(int glyph)
    {
        int entrySize = IndexToLocFormat == 0 ? 2 : 4;
        if (glyph < 0 || (glyph + 2) * entrySize > _locaLength)
        {
            return (0, 0);
        }

        long start;
        long end;
        if (IndexToLocFormat == 0)
        {
            start = (long)U16(_locaOffset + (2 * glyph)) * 2;
            end = (long)U16(_locaOffset + (2 * glyph) + 2) * 2;
        }
        else
        {
            start = ReadU32(_data, _locaOffset + (4 * glyph));
            end = ReadU32(_data, _locaOffset + (4 * glyph) + 4);
        }

        if (start < 0 || end < start || end > _glyfLength)
        {
            return (0, 0);
        }

        return ((int)start, (int)end);
    }

    private IEnumerable<int> EnumerateComponents(int glyph)
    {
        if (glyph < 0 || glyph >= NumGlyphs)
        {
            yield break;
        }

        (int start, int end) = GetGlyphRange(glyph);
        if (end - start < 10)
        {
            yield break;
        }

        int position = _glyfOffset + start;
        if (S16(position) >= 0)
        {
            yield break;
        }

        position += 10;
        int limit = _glyfOffset + end;
        while (position + 4 <= limit)
        {
            int flags = U16(position);
            int component = U16(position + 2);
            yield return component;
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

            if ((flags & 0x0020) == 0)
            {
                break;
            }
        }
    }

    private static byte[] AssembleSfnt(int version, List<(string Tag, byte[] Data)> tables)
    {
        tables.Sort((a, b) => string.CompareOrdinal(a.Tag, b.Tag));
        int count = tables.Count;
        int headerSize = 12 + (16 * count);
        long total = headerSize;
        foreach ((string _, byte[] data) in tables)
        {
            total += (data.Length + 3L) & ~3L;
        }

        var output = new byte[total];
        WriteU32(output, 0, (uint)version);
        int entrySelector = 0;
        while ((1 << (entrySelector + 1)) <= count)
        {
            entrySelector++;
        }

        int searchRange = (1 << entrySelector) * 16;
        output[4] = (byte)(count >> 8);
        output[5] = (byte)count;
        output[6] = (byte)(searchRange >> 8);
        output[7] = (byte)searchRange;
        output[8] = (byte)(entrySelector >> 8);
        output[9] = (byte)entrySelector;
        int rangeShift = (count * 16) - searchRange;
        output[10] = (byte)(rangeShift >> 8);
        output[11] = (byte)rangeShift;

        int position = headerSize;
        int headOffset = -1;
        for (int i = 0; i < count; i++)
        {
            (string tag, byte[] data) = tables[i];
            int record = 12 + (16 * i);
            for (int c = 0; c < 4; c++)
            {
                output[record + c] = (byte)tag[c];
            }

            WriteU32(output, record + 4, Checksum(data));
            WriteU32(output, record + 8, (uint)position);
            WriteU32(output, record + 12, (uint)data.Length);
            Buffer.BlockCopy(data, 0, output, position, data.Length);
            if (tag == "head")
            {
                headOffset = position;
            }

            position += (data.Length + 3) & ~3;
        }

        if (headOffset >= 0)
        {
            WriteU32(output, headOffset + 8, 0);
            uint adjustment = unchecked(0xB1B0AFBAu - Checksum(output));
            WriteU32(output, headOffset + 8, adjustment);
        }

        return output;
    }

    private static uint Checksum(byte[] data)
    {
        uint sum = 0;
        for (int i = 0; i < data.Length; i += 4)
        {
            uint word = 0;
            for (int j = 0; j < 4; j++)
            {
                word <<= 8;
                if (i + j < data.Length)
                {
                    word |= data[i + j];
                }
            }

            sum = unchecked(sum + word);
        }

        return sum;
    }

    private string ReadPostScriptName()
    {
        if (!_tables.TryGetValue("name", out (int Offset, int Length) name) || name.Length < 6)
        {
            return "Font";
        }

        try
        {
            int count = U16(name.Offset + 2);
            int stringOffset = name.Offset + U16(name.Offset + 4);
            for (int i = 0; i < count; i++)
            {
                int record = name.Offset + 6 + (12 * i);
                if (record + 12 > name.Offset + name.Length)
                {
                    break;
                }

                int platform = U16(record);
                int nameId = U16(record + 6);
                int length = U16(record + 8);
                int offset = stringOffset + U16(record + 10);
                if (nameId != 6 || offset + length > _data.Length)
                {
                    continue;
                }

                string text = platform == 3 || platform == 0
                    ? Encoding.BigEndianUnicode.GetString(_data, offset, length)
                    : Encoding.ASCII.GetString(_data, offset, length);
                var clean = new StringBuilder();
                foreach (char c in text)
                {
                    if (c > 0x20 && c < 0x7F && "[](){}<>/%".IndexOf(c) < 0)
                    {
                        clean.Append(c);
                    }
                }

                if (clean.Length > 0)
                {
                    return clean.ToString();
                }
            }
        }
        catch (ArgumentException)
        {
            // 名稱表損毀時使用預設名稱。
        }

        return "Font";
    }

    private void ParseCmaps()
    {
        if (!_tables.TryGetValue("cmap", out (int Offset, int Length) cmap) || cmap.Length < 4)
        {
            return;
        }

        int count = U16(cmap.Offset + 2);
        var candidates = new List<(int Priority, CmapSubtable Table)>();
        for (int i = 0; i < count; i++)
        {
            int record = cmap.Offset + 4 + (8 * i);
            if (record + 8 > cmap.Offset + cmap.Length)
            {
                break;
            }

            int platform = U16(record);
            int encoding = U16(record + 2);
            long offset = ReadU32(_data, record + 4);
            if (offset + 4 > cmap.Length)
            {
                continue;
            }

            int position = cmap.Offset + (int)offset;
            int format = U16(position);
            int priority = (platform, encoding) switch
            {
                (3, 10) => 0,
                (0, 4) or (0, 6) => 1,
                (3, 1) => 2,
                (0, _) => 3,
                _ => 9,
            };
            if (priority == 9 || (format != 4 && format != 12 && format != 0 && format != 6))
            {
                continue;
            }

            candidates.Add((priority * 16 + (format == 12 ? 0 : 1), new CmapSubtable(position, format)));
        }

        candidates.Sort((a, b) => a.Priority.CompareTo(b.Priority));
        foreach ((int _, CmapSubtable table) in candidates)
        {
            _cmaps.Add(table);
        }
    }

    private sealed class CmapSubtable(int offset, int format)
    {
        internal int Lookup(SfntFont font, int codePoint)
        {
            byte[] d = font._data;
            switch (format)
            {
                case 0:
                    return codePoint is >= 0 and < 256 && offset + 6 + codePoint < d.Length ? d[offset + 6 + codePoint] : 0;
                case 6:
                    {
                        int first = font.U16(offset + 6);
                        int count = font.U16(offset + 8);
                        int index = codePoint - first;
                        return index >= 0 && index < count ? font.U16(offset + 10 + (2 * index)) : 0;
                    }

                case 4:
                    return codePoint > 0xFFFF ? 0 : LookupFormat4(font, codePoint);
                case 12:
                    return LookupFormat12(font, codePoint);
                default:
                    return 0;
            }
        }

        private int LookupFormat4(SfntFont font, int codePoint)
        {
            int segCount = font.U16(offset + 6) / 2;
            int endCodes = offset + 14;
            int startCodes = endCodes + (2 * segCount) + 2;
            int idDeltas = startCodes + (2 * segCount);
            int idRangeOffsets = idDeltas + (2 * segCount);
            int low = 0;
            int high = segCount - 1;
            while (low <= high)
            {
                int mid = (low + high) / 2;
                int end = font.U16(endCodes + (2 * mid));
                if (codePoint > end)
                {
                    low = mid + 1;
                    continue;
                }

                int start = font.U16(startCodes + (2 * mid));
                if (codePoint < start)
                {
                    high = mid - 1;
                    continue;
                }

                int delta = font.U16(idDeltas + (2 * mid));
                int rangeOffset = font.U16(idRangeOffsets + (2 * mid));
                if (rangeOffset == 0)
                {
                    return (codePoint + delta) & 0xFFFF;
                }

                int glyphAddress = idRangeOffsets + (2 * mid) + rangeOffset + (2 * (codePoint - start));
                if (glyphAddress + 2 > font._data.Length)
                {
                    return 0;
                }

                int glyph = font.U16(glyphAddress);
                return glyph == 0 ? 0 : (glyph + delta) & 0xFFFF;
            }

            return 0;
        }

        private int LookupFormat12(SfntFont font, int codePoint)
        {
            long groups = ReadU32(font._data, offset + 12);
            long low = 0;
            long high = groups - 1;
            while (low <= high)
            {
                long mid = (low + high) / 2;
                int group = offset + 16 + (int)(12 * mid);
                if (group + 12 > font._data.Length)
                {
                    return 0;
                }

                long start = ReadU32(font._data, group);
                long end = ReadU32(font._data, group + 4);
                if (codePoint < start)
                {
                    high = mid - 1;
                }
                else if (codePoint > end)
                {
                    low = mid + 1;
                }
                else
                {
                    return (int)(ReadU32(font._data, group + 8) + (codePoint - start));
                }
            }

            return 0;
        }
    }

    private int U16(int offset) => (_data[offset] << 8) | _data[offset + 1];

    private int S16(int offset) => (short)U16(offset);

    private static uint ReadU32(byte[] data, int offset) =>
        (uint)((data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3]);

    private static void WriteU32(byte[] data, int offset, uint value)
    {
        data[offset] = (byte)(value >> 24);
        data[offset + 1] = (byte)(value >> 16);
        data[offset + 2] = (byte)(value >> 8);
        data[offset + 3] = (byte)value;
    }
}
