using System;

namespace OdfKit.DOM;

/// <summary>
/// 表示 ODF schema 中名為 <c>countryCode</c> 的國別代碼 lexical form。
/// </summary>
public readonly struct OdfCountryCode : IEquatable<OdfCountryCode>
{
    /// <summary>
    /// 以國別代碼 lexical form 建立 <see cref="OdfCountryCode"/>。
    /// </summary>
    /// <param name="value">國別代碼，例如 <c>US</c> 或 <c>TW</c>。</param>
    /// <exception cref="ArgumentException">當國別代碼不符合 ODF <c>countryCode</c> 格式時擲回。</exception>
    public OdfCountryCode(string value)
    {
        if (!IsValid(value))
        {
            throw new ArgumentException("國別代碼必須是 1 到 8 個英文字母或數字。", nameof(value));
        }

        Value = value;
    }

    /// <summary>
    /// 取得原始國別代碼字串。
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// 嘗試解析國別代碼。
    /// </summary>
    /// <param name="value">國別代碼字串。</param>
    /// <param name="countryCode">成功時傳回解析後的國別代碼。</param>
    /// <returns>若字串符合 ODF <c>countryCode</c> 格式則為 <see langword="true"/>。</returns>
    public static bool TryParse(string? value, out OdfCountryCode countryCode)
    {
        if (IsValid(value))
        {
            countryCode = new OdfCountryCode(value!);
            return true;
        }

        countryCode = default;
        return false;
    }

    /// <summary>
    /// 傳回原始國別代碼字串。
    /// </summary>
    /// <returns>國別代碼字串。</returns>
    public override string ToString() => Value ?? string.Empty;

    /// <summary>
    /// 判斷目前值是否等於另一個國別代碼。
    /// </summary>
    /// <param name="other">要比較的國別代碼。</param>
    /// <returns>若 lexical form 相同則為 <see langword="true"/>。</returns>
    public bool Equals(OdfCountryCode other) => string.Equals(Value, other.Value, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is OdfCountryCode other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);

    /// <summary>
    /// 判斷兩個國別代碼是否相等。
    /// </summary>
    /// <param name="left">左側國別代碼。</param>
    /// <param name="right">右側國別代碼。</param>
    /// <returns>若兩者相等則為 <see langword="true"/>。</returns>
    public static bool operator ==(OdfCountryCode left, OdfCountryCode right) => left.Equals(right);

    /// <summary>
    /// 判斷兩個國別代碼是否不相等。
    /// </summary>
    /// <param name="left">左側國別代碼。</param>
    /// <param name="right">右側國別代碼。</param>
    /// <returns>若兩者不相等則為 <see langword="true"/>。</returns>
    public static bool operator !=(OdfCountryCode left, OdfCountryCode right) => !left.Equals(right);

    internal static bool IsAlphaNumericCode(string? value)
    {
        if (value is null || value.Length is < 1 or > 8)
        {
            return false;
        }

        foreach (char ch in value)
        {
            if (!OdfTokenCharacters.IsAsciiLetterOrDigit(ch))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsValid(string? value) => IsAlphaNumericCode(value);
}
