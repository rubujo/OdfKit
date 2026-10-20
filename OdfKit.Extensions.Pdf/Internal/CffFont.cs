using System;
using System.Collections.Generic;
using System.IO;

namespace OdfKit.Export.PdfInternal;

/// <summary>
/// 讀取 CFF 1.0 字型程式（OpenType 的 <c>CFF </c> 資料表）：取得字形對應的 CID，並能保留字形編號地
/// 替換未使用字形的 Charstring 以縮小檔案。實作僅依 Adobe Technical Note #5176（CFF 格式）與
/// ISO 32000 的字型嵌入規則，不參考任何第三方實作程式碼。
/// Reads a CFF 1.0 font program (the OpenType <c>CFF </c> table): resolves glyph-to-CID mapping and can shrink
/// the font by replacing unused charstrings while keeping glyph ids. Based only on Adobe Technical Note #5176
/// and the ISO 32000 font embedding rules.
/// </summary>
internal sealed class CffFont
{
    private const int MaxGlyphs = 65535;
    private const byte EndCharOperator = 14;

    // Top DICT 與 Font DICT 中的運算子（TN #5176 附錄 H）。
    private const int OpCharset = 15;
    private const int OpCharStrings = 17;
    private const int OpPrivate = 18;
    private const int OpSubrs = 19;
    private const int OpRos = 1230;
    private const int OpFdArray = 1236;
    private const int OpFdSelect = 1237;

    private readonly byte[] _data;
    private readonly int _headerSize;
    private readonly (int Start, int End) _nameIndex;
    private readonly List<(int Op, byte[] Operands)> _topDict;
    private readonly (int Start, int End) _stringIndex;
    private readonly (int Start, int End) _globalSubrIndex;
    private readonly (int Start, int End) _charStrings;
    private readonly List<(int Start, int End)> _charStringRanges;
    private readonly int _charsetOffset;
    private readonly int _charsetLength;
    private readonly int _fdSelectOffset;
    private readonly int _fdSelectLength;
    private readonly int _fdArrayOffset;
    private readonly int[] _glyphToCid;

    private CffFont(byte[] data, int headerSize, (int, int) nameIndex, List<(int, byte[])> topDict, (int, int) stringIndex, (int, int) globalSubrs,
        (int, int) charStrings, List<(int, int)> charStringRanges, int charsetOffset, int charsetLength, int fdSelectOffset, int fdSelectLength,
        int fdArrayOffset, bool isCidKeyed, int[] glyphToCid)
    {
        _data = data;
        _headerSize = headerSize;
        _nameIndex = nameIndex;
        _topDict = topDict;
        _stringIndex = stringIndex;
        _globalSubrIndex = globalSubrs;
        _charStrings = charStrings;
        _charStringRanges = charStringRanges;
        _charsetOffset = charsetOffset;
        _charsetLength = charsetLength;
        _fdSelectOffset = fdSelectOffset;
        _fdSelectLength = fdSelectLength;
        _fdArrayOffset = fdArrayOffset;
        IsCidKeyed = isCidKeyed;
        _glyphToCid = glyphToCid;
        GlyphCount = charStringRanges.Count;
    }

    internal bool IsCidKeyed { get; }

    internal int GlyphCount { get; }

    /// <summary>
    /// 字形編號對應的 CID；名稱式 CFF 的 CID 等於字形編號。
    /// </summary>
    internal int CidOf(int glyph) => glyph >= 0 && glyph < _glyphToCid.Length ? _glyphToCid[glyph] : glyph;

