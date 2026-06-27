using System;
using System.Buffers;
using System.Text;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OdfKit.Core;
#if NET10_0_OR_GREATER
using System.Runtime.Intrinsics;
#endif

namespace OdfKit.DOM;

/// <summary>
/// 針對 ODF 熱路徑設計的自訂、超輕量零分配 UTF-8 XML 拉取解析器。
/// </summary>
public ref struct OdfUtf8XmlReader
{
    private ReadOnlySpan<byte> _xml;
    private ReadOnlySpan<byte> _currentValue;
    private int _position;
    private int _valueChunkPosition;

    /// <summary>
    /// 取得目前解析器在 XML 緩衝區中的位元組位置。
    /// </summary>
    public readonly int Position => _position;

    /// <summary>
    /// 初始化 <see cref="OdfUtf8XmlReader"/> 結構的新執行個體。
    /// </summary>
    /// <param name="xml">XML 的 UTF-8 位元組 Span</param>
    public OdfUtf8XmlReader(ReadOnlySpan<byte> xml)
    {
        _xml = xml;
        _currentValue = default;
        _position = 0;
        _valueChunkPosition = 0;
    }

    /// <summary>
    /// 拉取並讀取下一個 XML 標記。
    /// </summary>
    /// <param name="token">輸出的標記資訊</param>
    /// <returns>若成功讀取到標記則為 true，若已到達結尾則為 false</returns>
    public bool Read(out OdfUtf8XmlToken token)
    {
        token = default;
        _currentValue = default;
        _valueChunkPosition = 0;

        if (_position >= _xml.Length)
            return false;

        // 跳過空白字元
        while (_position < _xml.Length && IsWhitespace(_xml[_position]))
        {
            _position++;
        }

        if (_position >= _xml.Length)
            return false;

        int start = _position;
        if (_xml[_position] == (byte)'<')
        {
            _position++;
            if (_position < _xml.Length && _xml[_position] == (byte)'/')
            {
                // 結束標籤
                _position++;
                int nameStart = _position;
                while (_position < _xml.Length && _xml[_position] != (byte)'>')
                {
                    _position++;
                }
                var name = _xml.Slice(nameStart, _position - nameStart);
                if (_position < _xml.Length)
                    _position++; // consume '>'

                token = new OdfUtf8XmlToken(OdfUtf8XmlTokenKind.EndElement, name, offset: start, length: _position - start);
                return true;
            }
            else if (_position < _xml.Length && _xml[_position] == (byte)'!')
            {
                // 註解或 DTD
                _position++;
                int depth = 1;
                while (_position < _xml.Length && depth > 0)
                {
                    if (_xml[_position] == (byte)'<')
                        depth++;
                    else if (_xml[_position] == (byte)'>')
                        depth--;
                    _position++;
                }
                token = new OdfUtf8XmlToken(OdfUtf8XmlTokenKind.Comment, _xml.Slice(start, _position - start), offset: start, length: _position - start);
                return true;
            }
            else if (_position < _xml.Length && _xml[_position] == (byte)'?')
            {
                // Processing Instruction
                _position++;
                while (_position < _xml.Length && !(_xml[_position] == (byte)'?' && _position + 1 < _xml.Length && _xml[_position + 1] == (byte)'>'))
                {
                    _position++;
                }
                _position += 2; // consume '?>'
                token = new OdfUtf8XmlToken(OdfUtf8XmlTokenKind.ProcessingInstruction, _xml.Slice(start, _position - start), offset: start, length: _position - start);
                return true;
            }
            else
            {
                // 開始標籤
                int nameStart = _position;
                while (_position < _xml.Length && !IsWhitespace(_xml[_position]) && _xml[_position] != (byte)'>' && _xml[_position] != (byte)'/')
                {
                    _position++;
                }
                var name = _xml.Slice(nameStart, _position - nameStart);

                // 處理屬性區段
                int attrStart = _position;
                bool isSelfClosing = false;
                while (_position < _xml.Length && _xml[_position] != (byte)'>')
                {
                    if (_xml[_position] == (byte)'/' && _position + 1 < _xml.Length && _xml[_position + 1] == (byte)'>')
                    {
                        isSelfClosing = true;
                        _position++;
                        break;
                    }
                    _position++;
                }
                var attributesSpan = _xml.Slice(attrStart, _position - attrStart);
                if (_position < _xml.Length)
                    _position++; // consume '>'

                token = new OdfUtf8XmlToken(
                    isSelfClosing ? OdfUtf8XmlTokenKind.SelfClosingElement : OdfUtf8XmlTokenKind.StartElement,
                    name,
                    attributesSpan,
                    offset: start,
                    length: _position - start);
                return true;
            }
        }

        // 文字內容需保留完整 entity 區段，後續由 GetStringMaybeDecoded 一次解碼。
        ScanToByte((byte)'<');
        _currentValue = _xml.Slice(start, _position - start);
        token = new OdfUtf8XmlToken(OdfUtf8XmlTokenKind.Text, _currentValue, offset: start, length: _position - start);
        return true;
    }

    /// <summary>
    /// 將目前文字值以 UTF-8 位元組分塊讀入指定緩衝區。
    /// </summary>
    /// <param name="buffer">接收目前文字值片段的緩衝區</param>
    /// <returns>本次寫入緩衝區的位元組數；若目前不是文字值或已讀完則為 0</returns>
    public int ReadValueChunk(scoped Span<byte> buffer)
    {
        if (buffer.Length == 0 || _currentValue.IsEmpty || _valueChunkPosition >= _currentValue.Length)
        {
            return 0;
        }

        int count = Math.Min(buffer.Length, _currentValue.Length - _valueChunkPosition);
        _currentValue.Slice(_valueChunkPosition, count).CopyTo(buffer);
        _valueChunkPosition += count;
        return count;
    }

    /// <summary>
    /// 將目前文字值從目前分塊游標位置直接複製到指定的位元組寫入器。
    /// </summary>
    /// <param name="writer">接收 UTF-8 文字值的位元組寫入器</param>
    /// <param name="chunkSize">每次向寫入器要求的最大連續緩衝區大小</param>
    /// <returns>本次複製的位元組數；若目前不是文字值或已讀完則為 0</returns>
    /// <exception cref="ArgumentNullException">當 <paramref name="writer"/> 為 <see langword="null"/> 時擲出</exception>
    /// <exception cref="ArgumentOutOfRangeException">當 <paramref name="chunkSize"/> 小於 1 時擲出</exception>
    public int CopyValueTo(IBufferWriter<byte> writer, int chunkSize = 81920)
    {
        if (writer is null)
        {
            throw new ArgumentNullException(nameof(writer));
        }

        if (chunkSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(chunkSize));
        }

        int total = 0;
        while (!_currentValue.IsEmpty && _valueChunkPosition < _currentValue.Length)
        {
            int count = Math.Min(chunkSize, _currentValue.Length - _valueChunkPosition);
            Span<byte> destination = writer.GetSpan(count);
            _currentValue.Slice(_valueChunkPosition, count).CopyTo(destination);
            writer.Advance(count);
            _valueChunkPosition += count;
            total += count;
        }

        return total;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ScanToByte(byte target)
    {
#if NET10_0_OR_GREATER
        if (Vector256.IsHardwareAccelerated && _xml.Length - _position >= Vector256<byte>.Count)
        {
            var targetVec = Vector256.Create(target);
            int limit = _xml.Length - Vector256<byte>.Count;
            while (_position <= limit)
            {
                var vec = Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(_xml), (nuint)_position);
                var equals = Vector256.Equals(vec, targetVec);
                uint mask = equals.ExtractMostSignificantBits();
                if (mask != 0)
                {
                    _position += System.Numerics.BitOperations.TrailingZeroCount(mask);
                    return;
                }
                _position += Vector256<byte>.Count;
            }
        }
#endif
        while (_position < _xml.Length && _xml[_position] != target)
        {
            _position++;
        }
    }

    private static bool IsWhitespace(byte b)
    {
        return b == (byte)' ' || b == (byte)'\t' || b == (byte)'\r' || b == (byte)'\n';
    }

    /// <summary>
    /// 快速比對常見命名空間字首。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetCommonPrefixLength(ReadOnlySpan<byte> name)
    {
        return TryGetKnownPrefix(name, out OdfUtf8KnownPrefix prefix)
            ? prefix.Length + 1
            : GetUnknownPrefixLength(name);
    }

    internal static bool TryResolveKnownQualifiedName(
        ReadOnlySpan<byte> name,
        IReadOnlyDictionary<string, string> namespaces,
        out string prefix,
        out string localName,
        out string namespaceUri)
    {
        prefix = string.Empty;
        localName = string.Empty;
        namespaceUri = string.Empty;

        if (!TryGetKnownPrefix(name, out OdfUtf8KnownPrefix knownPrefix))
            return false;

        prefix = knownPrefix.Prefix;
        namespaceUri = knownPrefix.NamespaceUri;

        if (namespaces.TryGetValue(prefix, out string? declaredNamespaceUri) &&
            !string.Equals(declaredNamespaceUri, namespaceUri, StringComparison.Ordinal))
        {
            return false;
        }

        ReadOnlySpan<byte> localNameSpan = name.Slice(knownPrefix.Length + 1);
        localName = GetUtf8String(localNameSpan);
        return localName.Length > 0;
    }

    internal static bool TryGetKnownQualifiedNameKind(
        ReadOnlySpan<byte> name,
        out OdfUtf8KnownQualifiedName kind)
    {
        kind = OdfUtf8KnownQualifiedName.Unknown;
        uint hash = ComputeKnownNameHash(name);

        switch (hash)
        {
            case 0x4ab3cf08 when name.SequenceEqual("office:document-content"u8):
                kind = OdfUtf8KnownQualifiedName.OfficeDocumentContent;
                return true;
            case 0x317f3721 when name.SequenceEqual("office:body"u8):
                kind = OdfUtf8KnownQualifiedName.OfficeBody;
                return true;
            case 0xe38c3e85 when name.SequenceEqual("office:spreadsheet"u8):
                kind = OdfUtf8KnownQualifiedName.OfficeSpreadsheet;
                return true;
            case 0x91bc257b when name.SequenceEqual("office:version"u8):
                kind = OdfUtf8KnownQualifiedName.OfficeVersion;
                return true;
            case 0x8ab1f851 when name.SequenceEqual("table:table"u8):
                kind = OdfUtf8KnownQualifiedName.TableTable;
                return true;
            case 0xeeeaa630 when name.SequenceEqual("table:table-row"u8):
                kind = OdfUtf8KnownQualifiedName.TableTableRow;
                return true;
            case 0x93d36b28 when name.SequenceEqual("table:table-cell"u8):
                kind = OdfUtf8KnownQualifiedName.TableTableCell;
                return true;
            case 0xa823c57c when name.SequenceEqual("table:name"u8):
                kind = OdfUtf8KnownQualifiedName.TableName;
                return true;
            case 0x15dd6134 when name.SequenceEqual("table:style-name"u8):
                kind = OdfUtf8KnownQualifiedName.TableStyleName;
                return true;
            case 0xc8abce74 when name.SequenceEqual("text:p"u8):
                kind = OdfUtf8KnownQualifiedName.TextP;
                return true;
            case 0x7ebe0a14 when name.SequenceEqual("text:span"u8):
                kind = OdfUtf8KnownQualifiedName.TextSpan;
                return true;
            case 0xb0aba8ac when name.SequenceEqual("text:h"u8):
                kind = OdfUtf8KnownQualifiedName.TextH;
                return true;
            case 0x8ccea19f when name.SequenceEqual("text:style-name"u8):
                kind = OdfUtf8KnownQualifiedName.TextStyleName;
                return true;
            case 0xb8804dfd when name.SequenceEqual("text:outline-level"u8):
                kind = OdfUtf8KnownQualifiedName.TextOutlineLevel;
                return true;
            case 0xb6b82d89 when name.SequenceEqual("style:style"u8):
                kind = OdfUtf8KnownQualifiedName.StyleStyle;
                return true;
            case 0xf3b30a30 when name.SequenceEqual("draw:frame"u8):
                kind = OdfUtf8KnownQualifiedName.DrawFrame;
                return true;
            case 0x76931970 when name.SequenceEqual("draw:image"u8):
                kind = OdfUtf8KnownQualifiedName.DrawImage;
                return true;
            case 0x975cb8e6 when name.SequenceEqual("xlink:href"u8):
                kind = OdfUtf8KnownQualifiedName.XLinkHref;
                return true;
            default:
                return false;
        }
    }

    private static int GetUnknownPrefixLength(ReadOnlySpan<byte> name)
    {
        int colonIdx = name.IndexOf((byte)':');
        return colonIdx >= 0 ? colonIdx + 1 : 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint ComputeKnownNameHash(ReadOnlySpan<byte> name)
    {
        const uint fnvOffsetBasis = 2166136261u;
        const uint fnvPrime = 16777619u;

        uint hash = fnvOffsetBasis;
        foreach (byte b in name)
        {
            hash ^= b;
            hash *= fnvPrime;
        }

        return hash;
    }

    private static bool TryGetKnownPrefix(ReadOnlySpan<byte> name, out OdfUtf8KnownPrefix prefix)
    {
        prefix = default;
        if (name.Length < 4)
            return false;

        switch (name[0])
        {
            case (byte)'c':
                return TryMatchKnownPrefix(name, "chart"u8, "chart", OdfNamespaces.Chart, out prefix) ||
                    TryMatchKnownPrefix(name, "config"u8, "config", OdfNamespaces.Config, out prefix);
            case (byte)'d':
                return TryMatchKnownPrefix(name, "draw"u8, "draw", OdfNamespaces.Draw, out prefix) ||
                    TryMatchKnownPrefix(name, "dc"u8, "dc", OdfNamespaces.Dc, out prefix);
            case (byte)'f':
                return TryMatchKnownPrefix(name, "fo"u8, "fo", OdfNamespaces.Fo, out prefix) ||
                    TryMatchKnownPrefix(name, "form"u8, "form", OdfNamespaces.Form, out prefix);
            case (byte)'m':
                return TryMatchKnownPrefix(name, "meta"u8, "meta", OdfNamespaces.Meta, out prefix);
            case (byte)'n':
                return TryMatchKnownPrefix(name, "number"u8, "number", OdfNamespaces.Number, out prefix);
            case (byte)'o':
                return TryMatchKnownPrefix(name, "office"u8, "office", OdfNamespaces.Office, out prefix);
            case (byte)'p':
                return TryMatchKnownPrefix(name, "presentation"u8, "presentation", OdfNamespaces.Presentation, out prefix);
            case (byte)'s':
                return TryMatchKnownPrefix(name, "style"u8, "style", OdfNamespaces.Style, out prefix) ||
                    TryMatchKnownPrefix(name, "svg"u8, "svg", OdfNamespaces.Svg, out prefix);
            case (byte)'t':
                return TryMatchKnownPrefix(name, "table"u8, "table", OdfNamespaces.Table, out prefix) ||
                    TryMatchKnownPrefix(name, "text"u8, "text", OdfNamespaces.Text, out prefix);
            case (byte)'x':
                return TryMatchKnownPrefix(name, "xlink"u8, "xlink", OdfNamespaces.XLink, out prefix);
            default:
                return false;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryMatchKnownPrefix(
        ReadOnlySpan<byte> name,
        ReadOnlySpan<byte> expectedPrefix,
        string prefixText,
        string namespaceUri,
        out OdfUtf8KnownPrefix prefix)
    {
        prefix = default;
        int prefixLength = expectedPrefix.Length;
        if (name.Length <= prefixLength || name[prefixLength] != (byte)':')
            return false;

        if (!StartsWithPrefix(name, expectedPrefix))
            return false;

        prefix = new OdfUtf8KnownPrefix(prefixText, namespaceUri, prefixLength);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool StartsWithPrefix(ReadOnlySpan<byte> name, ReadOnlySpan<byte> expectedPrefix)
    {
#if NET10_0_OR_GREATER
        if (Vector128.IsHardwareAccelerated && name.Length >= Vector128<byte>.Count)
        {
            Vector128<byte> nameVector = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(name));
            Vector128<byte> prefixVector = CreatePaddedPrefixVector(expectedPrefix);
            uint equalMask = Vector128.Equals(nameVector, prefixVector).ExtractMostSignificantBits();
            uint requiredMask = (1u << expectedPrefix.Length) - 1u;
            return (equalMask & requiredMask) == requiredMask;
        }
#endif

        return name.StartsWith(expectedPrefix);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string GetUtf8String(ReadOnlySpan<byte> bytes)
    {
        return Encoding.UTF8.GetString(
#if NETSTANDARD2_0
            bytes.ToArray()
#else
            bytes
#endif
        );
    }

#if NET10_0_OR_GREATER
    private static Vector128<byte> CreatePaddedPrefixVector(ReadOnlySpan<byte> prefix)
    {
        Span<byte> bytes = stackalloc byte[Vector128<byte>.Count];
        prefix.CopyTo(bytes);
        return Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(bytes));
    }
#endif

    internal static int LastEntityFastDecodeCountForTests;

    /// <summary>
    /// 解碼給定 Span 位元組為 UTF-8 字串，並在必要時進行 XML 實體（Entity）解碼。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string GetStringMaybeDecoded(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
            return string.Empty;

        bool hasEntity = HasEntityChar(bytes);
        if (!hasEntity)
        {
            return Encoding.UTF8.GetString(
#if NETSTANDARD2_0
                bytes.ToArray()
#else
                bytes
#endif
            );
        }

        LastEntityFastDecodeCountForTests++;
        return DecodeXmlEntities(bytes);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool HasEntityChar(ReadOnlySpan<byte> bytes)
    {
        int i = 0;
#if NET10_0_OR_GREATER
        if (Vector256.IsHardwareAccelerated && bytes.Length >= Vector256<byte>.Count)
        {
            var targetVec = Vector256.Create((byte)'&');
            int limit = bytes.Length - Vector256<byte>.Count;
            while (i <= limit)
            {
                var vec = Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(bytes), (nuint)i);
                var equals = Vector256.Equals(vec, targetVec);
                if (equals.ExtractMostSignificantBits() != 0)
                    return true;
                i += Vector256<byte>.Count;
            }
        }
        else if (Vector128.IsHardwareAccelerated && bytes.Length >= Vector128<byte>.Count)
        {
            var targetVec = Vector128.Create((byte)'&');
            int limit = bytes.Length - Vector128<byte>.Count;
            while (i <= limit)
            {
                var vec = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(bytes), (nuint)i);
                var equals = Vector128.Equals(vec, targetVec);
                if (equals.ExtractMostSignificantBits() != 0)
                    return true;
                i += Vector128<byte>.Count;
            }
        }
#endif
        while (i < bytes.Length)
        {
            if (bytes[i] == (byte)'&')
                return true;
            i++;
        }
        return false;
    }

    private static string DecodeXmlEntities(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
            return string.Empty;

        var builder = new StringBuilder(bytes.Length);
        int segmentStart = 0;
        int index = 0;
        while (index < bytes.Length)
        {
            if (bytes[index] != (byte)'&')
            {
                index++;
                continue;
            }

            if (!TryDecodeXmlEntity(bytes.Slice(index), out string? decoded, out int consumed))
            {
                index++;
                continue;
            }

            AppendUtf8(builder, bytes.Slice(segmentStart, index - segmentStart));
            builder.Append(decoded);
            index += consumed;
            segmentStart = index;
        }

        AppendUtf8(builder, bytes.Slice(segmentStart));
        return builder.ToString();
    }

    private static bool TryDecodeXmlEntity(ReadOnlySpan<byte> bytes, out string? decoded, out int consumed)
    {
        decoded = null;
        consumed = 0;

        if (bytes.StartsWith("&amp;"u8))
        {
            decoded = "&";
            consumed = 5;
            return true;
        }

        if (bytes.StartsWith("&lt;"u8))
        {
            decoded = "<";
            consumed = 4;
            return true;
        }

        if (bytes.StartsWith("&gt;"u8))
        {
            decoded = ">";
            consumed = 4;
            return true;
        }

        if (bytes.StartsWith("&quot;"u8))
        {
            decoded = "\"";
            consumed = 6;
            return true;
        }

        if (bytes.StartsWith("&apos;"u8) || bytes.StartsWith("&APOS;"u8))
        {
            decoded = "'";
            consumed = 6;
            return true;
        }

        if (TryDecodeNumericEntity(bytes, out decoded, out consumed))
        {
            return true;
        }

        return false;
    }

    private static bool TryDecodeNumericEntity(ReadOnlySpan<byte> bytes, out string? decoded, out int consumed)
    {
        decoded = null;
        consumed = 0;
        if (bytes.Length < 4 || bytes[0] != (byte)'&' || bytes[1] != (byte)'#')
        {
            return false;
        }

        bool hex = bytes.Length > 4 && (bytes[2] == (byte)'x' || bytes[2] == (byte)'X');
        int index = hex ? 3 : 2;
        int value = 0;
        int digits = 0;
        while (index < bytes.Length && bytes[index] != (byte)';')
        {
            int digit = hex ? GetHexDigit(bytes[index]) : GetDecimalDigit(bytes[index]);
            if (digit < 0)
            {
                return false;
            }

            value = (value * (hex ? 16 : 10)) + digit;
            if (value > 0x10FFFF)
            {
                return false;
            }

            index++;
            digits++;
        }

        if (digits == 0 || index >= bytes.Length || bytes[index] != (byte)';')
        {
            return false;
        }

        if (value is >= 0xD800 and <= 0xDFFF)
        {
            return false;
        }

        decoded = char.ConvertFromUtf32(value);
        consumed = index + 1;
        return true;
    }

    private static int GetDecimalDigit(byte value)
        => value is >= (byte)'0' and <= (byte)'9' ? value - (byte)'0' : -1;

    private static int GetHexDigit(byte value)
    {
        if (value is >= (byte)'0' and <= (byte)'9')
        {
            return value - (byte)'0';
        }

        if (value is >= (byte)'a' and <= (byte)'f')
        {
            return value - (byte)'a' + 10;
        }

        if (value is >= (byte)'A' and <= (byte)'F')
        {
            return value - (byte)'A' + 10;
        }

        return -1;
    }

    private static void AppendUtf8(StringBuilder builder, ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
        {
            return;
        }

        builder.Append(Encoding.UTF8.GetString(
#if NETSTANDARD2_0
            bytes.ToArray()
#else
            bytes
#endif
        ));
    }
}

