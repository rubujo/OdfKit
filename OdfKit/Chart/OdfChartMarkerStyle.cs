namespace OdfKit.Chart;

/// <summary>
/// Describes practical marker styling for line and scatter chart series.
/// 描述折線圖與散佈圖序列的實務標記樣式。
/// </summary>
/// <param name="Symbol">The marker symbol token, such as <c>circle</c> or <c>square</c>. / 標記符號 token，例如 <c>circle</c> 或 <c>square</c>。</param>
/// <param name="Size">The marker size, such as <c>0.25cm</c>. / 標記大小，例如 <c>0.25cm</c>。</param>
/// <param name="FillColor">The optional fill color. / 選用填滿色。</param>
/// <param name="StrokeColor">The optional stroke color. / 選用筆觸色。</param>
public sealed record OdfChartMarkerStyle(
    string? Symbol = null,
    string? Size = null,
    string? FillColor = null,
    string? StrokeColor = null);
