namespace OdfKit.Chart;

/// <summary>
/// 表示圖表資料序列的數據標籤設定。
/// </summary>
/// <param name="showValue">是否顯示資料數值</param>
/// <param name="showPercentage">是否顯示百分比</param>
/// <param name="showCategoryName">是否顯示分類名稱</param>
/// <param name="showLegendKey">是否顯示圖例符號</param>
public sealed class OdfChartDataLabelInfo(
    bool showValue = false,
    bool showPercentage = false,
    bool showCategoryName = false,
    bool showLegendKey = false)
{
    /// <summary>
    /// 依常用預設組合建立資料標籤設定。
    /// </summary>
    /// <param name="preset">資料標籤預設組合</param>
    /// <returns>對應的資料標籤設定</returns>
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
    /// 取得是否顯示資料數值。
    /// </summary>
    public bool ShowValue { get; } = showValue;

    /// <summary>
    /// 取得是否顯示百分比。
    /// </summary>
    public bool ShowPercentage { get; } = showPercentage;

    /// <summary>
    /// 取得是否顯示分類名稱。
    /// </summary>
    public bool ShowCategoryName { get; } = showCategoryName;

    /// <summary>
    /// 取得是否顯示圖例符號。
    /// </summary>
    public bool ShowLegendKey { get; } = showLegendKey;
}