internal readonly struct OdfUtf8KnownPrefix
{
    public OdfUtf8KnownPrefix(string prefix, string namespaceUri, int length)
    {
        Prefix = prefix;
        NamespaceUri = namespaceUri;
        Length = length;
    }

    public string Prefix { get; }

    public string NamespaceUri { get; }

    public int Length { get; }
}

internal enum OdfUtf8KnownQualifiedName
{
    Unknown = 0,
    OfficeDocumentContent,
    OfficeBody,
    OfficeSpreadsheet,
    OfficeVersion,
    TableTable,
    TableTableRow,
    TableTableCell,
    TableName,
    TableStyleName,
    TextP,
    TextSpan,
    TextH,
    TextStyleName,
    TextOutlineLevel,
    StyleStyle,
    DrawFrame,
    DrawImage,
    XLinkHref
}

/// <summary>
/// XML 標記種類。
/// </summary>
public enum OdfUtf8XmlTokenKind
{
    /// <summary>
    /// 開始元素。
    /// </summary>
    StartElement,
    /// <summary>
    /// 自我結束元素。
    /// </summary>
    SelfClosingElement,
    /// <summary>
    /// 結束元素。
    /// </summary>
    EndElement,
    /// <summary>
    /// 文字內容。
    /// </summary>
    Text,
    /// <summary>
    /// 處理指令。
    /// </summary>
    ProcessingInstruction,
    /// <summary>
    /// 註解。
    /// </summary>
    Comment
}

