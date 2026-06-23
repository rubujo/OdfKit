namespace OdfKit.Chart;

/// <summary>
/// 表示圖表資料序列的平均值線設定（<c>chart:mean-value</c>）。
/// </summary>
/// <param name="styleName">套用的樣式名稱</param>
public sealed class OdfChartMeanValueInfo(string? styleName)
{
    /// <summary>
    /// 取得套用的樣式名稱。
    /// </summary>
    public string? StyleName { get; } = styleName;
}
