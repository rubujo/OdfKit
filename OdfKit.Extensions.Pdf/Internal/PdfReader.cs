using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace OdfKit.Export.PdfInternal;

internal sealed class PdfName(string value)
{
    internal string Value { get; } = value;

    public override string ToString() => "/" + Value;
}

internal readonly struct PdfRef(int number, int generation) : IEquatable<PdfRef>
{
    internal int Number { get; } = number;

    internal int Generation { get; } = generation;

    public bool Equals(PdfRef other) => Number == other.Number && Generation == other.Generation;

    public override bool Equals(object? obj) => obj is PdfRef other && Equals(other);

    public override int GetHashCode() => (Number, Generation).GetHashCode();
}

internal sealed class PdfString(byte[] value)
{
    internal byte[] Value { get; } = value;

    /// <summary>
    /// 依 PDF 文字字串規則解碼（UTF-16BE／UTF-8 BOM，否則視為 Latin-1）。
    /// </summary>
    internal string Text()
    {
        if (Value.Length >= 2 && Value[0] == 0xFE && Value[1] == 0xFF)
        {
            return Encoding.BigEndianUnicode.GetString(Value, 2, Value.Length - 2);
        }

        if (Value.Length >= 3 && Value[0] == 0xEF && Value[1] == 0xBB && Value[2] == 0xBF)
        {
            return Encoding.UTF8.GetString(Value, 3, Value.Length - 3);
        }

        return Encoding.GetEncoding(28591).GetString(Value);
    }
}

internal sealed class PdfDict : Dictionary<string, object?>
{
    internal PdfDict()
    {
    }

    internal PdfDict(PdfDict source)
        : base(source)
    {
    }
}

internal sealed class PdfStream(PdfDict dictionary, byte[] raw)
{
    internal PdfDict Dictionary { get; } = dictionary;

    internal byte[] Raw { get; } = raw;
}

/// <summary>
/// 最小的 PDF 語法剖析器：物件語法、傳統與串流交叉參照表、物件串流與 Flate 解碼。
/// A minimal PDF parser: object syntax, classic and stream cross-reference tables, object streams and Flate decoding.
/// </summary>
internal sealed class PdfFile
{
    private const int MaxNesting = 64;
    private const int MaxObjects = 4_000_000;
    private const int MaxObjectStreamCount = 1_000_000;
    private const long MaxPredictorRowBytes = 1L << 26;

    private readonly byte[] _data;
    private readonly Dictionary<int, XrefEntry> _entries = [];
    private readonly Dictionary<int, object?> _cache = [];
    private readonly Dictionary<int, List<(int Number, object? Value)>> _objectStreams = [];

    private PdfFile(byte[] data)
    {
        _data = data;
    }

    private int _encryptObjectNumber = -1;

    private readonly struct XrefEntry(int type, long offset, int generation)
    {
        internal int Type { get; } = type;

        /// <summary>
        /// 類型 1 為檔案位移；類型 2 為所在物件串流的物件編號。
        /// </summary>
        internal long Offset { get; } = offset;

        /// <summary>
        /// 類型 1 為世代；類型 2 為在物件串流中的索引。
        /// </summary>
        internal int Generation { get; } = generation;
    }

    internal PdfDict Trailer { get; private set; } = new();

    /// <summary>
    /// 最新交叉參照區段的位移（增量更新的 /Prev）。
    /// </summary>
    internal long LastXrefOffset { get; private set; }

    /// <summary>
    /// 最新交叉參照區段是否為串流型（增量更新需使用相同型式）。
    /// </summary>
    internal bool LastXrefIsStream { get; private set; }

    internal bool IsEncrypted => Trailer.ContainsKey("Encrypt");

    /// <summary>
    /// 交叉參照表損毀而改以掃描重建時為 <see langword="true"/>。
    /// </summary>
    internal bool Rebuilt { get; private set; }

    /// <summary>
    /// 加密文件的解密器；未加密時為 <see langword="null"/>。
    /// </summary>
    internal PdfDecryptor? Decryptor { get; private set; }

    internal int NextObjectNumber
    {
        get
        {
            int max = 0;
            foreach (int number in _entries.Keys)
            {
                max = Math.Max(max, number);
            }

            return Math.Max(max + 1, Trailer.TryGetValue("Size", out object? size) && size is long l ? (int)Math.Min(l, MaxObjects) : 0);
        }
    }

    internal byte[] Data => _data;

