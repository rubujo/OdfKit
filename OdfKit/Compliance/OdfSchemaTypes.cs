using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace OdfKit.Compliance;

/// <summary>
/// 識別 ODF 元素在低階驗證期間所扮演的結構描述角色。
/// </summary>
public enum OdfSchemaElementRole
{
    /// <summary>
    /// 一般 ODF 元素。
    /// </summary>
    Element,

    /// <summary>
    /// 套件 XML 串流或單一 XML 文件的根元素。
    /// </summary>
    DocumentRoot,

    /// <summary>
    /// 在 <c>office:body</c> 下的直接內容種類元素。
    /// </summary>
    BodyContent
}

/// <summary>
/// 識別在結構描述中保留的 RELAX NG 名稱類別種類。
/// </summary>
public enum OdfSchemaNameClassKind
{
    /// <summary>
    /// 具體的名稱類別。
    /// </summary>
    Name,

    /// <summary>
    /// 命名空間範圍的名稱類別。
    /// </summary>
    NamespaceName,

    /// <summary>
    /// 萬用字元名稱類別。
    /// </summary>
    AnyName
}

/// <summary>
/// 識別在結構描述中保留的 RELAX NG 模式節點種類。
/// </summary>
public enum OdfSchemaPatternNodeKind
{
    /// <summary>
    /// 命名的元素模式。
    /// </summary>
    Element,

    /// <summary>
    /// 命名的屬性模式。
    /// </summary>
    Attribute,

    /// <summary>
    /// 對另一個命名模式的參考。
    /// </summary>
    Ref,

    /// <summary>
    /// 序列/群組模式。
    /// </summary>
    Group,

    /// <summary>
    /// 選擇模式。
    /// </summary>
    Choice,

    /// <summary>
    /// 交錯模式。
    /// </summary>
    Interleave,

    /// <summary>
    /// 選擇性出現包裝器。
    /// </summary>
    Optional,

    /// <summary>
    /// 出現零次或多次的包裝器。
    /// </summary>
    ZeroOrMore,

    /// <summary>
    /// 出現一次或多次的包裝器。
    /// </summary>
    OneOrMore,

    /// <summary>
    /// RELAX NG 排除 (except) 模式。
    /// </summary>
    Except,

    /// <summary>
    /// 常值文字模式。
    /// </summary>
    Text,

    /// <summary>
    /// 空內容模式。
    /// </summary>
    Empty,

    /// <summary>
    /// 不允許 (notAllowed) 模式。
    /// </summary>
    NotAllowed,

    /// <summary>
    /// 資料類型模式。
    /// </summary>
    Data,

    /// <summary>
    /// 常值數值模式。
    /// </summary>
    Value,

    /// <summary>
    /// 清單模式。
    /// </summary>
    List,

    /// <summary>
    /// 混合內容模式。
    /// </summary>
    Mixed,

    /// <summary>
    /// 具體名稱類別模式。
    /// </summary>
    Name,

    /// <summary>
    /// 命名空間範圍名稱類別模式。
    /// </summary>
    NamespaceName,

    /// <summary>
    /// 萬用字元名稱類別模式。
    /// </summary>
    AnyName,

    /// <summary>
    /// 已知但目前未分類的模式種類。
    /// </summary>
    Other
}

/// <summary>
/// 識別命名空間限定的 ODF 名稱，而不依賴 XML 前綴。
/// </summary>
public sealed class OdfQualifiedName : IEquatable<OdfQualifiedName>
{
    /// <summary>
    /// 初始化限定名稱的新執行個體。
    /// </summary>
    /// <param name="namespaceUri">命名空間 URI</param>
    /// <param name="localName">區域元素或屬性名稱</param>
    public OdfQualifiedName(string namespaceUri, string localName)
    {
        if (string.IsNullOrWhiteSpace(namespaceUri))
            throw new ArgumentException("Namespace URI cannot be empty.", nameof(namespaceUri));
        if (string.IsNullOrWhiteSpace(localName))
            throw new ArgumentException("Local name cannot be empty.", nameof(localName));
        NamespaceUri = namespaceUri;
        LocalName = localName;
    }

    /// <summary>
    /// 取得命名空間 URI。
    /// </summary>
    public string NamespaceUri { get; }

    /// <summary>
    /// 取得區域元素或屬性名稱。
    /// </summary>
    public string LocalName { get; }

