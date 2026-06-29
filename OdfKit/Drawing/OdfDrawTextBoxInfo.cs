using OdfKit.Styles;

namespace OdfKit.Drawing;

/// <summary>
/// Represents summary information for a text box on a drawing page.
/// 表示繪圖頁面上一個文字方塊的摘要資訊。
/// </summary>
/// <param name="pageName">The name of the drawing page. / 所在繪圖頁面名稱。</param>
/// <param name="id">The shape identifier. / 圖形識別碼。</param>
/// <param name="text">The text box content. / 文字方塊內容。</param>
/// <param name="x">The raw bounding box X coordinate text. / 邊界盒 X 座標原文。</param>
/// <param name="y">The raw bounding box Y coordinate text. / 邊界盒 Y 座標原文。</param>
/// <param name="width">The raw bounding box width text. / 邊界盒寬度原文。</param>
/// <param name="height">The raw bounding box height text. / 邊界盒高度原文。</param>
public sealed class OdfDrawTextBoxInfo(
    string pageName,
    string id,
    string text,
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
    /// Gets the text box content.
    /// 取得文字方塊內容。
    /// </summary>
    public string Text { get; } = text ?? string.Empty;

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
    public bool TryGetX(out OdfLength length) => OdfLength.TryParse(X, out length);

    /// <summary>
    /// Attempts to parse <see cref="Y"/> as an <see cref="OdfLength"/>.
    /// 嘗試將 <see cref="Y"/> 解析為 <see cref="OdfLength"/>。
    /// </summary>
    public bool TryGetY(out OdfLength length) => OdfLength.TryParse(Y, out length);

    /// <summary>
    /// Attempts to parse <see cref="Width"/> as an <see cref="OdfLength"/>.
    /// 嘗試將 <see cref="Width"/> 解析為 <see cref="OdfLength"/>。
    /// </summary>
    public bool TryGetWidth(out OdfLength length) => OdfLength.TryParse(Width, out length);

    /// <summary>
    /// Attempts to parse <see cref="Height"/> as an <see cref="OdfLength"/>.
    /// 嘗試將 <see cref="Height"/> 解析為 <see cref="OdfLength"/>。
    /// </summary>
    public bool TryGetHeight(out OdfLength length) => OdfLength.TryParse(Height, out length);
}
