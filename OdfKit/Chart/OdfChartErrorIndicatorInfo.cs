namespace OdfKit.Chart;

/// <summary>
/// 表示圖表資料序列的誤差棒設定（<c>chart:error-indicator</c>）。
/// </summary>
/// <param name="dimension">套用誤差棒的維度（例如 <c>y</c>、<c>x</c>）</param>
/// <param name="styleName">套用的樣式名稱，誤差量、誤差類別等細節由樣式的圖表屬性決定</param>
public sealed class OdfChartErrorIndicatorInfo(string? dimension, string? styleName)
{
    /// <summary>
    /// 取得套用誤差棒的維度。
    /// </summary>
    public string? Dimension { get; } = dimension;

    /// <summary>
    /// 取得套用的樣式名稱。
    /// </summary>
    public string? StyleName { get; } = styleName;
}
