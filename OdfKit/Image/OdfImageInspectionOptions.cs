namespace OdfKit.Image;

/// <summary>
/// Configures practical image inspection.
/// 設定實務圖片檢查行為。
/// </summary>
public sealed class OdfImageInspectionOptions
{
    /// <summary>
    /// Gets or sets the byte size that marks an image as large.
    /// 取得或設定判定圖片過大的位元組大小。
    /// </summary>
    public long LargeImageThresholdBytes { get; set; } = 5 * 1024 * 1024;

    /// <summary>
    /// Gets or sets whether missing alternative text should be reported.
    /// 取得或設定是否回報缺少替代文字。
    /// </summary>
    public bool ReportMissingAltText { get; set; } = true;
}
