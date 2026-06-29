namespace OdfKit.Chart;

/// <summary>
/// Represents a mean value line setting for a chart data series (<c>chart:mean-value</c>).
/// 表示圖表資料序列的平均值線設定（<c>chart:mean-value</c>）。
/// </summary>
/// <param name="styleName">The applied style name. / 套用的樣式名稱。</param>
public sealed class OdfChartMeanValueInfo(string? styleName)
{
    /// <summary>
    /// Gets the applied style name.
    /// 取得套用的樣式名稱。
    /// </summary>
    public string? StyleName { get; } = styleName;
}
