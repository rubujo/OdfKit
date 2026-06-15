using System;
using System.IO;
using System.Linq;
using System.Text;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Text;

namespace OdfKit.Export;

/// <summary>
/// 將 TextDocument 匯出為 HTML 的工具類別。
/// </summary>
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
    /// <param name="document">來源文字文件。</param>
    /// <param name="options">HTML 匯出選項；若為 null 則使用預設值。</param>
    /// <returns>HTML 內容字串。</returns>
    /// <exception cref="ArgumentNullException">當 document 為 null 時引發。</exception>
    public static string Export(TextDocument document, OdfHtmlExportOptions? options = null)
    {
        if (document is null) throw new ArgumentNullException(nameof(document));
        options ??= new OdfHtmlExportOptions();

        var context = BrowsingContext.New(Configuration.Default);
        var parser = context.GetService<IHtmlParser>()!;
        var htmlDoc = parser.ParseDocument(string.Empty);

        if (options.FullPage)
        {
            var meta = htmlDoc.CreateElement("meta");
            meta.SetAttribute("charset", options.Charset);
            htmlDoc.Head!.AppendChild(meta);

            if (!string.IsNullOrEmpty(options.Title))
            {
                var titleElem = htmlDoc.CreateElement("title");
                titleElem.TextContent = options.Title;
                htmlDoc.Head.AppendChild(titleElem);
            }

            if (options.InlineStyles)
            {
                var style = htmlDoc.CreateElement("style");
                style.TextContent = DefaultCss;
                htmlDoc.Head.AppendChild(style);
            }
        }

        var body = htmlDoc.Body!;
        ConvertBodyNodes(document.BodyTextRoot, body, htmlDoc);

        return options.FullPage
            ? "<!DOCTYPE html>\n" + htmlDoc.DocumentElement.OuterHtml
            : body.InnerHtml;
    }

    private static void ConvertBodyNodes(OdfNode odfNode, IElement parent, IHtmlDocument htmlDoc)
    {
        foreach (var child in odfNode.Children)
        {
            if (child.NamespaceUri != OdfNamespaces.Text)
            {
                ConvertBodyNodes(child, parent, htmlDoc);
                continue;
            }

            switch (child.LocalName)
            {
                case "h":
                {
                    int level = int.TryParse(child.GetAttribute("outline-level", OdfNamespaces.Text), out int l) ? l : 1;
                    level = level < 1 ? 1 : (level > 6 ? 6 : level);
                    var hElem = htmlDoc.CreateElement($"h{level}");
                    hElem.TextContent = child.TextContent ?? string.Empty;
                    parent.AppendChild(hElem);
                    break;
                }
                case "p":
                {
                    var pElem = htmlDoc.CreateElement("p");
                    ConvertParagraphContent(child, pElem, htmlDoc);
                    parent.AppendChild(pElem);
                    break;
                }
                case "list":
                {
                    var ul = htmlDoc.CreateElement("ul");
                    foreach (var item in child.Children)
                    {
                        if (item.LocalName == "list-item" && item.NamespaceUri == OdfNamespaces.Text)
                        {
                            var li = htmlDoc.CreateElement("li");
                            li.TextContent = item.TextContent ?? string.Empty;
                            ul.AppendChild(li);
                        }
                    }
                    parent.AppendChild(ul);
                    break;
                }
                default:
                    ConvertBodyNodes(child, parent, htmlDoc);
                    break;
            }
        }
    }

    private static void ConvertParagraphContent(OdfNode para, IElement pElem, IHtmlDocument htmlDoc)
    {
        foreach (var child in para.Children)
        {
            if (child.NodeType == OdfNodeType.Text)
            {
                pElem.AppendChild(htmlDoc.CreateTextNode(child.TextContent ?? string.Empty));
            }
            else if (child.LocalName == "span" && child.NamespaceUri == OdfNamespaces.Text)
            {
                var span = htmlDoc.CreateElement("span");
                ConvertParagraphContent(child, span, htmlDoc);
                pElem.AppendChild(span);
            }
            else if (child.LocalName == "note" && child.NamespaceUri == OdfNamespaces.Text)
            {
                var noteClass = child.GetAttribute("note-class", OdfNamespaces.Text);
                var citation = child.Children
                    .FirstOrDefault(c => c.LocalName == "note-citation" && c.NamespaceUri == OdfNamespaces.Text)?.TextContent ?? string.Empty;
                var sup = htmlDoc.CreateElement("sup");
                sup.SetAttribute("title", noteClass ?? string.Empty);
                sup.TextContent = citation;
                pElem.AppendChild(sup);
            }
        }
        if (!pElem.HasChildNodes)
        {
            pElem.TextContent = para.TextContent ?? string.Empty;
        }
    }
}