    internal static PdfFile Open(byte[] data, string? password = null)
    {
        var file = new PdfFile(data);
        try
        {
            file.LoadCrossReferences();
        }
        catch (Exception ex) when (ex is InvalidDataException or IndexOutOfRangeException or ArgumentException or OverflowException)
        {
            file._entries.Clear();
            file._cache.Clear();
            file._objectStreams.Clear();
            file.Rebuild();
        }

        if (!file.Trailer.ContainsKey("Root"))
        {
            file.Rebuild();
        }

        if (!file.Trailer.ContainsKey("Root"))
        {
            throw new InvalidDataException("The file is not a readable PDF document.");
        }

        if (file.Trailer.TryGetValue("Encrypt", out object? encryptValue))
        {
            if (encryptValue is PdfRef encryptRef)
            {
                file._encryptObjectNumber = encryptRef.Number;
            }

            PdfDict encrypt = file.ResolveDict(encryptValue) ?? throw new InvalidDataException("Invalid encryption dictionary.");
            file.Decryptor = PdfDecryptor.Create(file, encrypt, password);
            file._cache.Clear();
            file._objectStreams.Clear();
        }

        return file;
    }

    // ---------- 交叉參照 ----------

    private void LoadCrossReferences()
    {
        long offset = FindStartXref();
        LastXrefOffset = offset;
        bool first = true;
        var visited = new HashSet<long>();
        while (offset >= 0 && visited.Add(offset))
        {
            (PdfDict trailer, bool isStream) = ReadXrefSection(offset);
            if (first)
            {
                Trailer = trailer;
                LastXrefIsStream = isStream;
                first = false;
            }
            else
            {
                foreach (KeyValuePair<string, object?> pair in trailer)
                {
                    if (!Trailer.ContainsKey(pair.Key))
                    {
                        Trailer[pair.Key] = pair.Value;
                    }
                }
            }

            if (!isStream && trailer.TryGetValue("XRefStm", out object? stm) && stm is long stmOffset)
            {
                ReadXrefSection(stmOffset);
            }

            offset = trailer.TryGetValue("Prev", out object? prev) && prev is long p ? p : -1;
        }
    }

    private long FindStartXref()
    {
        byte[] marker = Encoding.ASCII.GetBytes("startxref");
        int searchStart = Math.Max(0, _data.Length - 2048);
        for (int i = _data.Length - marker.Length; i >= searchStart; i--)
        {
            if (Matches(i, marker))
            {
                var parser = new Lexer(_data, i + marker.Length);
                object? value = parser.ParseObject(0);
                if (value is long offset && offset >= 0 && offset < _data.Length)
                {
                    return offset;
                }
            }
        }

        throw new InvalidDataException("startxref not found.");
    }

    private bool Matches(int position, byte[] pattern)
    {
        if (position < 0 || position + pattern.Length > _data.Length)
        {
            return false;
        }

        for (int i = 0; i < pattern.Length; i++)
        {
            if (_data[position + i] != pattern[i])
            {
                return false;
            }
        }

        return true;
    }

