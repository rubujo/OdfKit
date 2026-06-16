using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace OdfKit.Compliance;

/// <summary>
/// 代表單一 ODF 版本的確定性結構描述中繼資料。
/// </summary>
public sealed class OdfSchemaSet
{
    private readonly IReadOnlyDictionary<OdfQualifiedName, OdfElementDefinition> _elements;
    private readonly IReadOnlyDictionary<OdfQualifiedName, OdfAttributeDefinition> _attributes;
    private readonly IReadOnlyList<OdfSchemaNameClass> _nameClasses;
    private readonly IReadOnlyDictionary<string, OdfSchemaPatternDefinition> _patterns;

    /// <summary>
    /// 初始化結構描述中繼資料集的新執行個體。
    /// </summary>
    /// <param name="version">ODF 版本</param>
    /// <param name="sourceUrl">來源 URL</param>
    /// <param name="sourceDate">來源日期</param>
    /// <param name="elements">元素定義集合</param>
    /// <param name="attributes">屬性定義集合</param>
    /// <param name="nameClasses">名稱類別集合</param>
    /// <param name="patterns">模式定義集合</param>
    public OdfSchemaSet(
        OdfVersion version,
        Uri sourceUrl,
        string sourceDate,
        IEnumerable<OdfElementDefinition> elements,
        IEnumerable<OdfAttributeDefinition>? attributes = null,
        IEnumerable<OdfSchemaNameClass>? nameClasses = null,
        IEnumerable<OdfSchemaPatternDefinition>? patterns = null)
    {
        Version = version;
        SourceUrl = sourceUrl ?? throw new ArgumentNullException(nameof(sourceUrl));
        SourceDate = sourceDate ?? throw new ArgumentNullException(nameof(sourceDate));

        var byName = new Dictionary<OdfQualifiedName, OdfElementDefinition>();
        foreach (OdfElementDefinition element in elements ?? throw new ArgumentNullException(nameof(elements)))
        {
            byName[element.Name] = element;
        }

        _elements = new ReadOnlyDictionary<OdfQualifiedName, OdfElementDefinition>(byName);

        var attributesByName = new Dictionary<OdfQualifiedName, OdfAttributeDefinition>();
        foreach (OdfAttributeDefinition attribute in attributes ?? [])
        {
            attributesByName[attribute.Name] = attribute;
        }

        _attributes = new ReadOnlyDictionary<OdfQualifiedName, OdfAttributeDefinition>(attributesByName);
        _nameClasses = new List<OdfSchemaNameClass>(nameClasses ?? []).AsReadOnly();

        var patternsByName = new Dictionary<string, OdfSchemaPatternDefinition>(StringComparer.Ordinal);
        foreach (OdfSchemaPatternDefinition pattern in patterns ?? [])
        {
            patternsByName[pattern.Name] = pattern;
        }

        _patterns = new ReadOnlyDictionary<string, OdfSchemaPatternDefinition>(patternsByName);
    }

    /// <summary>
    /// 取得此中繼資料集代表的 ODF 版本。
    /// </summary>
    public OdfVersion Version { get; }

    /// <summary>
    /// 取得此結構描述中繼資料集所使用的官方來源 URL。
    /// </summary>
    public Uri SourceUrl { get; }

    /// <summary>
    /// 取得作為 ISO 日期字串的來源日期。
    /// </summary>
    public string SourceDate { get; }

    /// <summary>
    /// 取得所有元素定義，以命名空間 URI 與區域名稱作為索引鍵。
    /// </summary>
    public IReadOnlyDictionary<OdfQualifiedName, OdfElementDefinition> Elements => _elements;

    /// <summary>
    /// 取得所有屬性定義，以命名空間 URI 與區域名稱作為索引鍵。
    /// </summary>
    public IReadOnlyDictionary<OdfQualifiedName, OdfAttributeDefinition> Attributes => _attributes;

    /// <summary>
    /// 取得從來源結構描述保留的 RELAX NG 名稱類別。
    /// </summary>
    public IReadOnlyList<OdfSchemaNameClass> NameClasses => _nameClasses;

    /// <summary>
    /// 取得具名 RELAX NG 模式樹，以定義名稱作為索引鍵。
    /// </summary>
    public IReadOnlyDictionary<string, OdfSchemaPatternDefinition> Patterns => _patterns;

