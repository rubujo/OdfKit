namespace OdfKit.Chart;

/// <summary>
/// Describes one bubble chart series bound to spreadsheet ranges.
/// 描述一組繫結至試算表範圍的泡泡圖序列。
/// </summary>
/// <param name="XValuesCellRangeAddress">The X value range address. / X 值範圍位址。</param>
/// <param name="YValuesCellRangeAddress">The Y value range address. / Y 值範圍位址。</param>
/// <param name="BubbleSizeCellRangeAddress">The bubble size range address. / 泡泡大小範圍位址。</param>
/// <param name="LabelCellAddress">The optional label cell address. / 選用的標籤儲存格位址。</param>
/// <param name="StyleName">The optional series style name. / 選用的序列樣式名稱。</param>
public sealed record OdfBubbleChartSeriesRequest(
    string XValuesCellRangeAddress,
    string YValuesCellRangeAddress,
    string BubbleSizeCellRangeAddress,
    string? LabelCellAddress = null,
    string? StyleName = null);