    private (PdfDict Trailer, bool IsStream) ReadXrefSection(long offset)
    {
        var lexer = new Lexer(_data, (int)offset);
        lexer.SkipWhitespace();
        if (lexer.Peek("xref"))
        {
            lexer.Advance(4);
            while (true)
            {
                lexer.SkipWhitespace();
                if (lexer.Peek("trailer"))
                {
                    lexer.Advance(7);
                    break;
                }

                object? start = lexer.ParseObject(0);
                object? count = lexer.ParseObject(0);
                if (start is not long first || count is not long total || total < 0 || total > MaxObjects)
                {
                    throw new InvalidDataException("Invalid xref subsection.");
                }

                for (long i = 0; i < total; i++)
                {
                    object? entryOffset = lexer.ParseObject(0);
                    object? generation = lexer.ParseObject(0);
                    lexer.SkipWhitespace();
                    int flag = lexer.ReadByte();
                    if (entryOffset is not long position || generation is not long gen)
                    {
                        throw new InvalidDataException("Invalid xref entry.");
                    }

                    int number = checked((int)(first + i));
                    if (flag == 'n' && !_entries.ContainsKey(number))
                    {
                        _entries[number] = new XrefEntry(1, position, (int)gen);
                    }
                    else if (flag == 'f' && !_entries.ContainsKey(number))
                    {
                        _entries[number] = new XrefEntry(0, 0, (int)gen);
                    }
                }
            }

            return (lexer.ParseObject(0) as PdfDict ?? throw new InvalidDataException("Invalid trailer."), false);
        }

        object? obj = ParseIndirectAt(offset, out _);
        if (obj is not PdfStream stream || !(stream.Dictionary.TryGetValue("Type", out object? type) && type is PdfName { Value: "XRef" }))
        {
            throw new InvalidDataException("Invalid xref stream.");
        }

        byte[] data = DecodeStream(stream) ?? throw new InvalidDataException("Cannot decode xref stream.");
        if (stream.Dictionary.TryGetValue("W", out object? w) && w is List<object?> widths && widths.Count == 3
            && widths[0] is long w0 && widths[1] is long w1 && widths[2] is long w2)
        {
            int entrySize = (int)(w0 + w1 + w2);
            var index = new List<long>();
            if (stream.Dictionary.TryGetValue("Index", out object? idx) && idx is List<object?> list)
            {
                foreach (object? item in list)
                {
                    index.Add(item is long l ? l : 0);
                }
            }
            else
            {
                index.Add(0);
                index.Add(stream.Dictionary.TryGetValue("Size", out object? size) && size is long s ? s : 0);
            }

            int position = 0;
            for (int section = 0; section + 1 < index.Count; section += 2)
            {
                for (long i = 0; i < index[section + 1]; i++)
                {
                    if (position + entrySize > data.Length)
                    {
                        break;
                    }

                    long type1 = w0 == 0 ? 1 : ReadBigEndian(data, position, (int)w0);
                    long field2 = ReadBigEndian(data, position + (int)w0, (int)w1);
                    long field3 = ReadBigEndian(data, position + (int)w0 + (int)w1, (int)w2);
                    position += entrySize;
                    int number = checked((int)(index[section] + i));
                    if (_entries.ContainsKey(number))
                    {
                        continue;
                    }

                    _entries[number] = type1 switch
                    {
                        1 => new XrefEntry(1, field2, (int)field3),
                        2 => new XrefEntry(2, field2, (int)field3),
                        _ => new XrefEntry(0, 0, 0),
                    };
                }
            }
        }

        return (stream.Dictionary, true);
    }

    private static long ReadBigEndian(byte[] data, int position, int width)
    {
        long value = 0;
        for (int i = 0; i < width; i++)
        {
            value = (value << 8) | data[position + i];
        }

        return value;
    }

    // 無法使用交叉參照表時，掃描檔案中的「n g obj」重建，並從所有 trailer／XRef 串流取得根物件。
    private void Rebuild()
    {
        Rebuilt = true;
        _entries.Clear();
        _cache.Clear();
        _objectStreams.Clear();
        Trailer = new PdfDict();
        for (int i = 0; i < _data.Length - 6; i++)
        {
            if (_data[i] != 'o' || _data[i + 1] != 'b' || _data[i + 2] != 'j' || (i + 3 < _data.Length && IsRegular(_data[i + 3])))
            {
                continue;
            }

            int end = i - 1;
            while (end >= 0 && IsWhitespace(_data[end]))
            {
                end--;
            }

            int genEnd = end;
            while (end >= 0 && _data[end] >= '0' && _data[end] <= '9')
            {
                end--;
            }

            if (end == genEnd)
            {
                continue;
            }

            int generation = ParseInt(end + 1, genEnd);
            while (end >= 0 && IsWhitespace(_data[end]))
            {
                end--;
            }

            int numEnd = end;
            while (end >= 0 && _data[end] >= '0' && _data[end] <= '9')
            {
                end--;
            }

            if (end == numEnd)
            {
                continue;
            }

            int number = ParseInt(end + 1, numEnd);
            _entries[number] = new XrefEntry(1, end + 1, generation);
        }

        foreach (int number in new List<int>(_entries.Keys))
        {
            object? value;
            try
            {
                value = GetObject(number);
            }
            catch (Exception ex) when (ex is InvalidDataException or IndexOutOfRangeException or ArgumentException or OverflowException)
            {
                continue;
            }

            if (value is PdfStream stream && stream.Dictionary.TryGetValue("Type", out object? type))
            {
                if (type is PdfName { Value: "XRef" })
                {
                    foreach (KeyValuePair<string, object?> pair in stream.Dictionary)
                    {
                        Trailer[pair.Key] = pair.Value;
                    }
                }
                else if (type is PdfName { Value: "ObjStm" })
                {
                    RegisterObjectStream(number, stream);
                }
            }
            else if (value is PdfDict dictionary && dictionary.TryGetValue("Type", out object? catalogType) && catalogType is PdfName { Value: "Catalog" })
            {
                Trailer["Root"] = new PdfRef(number, _entries[number].Generation);
            }
        }

        // 傳統 trailer 取最後一個。
        byte[] marker = Encoding.ASCII.GetBytes("trailer");
        for (int i = _data.Length - marker.Length; i >= 0; i--)
        {
            if (!Matches(i, marker))
            {
                continue;
            }

            try
            {
                var lexer = new Lexer(_data, i + marker.Length);
                if (lexer.ParseObject(0) is PdfDict trailer)
                {
                    foreach (KeyValuePair<string, object?> pair in trailer)
                    {
                        Trailer[pair.Key] = pair.Value;
                    }

                    break;
                }
            }
            catch (Exception ex) when (ex is InvalidDataException or IndexOutOfRangeException or ArgumentException)
            {
                continue;
            }
        }

        LastXrefOffset = 0;
        LastXrefIsStream = false;
    }

