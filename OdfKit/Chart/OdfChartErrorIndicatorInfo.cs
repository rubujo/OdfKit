namespace OdfKit.Chart;

/// <summary>
/// Represents an error bar setting for a chart data series (<c>chart:error-indicator</c>).
/// 表示圖表資料序列的誤差棒設定（<c>chart:error-indicator</c>）。
/// </summary>
/// <param name="dimension">The dimension the error bar applies to (e.g. <c>y</c>, <c>x</c>). / 套用誤差棒的維度（例如 <c>y</c>、<c>x</c>）。</param>
/// <param name="styleName">The applied style name; the error amount and category are determined by the style's chart properties. / 套用的樣式名稱，誤差量、誤差類別等細節由樣式的圖表屬性決定。</param>
public sealed class OdfChartErrorIndicatorInfo(string? dimension, string? styleName)
{
    /// <summary>
    /// Gets the dimension the error bar applies to.
    /// 取得套用誤差棒的維度。
    /// </summary>
    public string? Dimension { get; } = dimension;

    /// <summary>
    /// Gets the applied style name.
    /// 取得套用的樣式名稱。
    /// </summary>
    public string? StyleName { get; } = styleName;
}
