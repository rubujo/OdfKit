using System;
using System.Xml;

namespace OdfKit.DOM;

/// <summary>
/// 表示 ODF schema 中以 <c>NCName</c> 表示的樣式名稱或樣式參照。
/// </summary>
public readonly struct OdfStyleName : IEquatable<OdfStyleName>
{
    /// <summary>
    /// 以樣式名稱 lexical form 建立 <see cref="OdfStyleName"/>。
    /// </summary>
    /// <param name="value">樣式名稱，例如 <c>Standard</c> 或 <c>Heading1</c>。</param>
    /// <exception cref="ArgumentException">當樣式名稱不是有效 XML <c>NCName</c> 時擲回。</exception>
    public OdfStyleName(string value)
    {
        if (!IsValid(value))
        {
            throw new ArgumentException("樣式名稱必須是有效的 XML NCName。", nameof(value));
        }

        Value = value;
    }

    /// <summary>
    /// 取得原始樣式名稱。
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// 嘗試解析樣式名稱。
    /// </summary>
    /// <param name="value">樣式名稱字串。</param>
    /// <param name="styleName">成功時傳回解析後的樣式名稱。</param>
    /// <returns>若字串是有效 XML <c>NCName</c> 則為 <see langword="true"/>。</returns>
    public static bool TryParse(string? value, out OdfStyleName styleName)
    {
        if (IsValid(value))
        {
            styleName = new OdfStyleName(value!);
            return true;
        }

        styleName = default;
        return false;
    }

    /// <summary>
    /// 傳回原始樣式名稱。
    /// </summary>
    /// <returns>樣式名稱。</returns>
    public override string ToString() => Value ?? string.Empty;

    /// <summary>
    /// 判斷目前值是否等於另一個樣式名稱。
    /// </summary>
    /// <param name="other">要比較的樣式名稱。</param>
    /// <returns>若 lexical form 相同則為 <see langword="true"/>。</returns>
    public bool Equals(OdfStyleName other) => string.Equals(Value, other.Value, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is OdfStyleName other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);

    /// <summary>
    /// 判斷兩個樣式名稱是否相等。
    /// </summary>
    /// <param name="left">左側樣式名稱。</param>
    /// <param name="right">右側樣式名稱。</param>
    /// <returns>若兩者相等則為 <see langword="true"/>。</returns>
    public static bool operator ==(OdfStyleName left, OdfStyleName right) => left.Equals(right);

    /// <summary>
    /// 判斷兩個樣式名稱是否不相等。
    /// </summary>
    /// <param name="left">左側樣式名稱。</param>
    /// <param name="right">右側樣式名稱。</param>
    /// <returns>若兩者不相等則為 <see langword="true"/>。</returns>
    public static bool operator !=(OdfStyleName left, OdfStyleName right) => !left.Equals(right);

    private static bool IsValid(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        try
        {
            XmlConvert.VerifyNCName(value);
            return true;
        }
        catch (XmlException)
        {
            return false;
        }
    }
}
