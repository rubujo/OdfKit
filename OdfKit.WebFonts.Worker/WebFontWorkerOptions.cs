namespace OdfKit.WebFonts.Worker;

/// <summary>
/// Defines hard resource limits for background WebFont generation.
/// 定義背景 WebFont 產生工作的資源硬性限制。
/// </summary>
public sealed class WebFontWorkerOptions
{
    /// <summary>
    /// Gets or sets the optional directory for durable manifests and cross-process leases.
    /// 取得或設定用於耐久 manifest 與跨處理程序 lease 的選用目錄。
    /// </summary>
    /// <remarks>
    /// The directory must reside on a file system that honors exclusive file sharing. All processes must use the same directory and asset destination.
    /// 此目錄必須位於支援獨佔檔案共用語意的檔案系統；所有處理程序必須使用相同目錄與資產目的地。
    /// </remarks>
    public string? DurableCacheDirectory { get; set; }

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

    /// <summary>
    /// Gets or sets the maximum number of completed manifests retained in process memory.
    /// 取得或設定處理程序記憶體中保留的已完成 manifest 數量上限。
    /// </summary>
    public int MaxMemoryCacheEntries { get; set; } = 1024;

    /// <summary>
    /// Gets or sets the maximum durable manifest size in bytes.
    /// 取得或設定耐久 manifest 的位元組大小上限。
    /// </summary>
    public int MaxCachedManifestBytes { get; set; } = 1024 * 1024;

    /// <summary>
    /// Gets or sets the maximum asset count accepted from a durable manifest.
    /// 取得或設定耐久 manifest 可接受的資產數量上限。
    /// </summary>
    public int MaxCachedAssetCount { get; set; } = 16;

    /// <summary>
    /// Gets or sets the maximum size in bytes of one asset referenced by a durable manifest.
    /// 取得或設定耐久 manifest 所參照單一資產的位元組大小上限。
    /// </summary>
    public long MaxCachedAssetBytes { get; set; } = 64L * 1024 * 1024;

    /// <summary>
    /// Gets or sets the delay between attempts to acquire a cross-process generation lease.
    /// 取得或設定嘗試取得跨處理程序產生 lease 的間隔。
    /// </summary>
    public TimeSpan CacheLockRetryDelay { get; set; } = TimeSpan.FromMilliseconds(50);

    /// <summary>
    /// Gets or sets the maximum backoff delay between cross-process lease attempts.
    /// 取得或設定跨處理程序 lease 嘗試之間的最大退避時間。
    /// </summary>
    public TimeSpan MaxCacheLockRetryDelay { get; set; } = TimeSpan.FromSeconds(1);
}
