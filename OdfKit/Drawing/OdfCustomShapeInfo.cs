using OdfKit.Styles;

namespace OdfKit.Drawing;

/// <summary>
/// 表示繪圖頁面上一個自定義幾何圖形的摘要資訊。
/// </summary>
/// <param name="pageName">所在繪圖頁面名稱</param>
/// <param name="id">圖形識別碼</param>
/// <param name="geometryType">增強幾何類型名稱（<c>draw:enhanced-geometry/@draw:type</c>）</param>
/// <param name="x">邊界盒 X 座標原文</param>
/// <param name="y">邊界盒 Y 座標原文</param>
/// <param name="width">邊界盒寬度原文</param>
/// <param name="height">邊界盒高度原文</param>
public sealed class OdfCustomShapeInfo(
    string pageName,
    string id,
    string geometryType,
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
    /// 取得增強幾何類型名稱。
    /// </summary>
    public string GeometryType { get; } = geometryType ?? string.Empty;

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
    /// <param name="length">解析成功時傳回的長度值</param>
    /// <returns>若解析成功則為 <see langword="true"/></returns>
    public bool TryGetX(out OdfLength length) => OdfLength.TryParse(X, out length);

    /// <summary>
    /// 嘗試將 <see cref="Y"/> 解析為 <see cref="OdfLength"/>。
    /// </summary>
    /// <param name="length">解析成功時傳回的長度值</param>
    /// <returns>若解析成功則為 <see langword="true"/></returns>
    public bool TryGetY(out OdfLength length) => OdfLength.TryParse(Y, out length);

    /// <summary>
    /// 嘗試將 <see cref="Width"/> 解析為 <see cref="OdfLength"/>。
    /// </summary>
    /// <param name="length">解析成功時傳回的長度值</param>
    /// <returns>若解析成功則為 <see langword="true"/></returns>
    public bool TryGetWidth(out OdfLength length) => OdfLength.TryParse(Width, out length);

    /// <summary>
    /// 嘗試將 <see cref="Height"/> 解析為 <see cref="OdfLength"/>。
    /// </summary>
    /// <param name="length">解析成功時傳回的長度值</param>
    /// <returns>若解析成功則為 <see langword="true"/></returns>
    public bool TryGetHeight(out OdfLength length) => OdfLength.TryParse(Height, out length);
}
