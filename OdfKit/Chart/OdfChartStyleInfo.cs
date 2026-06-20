namespace OdfKit.Chart;

/// <summary>
/// 表示圖表自動樣式的高階摘要。
/// </summary>
/// <param name="name">樣式名稱。</param>
/// <param name="fillColor">填滿色（例如 <c>#FF0000</c>）。</param>
/// <param name="strokeColor">筆觸色。</param>
/// <param name="strokeWidth">筆觸寬度（例如 <c>0.05cm</c>）。</param>
/// <param name="fill">填滿樣式（例如 <c>solid</c> 或 <c>none</c>）。</param>
/// <param name="stroke">筆觸樣式（例如 <c>solid</c> 或 <c>none</c>）。</param>
/// <param name="threeDimensional">指出是否為 3D 圖表。</param>
/// <param name="angleOffset">3D 投影角度偏置。</param>
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
    /// 取得樣式名稱。
    /// </summary>
    public string Name { get; } = name ?? throw new System.ArgumentNullException(nameof(name));

    /// <summary>
    /// 取得填滿色。
    /// </summary>
    public string? FillColor { get; } = fillColor;

    /// <summary>
    /// 取得筆觸色。
    /// </summary>
    public string? StrokeColor { get; } = strokeColor;

    /// <summary>
    /// 取得筆觸寬度。
    /// </summary>
    public string? StrokeWidth { get; } = strokeWidth;

    /// <summary>
    /// 取得填滿樣式。
    /// </summary>
    public string? Fill { get; } = fill;

    /// <summary>
    /// 取得筆觸樣式。
    /// </summary>
    public string? Stroke { get; } = stroke;

    /// <summary>
    /// 取得是否為 3D 圖表。
    /// </summary>
    public bool? ThreeDimensional { get; } = threeDimensional;

    /// <summary>
    /// 取得 3D 投影角度偏置。
    /// </summary>
    public int? AngleOffset { get; } = angleOffset;
}
