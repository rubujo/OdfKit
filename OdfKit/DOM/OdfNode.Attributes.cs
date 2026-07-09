using System;
using System.Xml.Linq;
using OdfKit.Compliance;
using OdfKit.Core;

namespace OdfKit.DOM;
/// <summary>
/// Provides the OdfNode API.
/// 提供 OdfNode API。
/// </summary>

public partial class OdfNode
{
    #region Attributes Helper


    /// <summary>
    /// Executes the GetAttribute operation.
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
    /// Executes the GetAttribute operation.
    /// 取得指定屬性名稱與命名空間的屬性值。
    /// </summary>
    /// <returns>屬性值；如果找不到，則為 <see langword="null"/></returns>
    public string? GetAttribute(string localName, XNamespace namespaceUri) => GetAttribute(localName, namespaceUri.NamespaceName);

    /// <summary>
    /// Executes the SetAttribute operation.
    /// 設定指定屬性名稱與命名空間的屬性值。
    /// </summary>
    public void SetAttribute(string localName, string namespaceUri, string value) => SetAttribute(localName, namespaceUri, value, null);

    /// <summary>
    /// Additional public overload without optional parameters.
    /// 不含選用參數的公開多載。
    /// </summary>
    public void SetAttribute(string localName, string namespaceUri, string value, string? prefix)
    {
#if DEBUG
        ValidateAttributeWrite(localName, namespaceUri, value);
#endif
        value = OdfAttributeStringPool.InternValue(value);
        var key = new OdfAttributeName(localName, namespaceUri);
        string? existingPrefix = GetAttributePrefix(key);
        string? resolvedPrefix = OdfAttributeStringPool.InternName(ResolveAttributePrefix(namespaceUri, prefix) ?? string.Empty);
        if (!Attributes.TryGetValue(key, out string? existing) || existing != value)
        {
            IsModified = true;
            Attributes[key] = value;
        }

        if (!string.IsNullOrEmpty(resolvedPrefix) && resolvedPrefix is string attributePrefix)
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

        if (IsModified)
        {
            InvalidateStyle();
        }
    }

    /// <summary>
    /// Executes the SetAttribute operation.
    /// 設定指定屬性名稱與命名空間的屬性值。
    /// </summary>
    public void SetAttribute(string localName, XNamespace namespaceUri, string value) => SetAttribute(localName, namespaceUri.NamespaceName, value, null);

    /// <summary>
    /// Additional public overload without optional parameters.
    /// 不含選用參數的公開多載。
    /// </summary>
    public void SetAttribute(string localName, XNamespace namespaceUri, string value, string? prefix) => SetAttribute(localName, namespaceUri.NamespaceName, value, prefix);

    /// <summary>
    /// Removes the attribute with the specified local name and namespace.
    /// 移除指定屬性名稱與命名空間的屬性。
    /// </summary>
    /// <param name="localName">The attribute local name. / 屬性的局部名稱。</param>
    /// <param name="namespaceUri">The attribute namespace URI. / 屬性的命名空間 URI。</param>
    /// <returns><see langword="true"/> if the attribute was removed; otherwise, <see langword="false"/>. / 若已移除屬性則為 <see langword="true"/>；否則為 <see langword="false"/>。</returns>
    public bool RemoveAttribute(string localName, string namespaceUri)
    {
        var key = new OdfAttributeName(localName, namespaceUri);
        if (Attributes.Remove(key))
        {
            _attributePrefixes.Remove(key);
            IsModified = true;
            InvalidateStyle();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Executes the GetAttributePrefix operation.
    /// 取得指定屬性的原始命名空間前綴。
    /// </summary>
    /// <param name="attributeName">屬性名稱</param>
    /// <returns>原始前綴；若未記錄則為 <see langword="null"/></returns>
    public string? GetAttributePrefix(OdfAttributeName attributeName)
    {
        return _attributePrefixes.TryGetValue(attributeName, out string? prefix) ? prefix : null;
    }

    /// <summary>
    /// Removes the attribute with the specified local name and namespace.
    /// 移除指定屬性名稱與命名空間的屬性。
    /// </summary>
    /// <returns><see langword="true"/> if the attribute was removed; otherwise, <see langword="false"/>. / 若已移除屬性則為 <see langword="true"/>；否則為 <see langword="false"/>。</returns>
    public bool RemoveAttribute(string localName, XNamespace namespaceUri) => RemoveAttribute(localName, namespaceUri.NamespaceName);

    private static string? ResolveAttributePrefix(string namespaceUri, string? requestedPrefix)
    {
        if (!string.IsNullOrEmpty(requestedPrefix))
        {
            return requestedPrefix;
        }

        string defaultPrefix = OdfNamespaces.GetPrefix(namespaceUri);
        return string.IsNullOrEmpty(defaultPrefix) ? null : defaultPrefix;
    }

#if DEBUG
    private void ValidateAttributeWrite(string localName, string namespaceUri, string value)
    {
        if (string.IsNullOrEmpty(localName))
        {
            OdfKitDiagnostics.Warn("OdfNode.SetAttribute 收到空白屬性名稱。");
            return;
        }

        if (value is null)
        {
            OdfKitDiagnostics.Warn($"OdfNode.SetAttribute 收到 null 屬性值：{localName}。");
            return;
        }

        string knownPrefix = OdfNamespaces.GetPrefix(namespaceUri);
        if (!string.IsNullOrEmpty(knownPrefix))
        {
            var definition = OdfSchemaRegistry
                .GetSchema(GetDocumentVersion())
                .FindAttribute(namespaceUri, localName);
            if (definition is null)
            {
                OdfKitDiagnostics.Warn($"屬性 '{knownPrefix}:{localName}' 未定義於 ODF schema。");
            }
        }
    }
#endif


    #endregion
}
