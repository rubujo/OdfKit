using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.Styles;

namespace OdfKit.DOM;

/// <summary>
/// 產生 typed DOM 與 ODF schema 之間的覆蓋報告。
/// </summary>
public static class OdfTypedDomCoverage
{
    /// <summary>
    /// 依指定 schema 建立 machine-readable typed DOM 覆蓋報告。
    /// </summary>
    /// <param name="schema">要檢查的 schema；若為 <see langword="null"/>，則使用最新 schema。</param>
    /// <returns>typed DOM 覆蓋報告。</returns>
    public static OdfTypedDomCoverageReport Build(OdfSchemaSet? schema = null)
    {
        OdfSchemaSet resolvedSchema = schema ?? OdfSchemaRegistry.Latest;
        List<OdfTypedDomElementCoverage> elements = [];
        Dictionary<string, int> wrapperPropertyTypeCounts = new(StringComparer.Ordinal);
        foreach (OdfElementDefinition definition in resolvedSchema.Elements.Values
            .OrderBy(element => element.Name.NamespaceUri, StringComparer.Ordinal)
            .ThenBy(element => element.Name.LocalName, StringComparer.Ordinal))
        {
            OdfNode node = OdfNodeFactory.CreateElement(
                definition.Name.LocalName,
                definition.Name.NamespaceUri,
                OdfNamespaces.GetPrefix(definition.Name.NamespaceUri));
            Type wrapperType = node.GetType();
            bool hasTypedWrapper = wrapperType != typeof(OdfElement);
            elements.Add(new OdfTypedDomElementCoverage(
                definition.Name.NamespaceUri,
                definition.Name.LocalName,
                definition.Role.ToString(),
                definition.DocumentKind.ToString(),
                wrapperType.FullName ?? wrapperType.Name,
                hasTypedWrapper,
                CountDeclaredPublicProperties(wrapperType)));
            foreach (PropertyInfo property in GetDeclaredPublicProperties(wrapperType))
            {
                string propertyType = GetPropertyTypeName(property.PropertyType);
                wrapperPropertyTypeCounts[propertyType] = wrapperPropertyTypeCounts.TryGetValue(propertyType, out int count)
                    ? count + 1
                    : 1;
            }
        }

        Dictionary<string, int> attributeValueTypeCounts = resolvedSchema.Attributes.Values
            .GroupBy(attribute => string.IsNullOrWhiteSpace(attribute.ValueType) ? "string" : attribute.ValueType, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        return new OdfTypedDomCoverageReport(
            FormatVersion(resolvedSchema.Version),
            resolvedSchema.SourceUrl.ToString(),
            resolvedSchema.SourceDate,
            elements,
            resolvedSchema.Attributes.Count,
            attributeValueTypeCounts,
            wrapperPropertyTypeCounts
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal));
    }

    private static int CountDeclaredPublicProperties(Type type)
    {
        return GetDeclaredPublicProperties(type).Length;
    }

    private static PropertyInfo[] GetDeclaredPublicProperties(Type type)
    {
        return type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
    }

    private static string GetPropertyTypeName(Type type)
    {
        Type? nullableType = Nullable.GetUnderlyingType(type);
        Type resolvedType = nullableType ?? type;
        if (resolvedType == typeof(string))
        {
            return "string";
        }

        if (resolvedType == typeof(int))
        {
            return "int";
        }

        if (resolvedType == typeof(bool))
        {
            return "bool";
        }

        if (resolvedType == typeof(decimal))
        {
            return "decimal";
        }

        if (resolvedType == typeof(DateTime))
        {
            return "dateTime";
        }

        if (resolvedType == typeof(OdfTime))
        {
            return "time";
        }

        if (resolvedType == typeof(OdfLength))
        {
            return "length";
        }

        if (resolvedType == typeof(OdfDuration))
        {
            return "duration";
        }

        if (resolvedType == typeof(OdfAngle))
        {
            return "angle";
        }

        if (resolvedType == typeof(OdfStyleName))
        {
            return "styleName";
        }

        if (resolvedType == typeof(OdfColor))
        {
            return "color";
        }

        if (resolvedType == typeof(OdfIriReference))
        {
            return "iriReference";
        }

        if (resolvedType == typeof(OdfPercent))
        {
            return "percent";
        }

        if (resolvedType == typeof(OdfStyleFamily))
        {
            return "styleFamily";
        }

        if (resolvedType == typeof(OdfVersion))
        {
            return "odfVersion";
        }

        if (resolvedType == typeof(OdfMediaType))
        {
            return "mediaType";
        }

        return resolvedType.FullName ?? resolvedType.Name;
    }

    private static string FormatVersion(OdfVersion version)
    {
        return version switch
        {
            OdfVersion.Odf10 => "1.0",
            OdfVersion.Odf11 => "1.1",
            OdfVersion.Odf12 => "1.2",
            OdfVersion.Odf13 => "1.3",
            OdfVersion.Odf14 => "1.4",
            _ => version.ToString()
        };
    }
}

/// <summary>
/// 表示 typed DOM 覆蓋報告的摘要與元素對照清單。
/// </summary>
public sealed class OdfTypedDomCoverageReport
{
    private readonly IReadOnlyList<OdfTypedDomElementCoverage> elements;
    private readonly IReadOnlyDictionary<string, int> attributeValueTypeCounts;
    private readonly IReadOnlyDictionary<string, int> wrapperPropertyTypeCounts;

    /// <summary>
    /// 初始化 typed DOM 覆蓋報告。
    /// </summary>
    /// <param name="schemaVersion">schema 版本。</param>
    /// <param name="schemaSourceUrl">schema 來源 URL。</param>
    /// <param name="schemaSourceDate">schema 來源日期。</param>
    /// <param name="elements">元素覆蓋清單。</param>
    /// <param name="schemaAttributeCount">schema 屬性總數。</param>
    /// <param name="attributeValueTypeCounts">schema 屬性值類型分布。</param>
    /// <param name="wrapperPropertyTypeCounts">wrapper 屬性 CLR 類型分布。</param>
    public OdfTypedDomCoverageReport(
        string schemaVersion,
        string schemaSourceUrl,
        string schemaSourceDate,
        IReadOnlyList<OdfTypedDomElementCoverage> elements,
        int schemaAttributeCount,
        IReadOnlyDictionary<string, int> attributeValueTypeCounts,
        IReadOnlyDictionary<string, int> wrapperPropertyTypeCounts)
    {
        SchemaVersion = schemaVersion ?? throw new ArgumentNullException(nameof(schemaVersion));
        SchemaSourceUrl = schemaSourceUrl ?? throw new ArgumentNullException(nameof(schemaSourceUrl));
        SchemaSourceDate = schemaSourceDate ?? throw new ArgumentNullException(nameof(schemaSourceDate));
        this.elements = elements ?? throw new ArgumentNullException(nameof(elements));
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
    /// <returns>可被 JSON 序列化的報告模型。</returns>
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
                schemaAttributeCount = SchemaAttributeCount,
                wrapperPropertyCount = WrapperPropertyCount
            },
            attributeValueTypeCounts = AttributeValueTypeCounts,
            wrapperPropertyTypeCounts = WrapperPropertyTypeCounts,
            elements = Elements.Select(element => new
            {
                namespaceUri = element.NamespaceUri,
                localName = element.LocalName,
                role = element.Role,
                documentKind = element.DocumentKind,
                wrapperType = element.WrapperType,
                hasTypedWrapper = element.HasTypedWrapper,
                wrapperPropertyCount = element.WrapperPropertyCount
            }).ToArray()
        };
    }
}

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
