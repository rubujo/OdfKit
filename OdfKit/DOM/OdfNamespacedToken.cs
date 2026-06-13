using System;
using System.Xml;

namespace OdfKit.DOM;

/// <summary>
/// 表示 ODF schema 中名為 <c>namespacedToken</c> 的 QName lexical form。
/// </summary>
public readonly struct OdfNamespacedToken : IEquatable<OdfNamespacedToken>
{
    /// <summary>
    /// 以 QName lexical form 建立 <see cref="OdfNamespacedToken"/>。
    /// </summary>
    /// <param name="value">QName 字串，例如 <c>draw:shape</c>。</param>
    /// <exception cref="ArgumentException">當 QName 不符合 ODF <c>namespacedToken</c> 格式時擲回。</exception>
    public OdfNamespacedToken(string value)
    {
        if (!TryParseParts(value, out string? prefix, out string? localName))
        {
            throw new ArgumentException("命名空間 token 必須是 prefix:localName 格式，且兩段都必須是有效 XML NCName。", nameof(value));
        }

        Value = value;
        Prefix = prefix!;
        LocalName = localName!;
    }

    /// <summary>
    /// 取得原始 QName 字串。
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// 取得 QName 前綴。
    /// </summary>
    public string Prefix { get; }

    /// <summary>
    /// 取得 QName 局部名稱。
    /// </summary>
    public string LocalName { get; }

    /// <summary>
    /// 嘗試解析 QName 字串。
    /// </summary>
    /// <param name="value">QName 字串。</param>
    /// <param name="namespacedToken">成功時傳回解析後的命名空間 token。</param>
    /// <returns>若字串符合 ODF <c>namespacedToken</c> 格式則為 <see langword="true"/>。</returns>
    public static bool TryParse(string? value, out OdfNamespacedToken namespacedToken)
    {
        if (TryParseParts(value, out string? prefix, out string? localName))
        {
            namespacedToken = new OdfNamespacedToken(value!, prefix!, localName!);
            return true;
        }

        namespacedToken = default;
        return false;
    }

    /// <summary>
    /// 傳回原始 QName 字串。
    /// </summary>
    /// <returns>QName 字串。</returns>
    public override string ToString() => Value ?? string.Empty;

    /// <summary>
    /// 判斷目前值是否等於另一個命名空間 token。
    /// </summary>
    /// <param name="other">要比較的命名空間 token。</param>
    /// <returns>若 lexical form 相同則為 <see langword="true"/>。</returns>
    public bool Equals(OdfNamespacedToken other) => string.Equals(Value, other.Value, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is OdfNamespacedToken other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);

    /// <summary>
    /// 判斷兩個命名空間 token 是否相等。
    /// </summary>
    /// <param name="left">左側命名空間 token。</param>
    /// <param name="right">右側命名空間 token。</param>
    /// <returns>若兩者相等則為 <see langword="true"/>。</returns>
    public static bool operator ==(OdfNamespacedToken left, OdfNamespacedToken right) => left.Equals(right);

    /// <summary>
    /// 判斷兩個命名空間 token 是否不相等。
    /// </summary>
    /// <param name="left">左側命名空間 token。</param>
    /// <param name="right">右側命名空間 token。</param>
    /// <returns>若兩者不相等則為 <see langword="true"/>。</returns>
    public static bool operator !=(OdfNamespacedToken left, OdfNamespacedToken right) => !left.Equals(right);

    private OdfNamespacedToken(string value, string prefix, string localName)
    {
        Value = value;
        Prefix = prefix;
        LocalName = localName;
    }

    private static bool TryParseParts(string? value, out string? prefix, out string? localName)
    {
        prefix = null;
        localName = null;

        if (value is null || value.Length == 0)
        {
            return false;
        }

        int colonIndex = value.IndexOf(':');
        if (colonIndex <= 0 || colonIndex == value.Length - 1 || value.IndexOf(':', colonIndex + 1) >= 0)
        {
            return false;
        }

        prefix = value.Substring(0, colonIndex);
        localName = value.Substring(colonIndex + 1);
        return IsNcName(prefix) && IsNcName(localName);
    }

    private static bool IsNcName(string value)
    {
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
