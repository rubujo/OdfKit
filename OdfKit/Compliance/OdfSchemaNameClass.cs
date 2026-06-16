using System;

namespace OdfKit.Compliance;

/// <summary>
/// 描述保留用於結構描述驅動驗證的 RELAX NG 名稱類別條件約束。
/// </summary>
/// <param name="kind">名稱類別種類</param>
/// <param name="namespaceUri">命名空間 URI</param>
/// <param name="localName">區域名稱</param>
/// <param name="isExcept">是否出現在 <c>rng:except</c> 節點下</param>
public sealed class OdfSchemaNameClass(
    OdfSchemaNameClassKind kind,
    string namespaceUri,
    string localName,
    bool isExcept)
{
    /// <summary>
    /// 取得 RELAX NG 名稱類別種類。
    /// </summary>
    public OdfSchemaNameClassKind Kind { get; } = kind;

    /// <summary>
    /// 取得名稱類別限制的命名空間 URI（若有限制）。
    /// </summary>
    public string NamespaceUri { get; } = namespaceUri ?? string.Empty;

    /// <summary>
    /// 取得名稱類別限制的區域名稱（若有限制）。
    /// </summary>
    public string LocalName { get; } = localName ?? string.Empty;

    /// <summary>
    /// 取得此名稱類別是否出現在 <c>rng:except</c> 節點下。
    /// </summary>
    public bool IsExcept { get; } = isExcept;

    /// <summary>
    /// 傳回指定的命名空間限定名稱是否與此名稱類別相符。
    /// </summary>
    /// <param name="namespaceUri">命名空間 URI</param>
    /// <param name="localName">區域名稱</param>
    /// <returns>若相符則傳回 <see langword="true"/>；否則傳回 <see langword="false"/></returns>
    public bool Matches(string namespaceUri, string localName)
    {
        namespaceUri ??= string.Empty;
        localName ??= string.Empty;

        switch (Kind)
        {
            case OdfSchemaNameClassKind.Name:
                return string.Equals(NamespaceUri, namespaceUri, StringComparison.Ordinal) &&
                    string.Equals(LocalName, localName, StringComparison.Ordinal);
            case OdfSchemaNameClassKind.NamespaceName:
                return string.Equals(NamespaceUri, namespaceUri, StringComparison.Ordinal);
            case OdfSchemaNameClassKind.AnyName:
                return true;
            default:
                return false;
        }
    }
}

