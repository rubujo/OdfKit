using System;
using System.Text.RegularExpressions;

namespace OdfKit.DOM;

/// <summary>
/// 表示 XML Schema <c>language</c> datatype 的語言標記 lexical form。
/// </summary>
public readonly struct OdfLanguageTag : IEquatable<OdfLanguageTag>
{
    private static readonly Regex LanguageRegex = new(
        @"^[A-Za-z]{1,8}(?:-[A-Za-z0-9]{1,8})*$",
        RegexOptions.CultureInvariant);

    /// <summary>
    /// 以語言標記 lexical form 建立 <see cref="OdfLanguageTag"/>。
    /// </summary>
    /// <param name="value">語言標記，例如 <c>en-US</c> 或 <c>zh-Hant-TW</c>。</param>
    /// <exception cref="ArgumentException">當語言標記不符合 XML Schema <c>language</c> 格式時擲回。</exception>
    public OdfLanguageTag(string value)
    {
        if (!IsValid(value))
        {
            throw new ArgumentException("語言標記必須符合 XML Schema language 格式。", nameof(value));
        }

        Value = value;
    }

    /// <summary>
    /// 取得原始語言標記字串。
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// 嘗試解析語言標記。
    /// </summary>
    /// <param name="value">語言標記字串。</param>
    /// <param name="languageTag">成功時傳回解析後的語言標記。</param>
    /// <returns>若字串符合 XML Schema <c>language</c> 格式則為 <see langword="true"/>。</returns>
    public static bool TryParse(string? value, out OdfLanguageTag languageTag)
    {
        if (IsValid(value))
        {
            languageTag = new OdfLanguageTag(value!);
            return true;
        }

        languageTag = default;
        return false;
    }

    /// <summary>
    /// 傳回原始語言標記字串。
    /// </summary>
    /// <returns>語言標記字串。</returns>
    public override string ToString() => Value ?? string.Empty;

    /// <summary>
    /// 判斷目前值是否等於另一個語言標記。
    /// </summary>
    /// <param name="other">要比較的語言標記。</param>
    /// <returns>若 lexical form 相同則為 <see langword="true"/>。</returns>
    public bool Equals(OdfLanguageTag other) => string.Equals(Value, other.Value, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is OdfLanguageTag other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);

    /// <summary>
    /// 判斷兩個語言標記是否相等。
    /// </summary>
    /// <param name="left">左側語言標記。</param>
    /// <param name="right">右側語言標記。</param>
    /// <returns>若兩者相等則為 <see langword="true"/>。</returns>
    public static bool operator ==(OdfLanguageTag left, OdfLanguageTag right) => left.Equals(right);

    /// <summary>
    /// 判斷兩個語言標記是否不相等。
    /// </summary>
    /// <param name="left">左側語言標記。</param>
    /// <param name="right">右側語言標記。</param>
    /// <returns>若兩者不相等則為 <see langword="true"/>。</returns>
    public static bool operator !=(OdfLanguageTag left, OdfLanguageTag right) => !left.Equals(right);

    private static bool IsValid(string? value) => value is not null && LanguageRegex.IsMatch(value);
}
