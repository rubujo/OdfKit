namespace OdfKit.Chart;

/// <summary>
/// Summarizes one bubble chart series in document order.
/// 摘要表示文件順序中的一組泡泡圖序列。
/// </summary>
/// <param name="XValuesCellRangeAddress">The X value range address. / X 值範圍位址。</param>
/// <param name="YValuesCellRangeAddress">The Y value range address. / Y 值範圍位址。</param>
/// <param name="BubbleSizeCellRangeAddress">The bubble size range address. / 泡泡大小範圍位址。</param>
/// <param name="LabelCellAddress">The optional label cell address. / 選用的標籤儲存格位址。</param>
/// <param name="StyleName">The optional series style name. / 選用的序列樣式名稱。</param>
public sealed record OdfBubbleChartSeriesInfo(
    string? XValuesCellRangeAddress,
    string? YValuesCellRangeAddress,
    string? BubbleSizeCellRangeAddress,
    string? LabelCellAddress,
    string? StyleName);
