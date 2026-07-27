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
    /// Partitions unique document grapheme clusters across ordered font sources by actual glyph coverage.
    /// 依實際 glyph 覆蓋範圍，將文件中不重複的 grapheme cluster 分配至有序字型來源。
    /// </summary>
    /// <remarks>
    /// Earlier routes have priority when more than one source supports the same cluster. Text unsupported by every route is omitted so the document's default font fallback remains in control.
    /// 多個來源皆支援同一 cluster 時，較前面的路由優先；所有路由皆不支援的文字會省略，讓文件的預設字型遞補繼續生效。
    /// </remarks>
    /// <param name="document">The source text document. / 來源文字文件。</param>
    /// <param name="routes">The ordered trusted font-source routes. / 有序的受信任字型來源路由。</param>
    /// <param name="coverageFilter">The actual font coverage filter. / 實際字型覆蓋篩選器。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消權杖。</param>
    /// <returns>One nonempty subset request per matched route. / 每個命中路由各一個非空子集要求。</returns>
    public static async Task<IReadOnlyList<WebFontSubsetRequest>> CollectSupportedAsync(
        TextDocument document,
        IReadOnlyList<OdfWebFontSourceRoute> routes,
        IWebFontTextCoverageFilter coverageFilter,
        CancellationToken cancellationToken = default)
    {
        if (document is null
            || routes is null
            || routes.Count == 0
            || routes.Any(route => !IsValidRoute(route))
            || coverageFilter is null)
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_WebFont_RequestInvalid"));
        }

        List<WebFontTextSequence> remaining = CollectUniqueClusters(document);
        var requests = new List<WebFontSubsetRequest>(routes.Count);
        foreach (OdfWebFontSourceRoute route in routes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (remaining.Count == 0)
            {
                break;
            }

            IReadOnlyList<WebFontTextSequence> supported = await coverageFilter
                .FilterSupportedSequencesAsync(route.Face, remaining, cancellationToken)
                .ConfigureAwait(false);
            if (supported.Count == 0)
            {
                continue;
            }

            var supportedText = new HashSet<string>(
                supported.Select(sequence => sequence.Text),
                StringComparer.Ordinal);
            if (supportedText.Count != supported.Count
                || supportedText.Any(text => !remaining.Any(sequence => sequence.Text == text)))
            {
                throw new InvalidDataException(OdfLocalizer.GetMessage("Err_WebFont_DataInvalid"));
            }

            requests.Add(new WebFontSubsetRequest
            {
                Face = route.Face,
                ProfileId = route.ProfileId,
                FontFamily = route.FontFamily,
                Sequences = supported,
                Formats = route.Formats,
                RequiredBrowserTargets = route.RequiredBrowserTargets
            });
            remaining.RemoveAll(sequence => supportedText.Contains(sequence.Text));
        }

        return requests;
    }

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
            || requiredBrowserTargets.Any(target => !OdfKit.Internal.OdfEnumHelper.IsDefined(target)))
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

    private static bool IsValidRoute(OdfWebFontSourceRoute? route)
        => route is not null
            && route.Face is not null
            && !string.IsNullOrWhiteSpace(route.ProfileId)
            && !string.IsNullOrWhiteSpace(route.FontFamily)
            && route.Formats is { Count: > 0 }
            && route.RequiredBrowserTargets is not null
            && route.RequiredBrowserTargets.All(target => OdfKit.Internal.OdfEnumHelper.IsDefined(target));

    private static List<WebFontTextSequence> CollectUniqueClusters(TextDocument document)
    {
        var result = new List<WebFontTextSequence>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (OdfNode node in document.BodyTextRoot.Descendants()
                     .Where(node => node.NodeType == OdfNodeType.Text && node.TextContent.Length > 0))
        {
            foreach (string cluster in EnumerateClusters(node.TextContent))
            {
                if (seen.Add(cluster))
                {
                    result.Add(WebFontTextSequence.Create(cluster));
                }
            }
        }

        return result;
    }

    private static IEnumerable<string> EnumerateClusters(string text)
    {
        System.Globalization.TextElementEnumerator elements =
            System.Globalization.StringInfo.GetTextElementEnumerator(text);
        string? current = null;
        while (elements.MoveNext())
        {
            string next = elements.GetTextElement();
            if (current is null)
            {
                current = next;
                continue;
            }

            int first = char.ConvertToUtf32(next, 0);
            int lastIndex = current.Length - 1;
            if (char.IsLowSurrogate(current[lastIndex])
                && lastIndex > 0
                && char.IsHighSurrogate(current[lastIndex - 1]))
            {
                lastIndex--;
            }

            int last = char.ConvertToUtf32(current, lastIndex);
            System.Globalization.UnicodeCategory category =
                System.Globalization.CharUnicodeInfo.GetUnicodeCategory(next, 0);
            bool coalesce = first is >= 0xFE00 and <= 0xFE0F
                or >= 0xE0100 and <= 0xE01EF
                or >= 0x1F3FB and <= 0x1F3FF
                or >= 0xE0020 and <= 0xE007F
                || first == 0x200D
                || last == 0x200D
                || category is System.Globalization.UnicodeCategory.NonSpacingMark
                    or System.Globalization.UnicodeCategory.SpacingCombiningMark
                    or System.Globalization.UnicodeCategory.EnclosingMark;
            if (!coalesce && first is >= 0x1F1E6 and <= 0x1F1FF)
            {
                int regionalCount = 0;
                for (int index = current.Length - 1; index >= 0;)
                {
                    int scalarIndex = char.IsLowSurrogate(current[index])
                        && index > 0
                        && char.IsHighSurrogate(current[index - 1])
                            ? index - 1
                            : index;
                    int scalar = char.ConvertToUtf32(current, scalarIndex);
                    if (scalar is not (>= 0x1F1E6 and <= 0x1F1FF))
                    {
                        break;
                    }

                    regionalCount++;
                    index = scalarIndex - 1;
                }

                coalesce = regionalCount % 2 == 1;
            }
            if (coalesce)
            {
                current = string.Concat(current, next);
                continue;
            }

            yield return current;
            current = next;
        }

        if (current is not null)
        {
            yield return current;
        }
    }
}
