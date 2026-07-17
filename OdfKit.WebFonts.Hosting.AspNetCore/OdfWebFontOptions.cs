namespace OdfKit.WebFonts.Hosting.AspNetCore;

/// <summary>
/// Configures the bounded, read-only WebFont asset store.
/// 設定有界且唯讀的 WebFont 資產儲存區。
/// </summary>
public sealed class OdfWebFontOptions
{
    internal bool AllowMissingManifestForGeneration { get; set; }

    /// <summary>
    /// Gets or sets the application route prefix used when no public CDN base URL is configured.
    /// 取得或設定未指定公開 CDN 基底 URL 時使用的應用程式路由前綴。
    /// </summary>
    public string RoutePrefix { get; set; } = "/_odf-fonts";

    /// <summary>
    /// Gets or sets the optional absolute public base URL for CDN-hosted assets.
    /// 取得或設定 CDN 託管資產的選用公開絕對基底 URL。
    /// </summary>
    public string? PublicBaseUrl { get; set; }

    /// <summary>
    /// Gets or sets the trusted root directory containing the manifest and generated assets.
    /// 取得或設定包含 manifest 與產生資產的受信任根目錄。
    /// </summary>
    public string AssetRootPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the manifest file name within <see cref="AssetRootPath"/>.
    /// 取得或設定 <see cref="AssetRootPath"/> 內的 manifest 檔名。
    /// </summary>
    public string ManifestFileName { get; set; } = "webfonts.json";

    /// <summary>
    /// Gets or sets the maximum accepted manifest size in bytes.
    /// 取得或設定可接受的 manifest 位元組大小上限。
    /// </summary>
    public long MaxManifestBytes { get; set; } = 1024 * 1024;

    /// <summary>
    /// Gets or sets the maximum number of assets declared by one manifest.
    /// 取得或設定單一 manifest 可宣告的資產數量上限。
    /// </summary>
    public int MaxAssetCount { get; set; } = 4096;

    /// <summary>
    /// Gets or sets the maximum accepted size of one generated asset in bytes.
    /// 取得或設定單一產生資產可接受的位元組大小上限。
    /// </summary>
    public long MaxAssetBytes { get; set; } = 32L * 1024 * 1024;

    /// <summary>
    /// Gets the exact origins allowed to load locally hosted WebFont resources cross-origin.
    /// 取得允許跨來源載入本機託管 WebFont 資源的精確來源。
    /// </summary>
    public ICollection<string> AllowedOrigins { get; } = new List<string>();

    /// <summary>
    /// Gets or sets the Cross-Origin-Resource-Policy response mode for locally hosted assets.
    /// 取得或設定本機託管資產的 Cross-Origin-Resource-Policy 回應模式。
    /// </summary>
    public OdfWebFontCrossOriginPolicy CrossOriginResourcePolicy { get; set; }
        = OdfWebFontCrossOriginPolicy.SameOrigin;
}
