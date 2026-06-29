namespace OdfKit.Text;

/// <summary>
/// Represents odf media optimization request.
/// 表示單一媒體項目的最佳化請求。
/// </summary>
/// <param name="PackagePath">The path or URI. / 媒體在 ODF 封裝中的路徑</param>
/// <param name="MediaType">The value to use. / 目前媒體類型</param>
/// <param name="Bytes">The value to use. / 目前媒體內容</param>
/// <param name="Width">The name or identifier. / 圖片框架寬度</param>
/// <param name="Height">The numeric value. / 圖片框架高度</param>
/// <param name="MaxDpi">The value to use. / 目標最大 DPI</param>
/// <param name="JpegQuality">The value to use. / JPEG 輸出品質，範圍為 1 至 100</param>
public sealed record OdfMediaOptimizationRequest(
    string PackagePath,
    string MediaType,
    byte[] Bytes,
    string? Width,
    string? Height,
    double MaxDpi,
    int JpegQuality);
