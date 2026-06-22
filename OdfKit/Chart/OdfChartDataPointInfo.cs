namespace OdfKit.Chart;

/// <summary>
/// 表示圖表資料序列中一筆 <c>chart:data-point</c> 樣式覆蓋設定。
/// </summary>
/// <param name="repeated">此筆設定套用的連續資料點數量</param>
/// <param name="styleName">套用的樣式名稱</param>
public sealed class OdfChartDataPointInfo(int repeated, string? styleName)
{
    /// <summary>
    /// 取得此筆設定套用的連續資料點數量。
    /// </summary>
    public int Repeated { get; } = repeated;

    /// <summary>
    /// 取得套用的樣式名稱。
    /// </summary>
    public string? StyleName { get; } = styleName;
}
