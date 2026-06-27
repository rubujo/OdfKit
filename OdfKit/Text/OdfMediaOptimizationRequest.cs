namespace OdfKit.Text;

/// <summary>
/// 表示單一媒體項目的最佳化請求。
/// </summary>
/// <param name="PackagePath">媒體在 ODF 封裝中的路徑</param>
/// <param name="MediaType">目前媒體類型</param>
/// <param name="Bytes">目前媒體內容</param>
/// <param name="Width">圖片框架寬度</param>
/// <param name="Height">圖片框架高度</param>
/// <param name="MaxDpi">目標最大 DPI</param>
/// <param name="JpegQuality">JPEG 輸出品質，範圍為 1 至 100</param>
public sealed record OdfMediaOptimizationRequest(
    string PackagePath,
    string MediaType,
    byte[] Bytes,
    string? Width,
    string? Height,
    double MaxDpi,
    int JpegQuality);
