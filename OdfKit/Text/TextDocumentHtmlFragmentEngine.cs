using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text;

/// <summary>
/// 文字文件 HTML 片段解析引擎（內部協作者）。
/// </summary>
internal static class TextDocumentHtmlFragmentEngine
{
    private sealed class SpanState
    {
        public bool? Bold { get; set; }
        public bool? Italic { get; set; }
        public bool? Underline { get; set; }
    }

    internal static void AddHtmlFragment(TextDocument document, OdfParagraph paragraph, string html)
    {
        if (paragraph is null)
            throw new ArgumentNullException(nameof(paragraph));
        if (string.IsNullOrWhiteSpace(html))
            return;

        html = Regex.Replace(html, @"<!--[\s\S]*?-->", "");
        html = Regex.Replace(html, @"<(script|style)\b[^>]*>([\s\S]*?)<\/\1\s*>", "", RegexOptions.IgnoreCase);
        html = Regex.Replace(html, @"<(script|style)\b[^>]*\/>", "", RegexOptions.IgnoreCase);
        html = Regex.Replace(html, @"<(script|style)\b[^>]*>([\s\S]*)$", "", RegexOptions.IgnoreCase);

        var tokenRegex = new Regex(@"(<!--[\s\S]*?-->|</?[a-zA-Z][^>]*>|[^<]+|<)", RegexOptions.Compiled);
        var matches = tokenRegex.Matches(html);

        bool isBold = false;
        bool isItalic = false;
        bool isUnderline = false;
        string? currentHref = null;
        List<SpanState> spanStack = [];
        bool inScriptOrStyle = false;

        foreach (Match match in matches)
        {
            string text = match.Value;
            bool isTag = false;
            bool isClosing = false;
            string tagName = "";

            if (text.StartsWith("<") && !text.StartsWith("<!--"))
            {
                var tagMatch = Regex.Match(text, @"^<\s*(/?)\s*([a-zA-Z0-9]+)", RegexOptions.IgnoreCase);
                if (tagMatch.Success)
                {
                    isTag = true;
                    isClosing = tagMatch.Groups[1].Value == "/";
                    tagName = tagMatch.Groups[2].Value.ToLowerInvariant();
                }
            }

            if (isTag)
            {
                if (tagName == "script" || tagName == "style")
                {
                    bool isSelfClosing = text.EndsWith("/>");
                    if (!isSelfClosing)
                        inScriptOrStyle = !isClosing;
                    continue;
                }

                if (inScriptOrStyle)
                    continue;

                if (tagName == "b" || tagName == "strong")
                    isBold = !isClosing;
                else if (tagName == "i" || tagName == "em")
                    isItalic = !isClosing;
                else if (tagName == "u")
                    isUnderline = !isClosing;
                else if (tagName == "br")
                {
                    if (!isClosing)
                        paragraph.Node.AppendChild(new OdfNode(OdfNodeType.Element, "line-break", OdfNamespaces.Text, "text"));
                }
                else if (tagName == "a")
                {
                    if (isClosing)
                        currentHref = null;
                    else
                    {
                        var hrefMatch = Regex.Match(text, @"href\s*=\s*['""]?([^'""\s>]+)['""]?", RegexOptions.IgnoreCase);
                        if (hrefMatch.Success)
                            currentHref = hrefMatch.Groups[1].Value;
                    }
                }
                else if (tagName == "span")
                {
                    if (isClosing)
                    {
                        if (spanStack.Count > 0)
                            spanStack.RemoveAt(spanStack.Count - 1);
                    }
                    else
                    {
                        bool? styleBold = null;
                        bool? styleItalic = null;
                        bool? styleUnderline = null;

                        var styleMatch = Regex.Match(text, @"style\s*=\s*(?:""([^""]*)""|'([^']*)')", RegexOptions.IgnoreCase);
                        if (styleMatch.Success)
                        {
                            string styleStr = styleMatch.Groups[1].Success ? styleMatch.Groups[1].Value : styleMatch.Groups[2].Value;

                            var boldMatch = Regex.Match(styleStr, @"font-weight\s*:\s*([^;]+)", RegexOptions.IgnoreCase);
                            if (boldMatch.Success)
                            {
                                string val = boldMatch.Groups[1].Value.Trim().ToLowerInvariant();
                                if (val == "bold" || val == "700" || val == "800" || val == "900")
                                    styleBold = true;
                                else if (val == "normal" || val == "400")
                                    styleBold = false;
                            }

                            var italicMatch = Regex.Match(styleStr, @"font-style\s*:\s*([^;]+)", RegexOptions.IgnoreCase);
                            if (italicMatch.Success)
                            {
                                string val = italicMatch.Groups[1].Value.Trim().ToLowerInvariant();
                                if (val == "italic" || val == "oblique")
                                    styleItalic = true;
                                else if (val == "normal")
                                    styleItalic = false;
                            }

                            var underlineMatch = Regex.Match(styleStr, @"text-decoration\s*:\s*([^;]+)", RegexOptions.IgnoreCase);
                            if (underlineMatch.Success)
                            {
                                string val = underlineMatch.Groups[1].Value.Trim().ToLowerInvariant();
                                if (val == "underline")
                                    styleUnderline = true;
                                else if (val == "none")
                                    styleUnderline = false;
                            }
                        }

                        spanStack.Add(new SpanState { Bold = styleBold, Italic = styleItalic, Underline = styleUnderline });
                    }
                }
            }
            else
            {
                if (inScriptOrStyle)
                    continue;

                string decodedText = TextDocument.DecodeHtmlEntities(text);

                bool activeBold = isBold;
                bool activeItalic = isItalic;
                bool activeUnderline = isUnderline;

                foreach (var state in spanStack)
                {
                    if (state.Bold.HasValue)
                        activeBold = state.Bold.Value;
                    if (state.Italic.HasValue)
                        activeItalic = state.Italic.Value;
                    if (state.Underline.HasValue)
                        activeUnderline = state.Underline.Value;
                }

                if (currentHref is not null)
                {
                    var aNode = new OdfNode(OdfNodeType.Element, "a", OdfNamespaces.Text, "text");
                    aNode.SetAttribute("href", OdfNamespaces.XLink, currentHref, "xlink");
                    if (activeBold || activeItalic || activeUnderline)
                    {
                        var span = new OdfNode(OdfNodeType.Element, "span", OdfNamespaces.Text, "text");
                        var run = new OdfTextRun(span, document) { Text = decodedText, IsBold = activeBold, IsItalic = activeItalic, IsUnderline = activeUnderline };
                        aNode.AppendChild(span);
                    }
                    else
                    {
                        aNode.AppendChild(new OdfNode(OdfNodeType.Text, string.Empty, string.Empty) { TextContent = decodedText });
                    }
                    paragraph.Node.AppendChild(aNode);
                }
                else
                {
                    if (activeBold || activeItalic || activeUnderline)
                    {
                        var span = new OdfNode(OdfNodeType.Element, "span", OdfNamespaces.Text, "text");
                        var run = new OdfTextRun(span, document) { Text = decodedText, IsBold = activeBold, IsItalic = activeItalic, IsUnderline = activeUnderline };
                        paragraph.Node.AppendChild(span);
                    }
                    else
                    {
                        var lastChild = paragraph.Node.Children.Count > 0 ? paragraph.Node.Children[paragraph.Node.Children.Count - 1] : null;
                        if (lastChild is not null && lastChild.NodeType == OdfNodeType.Text)
                            lastChild.TextContent += decodedText;
                        else
                            paragraph.Node.AppendChild(new OdfNode(OdfNodeType.Text, string.Empty, string.Empty) { TextContent = decodedText });
                    }
                }
            }
        }
    }
}
