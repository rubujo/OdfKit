using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using OdfKit.DOM;

namespace OdfKit.DOM;

/// <summary>
/// 表示 typed DOM 覆蓋報告的摘要與元素對照清單。
/// </summary>
public sealed class OdfTypedDomCoverageReport
{
    private readonly IReadOnlyList<OdfTypedDomElementCoverage> elements;
    private readonly IReadOnlyList<OdfTypedDomChildElementRelationCoverage> childElementRelations;
    private readonly IReadOnlyList<OdfTypedDomAttributeDatatypeCoverage> attributeDatatypeCoverage;
    private readonly IReadOnlyDictionary<string, int> attributeValueTypeCounts;
    private readonly IReadOnlyDictionary<string, int> wrapperPropertyTypeCounts;

    /// <summary>
    /// 初始化 typed DOM 覆蓋報告。
    /// </summary>
    /// <param name="schemaVersion">schema 版本</param>
    /// <param name="schemaSourceUrl">schema 來源 URL</param>
    /// <param name="schemaSourceDate">schema 來源日期</param>
    /// <param name="elements">元素覆蓋清單</param>
    /// <param name="childElementRelations">schema 直接子元素關係清單</param>
    /// <param name="attributeDatatypeCoverage">schema 屬性值類型與 typed helper 對照清單</param>
    /// <param name="schemaAttributeCount">schema 屬性總數</param>
    /// <param name="attributeValueTypeCounts">schema 屬性值類型分布</param>
    /// <param name="wrapperPropertyTypeCounts">wrapper 屬性 CLR 類型分布</param>
    public OdfTypedDomCoverageReport(
        string schemaVersion,
        string schemaSourceUrl,
        string schemaSourceDate,
        IReadOnlyList<OdfTypedDomElementCoverage> elements,
        IReadOnlyList<OdfTypedDomChildElementRelationCoverage> childElementRelations,
        IReadOnlyList<OdfTypedDomAttributeDatatypeCoverage> attributeDatatypeCoverage,
        int schemaAttributeCount,
        IReadOnlyDictionary<string, int> attributeValueTypeCounts,
        IReadOnlyDictionary<string, int> wrapperPropertyTypeCounts)
    {
        SchemaVersion = schemaVersion ?? throw new ArgumentNullException(nameof(schemaVersion));
        SchemaSourceUrl = schemaSourceUrl ?? throw new ArgumentNullException(nameof(schemaSourceUrl));
        SchemaSourceDate = schemaSourceDate ?? throw new ArgumentNullException(nameof(schemaSourceDate));
        this.elements = elements ?? throw new ArgumentNullException(nameof(elements));
        this.childElementRelations = childElementRelations ?? throw new ArgumentNullException(nameof(childElementRelations));
        this.attributeDatatypeCoverage = attributeDatatypeCoverage ?? throw new ArgumentNullException(nameof(attributeDatatypeCoverage));
        SchemaAttributeCount = schemaAttributeCount;
        this.attributeValueTypeCounts = attributeValueTypeCounts ?? throw new ArgumentNullException(nameof(attributeValueTypeCounts));
        this.wrapperPropertyTypeCounts = wrapperPropertyTypeCounts ?? throw new ArgumentNullException(nameof(wrapperPropertyTypeCounts));
    }

    /// <summary>
    /// 取得 schema 版本。
    /// </summary>
    public string SchemaVersion { get; }

    /// <summary>
    /// 取得 schema 來源 URL。
    /// </summary>
    public string SchemaSourceUrl { get; }

    /// <summary>
    /// 取得 schema 來源日期。
    /// </summary>
    public string SchemaSourceDate { get; }

    /// <summary>
    /// 取得 schema 元素總數。
    /// </summary>
    public int SchemaElementCount => elements.Count;

    /// <summary>
    /// 取得 schema 中具名直接子元素關係總數。
    /// </summary>
    public int SchemaChildElementRelationCount => childElementRelations.Count;

    /// <summary>
    /// 取得具備專門 wrapper 的 schema 元素數。
    /// </summary>
    public int TypedElementCount => elements.Count(element => element.HasTypedWrapper);

