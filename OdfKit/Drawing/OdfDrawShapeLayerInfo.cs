namespace OdfKit.Drawing;

/// <summary>
/// 表示繪圖頁面上一個圖形圖層指派的摘要資訊。
/// </summary>
/// <param name="pageName">所在繪圖頁面名稱</param>
/// <param name="shapeId">圖形識別碼</param>
/// <param name="shapeType">圖形元素本地名稱（例如 frame、rect）</param>
/// <param name="layerName">指派的圖層名稱（<c>draw:layer</c>）</param>
public sealed class OdfDrawShapeLayerInfo(
    string pageName,
    string shapeId,
    string shapeType,
    string layerName)
{
    /// <summary>
    /// 取得所在繪圖頁面名稱。
    /// </summary>
    public string PageName { get; } = pageName ?? string.Empty;

    /// <summary>
    /// 取得圖形識別碼。
    /// </summary>
    public string Id { get; } = shapeId ?? string.Empty;

    /// <summary>
    /// 取得圖形元素本地名稱。
    /// </summary>
    public string ShapeType { get; } = shapeType ?? string.Empty;

    /// <summary>
    /// 取得指派的圖層名稱。
    /// </summary>
    public string LayerName { get; } = layerName ?? string.Empty;
}