    /// <summary>
    /// 解析 CFF 資料；格式不支援、損毀或超過上限時回傳 <see langword="null"/>。
    /// </summary>
    internal static CffFont? TryParse(byte[] data)
    {
        try
        {
            return Parse(data);
        }
        catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentException or OverflowException or InvalidDataException)
        {
            return null;
        }
    }

    private static CffFont? Parse(byte[] data)
    {
        if (data.Length < 4 || data[0] != 1)
        {
            return null;
        }

        int headerSize = data[2];
        int position = headerSize;
        (int, int) nameIndex = ReadIndexExtent(data, position);
        position = nameIndex.Item2;
        (int, int) topIndex = ReadIndexExtent(data, position);
        position = topIndex.Item2;
        (int, int) stringIndex = ReadIndexExtent(data, position);
        position = stringIndex.Item2;
        (int, int) globalSubrs = ReadIndexExtent(data, position);

        List<(int Start, int End)> topEntries = ReadIndexEntries(data, topIndex.Item1);
        if (topEntries.Count != 1)
        {
            return null;
        }

        List<(int Op, byte[] Operands)> topDict = ParseDict(data, topEntries[0].Start, topEntries[0].End);
        int charStringsOffset = OffsetOperand(topDict, OpCharStrings);
        if (charStringsOffset <= 0)
        {
            return null;
        }

        (int, int) charStrings = ReadIndexExtent(data, charStringsOffset);
        List<(int Start, int End)> ranges = ReadIndexEntries(data, charStringsOffset);
        int glyphCount = ranges.Count;
        if (glyphCount == 0 || glyphCount > MaxGlyphs)
        {
            return null;
        }

        bool cidKeyed = topDict.Exists(entry => entry.Op == OpRos);
        int charsetOffset = Math.Max(0, OffsetOperand(topDict, OpCharset));
        int charsetLength = 0;
        var glyphToCid = new int[glyphCount];
        for (int i = 0; i < glyphCount; i++)
        {
            glyphToCid[i] = i;
        }

        if (cidKeyed)
        {
            if (charsetOffset < 3)
            {
                return null;
            }

            charsetLength = ParseCharset(data, charsetOffset, glyphCount, glyphToCid);
        }

        int fdSelectOffset = 0;
        int fdSelectLength = 0;
        int fdArrayOffset = 0;
        if (cidKeyed)
        {
            fdSelectOffset = OffsetOperand(topDict, OpFdSelect);
            fdArrayOffset = OffsetOperand(topDict, OpFdArray);
            if (fdSelectOffset <= 0 || fdArrayOffset <= 0)
            {
                return null;
            }

            fdSelectLength = FdSelectLength(data, fdSelectOffset, glyphCount);
        }

        return new CffFont(
            data, headerSize, nameIndex, topDict, stringIndex, globalSubrs, charStrings, ranges, charsetOffset, charsetLength,
            fdSelectOffset, fdSelectLength, fdArrayOffset, cidKeyed, glyphToCid);
    }

    /// <summary>
    /// 產生保留字形編號的子集：未使用字形的 Charstring 換成只有 <c>endchar</c> 的一位元組程式，其餘資料原樣保留。含 <c>seac</c> 的字形無法只靠保留集合還原，此時回傳 <see langword="null"/>。
    /// </summary>
    internal byte[]? BuildSubset(ICollection<int> usedGlyphs)
    {
        var keep = new HashSet<int>(usedGlyphs) { 0 };
        var charStringItems = new List<byte[]>(GlyphCount);
        for (int glyph = 0; glyph < GlyphCount; glyph++)
        {
            if (keep.Contains(glyph))
            {
                (int start, int end) = _charStringRanges[glyph];
                var copy = new byte[end - start];
                Buffer.BlockCopy(_data, start, copy, 0, copy.Length);
                charStringItems.Add(copy);
            }
            else
            {
                charStringItems.Add([EndCharOperator]);
            }
        }

        byte[] charStringsIndex = BuildIndex(charStringItems);

        // 固定版面：header、Name、Top DICT、String、Global Subrs、charset、FDSelect、CharStrings、(FDArray)、(Private+Subrs 區塊)。
        byte[] header = Slice(0, _headerSize);
        byte[] nameIndex = Slice(_nameIndex.Start, _nameIndex.End);
        byte[] stringIndex = Slice(_stringIndex.Start, _stringIndex.End);
        byte[] charset = _charsetLength > 0 ? Slice(_charsetOffset, _charsetOffset + _charsetLength) : [];
        byte[] fdSelect = _fdSelectLength > 0 ? Slice(_fdSelectOffset, _fdSelectOffset + _fdSelectLength) : [];

        var infos = new List<PrivateInfo>();
        var fontDicts = new List<List<(int Op, byte[] Operands)>>();
        if (IsCidKeyed)
        {
            foreach ((int start, int end) in ReadIndexEntries(_data, _fdArrayOffset))
            {
                List<(int Op, byte[] Operands)> fontDict = ParseDict(_data, start, end);
                fontDicts.Add(fontDict);
                infos.Add(LoadPrivate(fontDict));
            }
        }
        else
        {
            infos.Add(LoadPrivate(_topDict));
        }

        // 只保留被保留字形（直接或遞迴）呼叫的子程式，其餘換成只有 return 的程式；分析失敗時全部保留。
        List<(int Start, int End)> globalRanges = ReadIndexEntries(_data, _globalSubrIndex.Start);
        var usedGlobal = new bool[globalRanges.Count];
        var usedLocal = new bool[infos.Count][];
        for (int i = 0; i < infos.Count; i++)
        {
            usedLocal[i] = new bool[infos[i].Subrs?.Count ?? 0];
        }

        if (!TryMarkSubrs(keep, globalRanges, infos, usedGlobal, usedLocal, out bool usesSeac))
        {
            if (usesSeac)
            {
                return null;
            }

            SetAll(usedGlobal);
            foreach (bool[] local in usedLocal)
            {
                SetAll(local);
            }
        }

        byte[] globalSubrs = BuildSubrIndex(globalRanges, usedGlobal);
        var privateBlocks = new List<byte[]>();
        var privateSizes = new List<int>();
        for (int i = 0; i < infos.Count; i++)
        {
            (byte[] block, int dictSize) = BuildPrivate(infos[i], usedLocal[i]);
            privateBlocks.Add(block);
            privateSizes.Add(dictSize);
        }

        // 第一次估算大小：運算元一律用 5 位元組整數，所以版面在填入位移前就能確定。
        int topDictSize = TopDictBytes(_charsetLength > 0 ? 1 : 0, 0, 0, 0, 0).Length;
        byte[] topDictIndexPlaceholder = BuildIndex([new byte[topDictSize]]);
        int offset = header.Length + nameIndex.Length + topDictIndexPlaceholder.Length + stringIndex.Length + globalSubrs.Length;
        int newCharset = charset.Length > 0 ? offset : 0;
        offset += charset.Length;
        int newFdSelect = fdSelect.Length > 0 ? offset : 0;
        offset += fdSelect.Length;
        int newCharStrings = offset;
        offset += charStringsIndex.Length;

        byte[] fdArrayIndex = [];
        var privateOffsets = new List<int>();
        int newFdArray = 0;
        if (IsCidKeyed)
        {
            // FDArray 的大小只取決於 Font DICT 的運算元寬度，先用 0 位移估算長度。
            int fdArraySize = BuildIndex(fontDicts.ConvertAll(dict => FontDictBytes(dict, 0, 0))).Length;
            newFdArray = offset;
            offset += fdArraySize;
        }

        foreach (byte[] block in privateBlocks)
        {
            privateOffsets.Add(offset);
            offset += block.Length;
        }

        if (IsCidKeyed)
        {
            var newFontDicts = new List<byte[]>();
            for (int i = 0; i < fontDicts.Count; i++)
            {
                newFontDicts.Add(FontDictBytes(fontDicts[i], privateSizes[i], privateOffsets[i]));
            }

            fdArrayIndex = BuildIndex(newFontDicts);
        }

        int topPrivateSize = IsCidKeyed ? 0 : privateSizes[0];
        byte[] topDict = TopDictBytes(newCharset, newCharStrings, IsCidKeyed ? newFdArray : 0, newFdSelect, IsCidKeyed ? 0 : privateOffsets[0], topPrivateSize);
        byte[] topIndex = BuildIndex([topDict]);

        using var output = new MemoryStream(offset);
        output.Write(header, 0, header.Length);
        output.Write(nameIndex, 0, nameIndex.Length);
        output.Write(topIndex, 0, topIndex.Length);
        output.Write(stringIndex, 0, stringIndex.Length);
        output.Write(globalSubrs, 0, globalSubrs.Length);
        output.Write(charset, 0, charset.Length);
        output.Write(fdSelect, 0, fdSelect.Length);
        output.Write(charStringsIndex, 0, charStringsIndex.Length);
        output.Write(fdArrayIndex, 0, fdArrayIndex.Length);
        foreach (byte[] block in privateBlocks)
        {
            output.Write(block, 0, block.Length);
        }

        return output.ToArray();
    }

    // ---------- Top DICT／Font DICT 重寫 ----------

    private byte[] TopDictBytes(int charset, int charStrings, int fdArray, int fdSelect, int privateOffset, int privateSize = 0)
    {
        using var output = new MemoryStream();
        foreach ((int op, byte[] operands) in _topDict)
        {
            switch (op)
            {
                case OpCharset:
                    if (charset > 0)
                    {
                        WriteOffsetEntry(output, op, charset);
                    }

                    break;
                case OpCharStrings:
                    WriteOffsetEntry(output, op, charStrings);
                    break;
                case OpFdArray:
                    WriteOffsetEntry(output, op, fdArray);
                    break;
                case OpFdSelect:
                    WriteOffsetEntry(output, op, fdSelect);
                    break;
                case OpPrivate:
                    WriteIntOperand(output, privateSize);
                    WriteIntOperand(output, privateOffset);
                    WriteOperator(output, op);
                    break;
                default:
                    output.Write(operands, 0, operands.Length);
                    WriteOperator(output, op);
                    break;
            }
        }

        return output.ToArray();
    }

    private static byte[] FontDictBytes(List<(int Op, byte[] Operands)> dict, int privateSize, int privateOffset)
    {
        using var output = new MemoryStream();
        foreach ((int op, byte[] operands) in dict)
        {
            if (op == OpPrivate)
            {
                WriteIntOperand(output, privateSize);
                WriteIntOperand(output, privateOffset);
                WriteOperator(output, op);
            }
            else
            {
                output.Write(operands, 0, operands.Length);
                WriteOperator(output, op);
            }
        }

        return output.ToArray();
    }

    private static void WriteOffsetEntry(MemoryStream output, int op, int value)
    {
        WriteIntOperand(output, value);
        WriteOperator(output, op);
    }

    private static void WriteIntOperand(MemoryStream output, int value)
    {
        output.WriteByte(29);
        output.WriteByte((byte)(value >> 24));
        output.WriteByte((byte)(value >> 16));
        output.WriteByte((byte)(value >> 8));
        output.WriteByte((byte)value);
    }

    private static void WriteOperator(MemoryStream output, int op)
    {
        if (op >= 1200)
        {
            output.WriteByte(12);
            output.WriteByte((byte)(op - 1200));
        }
        else
        {
            output.WriteByte((byte)op);
        }
    }

    private sealed class PrivateInfo(List<(int Op, byte[] Operands)> entries, List<(int Start, int End)>? subrs)
    {
        internal List<(int Op, byte[] Operands)> Entries { get; } = entries;

        internal List<(int Start, int End)>? Subrs { get; } = subrs;
    }

    // 讀出 Private DICT 與其區域 Subrs 的範圍；沒有 Private DICT 時回傳空資訊。
    private PrivateInfo LoadPrivate(List<(int Op, byte[] Operands)> dict)
    {
        foreach ((int op, byte[] operands) in dict)
        {
            if (op != OpPrivate)
            {
                continue;
            }

            int size = ReadOperandAt(operands, 0);
            int offset = ReadOperandAt(operands, 1);
            if (size < 0 || offset < 0 || offset + size > _data.Length)
            {
                throw new InvalidDataException("Invalid Private DICT range.");
            }

            List<(int Op, byte[] Operands)> entries = ParseDict(_data, offset, offset + size);
            List<(int Start, int End)>? subrs = null;
            foreach ((int privateOp, byte[] privateOperands) in entries)
            {
                if (privateOp == OpSubrs)
                {
                    subrs = ReadIndexEntries(_data, offset + ReadOperandAt(privateOperands, 0));
                }
            }

            return new PrivateInfo(entries, subrs);
        }

        return new PrivateInfo([], null);
    }

    // 重寫 Private DICT（Subrs 位移固定 5 位元組，並緊接在 DICT 之後）與精簡後的區域 Subrs。
    private (byte[] Block, int DictSize) BuildPrivate(PrivateInfo info, bool[] used)
    {
        if (info.Entries.Count == 0)
        {
            return ([], 0);
        }

        int dictSize = SerializePrivate(info, 0).Length;
        byte[] dictBytes = SerializePrivate(info, dictSize);
        if (info.Subrs is null)
        {
            return (dictBytes, dictSize);
        }

        byte[] subrs = BuildSubrIndex(info.Subrs, used);
        var block = new byte[dictBytes.Length + subrs.Length];
        Buffer.BlockCopy(dictBytes, 0, block, 0, dictBytes.Length);
        Buffer.BlockCopy(subrs, 0, block, dictBytes.Length, subrs.Length);
        return (block, dictSize);
    }

    private static byte[] SerializePrivate(PrivateInfo info, int subrsOffset)
    {
        using var dict = new MemoryStream();
        foreach ((int op, byte[] operands) in info.Entries)
        {
            if (op == OpSubrs)
            {
                WriteIntOperand(dict, subrsOffset);
            }
            else
            {
                dict.Write(operands, 0, operands.Length);
            }

            WriteOperator(dict, op);
        }

        return dict.ToArray();
    }

    private byte[] BuildSubrIndex(List<(int Start, int End)> ranges, bool[] used)
    {
        var items = new List<byte[]>(ranges.Count);
        for (int i = 0; i < ranges.Count; i++)
        {
            items.Add(used[i] ? Slice(ranges[i].Start, ranges[i].End) : [ReturnOperator]);
        }

        return BuildIndex(items);
    }

    // ---------- Type 2 Charstring 掃描（只為找出被呼叫的子程式） ----------

    private const byte ReturnOperator = 11;
    private const int MaxSubrDepth = 10;
    private const int MaxSteps = 20_000_000;

    private sealed class ScanState
    {
        internal List<int> Stack { get; } = [];

        internal int Stems { get; set; }

        internal int Steps { get; set; }

        internal bool Ended { get; set; }

        internal bool Seac { get; set; }
    }

    private bool TryMarkSubrs(HashSet<int> keep, List<(int Start, int End)> globals, List<PrivateInfo> infos, bool[] usedGlobal, bool[][] usedLocal, out bool usesSeac)
    {
        usesSeac = false;
        try
        {
            foreach (int glyph in keep)
            {
                if (glyph < 0 || glyph >= GlyphCount)
                {
                    continue;
                }

                int fd = IsCidKeyed ? FdOf(glyph) : 0;
                if (fd < 0 || fd >= infos.Count)
                {
                    return false;
                }

                var state = new ScanState();
                (int start, int end) = _charStringRanges[glyph];
                Scan(start, end, 0, globals, infos[fd].Subrs, usedGlobal, usedLocal[fd], state);
                if (state.Seac)
                {
                    usesSeac = true;
                    return false;
                }

                if (state.Steps > MaxSteps)
                {
                    return false;
                }
            }

            return true;
        }
        catch (Exception ex) when (ex is IndexOutOfRangeException or InvalidDataException or ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    private static void SetAll(bool[] flags)
    {
        for (int i = 0; i < flags.Length; i++)
        {
            flags[i] = true;
        }
    }

    private int FdOf(int glyph)
    {
        int format = _data[_fdSelectOffset];
        if (format == 0)
        {
            return _data[_fdSelectOffset + 1 + glyph];
        }

        int ranges = (_data[_fdSelectOffset + 1] << 8) | _data[_fdSelectOffset + 2];
        int position = _fdSelectOffset + 3;
        for (int i = 0; i < ranges; i++)
        {
            int first = (_data[position] << 8) | _data[position + 1];
            int next = (_data[position + 3] << 8) | _data[position + 4];
            if (glyph >= first && glyph < next)
            {
                return _data[position + 2];
            }

            position += 3;
        }

        return -1;
    }

    private static int SubrBias(int count) => count < 1240 ? 107 : (count < 33900 ? 1131 : 32768);

    private void Scan(
        int start,
        int end,
        int depth,
        List<(int Start, int End)> globals,
        List<(int Start, int End)>? locals,
        bool[] usedGlobal,
        bool[] usedLocal,
        ScanState state)
    {
        if (depth > MaxSubrDepth)
        {
            throw new InvalidDataException("Subroutine nesting too deep.");
        }

        int position = start;
        while (position < end && !state.Ended)
        {
            if (++state.Steps > MaxSteps)
            {
                return;
            }

            byte b0 = _data[position];
            if (b0 >= 32 || b0 == 28)
            {
                int value;
                if (b0 == 28)
                {
                    value = (short)((_data[position + 1] << 8) | _data[position + 2]);
                    position += 3;
                }
                else if (b0 <= 246)
                {
                    value = b0 - 139;
                    position++;
                }
                else if (b0 <= 250)
                {
                    value = ((b0 - 247) * 256) + _data[position + 1] + 108;
                    position += 2;
                }
                else if (b0 <= 254)
                {
                    value = (-(b0 - 251) * 256) - _data[position + 1] - 108;
                    position += 2;
                }
                else
                {
                    value = (_data[position + 1] << 8) | _data[position + 2];
                    position += 5;
                }

                if (state.Stack.Count < 513)
                {
                    state.Stack.Add(value);
                }

                continue;
            }

            position++;
            switch (b0)
            {
                case 1:
                case 3:
                case 18:
                case 23:
                    state.Stems += state.Stack.Count / 2;
                    state.Stack.Clear();
                    break;
                case 19:
                case 20:
                    state.Stems += state.Stack.Count / 2;
                    state.Stack.Clear();
                    position += (state.Stems + 7) / 8;
                    break;
                case 10:
                case 29:
                    CallSubr(b0 == 29, depth, globals, locals, usedGlobal, usedLocal, state);
                    break;
                case ReturnOperator:
                    return;
                case EndCharOperator:
                    // 帶 4 或 5 個運算元的 endchar 是 seac（以標準編碼組合字形），組合的元件字形不在保留集合內。
                    state.Seac = state.Stack.Count >= 4;
                    state.Ended = true;
                    return;
                case 12:
                    position++;
                    state.Stack.Clear();
                    break;
                default:
                    state.Stack.Clear();
                    break;
            }
        }
    }

    private void CallSubr(
        bool global,
        int depth,
        List<(int Start, int End)> globals,
        List<(int Start, int End)>? locals,
        bool[] usedGlobal,
        bool[] usedLocal,
        ScanState state)
    {
        if (state.Stack.Count == 0)
        {
            throw new InvalidDataException("callsubr without operand.");
        }

        int top = state.Stack[state.Stack.Count - 1];
        state.Stack.RemoveAt(state.Stack.Count - 1);
        List<(int Start, int End)>? table = global ? globals : locals;
        if (table is null)
        {
            throw new InvalidDataException("No subroutine table.");
        }

        int index = top + SubrBias(table.Count);
        if (index < 0 || index >= table.Count)
        {
            throw new InvalidDataException("Subroutine index out of range.");
        }

        (global ? usedGlobal : usedLocal)[index] = true;
        Scan(table[index].Start, table[index].End, depth + 1, globals, locals, usedGlobal, usedLocal, state);
    }

    // ---------- DICT 剖析 ----------

    private static List<(int Op, byte[] Operands)> ParseDict(byte[] data, int start, int end)
    {
        var entries = new List<(int, byte[])>();
        int position = start;
        int operandStart = start;
        while (position < end)
        {
            byte b0 = data[position];
            if (b0 <= 21)
            {
                int op = b0;
                int next = position + 1;
                if (b0 == 12)
                {
                    op = 1200 + data[position + 1];
                    next = position + 2;
                }

                var operands = new byte[position - operandStart];
                Buffer.BlockCopy(data, operandStart, operands, 0, operands.Length);
                entries.Add((op, operands));
                position = next;
                operandStart = position;
            }
            else if (b0 == 28)
            {
                position += 3;
            }
            else if (b0 == 29)
            {
                position += 5;
            }
            else if (b0 == 30)
            {
                position++;
                while (position < end)
                {
                    byte nibbles = data[position++];
                    if ((nibbles & 0x0F) == 0x0F || (nibbles >> 4) == 0x0F)
                    {
                        break;
                    }
                }
            }
            else if (b0 >= 32 && b0 <= 246)
            {
                position++;
            }
            else if (b0 >= 247 && b0 <= 254)
            {
                position += 2;
            }
            else
            {
                throw new InvalidDataException("Invalid DICT byte.");
            }
        }

        return entries;
    }

    private static int OffsetOperand(List<(int Op, byte[] Operands)> dict, int op)
    {
        foreach ((int entryOp, byte[] operands) in dict)
        {
            if (entryOp == op)
            {
                return ReadOperandAt(operands, 0);
            }
        }

        return -1;
    }

    // 讀取運算元串列中第 index 個整數運算元。
    private static int ReadOperandAt(byte[] operands, int index)
    {
        int position = 0;
        for (int i = 0; ; i++)
        {
            if (position >= operands.Length)
            {
                throw new InvalidDataException("Missing DICT operand.");
            }

            byte b0 = operands[position];
            int value;
            int length;
            if (b0 == 28)
            {
                value = (short)((operands[position + 1] << 8) | operands[position + 2]);
                length = 3;
            }
            else if (b0 == 29)
            {
                value = (operands[position + 1] << 24) | (operands[position + 2] << 16) | (operands[position + 3] << 8) | operands[position + 4];
                length = 5;
            }
            else if (b0 == 30)
            {
                length = 1;
                while (position + length < operands.Length)
                {
                    byte nibbles = operands[position + length++];
                    if ((nibbles & 0x0F) == 0x0F || (nibbles >> 4) == 0x0F)
                    {
                        break;
                    }
                }

                value = 0;
            }
            else if (b0 >= 32 && b0 <= 246)
            {
                value = b0 - 139;
                length = 1;
            }
            else if (b0 >= 247 && b0 <= 250)
            {
                value = ((b0 - 247) * 256) + operands[position + 1] + 108;
                length = 2;
            }
            else if (b0 >= 251 && b0 <= 254)
            {
                value = (-(b0 - 251) * 256) - operands[position + 1] - 108;
                length = 2;
            }
            else
            {
                throw new InvalidDataException("Invalid DICT operand.");
            }

            if (i == index)
            {
                return value;
            }

            position += length;
        }
    }

    // ---------- INDEX ----------

    private static (int Start, int End) ReadIndexExtent(byte[] data, int position)
    {
        int count = (data[position] << 8) | data[position + 1];
        if (count == 0)
        {
            return (position, position + 2);
        }

        int offSize = data[position + 2];
        if (offSize < 1 || offSize > 4)
        {
            throw new InvalidDataException("Invalid INDEX offSize.");
        }

        int offsetsStart = position + 3;
        int dataStart = offsetsStart + ((count + 1) * offSize) - 1;
        long last = ReadOffset(data, offsetsStart + (count * offSize), offSize);
        long end = dataStart + last;
        if (end > data.Length || last < 1)
        {
            throw new InvalidDataException("INDEX extends beyond the data.");
        }

        return (position, (int)end);
    }

    private static List<(int Start, int End)> ReadIndexEntries(byte[] data, int position)
    {
        int count = (data[position] << 8) | data[position + 1];
        var entries = new List<(int, int)>(count);
        if (count == 0)
        {
            return entries;
        }

        int offSize = data[position + 2];
        int offsetsStart = position + 3;
        int dataStart = offsetsStart + ((count + 1) * offSize) - 1;
        long previous = ReadOffset(data, offsetsStart, offSize);
        for (int i = 1; i <= count; i++)
        {
            long next = ReadOffset(data, offsetsStart + (i * offSize), offSize);
            if (next < previous || dataStart + next > data.Length)
            {
                throw new InvalidDataException("INDEX offsets are not monotonic.");
            }

            entries.Add(((int)(dataStart + previous), (int)(dataStart + next)));
            previous = next;
        }

        return entries;
    }

    private static long ReadOffset(byte[] data, int position, int size)
    {
        long value = 0;
        for (int i = 0; i < size; i++)
        {
            value = (value << 8) | data[position + i];
        }

        return value;
    }

    private static byte[] BuildIndex(List<byte[]> items)
    {
        if (items.Count == 0)
        {
            return [0, 0];
        }

        long total = 1;
        foreach (byte[] item in items)
        {
            total += item.Length;
        }

        int offSize = total <= 0xFF ? 1 : (total <= 0xFFFF ? 2 : (total <= 0xFFFFFF ? 3 : 4));
        using var output = new MemoryStream();
        output.WriteByte((byte)(items.Count >> 8));
        output.WriteByte((byte)items.Count);
        output.WriteByte((byte)offSize);
        long offset = 1;
        WriteOffset(output, offset, offSize);
        foreach (byte[] item in items)
        {
            offset += item.Length;
            WriteOffset(output, offset, offSize);
        }

        foreach (byte[] item in items)
        {
            output.Write(item, 0, item.Length);
        }

        return output.ToArray();
    }

    private static void WriteOffset(MemoryStream output, long value, int size)
    {
        for (int i = size - 1; i >= 0; i--)
        {
            output.WriteByte((byte)(value >> (8 * i)));
        }
    }

    // ---------- charset 與 FDSelect ----------

    private static int ParseCharset(byte[] data, int offset, int glyphCount, int[] glyphToCid)
    {
        int format = data[offset];
        int position = offset + 1;
        int glyph = 1;
        switch (format)
        {
            case 0:
                for (; glyph < glyphCount; glyph++)
                {
                    glyphToCid[glyph] = (data[position] << 8) | data[position + 1];
                    position += 2;
                }

                break;
            case 1:
            case 2:
                while (glyph < glyphCount)
                {
                    int first = (data[position] << 8) | data[position + 1];
                    int left;
                    if (format == 1)
                    {
                        left = data[position + 2];
                        position += 3;
                    }
                    else
                    {
                        left = (data[position + 2] << 8) | data[position + 3];
                        position += 4;
                    }

                    for (int i = 0; i <= left && glyph < glyphCount; i++, glyph++)
                    {
                        glyphToCid[glyph] = first + i;
                    }
                }

                break;
            default:
                throw new InvalidDataException("Unsupported charset format.");
        }

        return position - offset;
    }

    private static int FdSelectLength(byte[] data, int offset, int glyphCount)
    {
        int format = data[offset];
        switch (format)
        {
            case 0:
                return 1 + glyphCount;
            case 3:
                {
                    int ranges = (data[offset + 1] << 8) | data[offset + 2];
                    return 1 + 2 + (ranges * 3) + 2;
                }

            default:
                throw new InvalidDataException("Unsupported FDSelect format.");
        }
    }

    private byte[] Slice(int start, int end)
    {
        var copy = new byte[end - start];
        Buffer.BlockCopy(_data, start, copy, 0, copy.Length);
        return copy;
    }
}
