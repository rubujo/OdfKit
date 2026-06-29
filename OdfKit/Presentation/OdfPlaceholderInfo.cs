using OdfKit.Styles;

namespace OdfKit.Presentation;

/// <summary>
/// Represents summary information for a placeholder on a slide.
/// 表示投影片上一個預留位置的摘要資訊。
/// </summary>
/// <param name="id">The placeholder shape identifier. / 預留位置圖形識別碼。</param>
/// <param name="placeholderType">The placeholder type. / 預留位置類型。</param>
/// <param name="x">The raw bounding-box X coordinate. / 邊界盒 X 座標原文。</param>
/// <param name="y">The raw bounding-box Y coordinate. / 邊界盒 Y 座標原文。</param>
/// <param name="width">The raw bounding-box width. / 邊界盒寬度原文。</param>
/// <param name="height">The raw bounding-box height. / 邊界盒高度原文。</param>
public sealed class OdfPlaceholderInfo(
    string id,
    OdfPlaceholderType placeholderType,
    string? x,
    string? y,
    string? width,
    string? height)
{
    /// <summary>
    /// Gets the placeholder shape identifier.
    /// 取得預留位置圖形識別碼。
    /// </summary>
    public string Id { get; } = id ?? string.Empty;

    /// <summary>
    /// Gets the placeholder type.
    /// 取得預留位置類型。
    /// </summary>
    public OdfPlaceholderType PlaceholderType { get; } = placeholderType;

    /// <summary>
    /// Gets the raw bounding-box X coordinate.
    /// 取得邊界盒 X 座標原文。
    /// </summary>
    public string? X { get; } = x;

    /// <summary>
    /// Gets the raw bounding-box Y coordinate.
    /// 取得邊界盒 Y 座標原文。
    /// </summary>
    public string? Y { get; } = y;

    /// <summary>
    /// Gets the raw bounding-box width.
    /// 取得邊界盒寬度原文。
    /// </summary>
    public string? Width { get; } = width;

    /// <summary>
    /// Gets the raw bounding-box height.
    /// 取得邊界盒高度原文。
    /// </summary>
    public string? Height { get; } = height;
}
