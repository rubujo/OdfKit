namespace OdfKit.WebFonts.Build;

/// <summary>
/// Describes one reproducible build-time WebFont job.
/// 描述單一可重現的建置期 WebFont 工作。
/// </summary>
public sealed class WebFontBuildOptions
{
    /// <summary>
    /// Gets or initializes the source font path.
    /// 取得或初始化來源字型路徑。
    /// </summary>
    public string FontPath { get; init; } = string.Empty;

    /// <summary>
    /// Gets or initializes the opaque font source identifier.
    /// 取得或初始化不透明的字型來源識別碼。
    /// </summary>
    public string FontSourceId { get; init; } = "source";

    /// <summary>
    /// Gets or initializes the zero-based collection face index.
    /// 取得或初始化以零為基準的 collection face 索引。
    /// </summary>
    public int FaceIndex { get; init; }

    /// <summary>
    /// Gets or initializes the text corpus path.
    /// 取得或初始化文字 corpus 路徑。
    /// </summary>
    public string TextPath { get; init; } = string.Empty;

    /// <summary>
    /// Gets or initializes additional UTF-8 content files scanned in deterministic path order.
    /// 取得或初始化依確定性路徑順序掃描的其他 UTF-8 內容檔案。
    /// </summary>
    public IReadOnlyList<string> ContentPaths { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets or initializes the maximum combined source-content size in bytes.
    /// 取得或初始化來源內容合計位元組大小上限。
    /// </summary>
    public long MaxCorpusBytes { get; init; } = 16L * 1024 * 1024;

    /// <summary>
    /// Gets or initializes the maximum unique Unicode scalar count.
    /// 取得或初始化唯一 Unicode 純量值數量上限。
    /// </summary>
    public int MaxUniqueUnicodeScalars { get; init; } = 100_000;

    /// <summary>
    /// Gets or initializes the fixed Unicode bucket width used for stable asset slicing, or zero to disable slicing.
    /// 取得或初始化穩定資產切片使用的固定 Unicode bucket 寬度；設為零則停用切片。
    /// </summary>
    public int UnicodeRangeSliceSize { get; init; }

    /// <summary>
    /// Gets or initializes the maximum number of stable Unicode slices.
    /// 取得或初始化穩定 Unicode 切片數量上限。
    /// </summary>
    public int MaxSliceCount { get; init; } = 512;

    /// <summary>
    /// Gets or initializes the optional legacy encoding name.
    /// 取得或初始化選用的舊式編碼名稱。
    /// </summary>
    public string? LegacyEncoding { get; init; }

    /// <summary>
    /// Gets or initializes the optional direct Big5E mapping path.
    /// 取得或初始化選用的 Big5E 直接對照表路徑。
    /// </summary>
    public string? Big5EMappingPath { get; init; }

    /// <summary>
    /// Gets or initializes an optional bounded JSON mapping profile path.
    /// 取得或初始化選用的有界 JSON mapping profile 路徑。
    /// </summary>
    public string? JsonProfilePath { get; init; }

    /// <summary>
    /// Gets or initializes the pinned official CNS 11643 mapping archive path for EUC-TW input.
    /// 取得或初始化供 EUC-TW 輸入使用且已鎖定的官方 CNS 11643 對照表封存檔路徑。
    /// </summary>
    public string? CnsMappingArchivePath { get; init; }

    /// <summary>
    /// Gets or initializes the mapping profile identifier.
    /// 取得或初始化 mapping profile 識別碼。
    /// </summary>
    public string ProfileId { get; init; } = "default";

    /// <summary>
    /// Gets or initializes the CSS font family.
    /// 取得或初始化 CSS 字型家族。
    /// </summary>
    public string FontFamily { get; init; } = "OdfKitWebFont";

    /// <summary>
    /// Gets or initializes the emitted CSS font-display strategy.
    /// 取得或初始化輸出的 CSS font-display 策略。
    /// </summary>
    public WebFontDisplayMode FontDisplay { get; init; } = WebFontDisplayMode.Swap;

    /// <summary>
    /// Gets or initializes the optional local fallback face and metric overrides.
    /// 取得或初始化選用的本機 fallback face 與字型度量覆寫。
    /// </summary>
    public WebFontFallbackMetrics? FallbackMetrics { get; init; }

    /// <summary>
    /// Gets or initializes the output directory.
    /// 取得或初始化輸出目錄。
    /// </summary>
    public string OutputDirectory { get; init; } = string.Empty;

    /// <summary>
    /// Gets or initializes the maximum source font size in bytes.
    /// 取得或初始化來源字型的最大位元組數。
    /// </summary>
    public long MaxSourceBytes { get; init; } = 256L * 1024 * 1024;

    /// <summary>
    /// Gets or initializes the maximum generated asset size in bytes.
    /// 取得或初始化產生資產的最大位元組數。
    /// </summary>
    public long MaxOutputBytes { get; init; } = 32L * 1024 * 1024;

    /// <summary>
    /// Gets or initializes a value indicating whether source table checksums must be valid.
    /// 取得或初始化是否必須驗證來源表格 checksum。
    /// </summary>
    public bool ValidateSourceChecksums { get; init; } = true;

    /// <summary>
    /// Gets or initializes the output formats.
    /// 取得或初始化輸出格式。
    /// </summary>
    public IReadOnlyList<WebFontFormat> Formats { get; init; } = [WebFontFormat.Woff2];
}
