using System;
using System.Collections.Generic;

namespace OdfKit.Compliance;

/// <summary>
/// Provides the OdfSchemaPatternNode API.
/// 描述保留用於未來結構描述驗證的一個 RELAX NG 模式節點。
/// </summary>
public sealed class OdfSchemaPatternNode
{
    private readonly IReadOnlyList<OdfSchemaNameClass> _nameClasses;
    private readonly IReadOnlyList<OdfSchemaPatternNode> _children;
    private readonly IReadOnlyList<OdfSchemaDatatypeParameter> _dataParameters;
    /// <summary>
    /// Convenience overload that uses default values for remaining parameters.
    /// 便利多載：其餘參數使用預設值並轉呼叫最長多載。
    /// </summary>
    public OdfSchemaPatternNode(OdfSchemaPatternNodeKind kind, string occurrence, string namespaceUri, string localName, string referenceName, string dataType, string value) : this(kind, occurrence, namespaceUri, localName, referenceName, dataType, value, null, null, null, null) { }

    /// <summary>
    /// Convenience overload that uses default values for remaining parameters.
    /// 便利多載：其餘參數使用預設值並轉呼叫最長多載。
    /// </summary>
    public OdfSchemaPatternNode(OdfSchemaPatternNodeKind kind, string occurrence, string namespaceUri, string localName, string referenceName, string dataType, string value, IEnumerable<OdfSchemaNameClass>? nameClasses) : this(kind, occurrence, namespaceUri, localName, referenceName, dataType, value, nameClasses, null, null, null) { }

    /// <summary>
    /// Convenience overload that uses default values for remaining parameters.
    /// 便利多載：其餘參數使用預設值並轉呼叫最長多載。
    /// </summary>
    public OdfSchemaPatternNode(OdfSchemaPatternNodeKind kind, string occurrence, string namespaceUri, string localName, string referenceName, string dataType, string value, IEnumerable<OdfSchemaNameClass>? nameClasses, IEnumerable<OdfSchemaPatternNode>? children) : this(kind, occurrence, namespaceUri, localName, referenceName, dataType, value, nameClasses, children, null, null) { }

    /// <summary>
    /// Convenience overload that uses default values for remaining parameters.
    /// 便利多載：其餘參數使用預設值並轉呼叫最長多載。
    /// </summary>
    public OdfSchemaPatternNode(OdfSchemaPatternNodeKind kind, string occurrence, string namespaceUri, string localName, string referenceName, string dataType, string value, IEnumerable<OdfSchemaNameClass>? nameClasses, IEnumerable<OdfSchemaPatternNode>? children, IEnumerable<KeyValuePair<string, string>>? dataParameters) : this(kind, occurrence, namespaceUri, localName, referenceName, dataType, value, nameClasses, children, dataParameters, null) { }


    /// <summary>
    /// Executes the OdfSchemaPatternNode operation.
    /// 初始化模式節點中繼資料專案的新執行個體。
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
    public OdfSchemaPatternNode(OdfSchemaPatternNodeKind kind, string occurrence, string namespaceUri, string localName, string referenceName, string dataType, string value, IEnumerable<OdfSchemaNameClass>? nameClasses, IEnumerable<OdfSchemaPatternNode>? children, IEnumerable<KeyValuePair<string, string>>? dataParameters, string? dataTypeLibrary)
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
    /// Gets the Kind value.
    /// 取得 RELAX NG 模式節點種類。
    /// </summary>
    public OdfSchemaPatternNodeKind Kind { get; }

    /// <summary>
    /// Gets the Occurrence value.
    /// 取得為此節點捕獲的最近出現次數包裝器。
    /// </summary>
    public string Occurrence { get; }

    /// <summary>
    /// Gets the NamespaceUri value.
    /// 取得此節點攜帶限定名稱時的命名空間 URI。
    /// </summary>
    public string NamespaceUri { get; }

    /// <summary>
    /// Gets the LocalName value.
    /// 取得此節點攜帶限定名稱時的區域名稱。
    /// </summary>
    public string LocalName { get; }

    /// <summary>
    /// Gets the ReferenceName value.
    /// 取得參考節點的參考模式名稱。
    /// </summary>
    public string ReferenceName { get; }

    /// <summary>
    /// Gets the DataType value.
    /// 取得資料類型節點的資料類型名稱。
    /// </summary>
    public string DataType { get; }

    /// <summary>
    /// Gets the DataTypeLibrary value.
    /// 取得資料類型或數值節點的 RELAX NG 資料類型程式庫 URI。
    /// </summary>
    public string DataTypeLibrary { get; }

    /// <summary>
    /// Gets the Value value.
    /// 取得數值節點的常值。
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Provides the DataParameters member.
    /// 取得資料節點的資料類型參數（例如 RELAX NG 參數 facet）。
    /// </summary>
    public IReadOnlyList<OdfSchemaDatatypeParameter> DataParameters => _dataParameters;

    /// <summary>
    /// Provides the NameClasses member.
    /// 取得直接附加到此節點的名稱類別。
    /// </summary>
    public IReadOnlyList<OdfSchemaNameClass> NameClasses => _nameClasses;

    /// <summary>
    /// Provides the Children member.
    /// 取得子模式節點。
    /// </summary>
    public IReadOnlyList<OdfSchemaPatternNode> Children => _children;
}

