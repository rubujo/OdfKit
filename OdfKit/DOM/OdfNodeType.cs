using System;
using System.Collections.Generic;

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
    public string LocalName { get; } = localName;

    /// <summary>
    /// 取得屬性的命名空間 URI。
    /// </summary>
    public string NamespaceUri { get; } = namespaceUri;

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
    public override int GetHashCode() =>
        (LocalName?.GetHashCode() ?? 0) ^ (NamespaceUri?.GetHashCode() ?? 0);
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
