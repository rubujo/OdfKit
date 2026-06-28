namespace OdfKit.Chart;

/// <summary>
/// 定義常用的圖表資料標籤預設組合。
/// </summary>
public enum OdfChartDataLabelPreset
{
    /// <summary>
    /// 不顯示資料標籤。
    /// </summary>
    None,

    /// <summary>
    /// 只顯示資料數值。
    /// </summary>
    Value,

    /// <summary>
    /// 只顯示百分比。
    /// </summary>
    Percentage,

    /// <summary>
    /// 同時顯示資料數值與百分比。
    /// </summary>
    ValueAndPercentage,

    /// <summary>
    /// 顯示資料數值與分類名稱。
    /// </summary>
    ValueAndCategoryName,

    /// <summary>
    /// 顯示百分比與分類名稱。
    /// </summary>
    PercentageAndCategoryName,

    /// <summary>
    /// 顯示資料數值、百分比、分類名稱與圖例符號。
    /// </summary>
    Full,
}
