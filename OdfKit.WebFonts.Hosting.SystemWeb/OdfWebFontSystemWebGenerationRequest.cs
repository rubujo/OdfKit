namespace OdfKit.WebFonts.Hosting.SystemWeb;

/// <summary>
/// Describes an authenticated System.Web request for dynamically generated WebFont assets.
/// 描述經授權的 System.Web 動態 WebFont 資產產生要求。
/// </summary>
public sealed class OdfWebFontSystemWebGenerationRequest
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
    /// Gets or initializes the allowlisted profile identifier.
    /// 取得或初始化允許清單內的 profile 識別碼。
    /// </summary>
    public string ProfileId { get; init; } = string.Empty;

    /// <summary>
    /// Gets or initializes the CSS font family.
    /// 取得或初始化 CSS 字型家族。
    /// </summary>
    public string FontFamily { get; init; } = string.Empty;

    /// <summary>
    /// Gets or initializes the Unicode sequences preserved without normalization.
    /// 取得或初始化不經正規化且必須保留的 Unicode sequence。
    /// </summary>
    public IReadOnlyList<string> Sequences { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets or initializes the requested allowlisted formats.
    /// 取得或初始化要求的允許清單格式。
    /// </summary>
    public IReadOnlyList<WebFontFormat> Formats { get; init; } = Array.Empty<WebFontFormat>();
}
