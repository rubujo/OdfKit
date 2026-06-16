using System;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Chart;

/// <summary>
/// 表示圖表資料序列的高階摘要。
/// </summary>
/// <param name="valuesCellRangeAddress">資料值儲存格範圍位址。</param>
/// <param name="labelCellAddress">選用的標籤儲存格位址。</param>
public sealed class OdfChartSeriesInfo(string valuesCellRangeAddress, string? labelCellAddress)
{
    /// <summary>
    /// 取得資料值儲存格範圍位址。
    /// </summary>
    public string ValuesCellRangeAddress { get; } = valuesCellRangeAddress ?? throw new ArgumentNullException(nameof(valuesCellRangeAddress));

    /// <summary>
    /// 取得選用的標籤儲存格位址。
    /// </summary>
    public string? LabelCellAddress { get; } = labelCellAddress;
}
