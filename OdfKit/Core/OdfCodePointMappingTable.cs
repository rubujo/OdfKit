using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using OdfKit.Compliance;

namespace OdfKit.Core;

/// <summary>
/// Provides format-neutral code point mapping table helpers.
/// 提供與格式無關的碼位對照表輔助功能。
/// </summary>
/// <remarks>
/// Use <see cref="ParseDelimitedHex"/> for line-based tables of delimited hexadecimal pairs
/// (Unicode.org vendor mapping files, UCD-style lists), <see cref="Parse"/> with a line parser
/// delegate for custom line formats, and JSON or spreadsheet sources (for example the Japanese
/// MJ shrink map) should be converted by the caller with a dedicated deserializer. Parsed values
/// are not validated as Unicode scalars because targets may be non-Unicode codes (for example Big5).
/// Both parsers enforce a documented resource budget: at most 4,096 characters per line and
/// 2,000,000 entries per table; exceeding either throws <see cref="FormatException"/>. Mapping
/// A bounded buffered reader rejects overlong lines before allocating their complete contents.
/// 行式的十六進位對照表（Unicode.org 官方對照檔、UCD 式清單）用 <see cref="ParseDelimitedHex"/>；
/// 自訂行格式用帶委派的 <see cref="Parse"/>；JSON 或試算表來源（例如日本 MJ 縮退對照）請由呼叫端
/// 以對應的反序列化器自行轉換。解析值不做 Unicode 純量驗證，因為目標值可能是非 Unicode 碼（例如 Big5）。
/// 兩個解析方法皆施行文件化的資源預算：每行至多 4,096 字元、每表至多 2,000,000 筆，超出即擲出
/// <see cref="FormatException"/>。解析器使用受限緩衝讀取，在配置完整資料行前即拒絕超長輸入。
/// </remarks>
public static class OdfCodePointMappingTable
{
    // 資源預算：對照表資料行實際多在百餘字元內、全字庫全集約 10.5 萬筆，
    // 上限取極寬裕值，僅為阻斷無上限輸入的資源耗損（見 docs/security-limits.md 的入口原則）。
    internal const int MaxLineLength = 4_096;
    internal const int MaxEntryCount = 2_000_000;

    // 例外訊息回帶原始行時的截斷長度：避免巨量資料行整段進入例外與日誌。
    private const int MaxLineInMessageLength = 64;

    /// <summary>
    /// Parses a line-based table of delimited hexadecimal pairs.
    /// 解析以分隔字元隔開之十六進位對的行式對照表。
    /// </summary>
    /// <remarks>
    /// Lines are trimmed and content after <c>#</c> is treated as a comment; blank lines are skipped.
    /// Each remaining line must contain at least two delimited fields; fields beyond the second are
    /// ignored (Unicode.org mapping files carry a name comment there). Fields may use an optional
    /// <c>0x</c> or <c>U+</c> prefix. Duplicate keys keep the last value. Range syntax such as
    /// <c>XXXX..YYYY</c> is not supported.
    /// 每行先修剪空白並將 <c>#</c> 之後視為註解；空行略過。其餘每行至少須有兩個分隔欄位，
    /// 第二欄之後忽略（Unicode.org 對照檔在該處放名稱註解）。欄位可帶選用的 <c>0x</c> 或
    /// <c>U+</c> 前綴。重複鍵保留最後一筆。不支援 <c>XXXX..YYYY</c> 範圍語法。
    /// </remarks>
    /// <param name="reader">The table content reader. / 對照表內容的讀取器。</param>
    /// <param name="separator">The field separator, for example tab or semicolon. / 欄位分隔字元，例如定位或分號。</param>
    /// <returns>The first-field-to-second-field mapping. / 第一欄值至第二欄值的對應表。</returns>
    /// <exception cref="ArgumentNullException">當 <paramref name="reader"/> 為 <see langword="null"/> 時擲出</exception>
    /// <exception cref="FormatException">當資料行不符合分隔十六進位格式，或超出行長／筆數資源預算時擲出</exception>
    public static IReadOnlyDictionary<int, int> ParseDelimitedHex(TextReader reader, char separator)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(reader, nameof(reader));

        char[] separators = [separator];
        var result = new Dictionary<int, int>();
        int parsedEntryCount = 0;
        var lineReader = new OdfBoundedLineReader(reader);
        string? originalLine;
        while ((originalLine = lineReader.ReadLine()) is not null)
        {
            string line = StripComment(originalLine).Trim();
            if (line.Length == 0)
            {
                continue;
            }

            string[] fields = line.Split(separators, StringSplitOptions.None);
            if (fields.Length < 2 ||
                !TryParseHexField(fields[0], out int key) ||
                !TryParseHexField(fields[1], out int value))
            {
                throw new FormatException(
                    OdfLocalizer.GetMessage("Err_OdfCodePointMappingTable_InvalidLine", FormatLineForMessage(originalLine)));
            }

            result[key] = value;
            parsedEntryCount++;
            EnsureEntryBudget(parsedEntryCount);
        }

