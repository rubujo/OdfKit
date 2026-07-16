namespace OdfKit.WebFonts.Hosting.SystemWeb;

/// <summary>
/// Configures bounded authenticated dynamic WebFont generation for System.Web.
/// 設定 System.Web 有界且須經授權的 WebFont 動態產生功能。
/// </summary>
public sealed class OdfWebFontSystemWebGenerationOptions
{
    /// <summary>
    /// Gets or sets the trusted destination directory for immutable generated assets.
    /// 取得或設定不可變產生資產的受信任目的目錄。
    /// </summary>
    public string AssetRootPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the required API key supplied through the request header.
    /// 取得或設定要求標頭必須提供的 API key。
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the maximum JSON request body size in bytes.
    /// 取得或設定 JSON 要求本文的最大位元組數。
    /// </summary>
    public int MaxRequestBodyBytes { get; set; } = 64 * 1024;

    /// <summary>
    /// Gets or sets the maximum number of concurrent generation operations.
    /// 取得或設定同時執行的產生作業數量上限。
    /// </summary>
    public int MaxConcurrentGenerations { get; set; } = 2;

    /// <summary>
    /// Gets or sets the maximum number of text sequences in one request.
    /// 取得或設定單一要求可包含的文字 sequence 數量上限。
    /// </summary>
    public int MaxSequenceCount { get; set; } = 256;

    /// <summary>
    /// Gets or sets the maximum total Unicode scalar count in one request.
    /// 取得或設定單一要求的 Unicode 純量值總數上限。
    /// </summary>
    public int MaxUnicodeScalarCount { get; set; } = 4096;

    /// <summary>
    /// Gets or sets the maximum generated asset size in bytes.
    /// 取得或設定單一產生資產的最大位元組數。
    /// </summary>
    public long MaxAssetBytes { get; set; } = 32L * 1024 * 1024;

    /// <summary>
    /// Gets or sets a value indicating whether public immutable assets emit wildcard CORS and cross-origin CORP headers.
    /// 取得或設定公開不可變資產是否輸出萬用字元 CORS 與跨來源 CORP 標頭。
    /// </summary>
    public bool AllowPublicCrossOriginAssets { get; set; }

    /// <summary>
    /// Gets the trusted local font paths keyed by opaque source identifier.
    /// 取得以不透明來源識別碼為索引的受信任本機字型路徑。
    /// </summary>
    public IDictionary<string, string> FontSources { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// Gets the exact trusted font faces clients may request.
    /// 取得用戶端可要求的精確受信任字型 face。
    /// </summary>
    public ICollection<WebFontFaceIdentity> AllowedFaces { get; } = new List<WebFontFaceIdentity>();

    /// <summary>
    /// Gets the exact profile identifiers clients may request.
    /// 取得用戶端可要求的精確 profile 識別碼。
    /// </summary>
    public ICollection<string> AllowedProfileIds { get; } = new List<string>();

    /// <summary>
    /// Gets the exact CSS font-family values clients may request.
    /// 取得用戶端可要求的精確 CSS 字型家族值。
    /// </summary>
    public ICollection<string> AllowedFontFamilies { get; } = new List<string>();

    /// <summary>
    /// Gets the output formats clients may request on .NET Framework.
    /// 取得用戶端可在 .NET Framework 要求的輸出格式。
    /// </summary>
    public ICollection<WebFontFormat> AllowedFormats { get; } = new List<WebFontFormat>
    {
        WebFontFormat.Woff,
        WebFontFormat.TrueType
    };
}
