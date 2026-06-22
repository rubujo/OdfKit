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