        return result;
    }

    /// <summary>
    /// Parses a line-based table with a caller-supplied line parser.
    /// 以呼叫端提供的行解析委派解析行式對照表。
    /// </summary>
    /// <remarks>
    /// Blank lines are skipped; every other line is passed verbatim to <paramref name="lineParser"/>.
    /// Return <see langword="null"/> to skip a line (for example comments); exceptions thrown by the
    /// delegate propagate unchanged. Duplicate keys keep the last value.
    /// 空行會被略過；其餘每行原樣交給 <paramref name="lineParser"/>。回傳 <see langword="null"/>
    /// 可略過該行（例如註解）；委派擲出的例外會原樣傳遞。重複鍵保留最後一筆。
    /// </remarks>
    /// <param name="reader">The table content reader. / 對照表內容的讀取器。</param>
    /// <param name="lineParser">The per-line parser returning a mapping pair or null. / 逐行解析並回傳對應項或 null 的委派。</param>
    /// <returns>The parsed mapping. / 解析所得的對應表。</returns>
    /// <exception cref="ArgumentNullException">當 <paramref name="reader"/> 或 <paramref name="lineParser"/> 為 <see langword="null"/> 時擲出</exception>
    /// <exception cref="FormatException">當資料行超出行長／筆數資源預算時擲出</exception>
    public static IReadOnlyDictionary<int, int> Parse(TextReader reader, Func<string, KeyValuePair<int, int>?> lineParser)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(reader, nameof(reader));

        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(lineParser, nameof(lineParser));

        var result = new Dictionary<int, int>();
        int parsedEntryCount = 0;
        var boundedReader = new OdfBoundedLineReader(reader);
        string? line;
        while ((line = boundedReader.ReadLine()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (lineParser(line) is KeyValuePair<int, int> pair)
            {
                result[pair.Key] = pair.Value;
                parsedEntryCount++;
                EnsureEntryBudget(parsedEntryCount);
            }
        }

        return result;
    }

    /// <summary>
    /// Joins two mappings by their shared string keys.
    /// 依共用的字串鍵聯結兩份對照表。
    /// </summary>
    /// <param name="keyToSource">The key-to-source-value mapping. / 鍵至來源值的對應表。</param>
    /// <param name="keyToTarget">The key-to-target-value mapping. / 鍵至目標值的對應表。</param>
    /// <returns>A source-value-to-target-value mapping for shared keys. / 共用鍵所形成的來源值至目標值對應表。</returns>
    /// <exception cref="ArgumentNullException">當任一對應表為 <see langword="null"/> 時擲出</exception>
    public static IReadOnlyDictionary<int, int> Join(
        IReadOnlyDictionary<string, int> keyToSource,
        IReadOnlyDictionary<string, int> keyToTarget)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(keyToSource, nameof(keyToSource));

        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(keyToTarget, nameof(keyToTarget));

        var result = new Dictionary<int, int>();
        foreach (KeyValuePair<string, int> source in keyToSource)
        {
            if (keyToTarget.TryGetValue(source.Key, out int target))
            {
                result[source.Value] = target;
            }
        }

        return result;
    }

    /// <summary>
    /// 檢查資料行長度是否在資源預算內；超出即擲出 FormatException。
    /// </summary>
    internal static void EnsureLineLength(string line)
    {
        if (line.Length > MaxLineLength)
        {
            throw new FormatException(
                OdfLocalizer.GetMessage("Err_OdfCodePointMappingTable_LineTooLong", MaxLineLength));
        }
    }

    /// <summary>
    /// 檢查累計項目數是否在資源預算內；超出即擲出 FormatException。
    /// </summary>
    internal static void EnsureEntryBudget(int entryCount)
    {
        if (entryCount > MaxEntryCount)
        {
            throw new FormatException(
                OdfLocalizer.GetMessage("Err_OdfCodePointMappingTable_TooManyEntries", MaxEntryCount));
        }
    }

    /// <summary>
    /// 將原始資料行整理為適合放入例外訊息的形式：截斷至上限並以空格取代控制字元，避免日誌注入與巨量訊息。
    /// </summary>
    internal static string FormatLineForMessage(string line)
    {
        int length = Math.Min(line.Length, MaxLineInMessageLength);
        var builder = new StringBuilder(length + 1);
        for (int i = 0; i < length; i++)
        {
            char ch = line[i];
            builder.Append(ch < ' ' ? ' ' : ch);
        }

        if (line.Length > MaxLineInMessageLength)
        {
            builder.Append('…');
        }

        return builder.ToString();
    }

    private static string StripComment(string line)
    {
        int commentIndex = line.IndexOf('#');
        return commentIndex < 0 ? line : line.Substring(0, commentIndex);
    }

    private static bool TryParseHexField(string field, out int value)
    {
        string text = field.Trim();
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ||
            text.StartsWith("U+", StringComparison.OrdinalIgnoreCase))
        {
            text = text.Substring(2);
        }

        // 8 位十六進位（如 FFFFFFFF）會溢位為負值，一律視為無效輸入，避免負「碼位」流入字典。
        return int.TryParse(text, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out value) &&
            value >= 0;
    }
}
