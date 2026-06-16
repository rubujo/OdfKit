using System;

namespace OdfKit.Image;

/// <summary>
/// 表示 ODI 主要影像的高階摘要。
/// </summary>
/// <param name="path">影像在 ODF 封裝中的路徑。</param>
/// <param name="mediaType">影像媒體類型。</param>
/// <param name="size">影像項目位元組大小。</param>
public sealed class OdfImageInfo(string path, string mediaType, long size)
{
    /// <summary>
    /// 取得影像在 ODF 封裝中的路徑。
    /// </summary>
    public string Path { get; } = path ?? throw new ArgumentNullException(nameof(path));

    /// <summary>
    /// 取得影像媒體類型。
    /// </summary>
    public string MediaType { get; } = mediaType ?? string.Empty;

    /// <summary>
    /// 取得影像項目位元組大小。
    /// </summary>
    public long Size { get; } = size;
}
