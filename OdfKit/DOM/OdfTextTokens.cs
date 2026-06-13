namespace OdfKit.DOM;

/// <summary>
/// 表示 ODF schema 中 <c>text:label-followed-by</c> 的清單標籤後接 token。
/// </summary>
public enum OdfTextLabelFollowedBy
{
    /// <summary>
    /// 清單定位點。
    /// </summary>
    ListTab,

    /// <summary>
    /// 不接任何內容。
    /// </summary>
    Nothing,

    /// <summary>
    /// 空白字元。
    /// </summary>
    Space
}

/// <summary>
/// 表示 ODF schema 中 <c>text:list-level-position-and-space-mode</c> 的清單層級定位模式 token。
/// </summary>
public enum OdfTextListLevelPositionMode
{
    /// <summary>
    /// 使用標籤對齊設定。
    /// </summary>
    LabelAlignment,

    /// <summary>
    /// 使用標籤寬度與位置設定。
    /// </summary>
    LabelWidthAndPosition
}

/// <summary>
/// 表示 ODF schema 中 <c>text:index-scope</c> 的索引範圍 token。
/// </summary>
public enum OdfTextIndexScope
{
    /// <summary>
    /// 章節範圍。
    /// </summary>
    Chapter,

    /// <summary>
    /// 文件範圍。
    /// </summary>
    Document
}

/// <summary>
/// 表示 ODF schema 中 <c>text:table-type</c> 的資料表來源類型 token。
/// </summary>
public enum OdfTextTableType
{
    /// <summary>
    /// 命令來源。
    /// </summary>
    Command,

    /// <summary>
    /// 查詢來源。
    /// </summary>
    Query,

    /// <summary>
    /// 資料表來源。
    /// </summary>
    Table
}

/// <summary>
/// 表示 ODF schema 中 <c>text:anchor-type</c> 的錨定類型 token。
/// </summary>
public enum OdfTextAnchorType
{
    /// <summary>
    /// 作為字元錨定。
    /// </summary>
    AsChar,

    /// <summary>
    /// 錨定至字元。
    /// </summary>
    Char,

    /// <summary>
    /// 錨定至框架。
    /// </summary>
    Frame,

    /// <summary>
    /// 錨定至頁面。
    /// </summary>
    Page,

    /// <summary>
    /// 錨定至段落。
    /// </summary>
    Paragraph
}

/// <summary>
/// 表示 ODF schema 中 <c>text:note-class</c> 的註解類別 token。
/// </summary>
public enum OdfTextNoteClass
{
    /// <summary>
    /// 尾註。
    /// </summary>
    Endnote,

    /// <summary>
    /// 註腳。
    /// </summary>
    Footnote
}

/// <summary>
/// 表示 ODF schema 中 <c>text:select-page</c> 的頁面選取 token。
/// </summary>
public enum OdfTextSelectPage
{
    /// <summary>
    /// 目前頁面。
    /// </summary>
    Current,

    /// <summary>
    /// 下一頁。
    /// </summary>
    Next,

    /// <summary>
    /// 前一頁。
    /// </summary>
    Previous
}
