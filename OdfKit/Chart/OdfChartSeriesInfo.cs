using System;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Chart;

/// <summary>
/// Represents high-level summary information for a chart data series.
/// 表示圖表資料序列的高階摘要。
/// </summary>
/// <param name="valuesCellRangeAddress">The data value cell range address. / 資料值儲存格範圍位址。</param>
/// <param name="labelCellAddress">The optional label cell address. / 選用的標籤儲存格位址。</param>
/// <param name="seriesClass">The series class (e.g. <c>chart:line</c>). / 序列類型（例如 <c>chart:line</c>）。</param>
/// <param name="styleName">The series style name. / 序列樣式名稱。</param>
/// <param name="attachedAxis">The name of the attached axis. / 附著的座標軸名稱。</param>
public sealed class OdfChartSeriesInfo(
    string valuesCellRangeAddress,
    string? labelCellAddress,
    string? seriesClass,
    string? styleName,
    string? attachedAxis)
{
    /// <summary>
    /// Gets the data value cell range address.
    /// 取得資料值儲存格範圍位址。
    /// </summary>
    public string ValuesCellRangeAddress { get; } = valuesCellRangeAddress ?? throw new ArgumentNullException(nameof(valuesCellRangeAddress));

    /// <summary>
    /// Gets the optional label cell address.
    /// 取得選用的標籤儲存格位址。
    /// </summary>
    public string? LabelCellAddress { get; } = labelCellAddress;

    /// <summary>
    /// Gets the series class.
    /// 取得序列類型。
    /// </summary>
    public string? SeriesClass { get; } = seriesClass;

    /// <summary>
    /// Gets the series style name.
    /// 取得序列樣式名稱。
    /// </summary>
    public string? StyleName { get; } = styleName;

    /// <summary>
    /// Gets the name of the attached axis.
    /// 取得附著的座標軸名稱。
    /// </summary>
    public string? AttachedAxis { get; } = attachedAxis;
}
