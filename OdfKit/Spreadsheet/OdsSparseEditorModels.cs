using System;

namespace OdfKit.Spreadsheet;

/// <summary>
/// Specifies how a sparse cell patch changes an existing merged range.
/// 指定稀疏儲存格修補如何變更既有合併範圍。
/// </summary>
public enum OdsSparseMergeMode
{
    /// <summary>
    /// Preserves an existing merge; spans greater than one retain the legacy behavior of creating a merge.
    /// 保留既有合併；大於一的 span 仍維持建立合併的既有行為。
    /// </summary>
    Preserve,

    /// <summary>
    /// Sets or resizes the merged range to the requested row and column spans.
    /// 將合併範圍設定或調整為要求的列與欄 span。
    /// </summary>
    Set,

    /// <summary>
    /// Removes the merged range anchored at the target cell.
    /// 移除以目標儲存格為錨點的合併範圍。
    /// </summary>
    Remove
}

/// <summary>
/// Specifies horizontal alignment for a sparse automatic cell style.
/// 指定稀疏 automatic cell style 的水平對齊方式。
/// </summary>
public enum OdsSparseHorizontalAlignment
{
    /// <summary>
    /// Uses the writing-mode start edge.
    /// 使用書寫方向的起始邊。
    /// </summary>
    Start,

    /// <summary>
    /// Centers the content.
    /// 將內容置中。
    /// </summary>
    Center,

    /// <summary>
    /// Uses the writing-mode end edge.
    /// 使用書寫方向的結束邊。
    /// </summary>
    End,

    /// <summary>
    /// Justifies the content.
    /// 將內容左右對齊。
    /// </summary>
    Justify
}

/// <summary>
/// Specifies vertical alignment for a sparse automatic cell style.
/// 指定稀疏 automatic cell style 的垂直對齊方式。
/// </summary>
public enum OdsSparseVerticalAlignment
{
    /// <summary>
    /// Aligns content to the top.
    /// 將內容靠上對齊。
    /// </summary>
    Top,

    /// <summary>
    /// Aligns content to the middle.
    /// 將內容置中對齊。
    /// </summary>
    Middle,

    /// <summary>
    /// Aligns content to the bottom.
    /// 將內容靠下對齊。
    /// </summary>
    Bottom
}

/// <summary>
/// Describes one bounded automatic table-cell style created by <see cref="OdsSparseEditor"/>.
/// 描述由 <see cref="OdsSparseEditor"/> 建立的單一有界 automatic table-cell style。
/// </summary>
public sealed class OdsSparseAutomaticCellStyle
{
    /// <summary>
    /// Gets or sets the unique ODF style name.
    /// 取得或設定唯一的 ODF 樣式名稱。
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets an existing or batch-created parent cell style name.
    /// 取得或設定既有或同批建立的父儲存格樣式名稱。
    /// </summary>
    public string? ParentStyleName { get; set; }

    /// <summary>
    /// Gets or sets the font family.
    /// 取得或設定字型家族。
    /// </summary>
    public string? FontFamily { get; set; }

    /// <summary>
    /// Gets or sets the font size in points.
    /// 取得或設定以點為單位的字型大小。
    /// </summary>
    public double? FontSizePoints { get; set; }

    /// <summary>
    /// Gets or sets whether the text is bold.
    /// 取得或設定文字是否為粗體。
    /// </summary>
    public bool? Bold { get; set; }

    /// <summary>
    /// Gets or sets whether the text is italic.
    /// 取得或設定文字是否為斜體。
    /// </summary>
    public bool? Italic { get; set; }

    /// <summary>
    /// Gets or sets the text color as <c>#RRGGBB</c>.
    /// 取得或設定格式為 <c>#RRGGBB</c> 的文字色彩。
    /// </summary>
    public string? TextColor { get; set; }

    /// <summary>
    /// Gets or sets the background color as <c>#RRGGBB</c> or <c>transparent</c>.
    /// 取得或設定格式為 <c>#RRGGBB</c> 或 <c>transparent</c> 的背景色彩。
    /// </summary>
    public string? BackgroundColor { get; set; }

    /// <summary>
    /// Gets or sets whether cell text wraps.
    /// 取得或設定儲存格文字是否換行。
    /// </summary>
    public bool? WrapText { get; set; }

    /// <summary>
    /// Gets or sets the horizontal alignment.
    /// 取得或設定水平對齊方式。
    /// </summary>
    public OdsSparseHorizontalAlignment? HorizontalAlignment { get; set; }

    /// <summary>
    /// Gets or sets the vertical alignment.
    /// 取得或設定垂直對齊方式。
    /// </summary>
    public OdsSparseVerticalAlignment? VerticalAlignment { get; set; }
}
