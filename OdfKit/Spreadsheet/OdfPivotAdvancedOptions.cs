using System;

namespace OdfKit.Spreadsheet;

/// <summary>
/// Specifies which pivot grand totals are materialized.
/// 指定要物化哪些樞紐分析總計。
/// </summary>
public enum OdfPivotGrandTotal
{
    /// <summary>
    /// No grand totals.
    /// 不顯示總計。
    /// </summary>
    None,

    /// <summary>
    /// Grand totals for rows.
    /// 顯示資料列總計。
    /// </summary>
    Row,

    /// <summary>
    /// Grand totals for columns.
    /// 顯示資料欄總計。
    /// </summary>
    Column,

    /// <summary>
    /// Grand totals for both axes.
    /// 顯示雙軸總計。
    /// </summary>
    Both,
}

/// <summary>
/// Specifies the ODF pivot field layout.
/// 指定 ODF 樞紐欄位版面。
/// </summary>
public enum OdfPivotLayout
{
    /// <summary>
    /// Outline layout with subtotals below.
    /// 小計置於下方的大綱版面。
    /// </summary>
    OutlineSubtotalsBottom,

    /// <summary>
    /// Outline layout with subtotals above.
    /// 小計置於上方的大綱版面。
    /// </summary>
    OutlineSubtotalsTop,

    /// <summary>
    /// Tabular layout.
    /// 表格式版面。
    /// </summary>
    Tabular,
}

/// <summary>
/// Specifies a derived display calculation for a pivot value field.
/// 指定樞紐值欄位的衍生顯示計算。
/// </summary>
public enum OdfPivotShowValuesAs
{
    /// <summary>
    /// Displays the aggregate without transformation.
    /// 顯示未轉換的彙總值。
    /// </summary>
    None,

    /// <summary>
    /// Displays a percentage of the row total.
    /// 顯示資料列總計百分比。
    /// </summary>
    PercentageOfRowTotal,

    /// <summary>
    /// Displays a percentage of the column total.
    /// 顯示資料欄總計百分比。
    /// </summary>
    PercentageOfColumnTotal,

    /// <summary>
    /// Displays a percentage of the grand total.
    /// 顯示總計百分比。
    /// </summary>
    PercentageOfGrandTotal,

    /// <summary>
    /// Displays a running total along the row axis.
    /// 沿資料列軸顯示累計總和。
    /// </summary>
    RunningTotal,

    /// <summary>
    /// Displays the difference from a named base member.
    /// 顯示與具名基準成員的差異。
    /// </summary>
    DifferenceFrom,

    /// <summary>
    /// Displays the percentage difference from a named base member.
    /// 顯示與具名基準成員的百分比差異。
    /// </summary>
    PercentageDifferenceFrom,

    /// <summary>
    /// Displays the pivot index calculation.
    /// 顯示樞紐索引計算。
    /// </summary>
    Index,
}

/// <summary>
/// Configures a pivot value display calculation.
/// 設定樞紐值的顯示計算。
/// </summary>
public sealed class OdfPivotValueOptions
{
    /// <summary>
    /// Gets or sets the display calculation.
    /// 取得或設定顯示計算。
    /// </summary>
    public OdfPivotShowValuesAs ShowValuesAs { get; set; }

    /// <summary>
    /// Gets or sets the base field for member-relative calculations.
    /// 取得或設定成員相對計算的基準欄位。
    /// </summary>
    public string? BaseFieldName { get; set; }

    /// <summary>
    /// Gets or sets the named base member.
    /// 取得或設定具名基準成員。
    /// </summary>
    public string? BaseMemberName { get; set; }
}

/// <summary>
/// Configures bounded date or numeric grouping for one pivot field.
/// 設定單一樞紐欄位的有界日期或數值分組。
/// </summary>
public sealed class OdfPivotGroupingOptions
{
    /// <summary>
    /// Gets or sets the date grouping unit, or <see langword="null"/> for numeric grouping.
    /// 取得或設定日期分組單位；數值分組時為 <see langword="null"/>。
    /// </summary>
    public OdfPivotDateGroup? DateGroup { get; set; }

    /// <summary>
    /// Gets or sets the inclusive numeric start.
    /// 取得或設定數值分組的含括起點。
    /// </summary>
    public double? Start { get; set; }

    /// <summary>
    /// Gets or sets the inclusive numeric end.
    /// 取得或設定數值分組的含括終點。
    /// </summary>
    public double? End { get; set; }

    /// <summary>
    /// Gets or sets the positive numeric interval.
    /// 取得或設定正數數值間距。
    /// </summary>
    public double? Interval { get; set; }
}

/// <summary>
/// Specifies a date grouping unit.
/// 指定日期分組單位。
/// </summary>
public enum OdfPivotDateGroup
{
    /// <summary>
    /// Years.
    /// 年。
    /// </summary>
    Years,
    /// <summary>
    /// Quarters.
    /// 季。
    /// </summary>
    Quarters,
    /// <summary>
    /// Months.
    /// 月。
    /// </summary>
    Months,
    /// <summary>
    /// Days.
    /// 日。
    /// </summary>
    Days,
    /// <summary>
    /// Hours.
    /// 小時。
    /// </summary>
    Hours,
    /// <summary>
    /// Minutes.
    /// 分鐘。
    /// </summary>
    Minutes,
    /// <summary>
    /// Seconds.
    /// 秒。
    /// </summary>
    Seconds,
}

/// <summary>
/// Configures existing cell styles applied to materialized pivot output.
/// 設定套用至物化樞紐輸出的既有儲存格樣式。
/// </summary>
public sealed class OdfPivotOutputStyleOptions
{
    /// <summary>
    /// Gets or sets the header cell style name.
    /// 取得或設定標題儲存格樣式名稱。
    /// </summary>
    public string? HeaderStyleName { get; set; }

    /// <summary>
    /// Gets or sets the data cell style name.
    /// 取得或設定資料儲存格樣式名稱。
    /// </summary>
    public string? DataStyleName { get; set; }

    /// <summary>
    /// Gets or sets the grand-total cell style name.
    /// 取得或設定總計儲存格樣式名稱。
    /// </summary>
    public string? GrandTotalStyleName { get; set; }
}
