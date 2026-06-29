namespace OdfKit.Chart;

/// <summary>
/// Represents a single <c>chart:data-point</c> style override entry for a chart data series.
/// 表示圖表資料序列中一筆 <c>chart:data-point</c> 樣式覆蓋設定。
/// </summary>
/// <param name="repeated">The number of consecutive data points this entry applies to. / 此筆設定套用的連續資料點數量。</param>
/// <param name="styleName">The applied style name. / 套用的樣式名稱。</param>
public sealed class OdfChartDataPointInfo(int repeated, string? styleName)
{
    /// <summary>
    /// Gets the number of consecutive data points this entry applies to.
    /// 取得此筆設定套用的連續資料點數量。
    /// </summary>
    public int Repeated { get; } = repeated;

    /// <summary>
    /// Gets the applied style name.
    /// 取得套用的樣式名稱。
    /// </summary>
    public string? StyleName { get; } = styleName;
}
