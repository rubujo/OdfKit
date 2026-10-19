using System;

using OdfKit.Compliance;
namespace OdfKit.DOM;

/// <summary>
/// Represents the single XML character lexical form named <c>character</c> in ODF schemas.
/// 表示 ODF schema 中名為 <c>character</c> 的單一 XML 字元詞法形式。
/// </summary>
public readonly struct OdfCharacter : IEquatable<OdfCharacter>
{
    /// <summary>
    /// Creates a single XML character value.
    /// 建立單一 XML 字元值。
    /// </summary>
    /// <param name="value">One XML character, including a valid surrogate pair. / 單一 XML 字元，包含合法代理對。</param>
    /// <exception cref="ArgumentException">The value is not one valid XML character. / 值不是單一合法 XML 字元。</exception>
    public OdfCharacter(string value)
    {
        if (!IsValid(value))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfCharacter_CharacterValueSingleText"), nameof(value));
        }

        Value = value;
    }

    /// <summary>
    /// Gets the Value value.
    /// 取得原始字元字串。
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Parses one XML character while preserving its original lexical form.
    /// 解析單一 XML 字元並保留原始詞法形式。
    /// </summary>
    /// <param name="value">The character string. / 字元字串。</param>
    /// <param name="character">The parsed character on success. / 成功時傳回解析後的字元。</param>
    /// <returns>Whether the value is one XML character. / 值是否為單一 XML 字元。</returns>
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
    /// Converts to string.
    /// 傳回原始字元字串。
    /// </summary>
    /// <returns>字元字串</returns>
    public override string ToString() => Value ?? string.Empty;

    /// <summary>
    /// Performs equals.
    /// 判斷目前值是否等於另一個字元。
    /// </summary>
    /// <param name="other">要比較的字元</param>
    /// <returns>若 lexical form 相同則為 <see langword="true"/></returns>
    public bool Equals(OdfCharacter other) => string.Equals(Value, other.Value, StringComparison.Ordinal);

    /// <summary>
    /// Performs equals.
    /// 執行 Equals。
    /// </summary>
    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is OdfCharacter other && Equals(other);

    /// <summary>
    /// Gets hash code.
    /// 取得 Hash Code。
    /// </summary>
    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);

    /// <summary>
    /// Performs equals.
    /// 判斷兩個字元是否相等。
    /// </summary>
    /// <param name="left">左側字元</param>
    /// <param name="right">右側字元</param>
    /// <returns>若兩者相等則為 <see langword="true"/></returns>
    public static bool operator ==(OdfCharacter left, OdfCharacter right) => left.Equals(right);

    /// <summary>
    /// Performs equals.
    /// 判斷兩個字元是否不相等。
    /// </summary>
    /// <param name="left">左側字元</param>
    /// <param name="right">右側字元</param>
    /// <returns>若兩者不相等則為 <see langword="true"/></returns>
    public static bool operator !=(OdfCharacter left, OdfCharacter right) => !left.Equals(right);

    private static bool IsValid(string? value)
    {
        if (value is null)
            return false;
        if (value.Length == 1)
        {
            char character = value[0];
            return character is '\t' or '\n' or '\r' ||
                character >= '\u0020' && character <= '\uD7FF' ||
                character >= '\uE000' && character <= '\uFFFD';
        }
        return value.Length == 2 && char.IsSurrogatePair(value, 0);
    }
}
