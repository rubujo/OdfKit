using OdfKit.Styles;

namespace OdfKit.Drawing;

/// <summary>
/// 表示繪圖頁面上一條連接線的摘要資訊。
/// </summary>
/// <param name="pageName">所在繪圖頁面名稱。</param>
/// <param name="id">連接線識別碼。</param>
/// <param name="connectorType">連接線幾何類型。</param>
/// <param name="startShapeId">起點圖形識別碼（圖形連結模式）。</param>
/// <param name="endShapeId">終點圖形識別碼（圖形連結模式）。</param>
/// <param name="startX">起點 X 座標原文（座標模式）。</param>
/// <param name="startY">起點 Y 座標原文（座標模式）。</param>
/// <param name="endX">終點 X 座標原文（座標模式）。</param>
/// <param name="endY">終點 Y 座標原文（座標模式）。</param>
public sealed class OdfConnectorInfo(
    string pageName,
    string id,
    OdfConnectorType connectorType,
    string? startShapeId,
    string? endShapeId,
    string? startX,
    string? startY,
    string? endX,
    string? endY)
{
    /// <summary>
    /// 取得所在繪圖頁面名稱。
    /// </summary>
    public string PageName { get; } = pageName ?? string.Empty;

    /// <summary>
    /// 取得連接線識別碼。
    /// </summary>
    public string Id { get; } = id ?? string.Empty;

    /// <summary>
    /// 取得連接線幾何類型。
    /// </summary>
    public OdfConnectorType ConnectorType { get; } = connectorType;

    /// <summary>
    /// 取得起點圖形識別碼。
    /// </summary>
    public string? StartShapeId { get; } = startShapeId;

    /// <summary>
    /// 取得終點圖形識別碼。
    /// </summary>
    public string? EndShapeId { get; } = endShapeId;

    /// <summary>
    /// 取得起點 X 座標原文。
    /// </summary>
    public string? StartX { get; } = startX;

    /// <summary>
    /// 取得起點 Y 座標原文。
    /// </summary>
    public string? StartY { get; } = startY;

    /// <summary>
    /// 取得終點 X 座標原文。
    /// </summary>
    public string? EndX { get; } = endX;

    /// <summary>
    /// 取得終點 Y 座標原文。
    /// </summary>
    public string? EndY { get; } = endY;

    /// <summary>
    /// 取得此連接線是否以圖形識別碼連結起訖端點。
    /// </summary>
    public bool IsShapeLinked =>
        !string.IsNullOrEmpty(StartShapeId) && !string.IsNullOrEmpty(EndShapeId);

    /// <summary>
    /// 嘗試將起點座標解析為 <see cref="OdfLength"/>。
    /// </summary>
    /// <param name="x">解析成功時傳回的起點 X 座標。</param>
    /// <param name="y">解析成功時傳回的起點 Y 座標。</param>
    /// <returns>若兩座標皆可解析則為 <see langword="true"/>。</returns>
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
    /// 嘗試將終點座標解析為 <see cref="OdfLength"/>。
    /// </summary>
    /// <param name="x">解析成功時傳回的終點 X 座標。</param>
    /// <param name="y">解析成功時傳回的終點 Y 座標。</param>
    /// <returns>若兩座標皆可解析則為 <see langword="true"/>。</returns>
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
