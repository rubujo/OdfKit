namespace OdfKit.WebFonts.Worker;

/// <summary>
/// Defines hard resource limits for background WebFont generation.
/// 定義背景 WebFont 產生工作的資源硬性限制。
/// </summary>
public sealed class WebFontWorkerOptions
{
    /// <summary>
    /// Gets or sets the maximum queued job count.
    /// 取得或設定佇列工作數上限。
    /// </summary>
    public int QueueCapacity { get; set; } = 32;

    /// <summary>
    /// Gets or sets the maximum number of concurrent generation processes.
    /// 取得或設定同時執行的產生程序數上限。
    /// </summary>
    public int MaxConcurrency { get; set; } = 1;

    /// <summary>
    /// Gets or sets the hard timeout for one generation job.
    /// 取得或設定單一產生工作的硬性逾時時間。
    /// </summary>
    public TimeSpan JobTimeout { get; set; } = TimeSpan.FromMinutes(3);
}