    private void RegisterObjectStream(int number, PdfStream stream)
    {
        byte[]? data = DecodeStream(stream);
        if (data is null || !stream.Dictionary.TryGetValue("N", out object? n) || n is not long count
            || !stream.Dictionary.TryGetValue("First", out object? f) || f is not long first
            || count < 0 || count > MaxObjectStreamCount || first < 0 || first > data.Length)
        {
            return;
        }

        var lexer = new Lexer(data, 0);
        var header = new List<(int Number, long Offset)>();
        for (long i = 0; i < count; i++)
        {
            if (lexer.ParseObject(0) is long objectNumber && lexer.ParseObject(0) is long objectOffset)
            {
                header.Add(((int)objectNumber, objectOffset));
            }
        }

        var objects = new List<(int Number, object? Value)>();
        foreach ((int objectNumber, long objectOffset) in header)
        {
            if (first + objectOffset < 0 || first + objectOffset >= data.Length)
            {
                continue;
            }

            var inner = new Lexer(data, (int)(first + objectOffset));
            objects.Add((objectNumber, inner.ParseObject(0)));
        }

        _objectStreams[number] = objects;
        for (int index = 0; index < objects.Count; index++)
        {
            if (!_entries.ContainsKey(objects[index].Number))
            {
                _entries[objects[index].Number] = new XrefEntry(2, number, index);
            }
        }
    }

    private int ParseInt(int start, int end)
    {
        long value = 0;
        for (int i = start; i <= end; i++)
        {
            value = (value * 10) + (_data[i] - '0');
            if (value > int.MaxValue)
            {
                return int.MaxValue;
            }
        }

        return (int)value;
    }

    private static bool IsWhitespace(byte b) => b == 0 || b == 9 || b == 10 || b == 12 || b == 13 || b == 32;

    private static bool IsRegular(byte b) => !IsWhitespace(b) && "()<>[]{}/%".IndexOf((char)b) < 0;

    // ---------- 物件 ----------

    /// <summary>
    /// 取得物件（不展開間接參照）；不存在時回傳 <see langword="null"/>。
    /// </summary>
    internal object? GetObject(int number)
    {
        if (_cache.TryGetValue(number, out object? cached))
        {
            return cached;
        }

        if (!_entries.TryGetValue(number, out XrefEntry entry))
        {
            return null;
        }

        object? value = null;
        if (entry.Type == 1)
        {
            value = ParseIndirectAt(entry.Offset, out int parsedNumber);
            if (parsedNumber != number)
            {
                value = null;
            }
            else if (Decryptor is not null && number != _encryptObjectNumber)
            {
                value = Transform(value, number, entry.Generation, encrypt: false);
            }
        }
        else if (entry.Type == 2)
        {
            int containerNumber = (int)entry.Offset;
            if (!_objectStreams.ContainsKey(containerNumber) && GetObject(containerNumber) is PdfStream container)
            {
                RegisterObjectStream(containerNumber, container);
            }

            if (_objectStreams.TryGetValue(containerNumber, out List<(int Number, object? Value)>? objects))
            {
                foreach ((int objectNumber, object? objectValue) in objects)
                {
                    if (objectNumber == number)
                    {
                        value = objectValue;
                        break;
                    }
                }
            }
        }

