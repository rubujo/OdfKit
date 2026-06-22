using System.Net;
using System.Text;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Text;

namespace OdfKit.Export;

/// <summary>
/// 將 TextDocument 匯出為 HTML 的工具類別。
/// </summary>
/// <remarks>
/// 以 <see cref="StringBuilder"/> 直接輸出 HTML，避免 AngleSharp 雙重 DOM 配置（PERF-4e）。
/// </remarks>
public static class OdfHtmlExporter
{
    private const string DefaultCss =
        "body{font-family:sans-serif;line-height:1.6;margin:2rem;}" +
        "h1,h2,h3,h4,h5,h6{margin-top:1.2em;margin-bottom:0.4em;}" +
        "p{margin:0.5em 0;}" +
        "table{border-collapse:collapse;width:100%;}" +
        "td,th{border:1px solid #ccc;padding:0.4em 0.8em;}" +
        "th{background:#f0f0f0;}";

    /// <summary>
    /// 將 TextDocument 匯出為 HTML 字串。
    /// </summary>
    /// <param name="document">來源文字文件</param>
    /// <param name="options">HTML 匯出選項；若為 null 則使用預設值</param>
    /// <returns>HTML 內容字串</returns>
    /// <exception cref="ArgumentNullException">當 document 為 null 時引發</exception>
    public static string Export(TextDocument document, OdfHtmlExportOptions? options = null)
    {
        if (document is null)
            throw new ArgumentNullException(nameof(document));
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
            ConvertBodyNodes(document.BodyTextRoot, sb);
            sb.Append("</body></html>");
            return sb.ToString();
        }

        ConvertBodyNodes(document.BodyTextRoot, sb);
        return sb.ToString();
    }

    private static void ConvertBodyNodes(OdfNode odfNode, StringBuilder sb)
    {
        foreach (var child in odfNode.Children)
        {
            if (child.NamespaceUri != OdfNamespaces.Text)
            {
                ConvertBodyNodes(child, sb);
                continue;
            }

            switch (child.LocalName)
            {
                case "h":
                    {
                        int level = int.TryParse(child.GetAttribute("outline-level", OdfNamespaces.Text), out int l) ? l : 1;
                        level = level < 1 ? 1 : (level > 6 ? 6 : level);
                        sb.Append('<').Append('h').Append(level).Append('>');
                        AppendEncodedText(sb, child.TextContent);
                        sb.Append("</h").Append(level).Append('>');
                        break;
                    }
                case "p":
                    {
                        sb.Append("<p>");
                        ConvertParagraphContent(child, sb);
                        sb.Append("</p>");
                        break;
                    }
                case "list":
                    {
                        sb.Append("<ul>");
                        foreach (var item in child.Children)
                        {
                            if (item.LocalName == "list-item" && item.NamespaceUri == OdfNamespaces.Text)
                            {
                                sb.Append("<li>");
                                AppendEncodedText(sb, item.TextContent);
                                sb.Append("</li>");
                            }
                        }

                        sb.Append("</ul>");
                        break;
                    }
                default:
                    ConvertBodyNodes(child, sb);
                    break;
            }
        }
    }

    private static void ConvertParagraphContent(OdfNode para, StringBuilder sb)
    {
        bool wroteContent = false;
        foreach (var child in para.Children)
        {
            if (child.NodeType == OdfNodeType.Text)
            {
                AppendEncodedText(sb, child.TextContent);
                wroteContent = true;
            }
            else if (child.LocalName == "span" && child.NamespaceUri == OdfNamespaces.Text)
            {
                sb.Append("<span>");
                ConvertParagraphContent(child, sb);
                sb.Append("</span>");
                wroteContent = true;
            }
            else if (child.LocalName == "note" && child.NamespaceUri == OdfNamespaces.Text)
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
            }
        }

        if (!wroteContent)
        {
            AppendEncodedText(sb, para.TextContent);
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
