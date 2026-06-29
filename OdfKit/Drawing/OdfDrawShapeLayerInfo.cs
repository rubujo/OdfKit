namespace OdfKit.Drawing;

/// <summary>
/// Represents summary information for a shape's layer assignment on a drawing page.
/// 表示繪圖頁面上一個圖形圖層指派的摘要資訊。
/// </summary>
/// <param name="pageName">The name of the drawing page. / 所在繪圖頁面名稱。</param>
/// <param name="shapeId">The shape identifier. / 圖形識別碼。</param>
/// <param name="shapeType">The local name of the shape element (e.g. frame, rect). / 圖形元素本地名稱（例如 frame、rect）。</param>
/// <param name="layerName">The assigned layer name (<c>draw:layer</c>). / 指派的圖層名稱（<c>draw:layer</c>）。</param>
public sealed class OdfDrawShapeLayerInfo(
    string pageName,
    string shapeId,
    string shapeType,
    string layerName)
{
    /// <summary>
    /// Gets the name of the drawing page.
    /// 取得所在繪圖頁面名稱。
    /// </summary>
    public string PageName { get; } = pageName ?? string.Empty;

    /// <summary>
    /// Gets the shape identifier.
    /// 取得圖形識別碼。
    /// </summary>
    public string Id { get; } = shapeId ?? string.Empty;

    /// <summary>
    /// Gets the local name of the shape element.
    /// 取得圖形元素本地名稱。
    /// </summary>
    public string ShapeType { get; } = shapeType ?? string.Empty;

    /// <summary>
    /// Gets the assigned layer name.
    /// 取得指派的圖層名稱。
    /// </summary>
    public string LayerName { get; } = layerName ?? string.Empty;
}