        _cache[number] = value;
        return value;
    }

    /// <summary>
    /// 若為間接參照則取回其目標，否則原樣回傳。
    /// </summary>
    internal object? Resolve(object? value, int depth = 0)
    {
        while (value is PdfRef reference && depth++ < 32)
        {
            value = GetObject(reference.Number);
        }

        return value;
    }

    internal PdfDict? ResolveDict(object? value) => Resolve(value) switch
    {
        PdfDict dictionary => dictionary,
        PdfStream stream => stream.Dictionary,
        _ => null,
    };

    internal List<object?>? ResolveArray(object? value) => Resolve(value) as List<object?>;

    internal int GetGeneration(int number) =>
        _entries.TryGetValue(number, out XrefEntry entry) && entry.Type == 1 ? entry.Generation : 0;

    /// <summary>
    /// 寫出增量更新前，依檔案的加密設定加密物件中的字串與串流。
    /// </summary>
    internal object? EncryptForWrite(object? value, int number, int generation) =>
        Decryptor is null ? value : Transform(value, number, generation, encrypt: true);

    private object? Transform(object? value, int number, int generation, bool encrypt)
    {
        PdfDecryptor decryptor = Decryptor!;
        switch (value)
        {
            case PdfString text:
                return new PdfString(encrypt ? decryptor.EncryptString(text.Value, number, generation) : decryptor.DecryptString(text.Value, number, generation));
            case List<object?> array:
                {
                    var copy = new List<object?>(array.Count);
                    foreach (object? item in array)
                    {
                        copy.Add(Transform(item, number, generation, encrypt));
                    }

                    return copy;
                }

            case PdfDict dictionary:
                return TransformDictionary(dictionary, number, generation, encrypt);
            case PdfStream stream:
                {
                    PdfDict transformed = TransformDictionary(stream.Dictionary, number, generation, encrypt);
                    bool isXref = stream.Dictionary.TryGetValue("Type", out object? type) && type is PdfName { Value: "XRef" };
                    bool isPlainMetadata = !decryptor.EncryptMetadata && type is PdfName { Value: "Metadata" };
                    byte[] raw = isXref || isPlainMetadata
                        ? stream.Raw
                        : (encrypt ? decryptor.EncryptStream(stream.Raw, number, generation) : decryptor.DecryptStream(stream.Raw, number, generation));
                    return new PdfStream(transformed, raw);
                }

            default:
                return value;
        }
    }

    private PdfDict TransformDictionary(PdfDict dictionary, int number, int generation, bool encrypt)
    {
        var copy = new PdfDict();
        foreach (KeyValuePair<string, object?> pair in dictionary)
        {
            copy[pair.Key] = Transform(pair.Value, number, generation, encrypt);
        }

        return copy;
    }

    private object? ParseIndirectAt(long offset, out int number)
    {
        number = -1;
        if (offset < 0 || offset >= _data.Length)
        {
            return null;
        }

        var lexer = new Lexer(_data, (int)offset);
        object? n = lexer.ParseObject(0);
        object? g = lexer.ParseObject(0);
        lexer.SkipWhitespace();
        if (n is not long objectNumber || g is not long || !lexer.Peek("obj"))
        {
            throw new InvalidDataException("Invalid indirect object header.");
        }

        number = (int)objectNumber;
        lexer.Advance(3);
        object? value = lexer.ParseObject(0);
        if (value is PdfDict dictionary)
        {
            lexer.SkipWhitespace();
            if (lexer.Peek("stream"))
            {
                lexer.Advance(6);
                if (lexer.PeekByte() == '\r')
                {
                    lexer.Advance(1);
                }

                if (lexer.PeekByte() == '\n')
                {
                    lexer.Advance(1);
                }

                int start = lexer.Position;
                long length = -1;
                if (dictionary.TryGetValue("Length", out object? lengthValue))
                {
                    object? resolved = lengthValue is PdfRef ? Resolve(lengthValue) : lengthValue;
                    if (resolved is long l)
                    {
                        length = l;
                    }
                }

                if (length < 0 || start + length > _data.Length || !EndsWithEndstream(start + (int)length))
                {
                    length = FindEndStream(start) - start;
                }

                if (length < 0)
                {
                    throw new InvalidDataException("Unterminated stream.");
                }

                var raw = new byte[length];
                Buffer.BlockCopy(_data, start, raw, 0, (int)length);
                return new PdfStream(dictionary, raw);
            }
        }

