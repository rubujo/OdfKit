namespace OdfKit.Chart;

/// <summary>
/// Represents high-level summary information for a chart automatic style.
/// 表示圖表自動樣式的高階摘要。
/// </summary>
/// <param name="name">The style name. / 樣式名稱。</param>
/// <param name="fillColor">The fill color (e.g. <c>#FF0000</c>). / 填滿色（例如 <c>#FF0000</c>）。</param>
/// <param name="strokeColor">The stroke color. / 筆觸色。</param>
/// <param name="strokeWidth">The stroke width (e.g. <c>0.05cm</c>). / 筆觸寬度（例如 <c>0.05cm</c>）。</param>
/// <param name="fill">The fill style (e.g. <c>solid</c> or <c>none</c>). / 填滿樣式（例如 <c>solid</c> 或 <c>none</c>）。</param>
/// <param name="stroke">The stroke style (e.g. <c>solid</c> or <c>none</c>). / 筆觸樣式（例如 <c>solid</c> 或 <c>none</c>）。</param>
/// <param name="threeDimensional">Indicates whether this is a 3D chart. / 指出是否為 3D 圖表。</param>
/// <param name="angleOffset">The 3D projection angle offset. / 3D 投影角度偏置。</param>
public sealed class OdfChartStyleInfo(
    string name,
    string? fillColor,
    string? strokeColor,
    string? strokeWidth,
    string? fill = null,
    string? stroke = null,
    bool? threeDimensional = null,
    int? angleOffset = null)
{
    /// <summary>
    /// Gets the style name.
    /// 取得樣式名稱。
    /// </summary>
    public string Name { get; } = name ?? throw new System.ArgumentNullException(nameof(name));

    /// <summary>
    /// Gets the fill color.
    /// 取得填滿色。
    /// </summary>
    public string? FillColor { get; } = fillColor;

    /// <summary>
    /// Gets the stroke color.
    /// 取得筆觸色。
    /// </summary>
    public string? StrokeColor { get; } = strokeColor;

    /// <summary>
    /// Gets the stroke width.
    /// 取得筆觸寬度。
    /// </summary>
    public string? StrokeWidth { get; } = strokeWidth;

    /// <summary>
    /// Gets the fill style.
    /// 取得填滿樣式。
    /// </summary>
    public string? Fill { get; } = fill;

    /// <summary>
    /// Gets the stroke style.
    /// 取得筆觸樣式。
    /// </summary>
    public string? Stroke { get; } = stroke;

    /// <summary>
    /// Gets whether this is a 3D chart.
    /// 取得是否為 3D 圖表。
    /// </summary>
    public bool? ThreeDimensional { get; } = threeDimensional;

    /// <summary>
    /// Gets the 3D projection angle offset.
    /// 取得 3D 投影角度偏置。
    /// </summary>
    public int? AngleOffset { get; } = angleOffset;
}
