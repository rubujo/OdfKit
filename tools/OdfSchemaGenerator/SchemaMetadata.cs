using System.Collections.Generic;

namespace OdfKit.Tools.OdfSchemaGenerator;

/// <summary>
/// 表示從 RELAX NG 檔案擷取的結構描述中繼資料。
/// </summary>
public sealed class SchemaMetadata
{
    /// <summary>
    /// 取得或設定結構描述的來源路徑或 URI。
    /// </summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// 取得或設定結構描述的來源日期。
    /// </summary>
    public string SourceDate { get; set; } = "generated";

    /// <summary>
    /// 取得或設定此結構描述所代表的 ODF 版本字串（例如 "1.1"、"1.2"、"1.3"、"1.4"）。
    /// </summary>
    public string Version { get; set; } = "1.4";

    /// <summary>
    /// 取得擷取出的元素。
    /// </summary>
    public List<SchemaNameMetadata> Elements { get; } = new();

    /// <summary>
    /// 取得擷取出的屬性。
    /// </summary>
    public List<SchemaNameMetadata> Attributes { get; } = new();

    /// <summary>
    /// 取得擷取出的 RELAX NG 具名模式。
    /// </summary>
    public List<SchemaPatternMetadata> Patterns { get; } = new();

    /// <summary>
    /// 取得無法在磁碟上解析的相對參照。
    /// </summary>
    public List<string> MissingReferences { get; } = new();

    /// <summary>
    /// 取得已記錄但未由產生器擷取的絕對非檔案參照。
    /// </summary>
    public List<string> ExternalReferences { get; } = new();

    /// <summary>
    /// 取得因解析結果落在結構描述根目錄外而被拒絕的檔案參照。
    /// </summary>
    public List<string> RejectedReferences { get; } = new();
}

/// <summary>
/// 表示一個具命名空間限定的結構描述名稱。
/// </summary>
public sealed class SchemaNameMetadata
{
    /// <summary>
    /// 取得或設定命名空間 URI。
    /// </summary>
    public string NamespaceUri { get; set; } = string.Empty;

    /// <summary>
    /// 取得或設定本地名稱。
    /// </summary>
    public string LocalName { get; set; } = string.Empty;
}

/// <summary>
/// 表示一個具名 RELAX NG 模式及其參照的其他模式。
/// </summary>
public sealed class SchemaPatternMetadata
{
    /// <summary>
    /// 取得或設定模式名稱。
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 取得參照的模式名稱。
    /// </summary>
    public List<string> References { get; } = new();

    /// <summary>
    /// 取得此具名模式所使用的 RELAX NG 模式種類。
    /// </summary>
    public List<string> PatternKinds { get; } = new();

    /// <summary>
    /// 取得可出現在此模式內的元素名稱。
    /// </summary>
    public List<SchemaPatternNameUseMetadata> ChildElements { get; } = new();

    /// <summary>
    /// 取得可出現在此模式內的屬性名稱。
    /// </summary>
    public List<SchemaPatternNameUseMetadata> Attributes { get; } = new();

    /// <summary>
    /// 取得此模式所使用的萬用字元／名稱類別限制。
    /// </summary>
    public List<SchemaNameClassMetadata> NameClasses { get; } = new();

    /// <summary>
    /// 取得此具名模式保留下來的根模式節點。
    /// </summary>
    public List<SchemaPatternNodeMetadata> PatternTree { get; } = new();
}

/// <summary>
/// 表示 RELAX NG 模式所使用的具命名空間限定名稱。
/// </summary>
public sealed class SchemaPatternNameUseMetadata
{
    /// <summary>
    /// 取得或設定命名空間 URI。
    /// </summary>
    public string NamespaceUri { get; set; } = string.Empty;

    /// <summary>
    /// 取得或設定本地名稱。
    /// </summary>
    public string LocalName { get; set; } = string.Empty;

    /// <summary>
    /// 取得或設定最接近的 RELAX NG 出現次數包裝。
    /// </summary>
    public string Occurrence { get; set; } = "exactlyOne";
}

/// <summary>
/// 表示 RELAX NG 的名稱類別，例如 name、nsName 或 anyName。
/// </summary>
public sealed class SchemaNameClassMetadata
{
    /// <summary>
    /// 取得或設定 RELAX NG 名稱類別種類。
    /// </summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>
    /// 取得或設定可用時的命名空間 URI。
    /// </summary>
    public string NamespaceUri { get; set; } = string.Empty;

    /// <summary>
    /// 取得或設定可用時的本地名稱。
    /// </summary>
    public string LocalName { get; set; } = string.Empty;

    /// <summary>
    /// 取得或設定一個值，指出此名稱類別是否出現在 rng:except 之下。
    /// </summary>
    public bool IsExcept { get; set; }
}

/// <summary>
/// 表示一個 RELAX NG 模式節點，並保留足夠的內容供未來的驗證器使用。
/// </summary>
public sealed class SchemaPatternNodeMetadata
{
    /// <summary>
    /// 取得或設定 RELAX NG 模式種類。
    /// </summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>
    /// 取得或設定最接近的 RELAX NG 出現次數包裝。
    /// </summary>
    public string Occurrence { get; set; } = "exactlyOne";

    /// <summary>
    /// 取得或設定此節點帶有限定名稱時的命名空間 URI。
    /// </summary>
    public string NamespaceUri { get; set; } = string.Empty;

    /// <summary>
    /// 取得或設定此節點帶有限定名稱時的本地名稱。
    /// </summary>
    public string LocalName { get; set; } = string.Empty;

    /// <summary>
    /// 取得或設定 rng:ref 節點所參照的模式名稱。
    /// </summary>
    public string ReferenceName { get; set; } = string.Empty;

    /// <summary>
    /// 取得或設定 rng:data 節點的資料型別名稱。
    /// </summary>
    public string DataType { get; set; } = string.Empty;

    /// <summary>
    /// 取得或設定 rng:data 或 rng:value 節點所使用的 RELAX NG 資料型別函式庫 URI。
    /// </summary>
    public string DataTypeLibrary { get; set; } = string.Empty;

    /// <summary>
    /// 取得或設定 rng:value 節點的文字值。
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// 取得附加在 rng:data 節點上的資料型別參數。
    /// </summary>
    public List<SchemaDatatypeParameterMetadata> DataParameters { get; } = new();

    /// <summary>
    /// 取得直接附加在此節點上的名稱類別。
    /// </summary>
    public List<SchemaNameClassMetadata> NameClasses { get; } = new();

    /// <summary>
    /// 取得子模式節點。
    /// </summary>
    public List<SchemaPatternNodeMetadata> Children { get; } = new();
}

/// <summary>
/// 表示一個 RELAX NG 資料型別參數。
/// </summary>
public sealed class SchemaDatatypeParameterMetadata
{
    /// <summary>
    /// 取得或設定參數名稱。
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 取得或設定參數值。
    /// </summary>
    public string Value { get; set; } = string.Empty;
}
