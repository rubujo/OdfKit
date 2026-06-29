namespace OdfKit.Chart;

/// <summary>
/// Defines common chart data label preset combinations.
/// 定義常用的圖表資料標籤預設組合。
/// </summary>
public enum OdfChartDataLabelPreset
{
    /// <summary>
    /// No data label is shown.
    /// 不顯示資料標籤。
    /// </summary>
    None,

    /// <summary>
    /// Only the data value is shown.
    /// 只顯示資料數值。
    /// </summary>
    Value,

    /// <summary>
    /// Only the percentage is shown.
    /// 只顯示百分比。
    /// </summary>
    Percentage,

    /// <summary>
    /// Both the data value and the percentage are shown.
    /// 同時顯示資料數值與百分比。
    /// </summary>
    ValueAndPercentage,

    /// <summary>
    /// The data value and the category name are shown.
    /// 顯示資料數值與分類名稱。
    /// </summary>
    ValueAndCategoryName,

    /// <summary>
    /// The percentage and the category name are shown.
    /// 顯示百分比與分類名稱。
    /// </summary>
    PercentageAndCategoryName,

    /// <summary>
    /// The data value, percentage, category name, and legend key are all shown.
    /// 顯示資料數值、百分比、分類名稱與圖例符號。
    /// </summary>
    Full,
}
