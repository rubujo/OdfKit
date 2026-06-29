namespace OdfKit.Chart;

/// <summary>
/// Represents a trend line (regression curve) setting for a chart data series (<c>chart:regression-curve</c>).
/// 表示圖表資料序列的趨勢線（迴歸曲線）設定（<c>chart:regression-curve</c>）。
/// </summary>
/// <param name="styleName">The applied style name; the regression type (linear, logarithmic, exponential, etc.) is determined by the style's chart properties. / 套用的樣式名稱，迴歸類型（線性、對數、指數等）由樣式的圖表屬性決定。</param>
public sealed class OdfChartRegressionCurveInfo(string? styleName)
{
    /// <summary>
    /// Gets the applied style name.
    /// 取得套用的樣式名稱。
    /// </summary>
    public string? StyleName { get; } = styleName;
}