    /// <inheritdoc />
    public bool Equals(OdfQualifiedName? other)
    {
        return other is not null &&
            string.Equals(NamespaceUri, other.NamespaceUri, StringComparison.Ordinal) &&
            string.Equals(LocalName, other.LocalName, StringComparison.Ordinal);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as OdfQualifiedName);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        unchecked
        {
            return (StringComparer.Ordinal.GetHashCode(NamespaceUri) * 397) ^
                StringComparer.Ordinal.GetHashCode(LocalName);
        }
    }

    /// <inheritdoc />
    public override string ToString() => "{" + NamespaceUri + "}" + LocalName;
}

/// <summary>
/// 描述 ODF 結構描述中已知的一個元素定義。
/// </summary>
/// <param name="name">限定名稱</param>
/// <param name="role">元素角色</param>
/// <param name="supportedVersions">支援的 ODF 版本範圍</param>
/// <param name="documentKind">文件種類</param>
public sealed class OdfElementDefinition(
    OdfQualifiedName name,
    OdfSchemaElementRole role,
    OdfVersionRange supportedVersions,
    OdfDocumentKind documentKind = OdfDocumentKind.Unknown)
{
    /// <summary>
    /// 取得命名空間限定的元素名稱。
    /// </summary>
    public OdfQualifiedName Name { get; } = name ?? throw new ArgumentNullException(nameof(name));

    /// <summary>
    /// 取得此元素的結構描述角色。
    /// </summary>
    public OdfSchemaElementRole Role { get; } = role;

    /// <summary>
    /// 取得支援此元素的 ODF 版本範圍。
    /// </summary>
    public OdfVersionRange SupportedVersions { get; } = supportedVersions ?? throw new ArgumentNullException(nameof(supportedVersions));

    /// <summary>
    /// 取得此元素所代表的文件種類（適用時）。
    /// </summary>
    public OdfDocumentKind DocumentKind { get; } = documentKind;
}

/// <summary>
/// 描述 ODF 結構描述中已知的一個屬性定義。
/// </summary>
/// <param name="name">限定名稱</param>
/// <param name="valueType">值類型</param>
/// <param name="supportedVersions">支援的 ODF 版本範圍</param>
/// <param name="isRequiredOnDocumentRoot">是否在文件根元素上為必要</param>
public sealed class OdfAttributeDefinition(
    OdfQualifiedName name,
    string valueType,
    OdfVersionRange supportedVersions,
    bool isRequiredOnDocumentRoot = false)
{
    /// <summary>
    /// 取得命名空間限定的屬性名稱。
    /// </summary>
    public OdfQualifiedName Name { get; } = name ?? throw new ArgumentNullException(nameof(name));

    /// <summary>
    /// 取得結構描述值類型名稱。
    /// </summary>
    public string ValueType { get; } = valueType ?? throw new ArgumentNullException(nameof(valueType));

    /// <summary>
    /// 取得支援此屬性的 ODF 版本範圍。
    /// </summary>
    public OdfVersionRange SupportedVersions { get; } = supportedVersions ?? throw new ArgumentNullException(nameof(supportedVersions));

    /// <summary>
    /// 取得一個值，表示此屬性在 ODF 文件根元素上是否為必要。
    /// </summary>
    public bool IsRequiredOnDocumentRoot { get; } = isRequiredOnDocumentRoot;
}

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

/// <summary>
/// 描述從結構描述中繼資料中保留的一個命名 RELAX NG 模式。
/// </summary>
public sealed class OdfSchemaPatternDefinition
{
    private readonly IReadOnlyList<OdfSchemaPatternNode> _roots;

    /// <summary>
    /// 初始化命名模式定義的新執行個體。
    /// </summary>
    /// <param name="name">定義名稱</param>
    /// <param name="roots">根節點集合</param>
    public OdfSchemaPatternDefinition(string name, IEnumerable<OdfSchemaPatternNode> roots)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Pattern name cannot be empty.", nameof(name));
        Name = name;
        _roots = new List<OdfSchemaPatternNode>(roots ?? throw new ArgumentNullException(nameof(roots))).AsReadOnly();
    }

    /// <summary>
    /// 取得 RELAX NG 定義名稱。
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 取得此命名模式的根節點。
    /// </summary>
    public IReadOnlyList<OdfSchemaPatternNode> Roots => _roots;
}

/// <summary>
/// 描述附加到 RELAX NG 資料模式的一個資料類型參數。
/// </summary>
/// <param name="name">參數名稱</param>
/// <param name="value">參數值</param>
public sealed class OdfSchemaDatatypeParameter(string name, string value)
{
    /// <summary>
    /// 取得參數名稱。
    /// </summary>
    public string Name { get; } = name ?? string.Empty;

