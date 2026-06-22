using System;

using OdfKit.Compliance;
namespace OdfKit.DOM;

/// <summary>
/// 表示 ODF schema 中名為 <c>textEncoding</c> 的文字編碼名稱 lexical form。
/// </summary>
public readonly struct OdfTextEncoding : IEquatable<OdfTextEncoding>
{
    /// <summary>
    /// 以文字編碼名稱 lexical form 建立 <see cref="OdfTextEncoding"/>。
    /// </summary>
    /// <param name="value">文字編碼名稱，例如 <c>UTF-8</c> 或 <c>windows-1252</c>。</param>
    /// <exception cref="ArgumentException">當文字編碼名稱不符合 ODF <c>textEncoding</c> 格式時擲回。</exception>
    public OdfTextEncoding(string value)
    {
        if (!IsValid(value))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfTextEncoding_TextEncodingNameStart"), nameof(value));
        }

        Value = value;
    }

    /// <summary>
    /// 取得原始文字編碼名稱。
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// 嘗試解析文字編碼名稱。
    /// </summary>
    /// <param name="value">文字編碼名稱字串。</param>
    /// <param name="textEncoding">成功時傳回解析後的文字編碼名稱。</param>
    /// <returns>若字串符合 ODF <c>textEncoding</c> 格式則為 <see langword="true"/>。</returns>
    public static bool TryParse(string? value, out OdfTextEncoding textEncoding)
    {
        if (IsValid(value))
        {
            textEncoding = new OdfTextEncoding(value!);
            return true;
        }

        textEncoding = default;
        return false;
    }

    /// <summary>
    /// 傳回原始文字編碼名稱。
    /// </summary>
    /// <returns>文字編碼名稱。</returns>
    public override string ToString() => Value ?? string.Empty;

    /// <summary>
    /// 判斷目前值是否等於另一個文字編碼名稱。
    /// </summary>
    /// <param name="other">要比較的文字編碼名稱。</param>
    /// <returns>若 lexical form 相同則為 <see langword="true"/>。</returns>
    public bool Equals(OdfTextEncoding other) => string.Equals(Value, other.Value, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is OdfTextEncoding other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);

    /// <summary>
    /// 判斷兩個文字編碼名稱是否相等。
    /// </summary>
    /// <param name="left">左側文字編碼名稱。</param>
    /// <param name="right">右側文字編碼名稱。</param>
    /// <returns>若兩者相等則為 <see langword="true"/>。</returns>
    public static bool operator ==(OdfTextEncoding left, OdfTextEncoding right) => left.Equals(right);

    /// <summary>
    /// 判斷兩個文字編碼名稱是否不相等。
    /// </summary>
    /// <param name="left">左側文字編碼名稱。</param>
    /// <param name="right">右側文字編碼名稱。</param>
    /// <returns>若兩者不相等則為 <see langword="true"/>。</returns>
    public static bool operator !=(OdfTextEncoding left, OdfTextEncoding right) => !left.Equals(right);

    private static bool IsValid(string? value)
    {
        if (value is null || value.Length == 0 || !IsAsciiLetter(value[0]))
        {
            return false;
        }

        for (int index = 1; index < value.Length; index++)
        {
            char ch = value[index];
            if (!OdfTokenCharacters.IsAsciiLetterOrDigit(ch) && ch is not '.' and not '_' and not '-')
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsAsciiLetter(char ch) => ch is >= 'A' and <= 'Z' or >= 'a' and <= 'z';
}
