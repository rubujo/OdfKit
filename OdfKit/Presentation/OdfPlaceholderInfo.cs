using OdfKit.Styles;

namespace OdfKit.Presentation;

/// <summary>
/// 表示投影片上一個預留位置的摘要資訊。
/// </summary>
/// <param name="id">預留位置圖形識別碼</param>
/// <param name="placeholderType">預留位置類型</param>
/// <param name="x">邊界盒 X 座標原文</param>
/// <param name="y">邊界盒 Y 座標原文</param>
/// <param name="width">邊界盒寬度原文</param>
/// <param name="height">邊界盒高度原文</param>
public sealed class OdfPlaceholderInfo(
    string id,
    OdfPlaceholderType placeholderType,
    string? x,
    string? y,
    string? width,
    string? height)
{
    /// <summary>
    /// 取得預留位置圖形識別碼。
    /// </summary>
    public string Id { get; } = id ?? string.Empty;

    /// <summary>
    /// 取得預留位置類型。
    /// </summary>
    public OdfPlaceholderType PlaceholderType { get; } = placeholderType;

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
}
