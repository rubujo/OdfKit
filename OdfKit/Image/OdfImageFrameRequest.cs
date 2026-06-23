using OdfKit.Styles;

namespace OdfKit.Image;

/// <summary>
/// 表示批次新增影像框架時的單筆請求。
/// </summary>
/// <param name="imageBytes">圖片位元組陣列</param>
/// <param name="x">X 軸座標位置</param>
/// <param name="y">Y 軸座標位置</param>
/// <param name="width">框架寬度</param>
/// <param name="height">框架高度</param>
/// <param name="preferredName">選用的偏好檔名</param>
/// <param name="name">選用的框架名稱</param>
/// <param name="title">選用的框架標題</param>
/// <param name="description">選用的框架描述</param>
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
    /// 取得圖片位元組陣列。
    /// </summary>
    public byte[] ImageBytes { get; } = imageBytes;

    /// <summary>
    /// 取得 X 軸座標位置。
    /// </summary>
    public OdfLength X { get; } = x;

    /// <summary>
    /// 取得 Y 軸座標位置。
    /// </summary>
    public OdfLength Y { get; } = y;

    /// <summary>
    /// 取得框架寬度。
    /// </summary>
    public OdfLength Width { get; } = width;

    /// <summary>
    /// 取得框架高度。
    /// </summary>
    public OdfLength Height { get; } = height;

    /// <summary>
    /// 取得選用的偏好檔名。
    /// </summary>
    public string? PreferredName { get; } = preferredName;

    /// <summary>
    /// 取得選用的框架名稱。
    /// </summary>
    public string? Name { get; } = name;

    /// <summary>
    /// 取得選用的框架標題。
    /// </summary>
    public string? Title { get; } = title;

    /// <summary>
    /// 取得選用的框架描述。
    /// </summary>
    public string? Description { get; } = description;
}
