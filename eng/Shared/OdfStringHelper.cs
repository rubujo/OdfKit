using System;

namespace OdfKit.Internal;

/// <summary>
/// Provides allocation-free string operations shared by all supported target frameworks.
/// 提供所有受支援目標框架共用的無配置字串操作。
/// </summary>
internal static class OdfStringHelper
{
    private static readonly char[] DrawingValueSeparators = { ' ', ',', ';', '\t', '\r', '\n' };
    private static readonly char[] DrawingArgumentSeparators = { ' ', ',', ';', '\t' };
    private static readonly char[] SpaceSeparator = { ' ' };
    private static readonly string[] LineSeparators = { "\r\n", "\r", "\n" };
    private static readonly string[] InKeywordSeparator = { " in " };

    /// <summary>Finds a directory separator. / 尋找目錄分隔符號。</summary>
    public static int IndexOfDirectorySeparator(string value)
    {
        int slashIndex = value.IndexOf('/');
        int backslashIndex = value.IndexOf('\\');
        if (slashIndex < 0)
        {
            return backslashIndex;
        }

        return backslashIndex < 0 ? slashIndex : Math.Min(slashIndex, backslashIndex);
    }

    /// <summary>Splits a drawing value list. / 分割繪圖值清單。</summary>
    public static string[] SplitDrawingValues(string value) =>
        value.Split(DrawingValueSeparators, StringSplitOptions.RemoveEmptyEntries);

    /// <summary>Splits a drawing argument list. / 分割繪圖引數清單。</summary>
    public static string[] SplitDrawingArguments(string value) =>
        value.Split(DrawingArgumentSeparators, StringSplitOptions.RemoveEmptyEntries);

    /// <summary>Splits space-delimited values. / 分割空白分隔值。</summary>
    public static string[] SplitSpaces(string value) =>
        value.Split(SpaceSeparator, StringSplitOptions.RemoveEmptyEntries);

    /// <summary>Splits text into lines while retaining empty entries. / 將文字分割為行並保留空項目。</summary>
    public static string[] SplitLines(string value) => value.Split(LineSeparators, StringSplitOptions.None);

    /// <summary>Splits an expression around the in keyword. / 以 in 關鍵字分割運算式。</summary>
    public static string[] SplitInExpression(string value) => value.Split(InKeywordSeparator, StringSplitOptions.None);

    /// <summary>
    /// Determines whether a value contains another string with explicit comparison semantics.
    /// 使用明確的比較語意判斷字串是否包含另一字串。
    /// </summary>
    public static bool Contains(string value, string candidate, StringComparison comparison)
    {
#if NET6_0_OR_GREATER
        return value.Contains(candidate, comparison);
#else
        return value.IndexOf(candidate, comparison) >= 0;
#endif
    }

    /// <summary>
    /// Creates a stable short identifier from a prefix and a new GUID.
    /// 使用前綴與新 GUID 建立穩定的短識別碼。
    /// </summary>
    public static string CreatePrefixedGuid(string prefix)
    {
        string value = Guid.NewGuid().ToString("N");
#if NET6_0_OR_GREATER
        return string.Concat(prefix, value.AsSpan(0, 8));
#else
        return prefix + value.Substring(0, 8);
#endif
    }

    /// <summary>
    /// Replaces a segment while retaining the text before and after it.
    /// 取代一段文字並保留其前後內容。
    /// </summary>
    public static string ReplaceSegment(string value, int startIndex, int endIndex, string replacement)
    {
#if NET6_0_OR_GREATER
        return string.Concat(value.AsSpan(0, startIndex), replacement, value.AsSpan(endIndex));
#else
        return value.Substring(0, startIndex) + replacement + value.Substring(endIndex);
#endif
    }

    /// <summary>
    /// Concatenates a prefix with a suffix that begins at the specified index.
    /// 串接前綴與從指定位置開始的後綴。
    /// </summary>
    public static string ConcatSuffix(string prefix, string value, int startIndex)
    {
#if NET6_0_OR_GREATER
        return string.Concat(prefix, value.AsSpan(startIndex));
#else
        return prefix + value.Substring(startIndex);
#endif
    }

    /// <summary>Concatenates a prefix with a bounded segment. / 串接前綴與指定範圍的區段。</summary>
    public static string ConcatSegment(string prefix, string value, int startIndex, int length)
    {
#if NET6_0_OR_GREATER
        return string.Concat(prefix, value.AsSpan(startIndex, length));
#else
        return prefix + value.Substring(startIndex, length);
#endif
    }

    /// <summary>
    /// Determines whether a string starts with the specified UTF-16 code unit.
    /// 判斷字串是否以指定的 UTF-16 碼元開頭。
    /// </summary>
    /// <param name="value">The string to inspect. / 要檢查的字串。</param>
    /// <param name="character">The character to match. / 要比對的字元。</param>
    /// <returns>True when the first character matches. / 第一個字元相符時為 true。</returns>
    public static bool StartsWith(string value, char character)
    {
        return value.Length != 0 && value[0] == character;
    }

    /// <summary>
    /// Determines whether a string ends with the specified UTF-16 code unit.
    /// 判斷字串是否以指定的 UTF-16 碼元結尾。
    /// </summary>
    /// <param name="value">The string to inspect. / 要檢查的字串。</param>
    /// <param name="character">The character to match. / 要比對的字元。</param>
    /// <returns>True when the last character matches. / 最後一個字元相符時為 true。</returns>
    public static bool EndsWith(string value, char character)
    {
        return value.Length != 0 && value[value.Length - 1] == character;
    }
}
