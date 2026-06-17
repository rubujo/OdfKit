namespace OdfKit.Chart;

/// <summary>
/// 表示圖表座標軸的高階摘要資訊。
/// </summary>
/// <param name="dimension">座標軸維度（例如 <c>x</c>、<c>y</c>）。</param>
/// <param name="title">座標軸標題。</param>
/// <param name="logarithmic">是否為對數刻度。</param>
/// <param name="reverseDirection">是否反向顯示。</param>
/// <param name="minimum">刻度最小值。</param>
/// <param name="maximum">刻度最大值。</param>
/// <param name="displayLabels">是否顯示刻度標籤。</param>
/// <param name="hasMajorGrid">是否顯示主網格線。</param>
/// <param name="hasMinorGrid">是否顯示次網格線。</param>
/// <param name="styleName">樣式名稱。</param>
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
    /// 取得座標軸維度。
    /// </summary>
    public string Dimension { get; } = dimension ?? string.Empty;

    /// <summary>
    /// 取得座標軸標題。
    /// </summary>
    public string? Title { get; } = title;

    /// <summary>
    /// 取得是否為對數刻度。
    /// </summary>
    public bool? Logarithmic { get; } = logarithmic;

    /// <summary>
    /// 取得是否反向顯示。
    /// </summary>
    public bool? ReverseDirection { get; } = reverseDirection;

    /// <summary>
    /// 取得刻度最小值。
    /// </summary>
    public double? Minimum { get; } = minimum;

    /// <summary>
    /// 取得刻度最大值。
    /// </summary>
    public double? Maximum { get; } = maximum;

    /// <summary>
    /// 取得是否顯示刻度標籤。
    /// </summary>
    public bool? DisplayLabels { get; } = displayLabels;

    /// <summary>
    /// 取得是否顯示主網格線。
    /// </summary>
    public bool HasMajorGrid { get; } = hasMajorGrid;

    /// <summary>
    /// 取得是否顯示次網格線。
    /// </summary>
    public bool HasMinorGrid { get; } = hasMinorGrid;

    /// <summary>
    /// 取得樣式名稱。
    /// </summary>
    public string? StyleName { get; } = styleName;
}
