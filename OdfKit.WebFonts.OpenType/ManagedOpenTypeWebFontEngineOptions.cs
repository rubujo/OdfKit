namespace OdfKit.WebFonts.OpenType;

/// <summary>
/// Configures the bounded managed OpenType subset engine.
/// 設定有界的受控 OpenType 子集引擎。
/// </summary>
public sealed class ManagedOpenTypeWebFontEngineOptions
{
    /// <summary>
    /// Gets the opaque source identifiers mapped to trusted local font paths.
    /// 取得對應至受信任本機字型路徑的不透明來源識別碼。
    /// </summary>
    public IDictionary<string, string> FontSources { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets the maximum source font size in bytes.
    /// 取得或設定來源字型的最大位元組數。
    /// </summary>
    public long MaxSourceBytes { get; set; } = 256L * 1024 * 1024;

    /// <summary>
    /// Gets or sets the maximum total bytes retained by the verified source-font cache.
    /// 取得或設定已驗證來源字型快取保留的位元組總上限。
    /// </summary>
    public long MaxCachedSourceBytes { get; set; } = 128L * 1024 * 1024;

    /// <summary>
    /// Gets or sets the maximum number of verified source fonts retained in memory.
    /// 取得或設定記憶體中保留的已驗證來源字型數量上限。
    /// </summary>
    public int MaxCachedSourceEntries { get; set; } = 4;

    /// <summary>
    /// Gets or sets the maximum generated asset size in bytes.
    /// 取得或設定產生資產的最大位元組數。
    /// </summary>
    public long MaxOutputBytes { get; set; } = 32L * 1024 * 1024;

    /// <summary>
    /// Gets or sets the maximum number of Unicode scalars per request.
    /// 取得或設定每個要求的 Unicode 純量值數上限。
    /// </summary>
    public int MaxUnicodeScalars { get; set; } = 100_000;

    /// <summary>
    /// Gets or sets the maximum sfnt table count.
    /// 取得或設定 sfnt 表格數量上限。
    /// </summary>
    public int MaxTableCount { get; set; } = 256;

    /// <summary>
    /// Gets or sets the maximum composite glyph nesting depth.
    /// 取得或設定複合字圖的巢狀深度上限。
    /// </summary>
    public int MaxCompositeDepth { get; set; } = 64;

    /// <summary>
    /// Gets or sets a value indicating whether source table checksums must be valid.
    /// 取得或設定是否必須驗證來源表格 checksum。
    /// </summary>
    public bool ValidateSourceChecksums { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether every CFF CharString in generated output is verified.
    /// 取得或設定是否驗證產生輸出中的每個 CFF CharString。
    /// </summary>
    /// <remarks>
    /// Verifying every CharString rescans all glyphs for each requested format, which dominates
    /// request latency for large CJK fonts. Keep it enabled for build-time generation; consider
    /// disabling it for latency-sensitive dynamic endpoints, where the subset writer output is
    /// already reparsed and structurally validated.
    /// 驗證每個 CharString 會對每種要求格式重新掃描所有字圖，是大型 CJK 字型請求延遲的
    /// 主要來源。建置期產生建議維持啟用；對延遲敏感的動態 endpoint 可考慮關閉，該路徑
    /// 仍會重新解析並結構驗證子集輸出。
    /// </remarks>
    public bool VerifyEveryOutputCharString { get; set; } = true;
}
