using System;

namespace OdfKit.DOM;

/// <summary>
/// Provides the OdfTypedDomChildElementRelationCoverage API.
/// 表示 schema 中一組父元素與直接子元素的覆蓋關係。
/// </summary>
public sealed class OdfTypedDomChildElementRelationCoverage
{
    /// <summary>
    /// Performs odf typed dom child element relation coverage.
    /// 初始化 schema 子元素關係覆蓋專案。
    /// </summary>
    /// <param name="parentNamespaceUri">父元素命名空間 URI</param>
    /// <param name="parentLocalName">父元素區域名稱</param>
    /// <param name="childNamespaceUri">子元素命名空間 URI</param>
    /// <param name="childLocalName">子元素區域名稱</param>
    /// <param name="occurrence">schema 中記錄的出現次數</param>
    public OdfTypedDomChildElementRelationCoverage(
        string parentNamespaceUri,
        string parentLocalName,
        string childNamespaceUri,
        string childLocalName,
        string occurrence)
    {
        ParentNamespaceUri = parentNamespaceUri ?? throw new ArgumentNullException(nameof(parentNamespaceUri));
        ParentLocalName = parentLocalName ?? throw new ArgumentNullException(nameof(parentLocalName));
        ChildNamespaceUri = childNamespaceUri ?? throw new ArgumentNullException(nameof(childNamespaceUri));
        ChildLocalName = childLocalName ?? throw new ArgumentNullException(nameof(childLocalName));
        Occurrence = string.IsNullOrWhiteSpace(occurrence) ? "exactlyOne" : occurrence;
    }

    /// <summary>
    /// Gets the ParentNamespaceUri value.
    /// 取得父元素命名空間 URI。
    /// </summary>
    public string ParentNamespaceUri { get; }

    /// <summary>
    /// Gets the ParentLocalName value.
    /// 取得父元素區域名稱。
    /// </summary>
    public string ParentLocalName { get; }

    /// <summary>
    /// Gets the ChildNamespaceUri value.
    /// 取得子元素命名空間 URI。
    /// </summary>
    public string ChildNamespaceUri { get; }

    /// <summary>
    /// Gets the ChildLocalName value.
    /// 取得子元素區域名稱。
    /// </summary>
    public string ChildLocalName { get; }

    /// <summary>
    /// Gets the Occurrence value.
    /// 取得 schema 中記錄的出現次數。
    /// </summary>
    public string Occurrence { get; }
}