/// <summary>
/// 輕量化的唯讀 XML 標記結構。
/// </summary>
public readonly ref struct OdfUtf8XmlToken
{
    /// <summary>
    /// 取得標記的種類。
    /// </summary>
    public OdfUtf8XmlTokenKind Kind { get; }

    /// <summary>
    /// 取得標記名稱的 UTF-8 位元組 Span。
    /// </summary>
    public ReadOnlySpan<byte> Name { get; }

    /// <summary>
    /// 取得標記屬性的 UTF-8 位元組 Span。
    /// </summary>
    public ReadOnlySpan<byte> Attributes { get; }

    /// <summary>
    /// 取得標記在原始 XML 緩衝區中的起始偏移量。
    /// </summary>
    public int Offset { get; }

    /// <summary>
    /// 取得標記在原始 XML 緩衝區中的位元組長度。
    /// </summary>
    public int Length { get; }

    /// <summary>
    /// 取得標記值的 UTF-8 位元組 Span。
    /// </summary>
    public ReadOnlySpan<byte> Value => Name;

    internal OdfUtf8XmlToken(OdfUtf8XmlTokenKind kind, ReadOnlySpan<byte> name, ReadOnlySpan<byte> attributes = default, int offset = 0, int length = 0)
    {
        Kind = kind;
        Name = name;
        Attributes = attributes;
        Offset = offset;
        Length = length;
    }

    /// <summary>
    /// 取得標記名稱的字串形式。
    /// </summary>
    /// <returns>標記名稱字串</returns>
    public string GetNameString() => Encoding.UTF8.GetString(Name.ToArray());

    /// <summary>
    /// 取得標記值的字串形式。
    /// </summary>
    /// <returns>標記值字串</returns>
    public string GetValueString() => Encoding.UTF8.GetString(Value.ToArray());
}
