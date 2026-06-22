using System;
using System.Globalization;

using OdfKit.Compliance;
namespace OdfKit.DOM;

/// <summary>
/// 表示 ODF schema 中名為 <c>character</c> 的單一字元 lexical form。
/// </summary>
public readonly struct OdfCharacter : IEquatable<OdfCharacter>
{
    /// <summary>
    /// 以單一字元 lexical form 建立 <see cref="OdfCharacter"/>。
    /// </summary>
    /// <param name="value">單一字元字串。</param>
    /// <exception cref="ArgumentException">當字串不是單一文字元素時擲回。</exception>
    public OdfCharacter(string value)
    {
        if (!IsValid(value))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfCharacter_CharacterValueSingleText"), nameof(value));
        }

        Value = value;
    }

    /// <summary>
    /// 取得原始字元字串。
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// 嘗試解析單一字元字串。
    /// </summary>
    /// <param name="value">字元字串。</param>
    /// <param name="character">成功時傳回解析後的字元。</param>
    /// <returns>若字串是單一文字元素則為 <see langword="true"/>。</returns>
    public static bool TryParse(string? value, out OdfCharacter character)
    {
        if (IsValid(value))
        {
            character = new OdfCharacter(value!);
            return true;
        }

        character = default;
        return false;
    }

    /// <summary>
    /// 傳回原始字元字串。
    /// </summary>
    /// <returns>字元字串。</returns>
    public override string ToString() => Value ?? string.Empty;

    /// <summary>
    /// 判斷目前值是否等於另一個字元。
    /// </summary>
    /// <param name="other">要比較的字元。</param>
    /// <returns>若 lexical form 相同則為 <see langword="true"/>。</returns>
    public bool Equals(OdfCharacter other) => string.Equals(Value, other.Value, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is OdfCharacter other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);

    /// <summary>
    /// 判斷兩個字元是否相等。
    /// </summary>
    /// <param name="left">左側字元。</param>
    /// <param name="right">右側字元。</param>
    /// <returns>若兩者相等則為 <see langword="true"/>。</returns>
    public static bool operator ==(OdfCharacter left, OdfCharacter right) => left.Equals(right);

    /// <summary>
    /// 判斷兩個字元是否不相等。
    /// </summary>
    /// <param name="left">左側字元。</param>
    /// <param name="right">右側字元。</param>
    /// <returns>若兩者不相等則為 <see langword="true"/>。</returns>
    public static bool operator !=(OdfCharacter left, OdfCharacter right) => !left.Equals(right);

    private static bool IsValid(string? value)
    {
        if (value is null || value.Length == 0 || new StringInfo(value).LengthInTextElements != 1)
        {
            return false;
        }

        foreach (char ch in value)
        {
            if (char.IsControl(ch))
            {
                return false;
            }
        }

        return true;
    }
}
