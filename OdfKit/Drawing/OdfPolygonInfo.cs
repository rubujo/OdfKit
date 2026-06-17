using OdfKit.Styles;

namespace OdfKit.Drawing;

/// <summary>
/// 表示繪圖頁面上一個多邊形圖形的摘要資訊。
/// </summary>
/// <param name="pageName">所在繪圖頁面名稱。</param>
/// <param name="id">圖形識別碼。</param>
/// <param name="points">相對頂點座標字串（<c>draw:points</c>）。</param>
/// <param name="x">邊界盒 X 座標原文。</param>
/// <param name="y">邊界盒 Y 座標原文。</param>
/// <param name="width">邊界盒寬度原文。</param>
/// <param name="height">邊界盒高度原文。</param>
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
    /// 取得所在繪圖頁面名稱。
    /// </summary>
    public string PageName { get; } = pageName ?? string.Empty;

    /// <summary>
    /// 取得圖形識別碼。
    /// </summary>
    public string Id { get; } = id ?? string.Empty;

    /// <summary>
    /// 取得相對頂點座標字串。
    /// </summary>
    public string Points { get; } = points ?? string.Empty;

    /// <summary>
    /// 取得邊界盒 X 座標原文。
    /// </summary>
    public string? X { get; } = x;

    /// <summary>
    /// 取得邊界盒 Y 座標原文。
    /// </summary>
    public string? Y { get; } = y;

    /// <summary>
    /// 取得邊界盒寬度原文。
    /// </summary>
    public string? Width { get; } = width;

    /// <summary>
    /// 取得邊界盒高度原文。
    /// </summary>
    public string? Height { get; } = height;

    /// <summary>
    /// 嘗試將 <see cref="X"/> 解析為 <see cref="OdfLength"/>。
    /// </summary>
    /// <param name="length">解析成功時傳回的長度值。</param>
    /// <returns>若解析成功則為 <see langword="true"/>。</returns>
    public bool TryGetX(out OdfLength length) => OdfLength.TryParse(X, out length);

    /// <summary>
    /// 嘗試將 <see cref="Y"/> 解析為 <see cref="OdfLength"/>。
    /// </summary>
    /// <param name="length">解析成功時傳回的長度值。</param>
    /// <returns>若解析成功則為 <see langword="true"/>。</returns>
    public bool TryGetY(out OdfLength length) => OdfLength.TryParse(Y, out length);
}
