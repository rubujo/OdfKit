using OdfKit.Styles;

namespace OdfKit.Drawing;

/// <summary>
/// Represents summary information for a connector on a drawing page.
/// 表示繪圖頁面上一條連接線的摘要資訊。
/// </summary>
/// <param name="pageName">The name of the drawing page. / 所在繪圖頁面名稱。</param>
/// <param name="id">The connector identifier. / 連接線識別碼。</param>
/// <param name="connectorType">The connector geometry type. / 連接線幾何類型。</param>
/// <param name="startShapeId">The start shape identifier (shape-linked mode). / 起點圖形識別碼（圖形連結模式）。</param>
/// <param name="endShapeId">The end shape identifier (shape-linked mode). / 終點圖形識別碼（圖形連結模式）。</param>
/// <param name="startX">The raw start X coordinate text (coordinate mode). / 起點 X 座標原文（座標模式）。</param>
/// <param name="startY">The raw start Y coordinate text (coordinate mode). / 起點 Y 座標原文（座標模式）。</param>
/// <param name="endX">The raw end X coordinate text (coordinate mode). / 終點 X 座標原文（座標模式）。</param>
/// <param name="endY">The raw end Y coordinate text (coordinate mode). / 終點 Y 座標原文（座標模式）。</param>
/// <param name="points">The custom routing vertex coordinate string (<c>draw:points</c>). / 自訂路由頂點座標字串（<c>draw:points</c>）。</param>
public sealed class OdfConnectorInfo(
    string pageName,
    string id,
    OdfConnectorType connectorType,
    string? startShapeId,
    string? endShapeId,
    string? startX,
    string? startY,
    string? endX,
    string? endY,
    string? points = null)
{
    /// <summary>
    /// Gets the name of the drawing page.
    /// 取得所在繪圖頁面名稱。
    /// </summary>
    public string PageName { get; } = pageName ?? string.Empty;

    /// <summary>
    /// Gets the connector identifier.
    /// 取得連接線識別碼。
    /// </summary>
    public string Id { get; } = id ?? string.Empty;

    /// <summary>
    /// Gets the connector geometry type.
    /// 取得連接線幾何類型。
    /// </summary>
    public OdfConnectorType ConnectorType { get; } = connectorType;

    /// <summary>
    /// Gets the start shape identifier.
    /// 取得起點圖形識別碼。
    /// </summary>
    public string? StartShapeId { get; } = startShapeId;

    /// <summary>
    /// Gets the end shape identifier.
    /// 取得終點圖形識別碼。
    /// </summary>
    public string? EndShapeId { get; } = endShapeId;

    /// <summary>
    /// Gets the raw start X coordinate text.
    /// 取得起點 X 座標原文。
    /// </summary>
    public string? StartX { get; } = startX;

    /// <summary>
    /// Gets the raw start Y coordinate text.
    /// 取得起點 Y 座標原文。
    /// </summary>
    public string? StartY { get; } = startY;

    /// <summary>
    /// Gets the raw end X coordinate text.
    /// 取得終點 X 座標原文。
    /// </summary>
    public string? EndX { get; } = endX;

    /// <summary>
    /// Gets the raw end Y coordinate text.
    /// 取得終點 Y 座標原文。
    /// </summary>
    public string? EndY { get; } = endY;

    /// <summary>
    /// Gets the custom routing vertex coordinate string.
    /// 取得自訂路由頂點座標字串。
    /// </summary>
    public string? Points { get; } = points;

    /// <summary>
    /// Gets whether this connector links its endpoints by shape identifier.
    /// 取得此連接線是否以圖形識別碼連結起訖端點。
    /// </summary>
    public bool IsShapeLinked =>
        !string.IsNullOrEmpty(StartShapeId) && !string.IsNullOrEmpty(EndShapeId);

    /// <summary>
    /// Attempts to parse the start point coordinates as <see cref="OdfLength"/>.
    /// 嘗試將起點座標解析為 <see cref="OdfLength"/>。
    /// </summary>
    /// <param name="x">The start X coordinate returned on successful parsing. / 解析成功時傳回的起點 X 座標。</param>
    /// <param name="y">The start Y coordinate returned on successful parsing. / 解析成功時傳回的起點 Y 座標。</param>
    /// <returns><see langword="true"/> if both coordinates can be parsed. / 若兩座標皆可解析則為 <see langword="true"/>。</returns>
    public bool TryGetStartPoint(out OdfLength x, out OdfLength y)
    {
        if (!OdfLength.TryParse(StartX, out x))
        {
            y = default;
            return false;
        }

        return OdfLength.TryParse(StartY, out y);
    }

    /// <summary>
    /// Attempts to parse the end point coordinates as <see cref="OdfLength"/>.
    /// 嘗試將終點座標解析為 <see cref="OdfLength"/>。
    /// </summary>
    /// <param name="x">The end X coordinate returned on successful parsing. / 解析成功時傳回的終點 X 座標。</param>
    /// <param name="y">The end Y coordinate returned on successful parsing. / 解析成功時傳回的終點 Y 座標。</param>
    /// <returns><see langword="true"/> if both coordinates can be parsed. / 若兩座標皆可解析則為 <see langword="true"/>。</returns>
    public bool TryGetEndPoint(out OdfLength x, out OdfLength y)
    {
        if (!OdfLength.TryParse(EndX, out x))
        {
            y = default;
            return false;
        }

        return OdfLength.TryParse(EndY, out y);
    }
}
