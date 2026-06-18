namespace OdfKit.Chart;

/// <summary>
/// 表示圖表自動樣式的高階摘要。
/// </summary>
/// <param name="name">樣式名稱。</param>
/// <param name="fillColor">填滿色（例如 <c>#FF0000</c>）。</param>
/// <param name="strokeColor">筆觸色。</param>
/// <param name="strokeWidth">筆觸寬度（例如 <c>0.05cm</c>）。</param>
public sealed class OdfChartStyleInfo(
    string name,
    string? fillColor,
    string? strokeColor,
    string? strokeWidth)
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
}
