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

/// <summary>
/// 表示 ODF schema 中 <c>text:reference-format</c> 的參照顯示格式 token。
/// </summary>
public enum OdfTextReferenceFormat
{
    /// <summary>
    /// 標號文字。
    /// </summary>
    Caption,

    /// <summary>
    /// 類別與值。
    /// </summary>
    CategoryAndValue,

    /// <summary>
    /// 章節。
    /// </summary>
    Chapter,

    /// <summary>
    /// 方向。
    /// </summary>
    Direction,

    /// <summary>
    /// 編號。
    /// </summary>
    Number,

    /// <summary>
    /// 全部上層編號。
    /// </summary>
    NumberAllSuperior,

    /// <summary>
    /// 不含上層編號。
    /// </summary>
    NumberNoSuperior,

    /// <summary>
    /// 頁面。
    /// </summary>
    Page,

    /// <summary>
    /// 文字。
    /// </summary>
    Text,

    /// <summary>
    /// 值。
    /// </summary>
    Value
}

/// <summary>
/// 表示 ODF schema 中 <c>text:start-numbering-at</c> 的起始編號範圍 token。
/// </summary>
public enum OdfTextStartNumberingAt
{
    /// <summary>
    /// 章節。
    /// </summary>
    Chapter,

    /// <summary>
    /// 文件。
    /// </summary>
    Document,

    /// <summary>
    /// 頁面。
    /// </summary>
    Page
}

/// <summary>
/// 表示 ODF schema 中 <c>text:footnotes-position</c> 的註腳位置 token。
/// </summary>
public enum OdfTextFootnotesPosition
{
    /// <summary>
    /// 文件末尾。
    /// </summary>
    Document,

    /// <summary>
    /// 頁面底部。
    /// </summary>
    Page,

    /// <summary>
    /// 區段末尾。
    /// </summary>
    Section,

    /// <summary>
    /// 文字位置。
    /// </summary>
    Text
}

/// <summary>
/// 表示 ODF schema 中 <c>text:caption-sequence-format</c> 的標號序列格式 token。
/// </summary>
public enum OdfTextCaptionSequenceFormat
{
    /// <summary>
    /// 標號。
    /// </summary>
    Caption,

    /// <summary>
    /// 類別與值。
    /// </summary>
    CategoryAndValue,

    /// <summary>
    /// 文字。
    /// </summary>
    Text
}

/// <summary>
/// 表示 ODF schema 中 <c>text:number-position</c> 的編號位置 token。
/// </summary>
public enum OdfTextNumberPosition
{
    /// <summary>
    /// 內側。
    /// </summary>
    Inner,

    /// <summary>
    /// 左側。
    /// </summary>
    Left,

    /// <summary>
    /// 外側。
    /// </summary>
    Outer,

    /// <summary>
    /// 右側。
    /// </summary>
    Right
}

/// <summary>
/// 表示 ODF schema 中 <c>text:placeholder-type</c> 的預留位置類型 token。
/// </summary>
public enum OdfTextPlaceholderType
{
    /// <summary>
    /// 影像。
    /// </summary>
    Image,

    /// <summary>
    /// 物件。
    /// </summary>
    Object,

    /// <summary>
    /// 表格。
    /// </summary>
    Table,

    /// <summary>
    /// 文字。
    /// </summary>
    Text,

    /// <summary>
    /// 文字方塊。
    /// </summary>
    TextBox
}

/// <summary>
/// 表示 ODF schema 中 <c>text:animation</c> 的文字動畫 token。
/// </summary>
public enum OdfTextAnimation
{
    /// <summary>
    /// 交替動畫。
    /// </summary>
    Alternate,

    /// <summary>
    /// 無動畫。
    /// </summary>
    None,

    /// <summary>
    /// 捲動動畫。
    /// </summary>
    Scroll,

    /// <summary>
    /// 滑動動畫。
    /// </summary>
    Slide
}

/// <summary>
/// 表示 ODF schema 中 <c>text:animation-direction</c> 的文字動畫方向 token。
/// </summary>
public enum OdfTextAnimationDirection
{
    /// <summary>
    /// 向下。
    /// </summary>
    Down,

    /// <summary>
    /// 向左。
    /// </summary>
    Left,

    /// <summary>
    /// 向右。
    /// </summary>
    Right,

    /// <summary>
    /// 向上。
    /// </summary>
    Up
}

/// <summary>
/// 表示 ODF schema 中 <c>text:kind</c> 的索引項目種類 token。
/// </summary>
public enum OdfTextKind
{
    /// <summary>
    /// 間距。
    /// </summary>
    Gap,

    /// <summary>
    /// 單位。
    /// </summary>
    Unit,

    /// <summary>
    /// 值。
    /// </summary>
    Value
}
