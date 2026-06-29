using OdfKit.Styles;

namespace OdfKit.Image;

/// <summary>
/// Represents a single request item when batch-adding image frames.
/// 表示批次新增影像框架時的單筆請求。
/// </summary>
/// <param name="imageBytes">The image byte array. / 圖片位元組陣列。</param>
/// <param name="x">The X-axis position. / X 軸座標位置。</param>
/// <param name="y">The Y-axis position. / Y 軸座標位置。</param>
/// <param name="width">The frame width. / 框架寬度。</param>
/// <param name="height">The frame height. / 框架高度。</param>
/// <param name="preferredName">The optional preferred file name. / 選用的偏好檔名。</param>
/// <param name="name">The optional frame name. / 選用的框架名稱。</param>
/// <param name="title">The optional frame title. / 選用的框架標題。</param>
/// <param name="description">The optional frame description. / 選用的框架描述。</param>
public sealed class OdfImageFrameRequest(
    byte[] imageBytes,
    OdfLength x,
    OdfLength y,
    OdfLength width,
    OdfLength height,
    string? preferredName = null,
    string? name = null,
    string? title = null,
    string? description = null)
{
    /// <summary>
    /// Gets the image byte array.
    /// 取得圖片位元組陣列。
    /// </summary>
    public byte[] ImageBytes { get; } = imageBytes;

    /// <summary>
    /// Gets the X-axis position.
    /// 取得 X 軸座標位置。
    /// </summary>
    public OdfLength X { get; } = x;

    /// <summary>
    /// Gets the Y-axis position.
    /// 取得 Y 軸座標位置。
    /// </summary>
    public OdfLength Y { get; } = y;

    /// <summary>
    /// Gets the frame width.
    /// 取得框架寬度。
    /// </summary>
    public OdfLength Width { get; } = width;

    /// <summary>
    /// Gets the frame height.
    /// 取得框架高度。
    /// </summary>
    public OdfLength Height { get; } = height;

    /// <summary>
    /// Gets the optional preferred file name.
    /// 取得選用的偏好檔名。
    /// </summary>
    public string? PreferredName { get; } = preferredName;

    /// <summary>
    /// Gets the optional frame name.
    /// 取得選用的框架名稱。
    /// </summary>
    public string? Name { get; } = name;

    /// <summary>
    /// Gets the optional frame title.
    /// 取得選用的框架標題。
    /// </summary>
    public string? Title { get; } = title;

    /// <summary>
    /// Gets the optional frame description.
    /// 取得選用的框架描述。
    /// </summary>
    public string? Description { get; } = description;
}
