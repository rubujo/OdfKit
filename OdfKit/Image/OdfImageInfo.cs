using System;

namespace OdfKit.Image;

/// <summary>
/// Represents a high-level summary of the main image of an ODI document.
/// 表示 ODI 主要影像的高階摘要。
/// </summary>
/// <param name="path">The path of the image within the ODF package. / 影像在 ODF 封裝中的路徑。</param>
/// <param name="mediaType">The media type of the image. / 影像媒體類型。</param>
/// <param name="size">The byte size of the image entry. / 影像專案位元組大小。</param>
public sealed class OdfImageInfo(string path, string mediaType, long size)
{
    /// <summary>
    /// Gets the path of the image within the ODF package.
    /// 取得影像在 ODF 封裝中的路徑。
    /// </summary>
    public string Path { get; } = path ?? throw new ArgumentNullException(nameof(path));

    /// <summary>
    /// Gets the media type of the image.
    /// 取得影像媒體類型。
    /// </summary>
    public string MediaType { get; } = mediaType ?? string.Empty;

    /// <summary>
    /// Gets the byte size of the image entry.
    /// 取得影像專案位元組大小。
    /// </summary>
    public long Size { get; } = size;
}
