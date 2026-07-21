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
    /// Gets or sets the maximum durable manifest count retained on disk.
    /// 取得或設定磁碟上保留的耐久 manifest 數量上限。
    /// </summary>
    public int MaxDurableManifestEntries { get; set; } = 4096;

    /// <summary>
    /// Gets or sets the maximum total byte count of durable manifests retained on disk.
    /// 取得或設定磁碟上保留之耐久 manifest 的總位元組上限。
    /// </summary>
    public long MaxDurableManifestBytes { get; set; } = 64L * 1024 * 1024;

    /// <summary>
    /// Gets or sets the maximum idle age of one durable manifest.
    /// 取得或設定單一耐久 manifest 的最長閒置時間。
    /// </summary>
    public TimeSpan DurableManifestMaxIdle { get; set; } = TimeSpan.FromDays(30);

    /// <summary>
    /// Gets or sets the soft byte budget for generated assets managed by the durable cache.
    /// 取得或設定耐久快取所管理產生資產的軟性位元組預算。
    /// </summary>
    /// <remarks>
    /// Assets still referenced by retained manifests are never removed. The budget may be exceeded temporarily while all unreferenced assets are younger than <see cref="DurableAssetMaxIdle"/>.
    /// 仍由保留 manifest 參照的資產絕不移除；所有未參照資產都比 <see cref="DurableAssetMaxIdle"/> 新時，可能暫時超出預算。
    /// </remarks>
    public long MaxDurableAssetBytes { get; set; } = 2L * 1024 * 1024 * 1024;

    /// <summary>
    /// Gets or sets the minimum idle age before an unreferenced generated asset may be removed.
    /// 取得或設定未參照產生資產可移除前的最短閒置時間。
    /// </summary>
    public TimeSpan DurableAssetMaxIdle { get; set; } = TimeSpan.FromDays(30);

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
