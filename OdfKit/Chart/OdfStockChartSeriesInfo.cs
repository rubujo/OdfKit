namespace OdfKit.Chart;

/// <summary>
/// Summarizes one stock chart series in document order.
/// 摘要表示文件順序中的一組股票圖序列。
/// </summary>
/// <param name="OpenCellRangeAddress">The open price range address. / 開盤價範圍位址。</param>
/// <param name="HighCellRangeAddress">The high price range address. / 最高價範圍位址。</param>
/// <param name="LowCellRangeAddress">The low price range address. / 最低價範圍位址。</param>
/// <param name="CloseCellRangeAddress">The close price range address. / 收盤價範圍位址。</param>
/// <param name="VolumeCellRangeAddress">The optional volume range address. / 選用的成交量範圍位址。</param>
/// <param name="LabelCellAddress">The optional label cell address. / 選用的標籤儲存格位址。</param>
/// <param name="StyleName">The optional series style name. / 選用的序列樣式名稱。</param>
public sealed record OdfStockChartSeriesInfo(
    string? OpenCellRangeAddress,
    string? HighCellRangeAddress,
    string? LowCellRangeAddress,
    string? CloseCellRangeAddress,
    string? VolumeCellRangeAddress,
    string? LabelCellAddress,
    string? StyleName);