    /// <summary>
    /// 取得參數值。
    /// </summary>
    public string Value { get; } = value ?? string.Empty;
}

/// <summary>
/// 描述保留用於未來結構描述驗證的一個 RELAX NG 模式節點。
/// </summary>
public sealed class OdfSchemaPatternNode
{
    private readonly IReadOnlyList<OdfSchemaNameClass> _nameClasses;
    private readonly IReadOnlyList<OdfSchemaPatternNode> _children;
    private readonly IReadOnlyList<OdfSchemaDatatypeParameter> _dataParameters;

    /// <summary>
    /// 初始化模式節點中繼資料項目的新執行個體。
    /// </summary>
    /// <param name="kind">模式節點種類</param>
    /// <param name="occurrence">出現機率/次數設定</param>
    /// <param name="namespaceUri">命名空間 URI</param>
    /// <param name="localName">區域名稱</param>
    /// <param name="referenceName">參考的模式名稱</param>
    /// <param name="dataType">資料類型</param>
    /// <param name="value">常值</param>
    /// <param name="nameClasses">名稱類別集合</param>
    /// <param name="children">子模式節點集合</param>
    /// <param name="dataParameters">資料類型參數集合</param>
    /// <param name="dataTypeLibrary">資料類型程式庫 URI</param>
    public OdfSchemaPatternNode(
        OdfSchemaPatternNodeKind kind,
        string occurrence,
        string namespaceUri,
        string localName,
        string referenceName,
        string dataType,
        string value,
        IEnumerable<OdfSchemaNameClass>? nameClasses = null,
        IEnumerable<OdfSchemaPatternNode>? children = null,
        IEnumerable<KeyValuePair<string, string>>? dataParameters = null,
        string? dataTypeLibrary = null)
    {
        Kind = kind;
        Occurrence = string.IsNullOrWhiteSpace(occurrence) ? "exactlyOne" : occurrence;
        NamespaceUri = namespaceUri ?? string.Empty;
        LocalName = localName ?? string.Empty;
        ReferenceName = referenceName ?? string.Empty;
        DataType = dataType ?? string.Empty;
        DataTypeLibrary = dataTypeLibrary ?? string.Empty;
        Value = value ?? string.Empty;
        _nameClasses = new List<OdfSchemaNameClass>(nameClasses ?? []).AsReadOnly();
        _children = new List<OdfSchemaPatternNode>(children ?? []).AsReadOnly();
        var parameters = new List<OdfSchemaDatatypeParameter>();
        foreach (KeyValuePair<string, string> parameter in dataParameters ?? [])
        {
            if (!string.IsNullOrWhiteSpace(parameter.Key))
            {
                parameters.Add(new OdfSchemaDatatypeParameter(parameter.Key, parameter.Value));
            }
        }

        _dataParameters = parameters.AsReadOnly();
    }

    /// <summary>
    /// 取得 RELAX NG 模式節點種類。
    /// </summary>
    public OdfSchemaPatternNodeKind Kind { get; }

    /// <summary>
    /// 取得為此節點捕獲的最近出現次數包裝器。
    /// </summary>
    public string Occurrence { get; }

    /// <summary>
    /// 取得此節點攜帶限定名稱時的命名空間 URI。
    /// </summary>
    public string NamespaceUri { get; }

    /// <summary>
    /// 取得此節點攜帶限定名稱時的區域名稱。
    /// </summary>
    public string LocalName { get; }

    /// <summary>
    /// 取得參考節點的參考模式名稱。
    /// </summary>
    public string ReferenceName { get; }

    /// <summary>
    /// 取得資料類型節點的資料類型名稱。
    /// </summary>
    public string DataType { get; }

    /// <summary>
    /// 取得資料類型或數值節點的 RELAX NG 資料類型程式庫 URI。
    /// </summary>
    public string DataTypeLibrary { get; }

    /// <summary>
    /// 取得數值節點的常值。
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// 取得資料節點的資料類型參數（例如 RELAX NG 參數 facet）。
    /// </summary>
    public IReadOnlyList<OdfSchemaDatatypeParameter> DataParameters => _dataParameters;

    /// <summary>
    /// 取得直接附加到此節點的名稱類別。
    /// </summary>
    public IReadOnlyList<OdfSchemaNameClass> NameClasses => _nameClasses;

    /// <summary>
    /// 取得子模式節點。
    /// </summary>
    public IReadOnlyList<OdfSchemaPatternNode> Children => _children;
}

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
