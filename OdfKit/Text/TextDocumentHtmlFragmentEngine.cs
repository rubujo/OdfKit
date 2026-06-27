using System;
using System.Collections.Generic;
using System.Text;
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
        public string? Color { get; set; }
        public string? FontSize { get; set; }
        public string? FontFamily { get; set; }
    }

    internal static void AddHtmlFragment(TextDocument document, OdfParagraph paragraph, string html)
    {
        if (paragraph is null)
            throw new ArgumentNullException(nameof(paragraph));

        AddHtmlFragment(document, paragraph.Node, html);
    }

    internal static void AddHtmlFragment(TextDocument document, OdfNode paragraphNode, string html)
    {
        if (document is null)
            throw new ArgumentNullException(nameof(document));
        if (paragraphNode is null)
            throw new ArgumentNullException(nameof(paragraphNode));
        if (string.IsNullOrWhiteSpace(html))
            return;

        html = Regex.Replace(html, @"<!--[\s\S]*?-->", string.Empty);
        html = Regex.Replace(html, @"<(script|style)\b[^>]*>([\s\S]*?)<\/\1\s*>", string.Empty, RegexOptions.IgnoreCase);
        html = Regex.Replace(html, @"<(script|style)\b[^>]*\/>", string.Empty, RegexOptions.IgnoreCase);
        html = Regex.Replace(html, @"<(script|style)\b[^>]*>([\s\S]*)$", string.Empty, RegexOptions.IgnoreCase);

        var tokenRegex = new Regex(@"(<!--[\s\S]*?-->|</?[a-zA-Z][^>]*>|[^<]+|<)", RegexOptions.Compiled);
        var matches = tokenRegex.Matches(html);

        InlineFormat format = default;
        string? currentHref = null;
        List<SpanState> spanStack = [];
        bool inScriptOrStyle = false;

        foreach (Match match in matches)
        {
            string token = match.Value;
            bool isTag = false;
            bool isClosing = false;
            string tagName = string.Empty;

            if (token.StartsWith("<", StringComparison.Ordinal) && !token.StartsWith("<!--", StringComparison.Ordinal))
            {
                var tagMatch = Regex.Match(token, @"^<\s*(/?)\s*([a-zA-Z0-9]+)", RegexOptions.IgnoreCase);
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
                    bool isSelfClosing = token.EndsWith("/>", StringComparison.Ordinal);
                    if (!isSelfClosing)
                        inScriptOrStyle = !isClosing;
                    continue;
                }

                if (inScriptOrStyle)
                    continue;

                if (tagName == "b" || tagName == "strong")
                    format.Bold = !isClosing;
                else if (tagName == "i" || tagName == "em")
                    format.Italic = !isClosing;
                else if (tagName == "u")
                    format.Underline = !isClosing;
                else if (tagName == "br")
                {
                    if (!isClosing)
                        paragraphNode.AppendChild(new OdfNode(OdfNodeType.Element, "line-break", OdfNamespaces.Text, "text"));
                }
                else if (tagName == "a")
                {
                    if (isClosing)
                        currentHref = null;
                    else
                    {
                        var hrefMatch = Regex.Match(token, @"href\s*=\s*['""]?([^'""\s>]+)['""]?", RegexOptions.IgnoreCase);
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
                        spanStack.Add(ParseSpanState(token));
                    }
                }

                continue;
            }

            if (inScriptOrStyle)
                continue;

            string decodedText = TextDocumentDomHelper.DecodeHtmlEntities(token);
            InlineFormat active = ResolveActiveFormat(format, spanStack);
            AppendTextNode(document, paragraphNode, decodedText, active, currentHref);
        }
    }

    internal static IReadOnlyList<OdfInlineTextSegment> ParseHtmlSegments(string html)
    {
        List<OdfInlineTextSegment> segments = [];
        if (string.IsNullOrWhiteSpace(html))
            return segments;

        using TextDocument document = TextDocument.Create();
        OdfParagraph paragraph = document.AddParagraph();
        AddHtmlFragment(document, paragraph, html);

        foreach (OdfNode child in paragraph.Node.Children)
            AppendSegmentsFromNode(child, document, segments);

        return segments;
    }

    internal static string ConvertMarkdownInlineToHtml(string markdown)
    {
        if (string.IsNullOrEmpty(markdown))
            return string.Empty;

        StringBuilder builder = new(markdown.Length);
        int index = 0;
        while (index < markdown.Length)
        {
            if (markdown[index] == '<')
            {
                int end = markdown.IndexOf('>', index + 1);
                if (end > index)
                {
                    builder.Append(markdown, index, end - index + 1);
                    index = end + 1;
                    continue;
                }
            }

            if (TryAppendDelimited(markdown, ref index, builder, "***", "<b><i>", "</i></b>") ||
                TryAppendDelimited(markdown, ref index, builder, "___", "<b><i>", "</i></b>") ||
                TryAppendDelimited(markdown, ref index, builder, "**", "<b>", "</b>") ||
                TryAppendDelimited(markdown, ref index, builder, "__", "<b>", "</b>") ||
                TryAppendDelimited(markdown, ref index, builder, "*", "<i>", "</i>") ||
                TryAppendDelimited(markdown, ref index, builder, "_", "<i>", "</i>"))
            {
                continue;
            }

            AppendEscaped(builder, markdown[index]);
            index++;
        }

        return builder.ToString();
    }

    private static SpanState ParseSpanState(string token)
    {
        var state = new SpanState();
        var styleMatch = Regex.Match(token, @"style\s*=\s*(?:""([^""]*)""|'([^']*)')", RegexOptions.IgnoreCase);
        if (!styleMatch.Success)
            return state;

        string style = styleMatch.Groups[1].Success ? styleMatch.Groups[1].Value : styleMatch.Groups[2].Value;
        string? fontWeight = MatchCssValue(style, "font-weight");
        if (fontWeight is not null)
        {
            string value = fontWeight.Trim().ToLowerInvariant();
            if (value is "bold" or "700" or "800" or "900")
                state.Bold = true;
            else if (value is "normal" or "400")
                state.Bold = false;
        }

        string? fontStyle = MatchCssValue(style, "font-style");
        if (fontStyle is not null)
        {
            string value = fontStyle.Trim().ToLowerInvariant();
            if (value is "italic" or "oblique")
                state.Italic = true;
            else if (value == "normal")
                state.Italic = false;
        }

        string? decoration = MatchCssValue(style, "text-decoration");
        if (decoration is not null)
        {
            string value = decoration.Trim().ToLowerInvariant();
            if (value.Contains("underline", StringComparison.Ordinal))
                state.Underline = true;
            else if (value.Contains("none", StringComparison.Ordinal))
                state.Underline = false;
        }

        state.Color = MatchCssValue(style, "color");
        state.FontSize = MatchCssValue(style, "font-size");
        state.FontFamily = TrimCssQuotes(MatchCssValue(style, "font-family"));
        return state;
    }

    private static InlineFormat ResolveActiveFormat(InlineFormat format, List<SpanState> spanStack)
    {
        InlineFormat active = format;
        foreach (SpanState state in spanStack)
        {
            if (state.Bold.HasValue)
                active.Bold = state.Bold.Value;
            if (state.Italic.HasValue)
                active.Italic = state.Italic.Value;
            if (state.Underline.HasValue)
                active.Underline = state.Underline.Value;

            active.Color = state.Color ?? active.Color;
            active.FontSize = state.FontSize ?? active.FontSize;
            active.FontFamily = state.FontFamily ?? active.FontFamily;
        }

        return active;
    }

    private static void AppendTextNode(TextDocument document, OdfNode paragraphNode, string text, InlineFormat format, string? href)
    {
        if (href is not null)
        {
            var aNode = new OdfNode(OdfNodeType.Element, "a", OdfNamespaces.Text, "text");
            aNode.SetAttribute("href", OdfNamespaces.XLink, href, "xlink");
            AppendFormattedOrPlainText(document, aNode, text, format);
            paragraphNode.AppendChild(aNode);
            return;
        }

        AppendFormattedOrPlainText(document, paragraphNode, text, format);
    }

    private static void AppendFormattedOrPlainText(TextDocument document, OdfNode parent, string text, InlineFormat format)
    {
        if (format.HasFormatting)
        {
            var span = new OdfNode(OdfNodeType.Element, "span", OdfNamespaces.Text, "text");
            ApplyRunStyle(new OdfTextRun(span, document) { Text = text }, format);
            parent.AppendChild(span);
            return;
        }

        OdfNode? lastChild = parent.Children.Count > 0 ? parent.Children[parent.Children.Count - 1] : null;
        if (lastChild is not null && lastChild.NodeType == OdfNodeType.Text)
            lastChild.TextContent += text;
        else
            parent.AppendChild(new OdfNode(OdfNodeType.Text, string.Empty, string.Empty) { TextContent = text });
    }

    private static void AppendSegmentsFromNode(OdfNode node, TextDocument document, List<OdfInlineTextSegment> segments)
    {
        if (node.NodeType == OdfNodeType.Text)
        {
            if (!string.IsNullOrEmpty(node.TextContent))
                segments.Add(OdfInlineTextSegment.CreateText(node.TextContent));
            return;
        }

        if (node.NodeType != OdfNodeType.Element)
            return;

        if (node.LocalName == "line-break" && node.NamespaceUri == OdfNamespaces.Text)
        {
            segments.Add(OdfInlineTextSegment.LineBreak);
            return;
        }

        if (node.LocalName == "span" && node.NamespaceUri == OdfNamespaces.Text)
        {
            string styleName = node.GetAttribute("style-name", OdfNamespaces.Text) ?? string.Empty;
            segments.Add(OdfInlineTextSegment.CreateText(
                node.TextContent,
                document.StyleEngine.GetStyleProperty(styleName, "font-weight", OdfNamespaces.Fo, "text") == "bold",
                document.StyleEngine.GetStyleProperty(styleName, "font-style", OdfNamespaces.Fo, "text") == "italic",
                document.StyleEngine.GetStyleProperty(styleName, "text-underline-style", OdfNamespaces.Style, "text") == "solid",
                document.StyleEngine.GetStyleProperty(styleName, "color", OdfNamespaces.Fo, "text"),
                document.StyleEngine.GetStyleProperty(styleName, "font-name", OdfNamespaces.Style, "text")));
            return;
        }

        foreach (OdfNode child in node.Children)
            AppendSegmentsFromNode(child, document, segments);
    }

    private static void ApplyRunStyle(OdfTextRun run, InlineFormat format)
    {
        run.IsBold = format.Bold;
        run.IsItalic = format.Italic;
        run.IsUnderline = format.Underline;
        if (!string.IsNullOrWhiteSpace(format.Color))
            run.Color = NormalizeCssColor(format.Color!);
        if (!string.IsNullOrWhiteSpace(format.FontSize))
            run.FontSize = NormalizeCssLength(format.FontSize!);
        if (!string.IsNullOrWhiteSpace(format.FontFamily))
            run.FontName = format.FontFamily;
    }

    private static string? MatchCssValue(string style, string name)
    {
        Match match = Regex.Match(style, Regex.Escape(name) + @"\s*:\s*([^;]+)", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    private static string? TrimCssQuotes(string? value)
    {
        if (value is null)
            return null;

        string trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed.Trim('"', '\'');
    }

    private static string NormalizeCssLength(string value)
    {
        string trimmed = value.Trim().ToLowerInvariant();
        if (trimmed.EndsWith("px", StringComparison.Ordinal) &&
            double.TryParse(trimmed.Substring(0, trimmed.Length - 2), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double pixels))
        {
            return (pixels * 0.75d).ToString("0.####", System.Globalization.CultureInfo.InvariantCulture) + "pt";
        }

        return trimmed;
    }

    private static string NormalizeCssColor(string value)
    {
        string trimmed = value.Trim();
        return trimmed.Equals("black", StringComparison.OrdinalIgnoreCase) ? "#000000" :
            trimmed.Equals("white", StringComparison.OrdinalIgnoreCase) ? "#FFFFFF" :
            trimmed.Equals("red", StringComparison.OrdinalIgnoreCase) ? "#FF0000" :
            trimmed.Equals("blue", StringComparison.OrdinalIgnoreCase) ? "#0000FF" :
            trimmed.Equals("green", StringComparison.OrdinalIgnoreCase) ? "#008000" :
            trimmed;
    }

    private static bool TryAppendDelimited(string markdown, ref int index, StringBuilder builder, string marker, string openTag, string closeTag)
    {
        if (!markdown.AsSpan(index).StartsWith(marker.AsSpan(), StringComparison.Ordinal))
            return false;

        int contentStart = index + marker.Length;
        int contentEnd = markdown.IndexOf(marker, contentStart, StringComparison.Ordinal);
        if (contentEnd < 0)
            return false;

        builder.Append(openTag);
        for (int i = contentStart; i < contentEnd; i++)
            AppendEscaped(builder, markdown[i]);
        builder.Append(closeTag);
        index = contentEnd + marker.Length;
        return true;
    }

    private static void AppendEscaped(StringBuilder builder, char ch)
    {
        _ = ch switch
        {
            '&' => builder.Append("&amp;"),
            '<' => builder.Append("&lt;"),
            '>' => builder.Append("&gt;"),
            '"' => builder.Append("&quot;"),
            _ => builder.Append(ch),
        };
    }

    private struct InlineFormat
    {
        public bool Bold { get; set; }
        public bool Italic { get; set; }
        public bool Underline { get; set; }
        public string? Color { get; set; }
        public string? FontSize { get; set; }
        public string? FontFamily { get; set; }

        public readonly bool HasFormatting =>
            Bold ||
            Italic ||
            Underline ||
            !string.IsNullOrWhiteSpace(Color) ||
            !string.IsNullOrWhiteSpace(FontSize) ||
            !string.IsNullOrWhiteSpace(FontFamily);
    }
}

internal readonly record struct OdfInlineTextSegment(
    string Text,
    bool IsLineBreak,
    bool Bold,
    bool Italic,
    bool Underline,
    string? Color,
    string? FontFamily)
{
    public static OdfInlineTextSegment LineBreak { get; } = new(string.Empty, true, false, false, false, null, null);

    public static OdfInlineTextSegment CreateText(
        string text,
        bool bold = false,
        bool italic = false,
        bool underline = false,
        string? color = null,
        string? fontFamily = null)
        => new(text, false, bold, italic, underline, color, fontFamily);
}
