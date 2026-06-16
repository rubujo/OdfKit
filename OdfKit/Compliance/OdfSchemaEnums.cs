using System;

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

