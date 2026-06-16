using System;

namespace OdfKit.DOM;

/// <summary>
/// 表示單一 schema 元素與 typed DOM wrapper 的對照結果。
/// </summary>
public sealed class OdfTypedDomElementCoverage
{
    /// <summary>
    /// 初始化單一元素覆蓋項目。
    /// </summary>
    /// <param name="namespaceUri">元素命名空間 URI。</param>
    /// <param name="localName">元素區域名稱。</param>
    /// <param name="role">schema 角色。</param>
    /// <param name="documentKind">文件種類。</param>
    /// <param name="wrapperType">wrapper 型別名稱。</param>
    /// <param name="hasTypedWrapper">是否具備專門 wrapper。</param>
    /// <param name="wrapperPropertyCount">wrapper 宣告的公開屬性數。</param>
    public OdfTypedDomElementCoverage(
        string namespaceUri,
        string localName,
        string role,
        string documentKind,
        string wrapperType,
        bool hasTypedWrapper,
        int wrapperPropertyCount)
    {
        NamespaceUri = namespaceUri ?? throw new ArgumentNullException(nameof(namespaceUri));
        LocalName = localName ?? throw new ArgumentNullException(nameof(localName));
        Role = role ?? throw new ArgumentNullException(nameof(role));
        DocumentKind = documentKind ?? throw new ArgumentNullException(nameof(documentKind));
        WrapperType = wrapperType ?? throw new ArgumentNullException(nameof(wrapperType));
        HasTypedWrapper = hasTypedWrapper;
        WrapperPropertyCount = wrapperPropertyCount;
    }

    /// <summary>
    /// 取得元素命名空間 URI。
    /// </summary>
    public string NamespaceUri { get; }

    /// <summary>
    /// 取得元素區域名稱。
    /// </summary>
    public string LocalName { get; }

    /// <summary>
    /// 取得 schema 角色。
    /// </summary>
    public string Role { get; }

    /// <summary>
    /// 取得文件種類。
    /// </summary>
    public string DocumentKind { get; }

    /// <summary>
    /// 取得 wrapper 型別名稱。
    /// </summary>
    public string WrapperType { get; }

    /// <summary>
    /// 取得是否具備專門 wrapper。
    /// </summary>
    public bool HasTypedWrapper { get; }

    /// <summary>
    /// 取得 wrapper 宣告的公開屬性數。
    /// </summary>
    public int WrapperPropertyCount { get; }
}
