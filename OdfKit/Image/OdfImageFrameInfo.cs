using OdfKit.Styles;

namespace OdfKit.Image;

/// <summary>
/// 表示 ODI 影像文件中一個影像框架的摘要資訊。
/// </summary>
/// <param name="name">框架名稱（<c>draw:name</c>）</param>
/// <param name="title">框架標題</param>
/// <param name="description">框架描述</param>
/// <param name="imageHref">影像資源參照路徑（<c>xlink:href</c>）</param>
/// <param name="mediaType">影像媒體類型</param>
/// <param name="size">影像位元組大小</param>
/// <param name="x">X 軸座標原文</param>
/// <param name="y">Y 軸座標原文</param>
/// <param name="width">寬度原文</param>
/// <param name="height">高度原文</param>
/// <param name="rotationDegrees">旋轉角度（度）</param>
/// <param name="crop">裁切邊界</param>
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
    /// 取得框架名稱。
    /// </summary>
    public string? Name { get; } = name;

    /// <summary>
    /// 取得框架標題。
    /// </summary>
    public string? Title { get; } = title;

    /// <summary>
    /// 取得框架描述。
    /// </summary>
    public string? Description { get; } = description;

    /// <summary>
    /// 取得影像資源參照路徑。
    /// </summary>
    public string? ImageHref { get; } = imageHref;

    /// <summary>
    /// 取得影像媒體類型。
    /// </summary>
    public string? MediaType { get; } = mediaType;

    /// <summary>
    /// 取得影像位元組大小。
    /// </summary>
    public long? Size { get; } = size;

    /// <summary>
    /// 取得 X 軸座標原文。
    /// </summary>
    public string? X { get; } = x;

    /// <summary>
    /// 取得 Y 軸座標原文。
    /// </summary>
    public string? Y { get; } = y;

    /// <summary>
    /// 取得寬度原文。
    /// </summary>
    public string? Width { get; } = width;

    /// <summary>
    /// 取得高度原文。
    /// </summary>
    public string? Height { get; } = height;

    /// <summary>
    /// 取得旋轉角度（度）。
    /// </summary>
    public double? RotationDegrees { get; } = rotationDegrees;

    /// <summary>
    /// 取得裁切邊界。
    /// </summary>
    public OdfImageCropInfo? Crop { get; } = crop;

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