        return value;
    }

    private bool EndsWithEndstream(int position)
    {
        int i = position;
        while (i < _data.Length && IsWhitespace(_data[i]))
        {
            i++;
        }

        return Matches(i, Encoding.ASCII.GetBytes("endstream"));
    }

    private int FindEndStream(int start)
    {
        byte[] marker = Encoding.ASCII.GetBytes("endstream");
        for (int i = start; i <= _data.Length - marker.Length; i++)
        {
            if (Matches(i, marker))
            {
                int end = i;
                if (end > start && _data[end - 1] == '\n')
                {
                    end--;
                }

                if (end > start && _data[end - 1] == '\r')
                {
                    end--;
                }

                return end;
            }
        }

        return -1;
    }

    // ---------- 串流解碼 ----------

    /// <summary>
    /// 解碼串流（支援 FlateDecode 與 ASCIIHexDecode）；遇到不支援的篩選器時回傳 <see langword="null"/>。
    /// </summary>
    internal byte[]? DecodeStream(PdfStream stream)
    {
        var filters = new List<string>();
        object? filter = Resolve(stream.Dictionary.TryGetValue("Filter", out object? f) ? f : null);
        if (filter is PdfName name)
        {
            filters.Add(name.Value);
        }
        else if (filter is List<object?> list)
        {
            foreach (object? item in list)
            {
                if (Resolve(item) is PdfName itemName)
                {
                    filters.Add(itemName.Value);
                }
            }
        }

        object? parms = Resolve(stream.Dictionary.TryGetValue("DecodeParms", out object? p) ? p : null);
        var parameterList = new List<PdfDict?>();
        if (parms is PdfDict single)
        {
            parameterList.Add(single);
        }
        else if (parms is List<object?> parmArray)
        {
            foreach (object? item in parmArray)
            {
                parameterList.Add(ResolveDict(item));
            }
        }

        byte[] data = stream.Raw;
        for (int i = 0; i < filters.Count; i++)
        {
            PdfDict? parameters = i < parameterList.Count ? parameterList[i] : null;
            switch (filters[i])
            {
                case "FlateDecode":
                case "Fl":
                    {
                        byte[]? inflated = PdfCore.Inflate(data, 0, data.Length);
                        if (inflated is null)
                        {
                            return null;
                        }

                        data = ApplyPredictor(inflated, parameters);
                        break;
                    }

                case "ASCIIHexDecode":
                case "AHx":
                    data = DecodeAsciiHex(data);
                    break;
                default:
                    return null;
            }
        }

        return data;
    }

    private byte[] ApplyPredictor(byte[] data, PdfDict? parameters)
    {
        long predictor = parameters is not null && Resolve(parameters.TryGetValue("Predictor", out object? p) ? p : null) is long pv ? pv : 1;
        if (predictor < 10)
        {
            return data;
        }

        long columns = parameters is not null && Resolve(parameters.TryGetValue("Columns", out object? c) ? c : null) is long cv ? cv : 1;
        long colors = parameters is not null && Resolve(parameters.TryGetValue("Colors", out object? k) ? k : null) is long kv ? kv : 1;
        long bits = parameters is not null && Resolve(parameters.TryGetValue("BitsPerComponent", out object? b) ? b : null) is long bv ? bv : 8;
        if (columns < 1 || colors < 1 || bits < 1 || bits > 16 || columns > MaxPredictorRowBytes || colors > 64
            || (((columns * colors * bits) + 7) / 8) > MaxPredictorRowBytes)
        {
            return data;
        }

        int bytesPerPixel = (int)Math.Max(1, colors * bits / 8);
        int rowBytes = (int)(((columns * colors * bits) + 7) / 8);
        int rows = data.Length / (rowBytes + 1);
        var output = new byte[rows * rowBytes];
        var previous = new byte[rowBytes];
        for (int row = 0; row < rows; row++)
        {
            int source = row * (rowBytes + 1);
            int filter = data[source];
            var current = new byte[rowBytes];
            Buffer.BlockCopy(data, source + 1, current, 0, rowBytes);
            for (int i = 0; i < rowBytes; i++)
            {
                int left = i >= bytesPerPixel ? current[i - bytesPerPixel] : 0;
                int up = previous[i];
                int upLeft = i >= bytesPerPixel ? previous[i - bytesPerPixel] : 0;
                int add = filter switch
                {
                    1 => left,
                    2 => up,
                    3 => (left + up) / 2,
                    4 => Paeth(left, up, upLeft),
                    _ => 0,
                };
                current[i] = unchecked((byte)(current[i] + add));
            }

            Buffer.BlockCopy(current, 0, output, row * rowBytes, rowBytes);
            previous = current;
        }

        return output;
    }

