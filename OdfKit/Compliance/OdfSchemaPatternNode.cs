using System;
using System.Collections.Generic;

namespace OdfKit.Compliance;

/// <summary>
/// 描述保留用於未來結構描述驗證的一個 RELAX NG 模式節點。
/// </summary>
public sealed class OdfSchemaPatternNode
{
    private readonly IReadOnlyList<OdfSchemaNameClass> _nameClasses;
    private readonly IReadOnlyList<OdfSchemaPatternNode> _children;
    private readonly IReadOnlyList<OdfSchemaDatatypeParameter> _dataParameters;

    /// <summary>
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

