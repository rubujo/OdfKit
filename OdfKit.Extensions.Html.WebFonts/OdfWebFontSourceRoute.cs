using OdfKit.WebFonts;

namespace OdfKit.Extensions.Html.WebFonts;

/// <summary>
/// Describes one ordered font-source route used while collecting ODF WebFont requirements.
/// 描述收集 ODF WebFont 需求時使用的一個有序字型來源路由。
/// </summary>
public sealed class OdfWebFontSourceRoute
{
    /// <summary>
    /// Initializes an ordered font-source route.
    /// 初始化有序字型來源路由。
    /// </summary>
    /// <param name="face">The trusted font face. / 受信任的字型 face。</param>
    /// <param name="profileId">The profile and mapping version identifier. / profile 與 mapping 版本識別碼。</param>
    /// <param name="fontFamily">The CSS font family. / CSS 字型家族。</param>
    /// <param name="formats">The required output formats. / 必要的輸出格式。</param>
    public OdfWebFontSourceRoute(
        WebFontFaceIdentity face,
        string profileId,
        string fontFamily,
        IReadOnlyList<WebFontFormat> formats)
        : this(face, profileId, fontFamily, formats, Array.Empty<WebFontBrowserTarget>())
    {
    }

    /// <summary>
    /// Initializes an ordered font-source route with explicit browser-engine requirements.
    /// 以明確的瀏覽器引擎需求初始化有序字型來源路由。
    /// </summary>
    /// <param name="face">The trusted font face. / 受信任的字型 face。</param>
    /// <param name="profileId">The profile and mapping version identifier. / profile 與 mapping 版本識別碼。</param>
    /// <param name="fontFamily">The CSS font family. / CSS 字型家族。</param>
    /// <param name="formats">The required output formats. / 必要的輸出格式。</param>
    /// <param name="requiredBrowserTargets">The required browser engines. / 必要的瀏覽器引擎。</param>
    public OdfWebFontSourceRoute(
        WebFontFaceIdentity face,
        string profileId,
        string fontFamily,
        IReadOnlyList<WebFontFormat> formats,
        IReadOnlyList<WebFontBrowserTarget> requiredBrowserTargets)
    {
        Face = face;
        ProfileId = profileId;
        FontFamily = fontFamily;
        Formats = formats;
        RequiredBrowserTargets = requiredBrowserTargets;
    }

    /// <summary>
    /// Gets the trusted font face.
    /// 取得受信任的字型 face。
    /// </summary>
    public WebFontFaceIdentity Face { get; }

    /// <summary>
    /// Gets the profile and mapping version identifier.
    /// 取得 profile 與 mapping 版本識別碼。
    /// </summary>
    public string ProfileId { get; }

    /// <summary>
    /// Gets the CSS font family.
    /// 取得 CSS 字型家族。
    /// </summary>
    public string FontFamily { get; }

    /// <summary>
    /// Gets the required output formats.
    /// 取得必要的輸出格式。
    /// </summary>
    public IReadOnlyList<WebFontFormat> Formats { get; }

    /// <summary>
    /// Gets the required browser engines.
    /// 取得必要的瀏覽器引擎。
    /// </summary>
    public IReadOnlyList<WebFontBrowserTarget> RequiredBrowserTargets { get; }
}
