namespace OdfKit.WebFonts.Build;

/// <summary>
/// Configures an opt-in local fallback face and CSS metric overrides.
/// 設定選擇啟用的本機 fallback face 與 CSS 字型度量覆寫。
/// </summary>
public sealed class WebFontFallbackMetrics
{
    /// <summary>
    /// Gets or initializes the generated fallback font-family name.
    /// 取得或初始化產生的 fallback font-family 名稱。
    /// </summary>
    public string FontFamily { get; init; } = "OdfKitWebFontFallback";

    /// <summary>
    /// Gets or initializes the trusted local font name used by the fallback face.
    /// 取得或初始化 fallback face 使用的受信任本機字型名稱。
    /// </summary>
    public string LocalFontName { get; init; } = string.Empty;

    /// <summary>
    /// Gets or initializes the CSS size-adjust percentage.
    /// 取得或初始化 CSS size-adjust 百分比。
    /// </summary>
    public double SizeAdjustPercentage { get; init; } = 100;

    /// <summary>
    /// Gets or initializes the CSS ascent-override percentage.
    /// 取得或初始化 CSS ascent-override 百分比。
    /// </summary>
    public double AscentOverridePercentage { get; init; } = 100;

    /// <summary>
    /// Gets or initializes the CSS descent-override percentage.
    /// 取得或初始化 CSS descent-override 百分比。
    /// </summary>
    public double DescentOverridePercentage { get; init; } = 20;

    /// <summary>
    /// Gets or initializes the CSS line-gap-override percentage.
    /// 取得或初始化 CSS line-gap-override 百分比。
    /// </summary>
    public double LineGapOverridePercentage { get; init; }
}
