namespace OdfKit.Chart;

/// <summary>
/// Describes one stock chart series bound to OHLC spreadsheet ranges.
/// 描述一組繫結至 OHLC 試算表範圍的股票圖序列。
/// </summary>
/// <param name="OpenCellRangeAddress">The open price range address. / 開盤價範圍位址。</param>
/// <param name="HighCellRangeAddress">The high price range address. / 最高價範圍位址。</param>
/// <param name="LowCellRangeAddress">The low price range address. / 最低價範圍位址。</param>
/// <param name="CloseCellRangeAddress">The close price range address. / 收盤價範圍位址。</param>
/// <param name="VolumeCellRangeAddress">The optional volume range address. / 選用的成交量範圍位址。</param>
/// <param name="LabelCellAddress">The optional label cell address. / 選用的標籤儲存格位址。</param>
/// <param name="StyleName">The optional series style name. / 選用的序列樣式名稱。</param>
public sealed record OdfStockChartSeriesRequest(
    string OpenCellRangeAddress,
    string HighCellRangeAddress,
    string LowCellRangeAddress,
    string CloseCellRangeAddress,
    string? VolumeCellRangeAddress = null,
    string? LabelCellAddress = null,
    string? StyleName = null);
