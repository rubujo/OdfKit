using System;
using System.Collections.Generic;
using OdfKit.Core;

namespace OdfKit.DOM;

/// <summary>
/// 表示 ODF 節點類型的列舉。
/// </summary>
public enum OdfNodeType
{
    /// <summary>
    /// 元素節點。
    /// </summary>
    Element,

    /// <summary>
    /// 文字節點。
    /// </summary>
    Text,

    /// <summary>
    /// 註解節點。
    /// </summary>
    Comment,

    /// <summary>
    /// XML 處理指令節點。
    /// </summary>
    ProcessingInstruction
}

/// <summary>
/// 表示 ODF 屬性名稱的結構。
/// </summary>
/// <param name="localName">屬性的局部名稱</param>
/// <param name="namespaceUri">屬性的命名空間 URI</param>
public struct OdfAttributeName(string localName, string namespaceUri) : IEquatable<OdfAttributeName>
{
    /// <summary>
    /// 取得屬性的局部名稱。
    /// </summary>
    public string LocalName { get; } = OdfAttributeStringPool.InternName(localName);

    /// <summary>
    /// 取得屬性的命名空間 URI。
    /// </summary>
    public string NamespaceUri { get; } = OdfAttributeStringPool.InternName(namespaceUri);

    /// <summary>
    /// 指示目前物件是否等於另一個相同類型的物件。
    /// </summary>
    /// <param name="other">要與目前物件進行比較的物件</param>
    /// <returns>如果目前物件等於 <paramref name="other"/> 參數，則為 <see langword="true"/>；否則為 <see langword="false"/></returns>
    public bool Equals(OdfAttributeName other) =>
        string.Equals(LocalName, other.LocalName, StringComparison.Ordinal) &&
        string.Equals(NamespaceUri, other.NamespaceUri, StringComparison.Ordinal);

    /// <summary>
    /// 指示此執行個體與指定的物件是否相等。
    /// </summary>
    /// <param name="obj">要比較的物件</param>
    /// <returns>如果 <paramref name="obj"/> 與這個執行個體具有相同的類型並表示相同的值，則為 <see langword="true"/>；否則為 <see langword="false"/></returns>
    public override bool Equals(object? obj) => obj is OdfAttributeName name && Equals(name);

    /// <summary>
    /// 傳回此執行個體的雜湊碼。
    /// </summary>
    /// <returns>32 位元有號整數雜湊碼</returns>
    public override int GetHashCode() => OdfHashing.Combine(LocalName, NamespaceUri);
}

/// <summary>
/// 用於比較 ODF 屬性名稱的比較器。
/// </summary>
internal class OdfAttributeNameComparer : IEqualityComparer<OdfAttributeName>
{
    /// <summary>
    /// 取得比較器的單例執行個體。
    /// </summary>
    public static readonly OdfAttributeNameComparer Instance = new();

    /// <summary>
    /// 判斷兩個屬性名稱是否相等。
    /// </summary>
    public bool Equals(OdfAttributeName x, OdfAttributeName y) => x.Equals(y);

    /// <summary>
    /// 取得屬性名稱的雜湊碼。
    /// </summary>
    public int GetHashCode(OdfAttributeName obj) => obj.GetHashCode();
}

internal static class OdfAttributeStringPool
{
    private static readonly object Lock = new();

    private static readonly Dictionary<string, string> Names = new(StringComparer.Ordinal)
    {
        ["boolean-value"] = "boolean-value",
        ["class"] = "class",
        ["date-value"] = "date-value",
        ["formula"] = "formula",
        ["href"] = "href",
        ["name"] = "name",
        ["number-columns-repeated"] = "number-columns-repeated",
        ["number-rows-repeated"] = "number-rows-repeated",
        ["style-name"] = "style-name",
        ["string-value"] = "string-value",
        ["table:name"] = "table:name",
        ["text:style-name"] = "text:style-name",
        ["value"] = "value",
        ["value-type"] = "value-type",
        [OdfNamespaces.Draw] = OdfNamespaces.Draw,
        [OdfNamespaces.Fo] = OdfNamespaces.Fo,
        [OdfNamespaces.Office] = OdfNamespaces.Office,
        [OdfNamespaces.Style] = OdfNamespaces.Style,
        [OdfNamespaces.Table] = OdfNamespaces.Table,
        [OdfNamespaces.Text] = OdfNamespaces.Text,
        [OdfNamespaces.XLink] = OdfNamespaces.XLink
    };

    private static readonly Dictionary<string, string> Values = new(StringComparer.Ordinal)
    {
        ["1.4"] = "1.4",
        ["auto"] = "auto",
        ["boolean"] = "boolean",
        ["center"] = "center",
        ["currency"] = "currency",
        ["date"] = "date",
        ["false"] = "false",
        ["float"] = "float",
        ["left"] = "left",
        ["none"] = "none",
        ["normal"] = "normal",
        ["percentage"] = "percentage",
        ["right"] = "right",
        ["solid"] = "solid",
        ["string"] = "string",
        ["time"] = "time",
        ["true"] = "true",
        ["wrap"] = "wrap"
    };

    internal static int NameHitCountForTests;

    internal static int ValueHitCountForTests;

    internal static string InternName(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        lock (Lock)
        {
            if (Names.TryGetValue(value, out string? interned))
            {
                NameHitCountForTests++;
                return interned;
            }

            if (value.Length <= 96)
            {
                Names[value] = value;
            }

            return value;
        }
    }

    internal static string InternValue(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        lock (Lock)
        {
            if (Values.TryGetValue(value, out string? interned))
            {
                ValueHitCountForTests++;
                return interned;
            }
        }

        return value;
    }
}
