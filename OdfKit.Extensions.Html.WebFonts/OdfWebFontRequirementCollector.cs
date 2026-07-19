using System.Net;
using OdfKit.Compliance;
using OdfKit.DOM;
using OdfKit.Text;
using OdfKit.WebFonts;

namespace OdfKit.Extensions.Html.WebFonts;

/// <summary>
/// Collects Unicode text sequences from an ODF text document without normalizing IVS or shaping clusters.
/// 從 ODF 文字文件收集 Unicode 文字序列，且不正規化 IVS 或塑形 cluster。
/// </summary>
public static class OdfWebFontRequirementCollector
{
    /// <summary>
    /// Creates a neutral subset request from all text nodes in a document.
    /// 從文件中的所有文字節點建立中性的子集要求。
    /// </summary>
    /// <param name="document">The source text document. / 來源文字文件。</param>
    /// <param name="face">The registered font face. / 已註冊的字型 face。</param>
    /// <param name="profileId">The profile and mapping version identifier. / profile 與 mapping 版本識別碼。</param>
    /// <param name="fontFamily">The CSS font family. / CSS 字型家族。</param>
    /// <param name="formats">The required output formats. / 必要的輸出格式。</param>
    /// <returns>A request that preserves each nonempty text-node sequence. / 保留每個非空白文字節點序列的要求。</returns>
    public static WebFontSubsetRequest Collect(
        TextDocument document,
        WebFontFaceIdentity face,
        string profileId,
        string fontFamily,
        IReadOnlyList<WebFontFormat> formats)
        => Collect(document, face, profileId, fontFamily, formats, Array.Empty<WebFontBrowserTarget>());

    /// <summary>
    /// Creates a neutral subset request with explicit browser-engine requirements.
    /// 建立含明確瀏覽器引擎要求的中性子集要求。
    /// </summary>
    /// <param name="document">The source text document. / 來源文字文件。</param>
    /// <param name="face">The registered font face. / 已註冊的字型 face。</param>
    /// <param name="profileId">The profile and mapping version identifier. / profile 與 mapping 版本識別碼。</param>
    /// <param name="fontFamily">The CSS font family. / CSS 字型家族。</param>
    /// <param name="formats">The required output formats. / 必要的輸出格式。</param>
    /// <param name="requiredBrowserTargets">The browser engines that must render retained color technologies. / 必須能呈現所保留色彩技術的瀏覽器引擎。</param>
    /// <returns>A request that preserves each nonempty text-node sequence. / 保留每個非空白文字節點序列的要求。</returns>
    public static WebFontSubsetRequest Collect(
        TextDocument document,
        WebFontFaceIdentity face,
        string profileId,
        string fontFamily,
        IReadOnlyList<WebFontFormat> formats,
        IReadOnlyList<WebFontBrowserTarget> requiredBrowserTargets)
    {
        if (document is null
            || face is null
            || string.IsNullOrWhiteSpace(profileId)
            || string.IsNullOrWhiteSpace(fontFamily)
            || formats is null
            || formats.Count == 0
            || requiredBrowserTargets is null
            || requiredBrowserTargets.Any(target => !Enum.IsDefined(typeof(WebFontBrowserTarget), target)))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_WebFont_RequestInvalid"));
        }

        WebFontTextSequence[] sequences = document.BodyTextRoot.Descendants()
            .Where(node => node.NodeType == OdfNodeType.Text && node.TextContent.Length > 0)
            .Select(node => WebFontTextSequence.Create(node.TextContent))
            .ToArray();
        if (sequences.Length == 0)
        {
            throw new InvalidDataException(OdfLocalizer.GetMessage("Err_WebFont_DataInvalid"));
        }

        return new WebFontSubsetRequest
        {
            Face = face,
            ProfileId = profileId,
            FontFamily = fontFamily,
            Sequences = sequences,
            Formats = formats,
            RequiredBrowserTargets = requiredBrowserTargets
        };
    }

    /// <summary>
    /// Inserts a safely encoded WebFont stylesheet link before the closing HTML head element.
    /// 在 HTML head 結束元素之前插入安全編碼的 WebFont 樣式表連結。
    /// </summary>
    /// <param name="html">The exported HTML. / 匯出的 HTML。</param>
    /// <param name="stylesheetUrl">The application-relative stylesheet URL. / 應用程式相對樣式表 URL。</param>
    /// <returns>HTML containing the stylesheet link. / 包含樣式表連結的 HTML。</returns>
    public static string AddStylesheetLink(string html, string stylesheetUrl)
    {
        if (html is null || string.IsNullOrWhiteSpace(stylesheetUrl))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_WebFont_RequestInvalid"));
        }

        string link = $"<link rel=\"stylesheet\" href=\"{WebUtility.HtmlEncode(stylesheetUrl)}\" />";
        int headEnd = html.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
        return headEnd >= 0 ? html.Insert(headEnd, link) : string.Concat(link, html);
    }
}
