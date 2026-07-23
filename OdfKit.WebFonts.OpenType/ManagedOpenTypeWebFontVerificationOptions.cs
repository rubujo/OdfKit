namespace OdfKit.WebFonts.OpenType;

/// <summary>
/// Configures resource limits for managed WebFont verification.
/// 設定 managed WebFont 驗證的資源限制。
/// </summary>
public sealed class ManagedOpenTypeWebFontVerificationOptions
{
    /// <summary>
    /// Gets the maximum accepted compressed or uncompressed input byte count.
    /// 取得可接受之壓縮或未壓縮輸入的最大位元組數。
    /// </summary>
    public long MaximumInputBytes { get; init; } = 32L * 1024 * 1024;

    /// <summary>
    /// Gets the maximum byte count after WOFF or WOFF2 expansion.
    /// 取得 WOFF 或 WOFF2 展開後的最大位元組數。
    /// </summary>
    public long MaximumExpandedBytes { get; init; } = 32L * 1024 * 1024;

    /// <summary>
    /// Gets the maximum number of sfnt tables accepted from one face.
    /// 取得單一字型 face 可接受的 sfnt table 數量上限。
    /// </summary>
    public int MaximumTableCount { get; init; } = 256;
}
