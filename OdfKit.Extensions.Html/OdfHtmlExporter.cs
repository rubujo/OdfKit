using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Text;

namespace OdfKit.Export;

/// <summary>
/// Exports ODF text documents to HTML.
/// 將 TextDocument 匯出為 HTML 的工具類別。
/// </summary>
/// <remarks>
/// 以 <see cref="StringBuilder"/> 直接輸出 HTML，避免 AngleSharp 雙重 DOM 配置（PERF-4e）。
/// </remarks>
public static class OdfHtmlExporter
{
    /// <summary>
    /// Exports HTML to a caller-owned stream.
    /// 將 HTML 匯出至呼叫端擁有的資料流。
    /// </summary>
    public static OdfExportReport ExportToStream(TextDocument document, Stream destination, OdfHtmlExportOptions? options) =>
        OdfManagedExportWriter.Write(destination, Export(document, options), OdfExportFormat.Html, "managed-html");

    /// <summary>
    /// Exports HTML asynchronously to a caller-owned stream.
    /// 將 HTML 非同步匯出至呼叫端擁有的資料流。
    /// </summary>
    public static Task<OdfExportReport> ExportToStreamAsync(TextDocument document, Stream destination, OdfHtmlExportOptions? options, CancellationToken cancellationToken) =>
        OdfManagedExportWriter.WriteAsync(destination, Export(document, options), OdfExportFormat.Html, "managed-html", cancellationToken);

    /// <summary>
    /// Exports HTML to a file path.
    /// 將 HTML 匯出至檔案路徑。
    /// </summary>
    public static OdfExportReport ExportToPath(TextDocument document, string path, OdfHtmlExportOptions? options) =>
        OdfManagedExportWriter.WritePath(path, Export(document, options), OdfExportFormat.Html, "managed-html");

    /// <summary>
    /// Exports HTML asynchronously to a file path.
    /// 將 HTML 非同步匯出至檔案路徑。
    /// </summary>
    public static Task<OdfExportReport> ExportToPathAsync(TextDocument document, string path, OdfHtmlExportOptions? options, CancellationToken cancellationToken) =>
        OdfManagedExportWriter.WritePathAsync(path, Export(document, options), OdfExportFormat.Html, "managed-html", cancellationToken);

    private const string DefaultCss =
        "body{font-family:sans-serif;line-height:1.6;margin:2rem;}" +
        "h1,h2,h3,h4,h5,h6{margin-top:1.2em;margin-bottom:0.4em;}" +
        "p{margin:0.5em 0;}" +
        "table{border-collapse:collapse;width:100%;}" +
        "td,th{border:1px solid #ccc;padding:0.4em 0.8em;}" +
        "th{background:#f0f0f0;}";

    /// <summary>
    /// Exports the specified text document as HTML.
    /// 將 TextDocument 匯出為 HTML 字串。
    /// </summary>
    /// <param name="document">The source or target object. / 來源文字文件</param>
    /// <param name="options">The value to use. / HTML 匯出選項；若為 null 則使用預設值</param>
    /// <returns>The result. / HTML 內容字串</returns>
    /// <exception cref="ArgumentNullException">Thrown when the documented condition occurs. / 當 document 為 null 時引發</exception>
    public static string Export(TextDocument document, OdfHtmlExportOptions? options = null)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(document, nameof(document));
        options ??= new OdfHtmlExportOptions();

        var sb = new StringBuilder(4096);
        if (options.FullPage)
        {
            sb.Append("<!DOCTYPE html>\n<html><head>");
            sb.Append("<meta charset=\"");
            sb.Append(WebUtility.HtmlEncode(options.Charset));
            sb.Append("\">");

            if (!string.IsNullOrEmpty(options.Title))
            {
                sb.Append("<title>");
                sb.Append(WebUtility.HtmlEncode(options.Title));
                sb.Append("</title>");
            }

            if (options.InlineStyles)
            {
                sb.Append("<style>");
                sb.Append(DefaultCss);
                sb.Append("</style>");
            }

            sb.Append("</head><body>");
            ConvertBodyNodes(document, document.BodyTextRoot, sb);
            sb.Append("</body></html>");
            return sb.ToString();
        }