    /// <summary>
    /// 取得仍回退到通用 <see cref="OdfElement"/> 的 schema 元素數。
    /// </summary>
    public int FallbackElementCount => elements.Count(element => !element.HasTypedWrapper);

    /// <summary>
    /// 取得 schema 屬性總數。
    /// </summary>
    public int SchemaAttributeCount { get; }

    /// <summary>
    /// 取得 wrapper 上公開屬性總數。
    /// </summary>
    public int WrapperPropertyCount => elements.Sum(element => element.WrapperPropertyCount);

    /// <summary>
    /// 取得專門 wrapper 覆蓋比例。
    /// </summary>
    public double TypedElementRatio => SchemaElementCount == 0
        ? 0
        : (double)TypedElementCount / SchemaElementCount;

    /// <summary>
    /// 取得元素覆蓋清單。
    /// </summary>
    public IReadOnlyList<OdfTypedDomElementCoverage> Elements => elements;

    /// <summary>
    /// 取得 schema 直接子元素關係清單。
    /// </summary>
    public IReadOnlyList<OdfTypedDomChildElementRelationCoverage> ChildElementRelations => childElementRelations;

    /// <summary>
    /// 取得 schema 屬性值類型與 typed helper 對照清單。
    /// </summary>
    public IReadOnlyList<OdfTypedDomAttributeDatatypeCoverage> AttributeDatatypeCoverage => attributeDatatypeCoverage;

    /// <summary>
    /// 取得 schema 屬性值類型分布。
    /// </summary>
    public IReadOnlyDictionary<string, int> AttributeValueTypeCounts => attributeValueTypeCounts;

    /// <summary>
    /// 取得 wrapper 屬性 CLR 類型分布。
    /// </summary>
    public IReadOnlyDictionary<string, int> WrapperPropertyTypeCounts => wrapperPropertyTypeCounts;

    /// <summary>
    /// 建立適合 JSON 序列化的匿名模型。
    /// </summary>
    /// <returns>可被 JSON 序列化的報告模型</returns>
    public object ToJsonModel()
    {
        return new
        {
            schemaVersion = SchemaVersion,
            schemaSourceUrl = SchemaSourceUrl,
            schemaSourceDate = SchemaSourceDate,
            summary = new
            {
                schemaElementCount = SchemaElementCount,
                typedElementCount = TypedElementCount,
                fallbackElementCount = FallbackElementCount,
                typedElementRatio = TypedElementRatio.ToString("0.0000", CultureInfo.InvariantCulture),
                schemaChildElementRelationCount = SchemaChildElementRelationCount,
                schemaAttributeCount = SchemaAttributeCount,
                wrapperPropertyCount = WrapperPropertyCount
            },
            attributeValueTypeCounts = AttributeValueTypeCounts,
            wrapperPropertyTypeCounts = WrapperPropertyTypeCounts,
            attributeDatatypeCoverage = AttributeDatatypeCoverage.Select(coverage => new
            {
                schemaValueType = coverage.SchemaValueType,
                schemaAttributeCount = coverage.SchemaAttributeCount,
                wrapperPropertyType = coverage.WrapperPropertyType,
                wrapperPropertyCount = coverage.WrapperPropertyCount,
                hasTypedHelper = coverage.HasTypedHelper,
                status = coverage.Status
            }).ToArray(),
            elements = Elements.Select(element => new
            {
                namespaceUri = element.NamespaceUri,
                localName = element.LocalName,
                role = element.Role,
                documentKind = element.DocumentKind,
                wrapperType = element.WrapperType,
                hasTypedWrapper = element.HasTypedWrapper,
                wrapperPropertyCount = element.WrapperPropertyCount
            }).ToArray(),
            childElementRelations = ChildElementRelations.Select(relation => new
            {
                parentNamespaceUri = relation.ParentNamespaceUri,
                parentLocalName = relation.ParentLocalName,
                childNamespaceUri = relation.ChildNamespaceUri,
                childLocalName = relation.ChildLocalName,
                occurrence = relation.Occurrence
            }).ToArray()
        };
    }
}

