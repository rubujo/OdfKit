namespace OdfKit.Chart;

/// <summary>
/// 表示圖表資料序列的趨勢線（迴歸曲線）設定（<c>chart:regression-curve</c>）。
/// </summary>
/// <param name="styleName">套用的樣式名稱，迴歸類型（線性、對數、指數等）由樣式的圖表屬性決定</param>
public sealed class OdfChartRegressionCurveInfo(string? styleName)
{
    /// <summary>
    /// 取得套用的樣式名稱。
    /// </summary>
    public string? StyleName { get; } = styleName;
}