    /// <summary>
    /// 藉由合併此中繼資料與另一個結構描述集，建立新的結構描述集。
    /// </summary>
    /// <param name="additional">要合併的額外結構描述集</param>
    /// <param name="overwriteExisting">是否覆寫已存在的定義</param>
    /// <returns>合併後的全新 <see cref="OdfSchemaSet"/> 執行個體</returns>
    public OdfSchemaSet MergeWith(OdfSchemaSet additional, bool overwriteExisting = false)
    {
        if (additional is null)
            throw new ArgumentNullException(nameof(additional));

        var elements = new Dictionary<OdfQualifiedName, OdfElementDefinition>();
        foreach (var pair in _elements)
        {
            elements[pair.Key] = pair.Value;
        }

        foreach (var pair in additional.Elements)
        {
            if (overwriteExisting || !elements.ContainsKey(pair.Key))
            {
                elements[pair.Key] = pair.Value;
            }
        }

        var attributes = new Dictionary<OdfQualifiedName, OdfAttributeDefinition>();
        foreach (var pair in _attributes)
        {
            attributes[pair.Key] = pair.Value;
        }

        foreach (var pair in additional.Attributes)
        {
            if (overwriteExisting || !attributes.ContainsKey(pair.Key))
            {
                attributes[pair.Key] = pair.Value;
            }
        }

        var nameClasses = new List<OdfSchemaNameClass>(_nameClasses);
        nameClasses.AddRange(additional.NameClasses);

        var patterns = new Dictionary<string, OdfSchemaPatternDefinition>(StringComparer.Ordinal);
        foreach (var pair in _patterns)
        {
            patterns[pair.Key] = pair.Value;
        }

        foreach (var pair in additional.Patterns)
        {
            if (overwriteExisting || !patterns.ContainsKey(pair.Key))
            {
                patterns[pair.Key] = pair.Value;
            }
        }

        return new OdfSchemaSet(
            additional.Version,
            additional.SourceUrl,
            additional.SourceDate,
            elements.Values,
            attributes.Values,
            nameClasses,
            patterns.Values);
    }

    /// <summary>
    /// 根據命名空間 URI 與區域名稱尋找元素定義。
    /// </summary>
    /// <param name="namespaceUri">命名空間 URI</param>
    /// <param name="localName">區域名稱</param>
    /// <returns>尋找到的元素定義；若未找到則傳回 <see langword="null"/></returns>
    public OdfElementDefinition? FindElement(string namespaceUri, string localName)
    {
        return _elements.TryGetValue(new OdfQualifiedName(namespaceUri, localName), out OdfElementDefinition? element)
            ? element
            : null;
    }

    /// <summary>
    /// 傳回此中繼資料集中是否存在該元素。
    /// </summary>
    /// <param name="namespaceUri">命名空間 URI</param>
    /// <param name="localName">區域名稱</param>
    /// <returns>若存在則傳回 <see langword="true"/>；否則傳回 <see langword="false"/></returns>
    public bool ContainsElement(string namespaceUri, string localName) => FindElement(namespaceUri, localName) is not null;

    /// <summary>
    /// 根據命名空間 URI 與區域名稱尋找屬性定義。
    /// </summary>
    /// <param name="namespaceUri">命名空間 URI</param>
    /// <param name="localName">區域名稱</param>
    /// <returns>尋找到的屬性定義；若未找到則傳回 <see langword="null"/></returns>
    public OdfAttributeDefinition? FindAttribute(string namespaceUri, string localName)
    {
        return _attributes.TryGetValue(new OdfQualifiedName(namespaceUri, localName), out OdfAttributeDefinition? attribute)
            ? attribute
            : null;
    }

    /// <summary>
    /// 根據定義名稱尋找具名的 RELAX NG 模式樹。
    /// </summary>
    /// <param name="name">定義名稱</param>
    /// <returns>尋找到的模式定義；若未找到則傳回 <see langword="null"/></returns>
    public OdfSchemaPatternDefinition? FindPattern(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        return _patterns.TryGetValue(name, out OdfSchemaPatternDefinition? pattern)
            ? pattern
            : null;
    }

    /// <summary>
    /// 尋找與限定名稱相符的所有保留 RELAX NG 名稱類別。
    /// </summary>
    /// <param name="namespaceUri">命名空間 URI</param>
    /// <param name="localName">區域名稱</param>
    /// <returns>符合的名稱類別清單</returns>
    public IReadOnlyList<OdfSchemaNameClass> FindMatchingNameClasses(string namespaceUri, string localName)
    {
        var matches = new List<OdfSchemaNameClass>();
        foreach (OdfSchemaNameClass nameClass in _nameClasses)
        {
            if (nameClass.Matches(namespaceUri, localName))
            {
                matches.Add(nameClass);
            }
        }

        return matches.AsReadOnly();
    }

    /// <summary>
    /// 傳回保留的扁平名稱類別集是否允許該限定名稱。
    /// </summary>
    /// <remarks>
    /// 此協助方法在不考慮 RELAX NG 模式內容的情況下，評估保留的名稱類別中繼資料：必須至少符合一個非排除的名稱類別，且不得有任何符合的排除名稱類別適用。完整的結構描述驗證仍必須評估周圍的 RELAX NG 模式樹。
    /// </remarks>
    /// <param name="namespaceUri">命名空間 URI</param>
    /// <param name="localName">區域名稱</param>
    /// <returns>若允許該名稱則傳回 <see langword="true"/>；否則傳回 <see langword="false"/></returns>
    public bool IsNameAllowedByNameClasses(string namespaceUri, string localName)
    {
        bool allowed = false;
        foreach (OdfSchemaNameClass nameClass in _nameClasses)
        {
            if (!nameClass.Matches(namespaceUri, localName))
            {
                continue;
            }

            if (nameClass.IsExcept)
            {
                return false;
            }

            allowed = true;
        }

        return allowed;
    }
}