    private static int Paeth(int a, int b, int c)
    {
        int p = a + b - c;
        int pa = Math.Abs(p - a);
        int pb = Math.Abs(p - b);
        int pc = Math.Abs(p - c);
        return pa <= pb && pa <= pc ? a : (pb <= pc ? b : c);
    }

    private static byte[] DecodeAsciiHex(byte[] data)
    {
        var output = new List<byte>();
        int high = -1;
        foreach (byte b in data)
        {
            if (b == '>')
            {
                break;
            }

            int digit = b >= '0' && b <= '9' ? b - '0' : (b >= 'a' && b <= 'f' ? b - 'a' + 10 : (b >= 'A' && b <= 'F' ? b - 'A' + 10 : -1));
            if (digit < 0)
            {
                continue;
            }

            if (high < 0)
            {
                high = digit;
            }
            else
            {
                output.Add((byte)((high << 4) | digit));
                high = -1;
            }
        }

        if (high >= 0)
        {
            output.Add((byte)(high << 4));
        }

        return output.ToArray();
    }

    // ---------- 語法分析器 ----------

    internal sealed class Lexer(byte[] data, int position)
    {
        private int _position = position;

        internal int Position => _position;

        internal int PeekByte() => _position < data.Length ? data[_position] : -1;

        internal int ReadByte() => _position < data.Length ? data[_position++] : -1;

        internal void Advance(int count) => _position += count;

        internal bool Peek(string keyword)
        {
            if (_position + keyword.Length > data.Length)
            {
                return false;
            }

            for (int i = 0; i < keyword.Length; i++)
            {
                if (data[_position + i] != keyword[i])
                {
                    return false;
                }
            }

            return true;
        }

        internal void SkipWhitespace()
        {
            while (_position < data.Length)
            {
                byte b = data[_position];
                if (IsWhitespace(b))
                {
                    _position++;
                }
                else if (b == '%')
                {
                    while (_position < data.Length && data[_position] != '\n' && data[_position] != '\r')
                    {
                        _position++;
                    }
                }
                else
                {
                    break;
                }
            }
        }

        internal object? ParseObject(int depth)
        {
            if (depth > MaxNesting)
            {
                throw new InvalidDataException("PDF object nesting is too deep.");
            }

            SkipWhitespace();
            int b = PeekByte();
            if (b < 0)
            {
                throw new InvalidDataException("Unexpected end of PDF data.");
            }

            if (b == '<')
            {
                if (_position + 1 < data.Length && data[_position + 1] == '<')
                {
                    _position += 2;
                    var dictionary = new PdfDict();
                    while (true)
                    {
                        SkipWhitespace();
                        if (Peek(">>"))
                        {
                            _position += 2;
                            return dictionary;
                        }

                        if (PeekByte() != '/')
                        {
                            throw new InvalidDataException("Dictionary key must be a name.");
                        }

                        string key = ParseName();
                        dictionary[key] = ParseObject(depth + 1);
                    }
                }

                return ParseHexString();
            }

            if (b == '[')
            {
                _position++;
                var array = new List<object?>();
                while (true)
                {
                    SkipWhitespace();
                    if (PeekByte() == ']')
                    {
                        _position++;
                        return array;
                    }

                    array.Add(ParseObject(depth + 1));
                }
            }

            if (b == '(')
            {
                return ParseLiteralString();
            }

            if (b == '/')
            {
                return new PdfName(ParseName());
            }

            if (b == '+' || b == '-' || b == '.' || (b >= '0' && b <= '9'))
            {
                return ParseNumberOrReference();
            }

            int start = _position;
            while (_position < data.Length && IsRegular(data[_position]))
            {
                _position++;
            }

            string keyword = Encoding.ASCII.GetString(data, start, _position - start);
            switch (keyword)
            {
                case "true":
                    return true;
                case "false":
                    return false;
                case "null":
                    return null;
                default:
                    throw new InvalidDataException("Unexpected token '" + keyword + "'.");
            }
        }

