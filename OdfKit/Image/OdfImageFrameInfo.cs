using OdfKit.Styles;

namespace OdfKit.Image;

/// <summary>
/// Represents summary information for an image frame in an ODI image document.
/// 表示 ODI 影像文件中一個影像框架的摘要資訊。
/// </summary>
/// <param name="name">The frame name (<c>draw:name</c>). / 框架名稱（<c>draw:name</c>）。</param>
/// <param name="title">The frame title. / 框架標題。</param>
/// <param name="description">The frame description. / 框架描述。</param>
/// <param name="imageHref">The image resource reference path (<c>xlink:href</c>). / 影像資源參照路徑（<c>xlink:href</c>）。</param>
/// <param name="mediaType">The image media type. / 影像媒體類型。</param>
/// <param name="size">The image byte size. / 影像位元組大小。</param>
/// <param name="x">The raw X-axis coordinate text. / X 軸座標原文。</param>
/// <param name="y">The raw Y-axis coordinate text. / Y 軸座標原文。</param>
/// <param name="width">The raw width text. / 寬度原文。</param>
/// <param name="height">The raw height text. / 高度原文。</param>
/// <param name="rotationDegrees">The rotation angle in degrees. / 旋轉角度（度）。</param>
/// <param name="crop">The crop bounds. / 裁切邊界。</param>
public sealed class OdfImageFrameInfo(
    string? name,
    string? title,
    string? description,
    string? imageHref,
    string? mediaType,
    long? size,
    string? x,
    string? y,
    string? width,
    string? height,
    double? rotationDegrees = null,
    OdfImageCropInfo? crop = null)
{
    /// <summary>
    /// Gets the frame name.
    /// 取得框架名稱。
    /// </summary>
    public string? Name { get; } = name;

    /// <summary>
    /// Gets the frame title.
    /// 取得框架標題。
    /// </summary>
    public string? Title { get; } = title;

    /// <summary>
    /// Gets the frame description.
    /// 取得框架描述。
    /// </summary>
    public string? Description { get; } = description;

    /// <summary>
    /// Gets the image resource reference path.
    /// 取得影像資源參照路徑。
    /// </summary>
    public string? ImageHref { get; } = imageHref;

    /// <summary>
    /// Gets the image media type.
    /// 取得影像媒體類型。
    /// </summary>
    public string? MediaType { get; } = mediaType;

    /// <summary>
    /// Gets the image byte size.
    /// 取得影像位元組大小。
    /// </summary>
    public long? Size { get; } = size;

    /// <summary>
    /// Gets the raw X-axis coordinate text.
    /// 取得 X 軸座標原文。
    /// </summary>
    public string? X { get; } = x;

    /// <summary>
    /// Gets the raw Y-axis coordinate text.
    /// 取得 Y 軸座標原文。
    /// </summary>
    public string? Y { get; } = y;

    /// <summary>
    /// Gets the raw width text.
    /// 取得寬度原文。
    /// </summary>
    public string? Width { get; } = width;

    /// <summary>
    /// Gets the raw height text.
    /// 取得高度原文。
    /// </summary>
    public string? Height { get; } = height;

    /// <summary>
    /// Gets the rotation angle in degrees.
    /// 取得旋轉角度（度）。
    /// </summary>
    public double? RotationDegrees { get; } = rotationDegrees;

    /// <summary>
    /// Gets the crop bounds.
    /// 取得裁切邊界。
    /// </summary>
    public OdfImageCropInfo? Crop { get; } = crop;

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

    /// <summary>
    /// Attempts to parse <see cref="Width"/> as an <see cref="OdfLength"/>.
    /// 嘗試將 <see cref="Width"/> 解析為 <see cref="OdfLength"/>。
    /// </summary>
    /// <param name="length">The length value returned on successful parsing. / 解析成功時傳回的長度值。</param>
    /// <returns><see langword="true"/> if parsing succeeded. / 若解析成功則為 <see langword="true"/>。</returns>
    public bool TryGetWidth(out OdfLength length) => OdfLength.TryParse(Width, out length);

    /// <summary>
    /// Attempts to parse <see cref="Height"/> as an <see cref="OdfLength"/>.
    /// 嘗試將 <see cref="Height"/> 解析為 <see cref="OdfLength"/>。
    /// </summary>
    /// <param name="length">The length value returned on successful parsing. / 解析成功時傳回的長度值。</param>
    /// <returns><see langword="true"/> if parsing succeeded. / 若解析成功則為 <see langword="true"/>。</returns>
    public bool TryGetHeight(out OdfLength length) => OdfLength.TryParse(Height, out length);
}
