namespace OdfKit.WebFonts.Hosting.AspNetCore;

/// <summary>
/// Describes an authenticated request for content-addressed WebFont assets.
/// 描述經授權後要求產生內容定址 WebFont 資產的資料。
/// </summary>
public sealed class OdfWebFontGenerationRequest
{
    /// <summary>
    /// Gets or initializes the allowlisted font source identifier.
    /// 取得或初始化允許清單內的字型來源識別碼。
    /// </summary>
    public string FontSourceId { get; init; } = string.Empty;

    /// <summary>
    /// Gets or initializes the zero-based face index.
    /// 取得或初始化以零為基準的 face 索引。
    /// </summary>
    public int FaceIndex { get; init; }

    /// <summary>
    /// Gets or initializes the allowlisted profile and mapping version identifier.
    /// 取得或初始化允許清單內的 profile 與 mapping 版本識別碼。
    /// </summary>
    public string ProfileId { get; init; } = string.Empty;

    /// <summary>
    /// Gets or initializes the CSS font family recorded in generated assets.
    /// 取得或初始化記錄於產生資產中的 CSS 字型家族。
    /// </summary>
    public string FontFamily { get; init; } = string.Empty;

    /// <summary>
    /// Gets or initializes the Unicode text sequences to preserve without normalization.
    /// 取得或初始化不經正規化且必須保留的 Unicode 文字序列。
    /// </summary>
    public IReadOnlyList<string> Sequences { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets or initializes the allowlisted output formats.
    /// 取得或初始化允許清單內的輸出格式。
    /// </summary>
    public IReadOnlyList<WebFontFormat> Formats { get; init; } = Array.Empty<WebFontFormat>();

    /// <summary>
    /// Gets or initializes the browser engines required to render retained color-font technologies.
    /// 取得或初始化必須能呈現所保留色彩字型技術的瀏覽器引擎。
    /// </summary>
    public IReadOnlyList<WebFontBrowserTarget> RequiredBrowserTargets { get; init; }
        = Array.Empty<WebFontBrowserTarget>();
}
