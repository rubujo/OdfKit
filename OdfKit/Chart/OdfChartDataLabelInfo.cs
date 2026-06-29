namespace OdfKit.Chart;

/// <summary>
/// Represents the data label settings for a chart data series.
/// 表示圖表資料序列的數據標籤設定。
/// </summary>
/// <param name="showValue">Whether to show the data value. / 是否顯示資料數值。</param>
/// <param name="showPercentage">Whether to show the percentage. / 是否顯示百分比。</param>
/// <param name="showCategoryName">Whether to show the category name. / 是否顯示分類名稱。</param>
/// <param name="showLegendKey">Whether to show the legend key. / 是否顯示圖例符號。</param>
public sealed class OdfChartDataLabelInfo(
    bool showValue = false,
    bool showPercentage = false,
    bool showCategoryName = false,
    bool showLegendKey = false)
{
    /// <summary>
    /// Creates a data label setting from a common preset combination.
    /// 依常用預設組合建立資料標籤設定。
    /// </summary>
    /// <param name="preset">The data label preset combination. / 資料標籤預設組合。</param>
    /// <returns>The corresponding data label setting. / 對應的資料標籤設定。</returns>
    public static OdfChartDataLabelInfo FromPreset(OdfChartDataLabelPreset preset) =>
        preset switch
        {
            OdfChartDataLabelPreset.Value => new(showValue: true),
            OdfChartDataLabelPreset.Percentage => new(showPercentage: true),
            OdfChartDataLabelPreset.ValueAndPercentage => new(showValue: true, showPercentage: true),
            OdfChartDataLabelPreset.ValueAndCategoryName => new(showValue: true, showCategoryName: true),
            OdfChartDataLabelPreset.PercentageAndCategoryName => new(showPercentage: true, showCategoryName: true),
            OdfChartDataLabelPreset.Full => new(showValue: true, showPercentage: true, showCategoryName: true, showLegendKey: true),
            _ => new(),
        };

    /// <summary>
    /// Gets whether to show the data value.
    /// 取得是否顯示資料數值。
    /// </summary>
    public bool ShowValue { get; } = showValue;

    /// <summary>
    /// Gets whether to show the percentage.
    /// 取得是否顯示百分比。
    /// </summary>
    public bool ShowPercentage { get; } = showPercentage;

    /// <summary>
    /// Gets whether to show the category name.
    /// 取得是否顯示分類名稱。
    /// </summary>
    public bool ShowCategoryName { get; } = showCategoryName;

    /// <summary>
    /// Gets whether to show the legend key.
    /// 取得是否顯示圖例符號。
    /// </summary>
    public bool ShowLegendKey { get; } = showLegendKey;
}