        ConvertBodyNodes(document, document.BodyTextRoot, sb);
        return sb.ToString();
    }

    private const int MaxRepeatedCells = 200;
    private const int MaxEmbeddedImageBytes = 5 * 1024 * 1024;

    private static void ConvertBodyNodes(TextDocument document, OdfNode odfNode, StringBuilder sb)
    {
        foreach (var child in odfNode.Children)
        {
            ConvertBodyNode(document, child, sb);
        }
    }

    private static void ConvertBodyNode(TextDocument document, OdfNode child, StringBuilder sb)
    {
        if (child.NodeType != OdfNodeType.Element)
        {
            return;
        }

        if (child.NamespaceUri == OdfNamespaces.Table)
        {
            if (child.LocalName == "table")
            {
                ConvertTable(document, child, sb);
            }

            return;
        }

        if (child.NamespaceUri != OdfNamespaces.Text)
        {
            ConvertBodyNodes(document, child, sb);
            return;
        }

        switch (child.LocalName)
        {
            case "h":
                {
                    int level = int.TryParse(child.GetAttribute("outline-level", OdfNamespaces.Text), out int l) ? l : 1;
                    level = level < 1 ? 1 : (level > 6 ? 6 : level);
                    sb.Append('<').Append('h').Append(level);
                    AppendBlockStyleAttribute(document, child, sb);
                    sb.Append('>');
                    ConvertParagraphContent(document, child, sb);
                    sb.Append("</h").Append(level).Append('>');
                    break;
                }
            case "p":
                {
                    sb.Append("<p");
                    AppendBlockStyleAttribute(document, child, sb);
                    sb.Append('>');
                    ConvertParagraphContent(document, child, sb);
                    sb.Append("</p>");
                    break;
                }
            case "list":
                ConvertList(document, child, sb, 0, null);
                break;
            case "tracked-changes":
            case "table-of-content-source":
            case "alphabetical-index-source":
            case "user-index-source":
                break;
            default:
                // 區段、目錄、索引等容器：輸出其內容（目錄項目與索引本文）。
                ConvertBodyNodes(document, child, sb);
                break;
        }
    }

    // ---------- 清單 ----------

    /// <summary>
    /// 轉換 <c>text:list</c>：依清單樣式決定 <c>ul</c> 或 <c>ol</c>（取該層的樣式），項目內的段落、巢狀清單與表格依序輸出。
    /// </summary>
    private static void ConvertList(TextDocument document, OdfNode list, StringBuilder sb, int level, string? inheritedStyleName)
    {
        string? styleName = list.GetAttribute("style-name", OdfNamespaces.Text) ?? inheritedStyleName;
        OdfNode? levelStyle = FindListLevelStyle(document, styleName, level + 1);
        bool ordered = levelStyle is not null && levelStyle.LocalName == "list-level-style-number";

        sb.Append(ordered ? "<ol" : "<ul");
        if (ordered)
        {
            string format = levelStyle!.GetAttribute("num-format", OdfNamespaces.Style) ?? "1";
            string? type = format switch
            {
                "a" => "a",
                "A" => "A",
                "i" => "i",
                "I" => "I",
                _ => null,
            };
            if (type is not null)
            {
                sb.Append(" type=\"").Append(type).Append('"');
            }

            string? start = levelStyle.GetAttribute("start-value", OdfNamespaces.Text);
            if (int.TryParse(start, out int startValue) && startValue > 1)
            {
                sb.Append(" start=\"").Append(startValue).Append('"');
            }
        }

        sb.Append('>');
        foreach (OdfNode item in list.Children)
        {
            if (item.NamespaceUri != OdfNamespaces.Text || (item.LocalName != "list-item" && item.LocalName != "list-header"))
            {
                continue;
            }

            sb.Append("<li>");
            bool firstBlock = true;
            foreach (OdfNode content in item.Children)
            {
                if (content.NodeType != OdfNodeType.Element)
                {
                    continue;
                }

                if (content.NamespaceUri == OdfNamespaces.Text && content.LocalName == "list")
                {
                    ConvertList(document, content, sb, level + 1, styleName);
                }
                else if (content.NamespaceUri == OdfNamespaces.Text && (content.LocalName == "p" || content.LocalName == "h"))
                {
                    // 項目的第一個段落直接內嵌在 li 中；之後的段落才各自成為區塊，避免每個項目多出一個空白段距。
                    if (firstBlock)
                    {
                        ConvertParagraphContent(document, content, sb);
                    }
                    else
                    {
                        sb.Append("<p>");
                        ConvertParagraphContent(document, content, sb);
                        sb.Append("</p>");
                    }

                    firstBlock = false;
                }
                else
                {
                    ConvertBodyNode(document, content, sb);
                    firstBlock = false;
                }
            }

            sb.Append("</li>");
        }

        sb.Append(ordered ? "</ol>" : "</ul>");
    }

    private static OdfNode? FindListLevelStyle(TextDocument document, string? styleName, int level)
    {
        if (string.IsNullOrEmpty(styleName))
        {
            return null;
        }

        foreach (OdfNode root in new[] { document.ContentDom, document.StylesDom })
        {
            foreach (OdfNode section in root.Children)
            {
                if (section.NamespaceUri != OdfNamespaces.Office
                    || (section.LocalName != "automatic-styles" && section.LocalName != "styles"))
                {
                    continue;
                }

                foreach (OdfNode listStyle in section.Children)
                {
                    if (listStyle.NamespaceUri != OdfNamespaces.Text
                        || listStyle.LocalName != "list-style"
                        || listStyle.GetAttribute("name", OdfNamespaces.Style) != styleName)
                    {
                        continue;
                    }

                    string wanted = level.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    foreach (OdfNode levelStyle in listStyle.Children)
                    {
                        if (levelStyle.LocalName.StartsWith("list-level-style-", System.StringComparison.Ordinal)
                            && levelStyle.GetAttribute("level", OdfNamespaces.Text) == wanted)
                        {
                            return levelStyle;
                        }
                    }
                }
            }
        }

        return null;
    }

    // ---------- 表格 ----------

    /// <summary>
    /// 轉換 <c>table:table</c>：標題列輸出為 <c>th</c>，合併儲存格轉為 <c>colspan</c>、<c>rowspan</c>，
    /// 被覆蓋的儲存格略過，重複列與重複欄展開（有上限，避免整列重複數百萬次的空白列）。
    /// </summary>
    private static void ConvertTable(TextDocument document, OdfNode table, StringBuilder sb)
    {
        sb.Append("<table>");
        foreach (OdfNode part in table.Children)
        {
            if (part.NamespaceUri != OdfNamespaces.Table)
            {
                continue;
            }

            if (part.LocalName == "table-header-rows")
            {
                sb.Append("<thead>");
                foreach (OdfNode row in part.Children)
                {
                    ConvertTableRow(document, row, sb, header: true);
                }

                sb.Append("</thead>");
            }
            else if (part.LocalName == "table-rows" || part.LocalName == "table-row-group")
            {
                foreach (OdfNode row in part.Children)
                {
                    ConvertTableRow(document, row, sb, header: false);
                }
            }
            else if (part.LocalName == "table-row")
            {
                ConvertTableRow(document, part, sb, header: false);
            }
        }

        sb.Append("</table>");
    }

    private static void ConvertTableRow(TextDocument document, OdfNode row, StringBuilder sb, bool header)
    {
        if (row.NamespaceUri != OdfNamespaces.Table || row.LocalName != "table-row")
        {
            return;
        }

        int repeat = ReadRepeat(row, "number-rows-repeated", MaxRepeatedCells);
        var cells = new StringBuilder();
        bool anyContent = false;
        foreach (OdfNode cell in row.Children)
        {
            if (cell.NamespaceUri != OdfNamespaces.Table)
            {
                continue;
            }

            if (cell.LocalName == "covered-table-cell")
            {
                continue;
            }

            if (cell.LocalName != "table-cell")
            {
                continue;
            }

            int columnRepeat = ReadRepeat(cell, "number-columns-repeated", MaxRepeatedCells);
            var cellHtml = new StringBuilder();
            ConvertBodyNodes(document, cell, cellHtml);
            if (cellHtml.Length > 0)
            {
                anyContent = true;
            }

            string tag = header ? "th" : "td";
            for (int copy = 0; copy < columnRepeat; copy++)
            {
                cells.Append('<').Append(tag);
                AppendSpanAttribute(cell, "number-columns-spanned", "colspan", cells);
                AppendSpanAttribute(cell, "number-rows-spanned", "rowspan", cells);
                cells.Append('>').Append(cellHtml).Append("</").Append(tag).Append('>');
            }
        }

        // 只含空白儲存格的重複列（試算表風格的尾端空列）不輸出。
        if (!anyContent && repeat > 1)
        {
            return;
        }

        for (int copy = 0; copy < repeat; copy++)
        {
            sb.Append("<tr>").Append(cells).Append("</tr>");
        }
    }

    private static int ReadRepeat(OdfNode node, string attribute, int limit)
    {
        string? text = node.GetAttribute(attribute, OdfNamespaces.Table);
        return int.TryParse(text, out int value) && value > 1 ? System.Math.Min(value, limit) : 1;
    }

    private static void AppendSpanAttribute(OdfNode cell, string odfAttribute, string htmlAttribute, StringBuilder sb)
    {
        if (int.TryParse(cell.GetAttribute(odfAttribute, OdfNamespaces.Table), out int span) && span > 1)
        {
            sb.Append(' ').Append(htmlAttribute).Append("=\"").Append(span).Append('"');
        }
    }

    // ---------- 行內內容 ----------

    private static void ConvertParagraphContent(TextDocument document, OdfNode para, StringBuilder sb)
    {
        bool wroteContent = false;
        foreach (var child in para.Children)
        {
            if (child.NodeType == OdfNodeType.Text)
            {
                AppendEncodedText(sb, child.TextContent);
                wroteContent = true;
                continue;
            }

            if (child.NamespaceUri != OdfNamespaces.Text)
            {
                if (child.NamespaceUri == OdfNamespaces.Draw && child.LocalName == "frame")
                {
                    wroteContent |= AppendImage(document, child, sb);
                }

                continue;
            }

            switch (child.LocalName)
            {
                case "span":
                    sb.Append("<span");
                    AppendInlineStyleAttribute(document, child, sb);
                    sb.Append('>');
                    ConvertParagraphContent(document, child, sb);
                    sb.Append("</span>");
                    wroteContent = true;
                    break;
                case "a":
                    {
                        string? href = child.GetAttribute("href", OdfNamespaces.XLink);
                        if (IsSafeHref(href))
                        {
                            sb.Append("<a href=\"").Append(WebUtility.HtmlEncode(href!)).Append("\">");
                            ConvertParagraphContent(document, child, sb);
                            sb.Append("</a>");
                        }
                        else
                        {
                            // 目標為 javascript: 等不安全的協定時只保留連結文字。
                            ConvertParagraphContent(document, child, sb);
                        }

                        wroteContent = true;
                        break;
                    }
                case "s":
                    {
                        int count = int.TryParse(child.GetAttribute("c", OdfNamespaces.Text), out int c) && c > 0 ? System.Math.Min(c, 1000) : 1;
                        for (int index = 0; index < count; index++)
                        {
                            sb.Append("&nbsp;");
                        }

                        wroteContent = true;
                        break;
                    }
                case "tab":
                    sb.Append("<span style=\"white-space:pre\">\t</span>");
                    wroteContent = true;
                    break;
                case "line-break":
                    sb.Append("<br>");
                    wroteContent = true;
                    break;
                case "bookmark":
                case "bookmark-start":
                    {
                        string? name = child.GetAttribute("name", OdfNamespaces.Text);
                        if (!string.IsNullOrEmpty(name))
                        {
                            sb.Append("<a id=\"").Append(WebUtility.HtmlEncode(name!)).Append("\"></a>");
                        }

                        break;
                    }
                case "bookmark-ref":
                case "page-number":
                case "page-count":
                case "date":
                case "time":
                case "title":
                case "subject":
                case "initial-creator":
                case "file-name":
                case "word-count":
                case "character-count":
                    // 欄位以儲存的顯示文字輸出。
                    AppendEncodedText(sb, child.TextContent);
                    wroteContent = true;
                    break;
                case "note":
                    {
                        string? noteClass = child.GetAttribute("note-class", OdfNamespaces.Text);
                        string citation = string.Empty;
                        foreach (var noteChild in child.Children)
                        {
                            if (noteChild.LocalName == "note-citation" && noteChild.NamespaceUri == OdfNamespaces.Text)
                            {
                                citation = noteChild.TextContent ?? string.Empty;
                                break;
                            }
                        }

                        sb.Append("<sup title=\"");
                        sb.Append(WebUtility.HtmlEncode(noteClass ?? string.Empty));
                        sb.Append("\">");
                        AppendEncodedText(sb, citation);
                        sb.Append("</sup>");
                        wroteContent = true;
                        break;
                    }
            }
        }

        if (!wroteContent)
        {
            AppendEncodedText(sb, para.TextContent);
        }
    }

    // 只允許網頁連結、郵件、電話與頁內錨點；相對路徑也允許。
    private static bool IsSafeHref(string? href)
    {
        if (string.IsNullOrWhiteSpace(href))
        {
            return false;
        }

        string text = href!.Trim();
        int colon = text.IndexOf(':');
        if (colon < 0)
        {
            return true;
        }

        // 冒號出現在路徑、查詢或錨點之後時是相對位址的一部分。
        int separator = FirstPathSeparator(text);
        if (separator >= 0 && separator < colon)
        {
            return true;
        }

        string scheme = text.Substring(0, colon);
        return scheme.Equals("http", System.StringComparison.OrdinalIgnoreCase)
            || scheme.Equals("https", System.StringComparison.OrdinalIgnoreCase)
            || scheme.Equals("mailto", System.StringComparison.OrdinalIgnoreCase)
            || scheme.Equals("tel", System.StringComparison.OrdinalIgnoreCase)
            || scheme.Equals("ftp", System.StringComparison.OrdinalIgnoreCase);
    }

    private static int FirstPathSeparator(string text)
    {
        for (int index = 0; index < text.Length; index++)
        {
            char current = text[index];
            if (current == '/' || current == '?' || current == '#')
            {
                return index;
            }
        }

        return -1;
    }

    // 圖片以資料 URI 內嵌，輸出的 HTML 不依賴外部檔案；超過上限或讀不到時以替代文字取代。
    private static bool AppendImage(TextDocument document, OdfNode frame, StringBuilder sb)
    {
        OdfNode? image = null;
        foreach (OdfNode child in frame.Children)
        {
            if (child.NamespaceUri == OdfNamespaces.Draw && child.LocalName == "image")
            {
                image = child;
                break;
            }
        }

        if (image is null)
        {
            return false;
        }

        string alt = string.Empty;
        foreach (OdfNode child in frame.Children)
        {
            if (child.NamespaceUri == OdfNamespaces.Svg && child.LocalName == "desc")
            {
                alt = child.TextContent ?? string.Empty;
            }
        }

        string? href = image.GetAttribute("href", OdfNamespaces.XLink);
        string? source = null;
        if (!string.IsNullOrEmpty(href) && !href!.StartsWith("http", System.StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                using Stream stream = document.Package.GetEntryStream(href!.TrimStart('.', '/'));
                using var buffer = new MemoryStream();
                stream.CopyTo(buffer);
                if (buffer.Length <= MaxEmbeddedImageBytes)
                {
                    source = "data:" + GuessImageMediaType(href) + ";base64," + System.Convert.ToBase64String(buffer.ToArray());
                }
            }
            catch (System.Exception ex) when (ex is IOException or KeyNotFoundException or System.ArgumentException or System.InvalidOperationException)
            {
                source = null;
            }
        }
        else if (IsSafeHref(href) && !string.IsNullOrEmpty(href))
        {
            source = href;
        }

        if (source is null)
        {
            AppendEncodedText(sb, alt);
            return alt.Length > 0;
        }

        sb.Append("<img src=\"").Append(WebUtility.HtmlEncode(source)).Append("\" alt=\"").Append(WebUtility.HtmlEncode(alt)).Append("\">");
        return true;
    }

    private static string GuessImageMediaType(string path)
    {
        string extension = Path.GetExtension(path).ToLowerInvariant();
        return extension switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".svg" => "image/svg+xml",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            _ => "application/octet-stream",
        };
    }

    // 段落對齊與縮排以內嵌樣式輸出。
    private static void AppendBlockStyleAttribute(TextDocument document, OdfNode block, StringBuilder sb)
    {
        string? styleName = block.GetAttribute("style-name", OdfNamespaces.Text);
        if (string.IsNullOrWhiteSpace(styleName))
        {
            return;
        }

        var declarations = new List<string>();
        string? align = document.StyleEngine.GetStyleProperty(styleName!, "text-align", OdfNamespaces.Fo, "paragraph");
        string? htmlAlign = align switch
        {
            "center" => "center",
            "end" or "right" => "right",
            "justify" => "justify",
            _ => null,
        };
        if (htmlAlign is not null)
        {
            declarations.Add("text-align:" + htmlAlign);
        }

        if (declarations.Count == 0)
        {
            return;
        }

        sb.Append(" style=\"").Append(WebUtility.HtmlEncode(string.Join("; ", declarations))).Append('"');
    }

    private static void AppendInlineStyleAttribute(TextDocument document, OdfNode span, StringBuilder sb)
    {
        string? styleName = span.GetAttribute("style-name", OdfNamespaces.Text);
        if (string.IsNullOrWhiteSpace(styleName))
        {
            return;
        }

        var declarations = new List<string>();
        string? fontWeight = document.StyleEngine.GetStyleProperty(styleName!, "font-weight", OdfNamespaces.Fo, "text")
            ?? document.StyleEngine.GetStyleProperty(styleName!, "font-weight-asian", OdfNamespaces.Style, "text");
        if (string.Equals(fontWeight, "bold", StringComparison.OrdinalIgnoreCase))
        {
            declarations.Add("font-weight:bold");
        }

        string? fontStyle = document.StyleEngine.GetStyleProperty(styleName!, "font-style", OdfNamespaces.Fo, "text")
            ?? document.StyleEngine.GetStyleProperty(styleName!, "font-style-asian", OdfNamespaces.Style, "text");
        if (string.Equals(fontStyle, "italic", StringComparison.OrdinalIgnoreCase))
        {
            declarations.Add("font-style:italic");
        }

        string? underline = document.StyleEngine.GetStyleProperty(styleName!, "text-underline-style", OdfNamespaces.Style, "text");
        string? lineThrough = document.StyleEngine.GetStyleProperty(styleName!, "text-line-through-style", OdfNamespaces.Style, "text");
        string? decoration = BuildTextDecoration(underline, lineThrough);
        if (!string.IsNullOrWhiteSpace(decoration))
        {
            declarations.Add("text-decoration:" + decoration);
        }

        AddCssDeclaration(document, styleName!, "font-size", OdfNamespaces.Fo, "text", declarations);
        AddCssDeclaration(document, styleName!, "color", OdfNamespaces.Fo, "text", declarations);
        AddCssDeclaration(document, styleName!, "background-color", OdfNamespaces.Fo, "text", declarations);
        AddCssDeclaration(document, styleName!, "text-transform", OdfNamespaces.Fo, "text", declarations);
        AddCssDeclaration(document, styleName!, "font-variant", OdfNamespaces.Fo, "text", declarations);

        if (declarations.Count == 0)
        {
            return;
        }

        sb.Append(" style=\"");
        sb.Append(WebUtility.HtmlEncode(string.Join("; ", declarations)));
        sb.Append('"');
    }

    private static string? BuildTextDecoration(string? underline, string? lineThrough)
    {
        bool hasUnderline = !string.IsNullOrWhiteSpace(underline) &&
            !string.Equals(underline, "none", StringComparison.OrdinalIgnoreCase);
        bool hasLineThrough = !string.IsNullOrWhiteSpace(lineThrough) &&
            !string.Equals(lineThrough, "none", StringComparison.OrdinalIgnoreCase);
        if (hasUnderline && hasLineThrough)
        {
            return "underline line-through";
        }

        return hasUnderline ? "underline" : hasLineThrough ? "line-through" : null;
    }

    private static void AddCssDeclaration(TextDocument document, string styleName, string propertyName, string namespaceUri, string family, List<string> declarations)
    {
        string? value = document.StyleEngine.GetStyleProperty(styleName, propertyName, namespaceUri, family);
        if (!string.IsNullOrWhiteSpace(value))
        {
            declarations.Add(propertyName + ":" + value);
        }
    }

    private static void AppendEncodedText(StringBuilder sb, string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        sb.Append(WebUtility.HtmlEncode(text));
    }
}
