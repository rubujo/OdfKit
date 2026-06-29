namespace OdfKit.Text;

/// <summary>
/// Represents an optimization request for a single media item.
/// 表示單一媒體項目的最佳化請求。
/// </summary>
/// <param name="PackagePath">The media's path within the ODF package. / 媒體在 ODF 封裝中的路徑。</param>
/// <param name="MediaType">The current media type. / 目前媒體類型。</param>
/// <param name="Bytes">The current media content. / 目前媒體內容。</param>
/// <param name="Width">The picture frame width. / 圖片框架寬度。</param>
/// <param name="Height">The picture frame height. / 圖片框架高度。</param>
/// <param name="MaxDpi">The target maximum DPI. / 目標最大 DPI。</param>
/// <param name="JpegQuality">The JPEG output quality, ranging from 1 to 100. / JPEG 輸出品質，範圍為 1 至 100。</param>
public sealed record OdfMediaOptimizationRequest(
    string PackagePath,
    string MediaType,
    byte[] Bytes,
    string? Width,
    string? Height,
    double MaxDpi,
    int JpegQuality);