        private string ParseName()
        {
            _position++; // '/'
            var bytes = new List<byte>();
            while (_position < data.Length && IsRegular(data[_position]))
            {
                byte b = data[_position++];
                if (b == '#' && _position + 1 < data.Length && Uri.IsHexDigit((char)data[_position]) && Uri.IsHexDigit((char)data[_position + 1]))
                {
                    bytes.Add(Convert.ToByte(Encoding.ASCII.GetString(data, _position, 2), 16));
                    _position += 2;
                }
                else
                {
                    bytes.Add(b);
                }
            }

            return Encoding.UTF8.GetString(bytes.ToArray());
        }

        private object ParseNumberOrReference()
        {
            int start = _position;
            while (_position < data.Length && (data[_position] == '+' || data[_position] == '-' || data[_position] == '.' || (data[_position] >= '0' && data[_position] <= '9')))
            {
                _position++;
            }

            string text = Encoding.ASCII.GetString(data, start, _position - start);
            if (ContainsDot(text))
            {
                return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double d) ? d : 0.0;
            }

            if (!long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long value))
            {
                return 0L;
            }

            // 「n g R」間接參照。
            int save = _position;
            if (value >= 0)
            {
                SkipWhitespace();
                int genStart = _position;
                while (_position < data.Length && data[_position] >= '0' && data[_position] <= '9')
                {
                    _position++;
                }

                if (_position > genStart && _position - genStart <= 5)
                {
                    long generation = long.Parse(Encoding.ASCII.GetString(data, genStart, _position - genStart), CultureInfo.InvariantCulture);
                    SkipWhitespace();
                    if (PeekByte() == 'R' && (_position + 1 >= data.Length || !IsRegular(data[_position + 1])))
                    {
                        _position++;
                        return new PdfRef((int)Math.Min(value, int.MaxValue), (int)generation);
                    }
                }
            }

            _position = save;
            return value;
        }

        private static bool ContainsDot(string text)
        {
            foreach (char c in text)
            {
                if (c == '.')
                {
                    return true;
                }
            }

            return false;
        }

        private PdfString ParseHexString()
        {
            _position++; // '<'
            var bytes = new List<byte>();
            int high = -1;
            while (_position < data.Length && data[_position] != '>')
            {
                byte b = data[_position++];
                int digit = b >= '0' && b <= '9' ? b - '0' : (b >= 'a' && b <= 'f' ? b - 'a' + 10 : (b >= 'A' && b <= 'F' ? b - 'A' + 10 : -1));
                if (digit < 0)
                {
                    continue;
                }

                if (high < 0)
                {
                    high = digit;
                }
                else
                {
                    bytes.Add((byte)((high << 4) | digit));
                    high = -1;
                }
            }

            if (high >= 0)
            {
                bytes.Add((byte)(high << 4));
            }

            _position++; // '>'
            return new PdfString(bytes.ToArray());
        }

        private PdfString ParseLiteralString()
        {
            _position++; // '('
            var bytes = new List<byte>();
            int depth = 1;
            while (_position < data.Length)
            {
                byte b = data[_position++];
                if (b == '\\' && _position < data.Length)
                {
                    byte escaped = data[_position++];
                    switch (escaped)
                    {
                        case (byte)'n':
                            bytes.Add(10);
                            break;
                        case (byte)'r':
                            bytes.Add(13);
                            break;
                        case (byte)'t':
                            bytes.Add(9);
                            break;
                        case (byte)'b':
                            bytes.Add(8);
                            break;
                        case (byte)'f':
                            bytes.Add(12);
                            break;
                        case 10:
                            break;
                        case 13:
                            if (_position < data.Length && data[_position] == 10)
                            {
                                _position++;
                            }

                            break;
                        default:
                            if (escaped >= '0' && escaped <= '7')
                            {
                                int value = escaped - '0';
                                for (int i = 0; i < 2 && _position < data.Length && data[_position] >= '0' && data[_position] <= '7'; i++)
                                {
                                    value = (value * 8) + (data[_position++] - '0');
                                }

                                bytes.Add((byte)value);
                            }
                            else
                            {
                                bytes.Add(escaped);
                            }

                            break;
                    }
                }
                else if (b == '(')
                {
                    depth++;
                    bytes.Add(b);
                }
                else if (b == ')')
                {
                    depth--;
                    if (depth == 0)
                    {
                        break;
                    }

                    bytes.Add(b);
                }
                else
                {
                    bytes.Add(b);
                }
            }

            return new PdfString(bytes.ToArray());
        }
    }
}
