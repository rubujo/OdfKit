using System;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Chart;

/// <summary>
/// 表示圖表資料序列的高階摘要。
/// </summary>
/// <param name="valuesCellRangeAddress">資料值儲存格範圍位址。</param>
/// <param name="labelCellAddress">選用的標籤儲存格位址。</param>
/// <param name="seriesClass">序列類型（例如 <c>chart:line</c>）。</param>
/// <param name="styleName">序列樣式名稱。</param>
/// <param name="attachedAxis">附著的座標軸名稱。</param>
public sealed class OdfChartSeriesInfo(
    string valuesCellRangeAddress,
    string? labelCellAddress,
    string? seriesClass,
    string? styleName,
    string? attachedAxis)
{
    /// <summary>
    /// 取得資料值儲存格範圍位址。
    /// </summary>
    public string ValuesCellRangeAddress { get; } = valuesCellRangeAddress ?? throw new ArgumentNullException(nameof(valuesCellRangeAddress));

    /// <summary>
    /// 取得選用的標籤儲存格位址。
    /// </summary>
    public string? LabelCellAddress { get; } = labelCellAddress;

    /// <summary>
    /// 取得序列類型。
    /// </summary>
    public string? SeriesClass { get; } = seriesClass;

    /// <summary>
    /// 取得序列樣式名稱。
    /// </summary>
    public string? StyleName { get; } = styleName;

    /// <summary>
    /// 取得附著的座標軸名稱。
    /// </summary>
    public string? AttachedAxis { get; } = attachedAxis;
}
