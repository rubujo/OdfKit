namespace OdfKit.WebFonts;

/// <summary>
/// Describes a deterministic request for one or more WebFont assets.
/// 描述產生一或多個 WebFont 資產的確定性要求。
/// </summary>
public sealed class WebFontSubsetRequest
{
    /// <summary>
    /// Gets or initializes the trusted font face.
    /// 取得或初始化受信任的字型 face。
    /// </summary>
    public WebFontFaceIdentity Face { get; init; } = new();

    /// <summary>
    /// Gets or initializes the profile and mapping version identifier.
    /// 取得或初始化 profile 與 mapping 版本識別碼。
    /// </summary>
    public string ProfileId { get; init; } = string.Empty;

    /// <summary>
    /// Gets or initializes the CSS font family.
    /// 取得或初始化 CSS 字型家族。
    /// </summary>
    public string FontFamily { get; init; } = string.Empty;

    /// <summary>
    /// Gets or initializes the ordered text sequences that must remain intact.
    /// 取得或初始化必須保持完整的有序文字序列。
    /// </summary>
    public IReadOnlyList<WebFontTextSequence> Sequences { get; init; } = Array.Empty<WebFontTextSequence>();

    /// <summary>
    /// Gets or initializes the required output formats.
    /// 取得或初始化必要的輸出格式。
    /// </summary>
    public IReadOnlyList<WebFontFormat> Formats { get; init; } = Array.Empty<WebFontFormat>();
}
