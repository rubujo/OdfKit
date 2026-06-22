using System;
using System.Xml;

using OdfKit.Compliance;
namespace OdfKit.DOM;

/// <summary>
/// 表示 ODF schema 中以 XML <c>NCName</c>、<c>ID</c> 或 <c>IDREF</c> 表示的名稱 lexical form。
/// </summary>
public readonly struct OdfXmlName : IEquatable<OdfXmlName>
{
    /// <summary>
    /// 以 XML 名稱 lexical form 建立 <see cref="OdfXmlName"/>。
    /// </summary>
    /// <param name="value">XML 名稱，例如 <c>Shape1</c> 或 <c>chart-title</c>。</param>
    /// <exception cref="ArgumentException">當名稱不是有效 XML <c>NCName</c> 時擲回。</exception>
    public OdfXmlName(string value)
    {
        if (!IsValid(value))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfXmlName_XmlNameValidNcname"), nameof(value));
        }

        Value = value;
    }

    /// <summary>
    /// 取得原始 XML 名稱。
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// 嘗試解析 XML 名稱。
    /// </summary>
    /// <param name="value">XML 名稱字串。</param>
    /// <param name="xmlName">成功時傳回解析後的 XML 名稱。</param>
    /// <returns>若字串是有效 XML <c>NCName</c> 則為 <see langword="true"/>。</returns>
    public static bool TryParse(string? value, out OdfXmlName xmlName)
    {
        if (IsValid(value))
        {
            xmlName = new OdfXmlName(value!);
            return true;
        }

        xmlName = default;
        return false;
    }

    /// <summary>
    /// 傳回原始 XML 名稱。
    /// </summary>
    /// <returns>XML 名稱。</returns>
    public override string ToString() => Value ?? string.Empty;

    /// <summary>
    /// 判斷目前值是否等於另一個 XML 名稱。
    /// </summary>
    /// <param name="other">要比較的 XML 名稱。</param>
    /// <returns>若 lexical form 相同則為 <see langword="true"/>。</returns>
    public bool Equals(OdfXmlName other) => string.Equals(Value, other.Value, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is OdfXmlName other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);

    /// <summary>
    /// 判斷兩個 XML 名稱是否相等。
    /// </summary>
    /// <param name="left">左側 XML 名稱。</param>
    /// <param name="right">右側 XML 名稱。</param>
    /// <returns>若兩者相等則為 <see langword="true"/>。</returns>
    public static bool operator ==(OdfXmlName left, OdfXmlName right) => left.Equals(right);

    /// <summary>
    /// 判斷兩個 XML 名稱是否不相等。
    /// </summary>
    /// <param name="left">左側 XML 名稱。</param>
    /// <param name="right">右側 XML 名稱。</param>
    /// <returns>若兩者不相等則為 <see langword="true"/>。</returns>
    public static bool operator !=(OdfXmlName left, OdfXmlName right) => !left.Equals(right);

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
