using System;
using System.Xml.Linq;

namespace OdfKit.DOM;

public partial class OdfNode
{
    #region Attributes Helper


    /// <summary>
    /// 取得指定屬性名稱與命名空間的屬性值。
    /// </summary>
    /// <param name="localName">屬性的局部名稱</param>
    /// <param name="namespaceUri">屬性的命名空間 URI</param>
    /// <returns>屬性值；如果找不到，則為 <see langword="null"/></returns>
    public string? GetAttribute(string localName, string namespaceUri)
    {
        var key = new OdfAttributeName(localName, namespaceUri);
        return Attributes.TryGetValue(key, out string? value) ? value : null;
    }

    /// <summary>
    /// 取得指定屬性名稱與命名空間的屬性值。
    /// </summary>
    /// <param name="localName">屬性的局部名稱</param>
    /// <param name="namespaceUri">屬性的命名空間</param>
    /// <returns>屬性值；如果找不到，則為 <see langword="null"/></returns>
    public string? GetAttribute(string localName, XNamespace namespaceUri) => GetAttribute(localName, namespaceUri.NamespaceName);

    /// <summary>
    /// 設定指定屬性名稱與命名空間的屬性值。
    /// </summary>
    /// <param name="localName">屬性的局部名稱</param>
    /// <param name="namespaceUri">屬性的命名空間 URI</param>
    /// <param name="value">要設定的屬性值</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    public void SetAttribute(string localName, string namespaceUri, string value, string? prefix = null)
    {
        var key = new OdfAttributeName(localName, namespaceUri);
        string? existingPrefix = GetAttributePrefix(key);
        if (!Attributes.TryGetValue(key, out string? existing) || existing != value)
        {
            IsModified = true;
            Attributes[key] = value;
        }

        if (!string.IsNullOrEmpty(prefix) && prefix is string attributePrefix)
        {
            if (!string.Equals(existingPrefix, attributePrefix, StringComparison.Ordinal))
            {
                IsModified = true;
            }

            _attributePrefixes[key] = attributePrefix;
        }
        else
        {
            if (existingPrefix is not null)
            {
                IsModified = true;
            }

            _attributePrefixes.Remove(key);
        }
    }

    /// <summary>
    /// 設定指定屬性名稱與命名空間的屬性值。
    /// </summary>
    /// <param name="localName">屬性的局部名稱</param>
    /// <param name="namespaceUri">屬性的命名空間</param>
    /// <param name="value">要設定的屬性值</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    public void SetAttribute(string localName, XNamespace namespaceUri, string value, string? prefix = null) => SetAttribute(localName, namespaceUri.NamespaceName, value, prefix);

    /// <summary>
    /// 移除指定屬性名稱與命名空間的屬性。
    /// </summary>
    /// <param name="localName">屬性的局部名稱</param>
    /// <param name="namespaceUri">屬性的命名空間 URI</param>
    public void RemoveAttribute(string localName, string namespaceUri)
    {
        var key = new OdfAttributeName(localName, namespaceUri);
        if (Attributes.Remove(key))
        {
            _attributePrefixes.Remove(key);
            IsModified = true;
        }
    }

    /// <summary>
    /// 取得指定屬性的原始命名空間前綴。
    /// </summary>
    /// <param name="attributeName">屬性名稱。</param>
    /// <returns>原始前綴；若未記錄則為 <see langword="null"/>。</returns>
    public string? GetAttributePrefix(OdfAttributeName attributeName)
    {
        return _attributePrefixes.TryGetValue(attributeName, out string? prefix) ? prefix : null;
    }

    /// <summary>
    /// 移除指定屬性名稱與命名空間的屬性。
    /// </summary>
    /// <param name="localName">屬性的局部名稱</param>
    /// <param name="namespaceUri">屬性的命名空間</param>
    public void RemoveAttribute(string localName, XNamespace namespaceUri) => RemoveAttribute(localName, namespaceUri.NamespaceName);


    #endregion
}
