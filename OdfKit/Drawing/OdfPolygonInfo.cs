using OdfKit.Styles;

namespace OdfKit.Drawing;

/// <summary>
/// Represents summary information for a polygon shape on a drawing page.
/// 表示繪圖頁面上一個多邊形圖形的摘要資訊。
/// </summary>
/// <param name="pageName">The name of the drawing page. / 所在繪圖頁面名稱。</param>
/// <param name="id">The shape identifier. / 圖形識別碼。</param>
/// <param name="points">The relative vertex coordinate string (<c>draw:points</c>). / 相對頂點座標字串（<c>draw:points</c>）。</param>
/// <param name="x">The raw bounding box X coordinate text. / 邊界盒 X 座標原文。</param>
/// <param name="y">The raw bounding box Y coordinate text. / 邊界盒 Y 座標原文。</param>
/// <param name="width">The raw bounding box width text. / 邊界盒寬度原文。</param>
/// <param name="height">The raw bounding box height text. / 邊界盒高度原文。</param>
public sealed class OdfPolygonInfo(
    string pageName,
    string id,
    string points,
    string? x,
    string? y,
    string? width,
    string? height)
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
    public string Id { get; } = id ?? string.Empty;

    /// <summary>
    /// Gets the relative vertex coordinate string.
    /// 取得相對頂點座標字串。
    /// </summary>
    public string Points { get; } = points ?? string.Empty;

    /// <summary>
    /// Gets the raw bounding box X coordinate text.
    /// 取得邊界盒 X 座標原文。
    /// </summary>
    public string? X { get; } = x;

    /// <summary>
    /// Gets the raw bounding box Y coordinate text.
    /// 取得邊界盒 Y 座標原文。
    /// </summary>
    public string? Y { get; } = y;

    /// <summary>
    /// Gets the raw bounding box width text.
    /// 取得邊界盒寬度原文。
    /// </summary>
    public string? Width { get; } = width;

    /// <summary>
    /// Gets the raw bounding box height text.
    /// 取得邊界盒高度原文。
    /// </summary>
    public string? Height { get; } = height;

    /// <summary>
    /// Attempts to parse <see cref="X"/> as an <see cref="OdfLength"/>.
    /// 嘗試將 <see cref="X"/> 解析為 <see cref="OdfLength"/>。
    /// </summary>
    /// <param name="length">The length value returned on successful parsing. / 解析成功時傳回的長度值。</param>
    /// <returns><see langword="true"/> if parsing succeeded. / 若解析成功則為 <see langword="true"/>。</returns>
    public bool TryGetX(out OdfLength length) => OdfLength.TryParse(X, out length);

    /// <summary>
    /// Attempts to parse <see cref="Y"/> as an <see cref="OdfLength"/>.
    /// 嘗試將 <see cref="Y"/> 解析為 <see cref="OdfLength"/>。
    /// </summary>
    /// <param name="length">The length value returned on successful parsing. / 解析成功時傳回的長度值。</param>
    /// <returns><see langword="true"/> if parsing succeeded. / 若解析成功則為 <see langword="true"/>。</returns>
    public bool TryGetY(out OdfLength length) => OdfLength.TryParse(Y, out length);
}
