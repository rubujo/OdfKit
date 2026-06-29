namespace OdfKit.Chart;

/// <summary>
/// Represents high-level summary information for a chart axis.
/// 表示圖表座標軸的高階摘要資訊。
/// </summary>
/// <param name="dimension">The axis dimension (e.g. <c>x</c>, <c>y</c>). / 座標軸維度（例如 <c>x</c>、<c>y</c>）。</param>
/// <param name="title">The axis title. / 座標軸標題。</param>
/// <param name="logarithmic">Whether the scale is logarithmic. / 是否為對數刻度。</param>
/// <param name="reverseDirection">Whether the direction is reversed. / 是否反向顯示。</param>
/// <param name="minimum">The minimum scale value. / 刻度最小值。</param>
/// <param name="maximum">The maximum scale value. / 刻度最大值。</param>
/// <param name="displayLabels">Whether scale labels are displayed. / 是否顯示刻度標籤。</param>
/// <param name="hasMajorGrid">Whether the major grid line is displayed. / 是否顯示主網格線。</param>
/// <param name="hasMinorGrid">Whether the minor grid line is displayed. / 是否顯示次網格線。</param>
/// <param name="styleName">The style name. / 樣式名稱。</param>
public sealed class OdfChartAxisInfo(
    string dimension,
    string? title,
    bool? logarithmic,
    bool? reverseDirection,
    double? minimum,
    double? maximum,
    bool? displayLabels,
    bool hasMajorGrid,
    bool hasMinorGrid,
    string? styleName)
{
    /// <summary>
    /// Gets the axis dimension.
    /// 取得座標軸維度。
    /// </summary>
    public string Dimension { get; } = dimension ?? string.Empty;

    /// <summary>
    /// Gets the axis title.
    /// 取得座標軸標題。
    /// </summary>
    public string? Title { get; } = title;

    /// <summary>
    /// Gets whether the scale is logarithmic.
    /// 取得是否為對數刻度。
    /// </summary>
    public bool? Logarithmic { get; } = logarithmic;

    /// <summary>
    /// Gets whether the direction is reversed.
    /// 取得是否反向顯示。
    /// </summary>
    public bool? ReverseDirection { get; } = reverseDirection;

    /// <summary>
    /// Gets the minimum scale value.
    /// 取得刻度最小值。
    /// </summary>
    public double? Minimum { get; } = minimum;

    /// <summary>
    /// Gets the maximum scale value.
    /// 取得刻度最大值。
    /// </summary>
    public double? Maximum { get; } = maximum;

    /// <summary>
    /// Gets whether scale labels are displayed.
    /// 取得是否顯示刻度標籤。
    /// </summary>
    public bool? DisplayLabels { get; } = displayLabels;

    /// <summary>
    /// Gets whether the major grid line is displayed.
    /// 取得是否顯示主網格線。
    /// </summary>
    public bool HasMajorGrid { get; } = hasMajorGrid;

    /// <summary>
    /// Gets whether the minor grid line is displayed.
    /// 取得是否顯示次網格線。
    /// </summary>
    public bool HasMinorGrid { get; } = hasMinorGrid;

    /// <summary>
    /// Gets the style name.
    /// 取得樣式名稱。
    /// </summary>
    public string? StyleName { get; } = styleName;
}
