using System;

using OdfKit.Compliance;
namespace OdfKit.DOM;

/// <summary>
/// 表示 ODF schema 中名為 <c>languageCode</c> 的語言代碼 lexical form。
/// </summary>
public readonly struct OdfLanguageCode : IEquatable<OdfLanguageCode>
{
    /// <summary>
    /// 以語言代碼 lexical form 建立 <see cref="OdfLanguageCode"/>。
    /// </summary>
    /// <param name="value">語言代碼，例如 <c>en</c> 或 <c>zh</c>。</param>
    /// <exception cref="ArgumentException">當語言代碼不符合 ODF <c>languageCode</c> 格式時擲回。</exception>
    public OdfLanguageCode(string value)
    {
        if (!IsValid(value))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfLanguageCode_LanguageCode18"), nameof(value));
        }

        Value = value;
    }

    /// <summary>
    /// 取得原始語言代碼字串。
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// 嘗試解析語言代碼。
    /// </summary>
    /// <param name="value">語言代碼字串。</param>
    /// <param name="languageCode">成功時傳回解析後的語言代碼。</param>
    /// <returns>若字串符合 ODF <c>languageCode</c> 格式則為 <see langword="true"/>。</returns>
    public static bool TryParse(string? value, out OdfLanguageCode languageCode)
    {
        if (IsValid(value))
        {
            languageCode = new OdfLanguageCode(value!);
            return true;
        }

        languageCode = default;
        return false;
    }

    /// <summary>
    /// 傳回原始語言代碼字串。
    /// </summary>
    /// <returns>語言代碼字串。</returns>
    public override string ToString() => Value ?? string.Empty;

    /// <summary>
    /// 判斷目前值是否等於另一個語言代碼。
    /// </summary>
    /// <param name="other">要比較的語言代碼。</param>
    /// <returns>若 lexical form 相同則為 <see langword="true"/>。</returns>
    public bool Equals(OdfLanguageCode other) => string.Equals(Value, other.Value, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is OdfLanguageCode other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);

    /// <summary>
    /// 判斷兩個語言代碼是否相等。
    /// </summary>
    /// <param name="left">左側語言代碼。</param>
    /// <param name="right">右側語言代碼。</param>
    /// <returns>若兩者相等則為 <see langword="true"/>。</returns>
    public static bool operator ==(OdfLanguageCode left, OdfLanguageCode right) => left.Equals(right);

    /// <summary>
    /// 判斷兩個語言代碼是否不相等。
    /// </summary>
    /// <param name="left">左側語言代碼。</param>
    /// <param name="right">右側語言代碼。</param>
    /// <returns>若兩者不相等則為 <see langword="true"/>。</returns>
    public static bool operator !=(OdfLanguageCode left, OdfLanguageCode right) => !left.Equals(right);

    private static bool IsValid(string? value)
    {
        if (value is null || value.Length is < 1 or > 8)
        {
            return false;
        }

        foreach (char ch in value)
        {
            if (!IsAsciiLetter(ch))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsAsciiLetter(char ch) => ch is >= 'A' and <= 'Z' or >= 'a' and <= 'z';
}
