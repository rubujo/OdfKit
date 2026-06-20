using System.Globalization;
using System.Text;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;
using OdfKit.Text;

namespace OdfKit.Export;

/// <summary>
/// 將 TextDocument 匯出為 RTF 的淨室轉換器。
/// </summary>
public static class OdfRtfExporter
{
    /// <summary>
    /// 將 TextDocument 匯出為 RTF 字串。
    /// </summary>
    /// <param name="document">來源文字文件。</param>
    /// <param name="options">RTF 匯出選項；若為 null 則使用預設值。</param>
    /// <returns>RTF 內容字串。</returns>
    /// <exception cref="ArgumentNullException">當 document 為 null 時引發。</exception>
    public static string Export(TextDocument document, OdfRtfExportOptions? options = null)
    {
        if (document is null)
            throw new ArgumentNullException(nameof(document));

        options ??= new OdfRtfExportOptions();

        var sb = new StringBuilder(2048);
        var context = new RtfExportContext(document);
        sb.Append(@"{\rtf1\ansi\deff0");
        sb.Append(@"{\fonttbl{\f0 ");
        AppendRtfText(sb, options.DefaultFontName);
        sb.Append(";}}");
        context.WriteColorTable(sb);
        WriteRtfMetadata(document, sb);
        WriteBody(document, document.BodyTextRoot, sb, context);
        sb.Append('}');
        return sb.ToString();
    }

    private static void WriteBody(TextDocument document, OdfNode node, StringBuilder sb, RtfExportContext context)
    {
        foreach (OdfNode child in node.Children)
        {
            if (child.NodeType != OdfNodeType.Element)
            {
                continue;
            }

            if (child.NamespaceUri == OdfNamespaces.Text && child.LocalName == "h")
            {
                int level = int.TryParse(child.GetAttribute("outline-level", OdfNamespaces.Text), out int parsed)
                    ? Math.Max(1, Math.Min(6, parsed))
                    : 1;
                int size = Math.Max(24, 44 - ((level - 1) * 4));
                sb.Append(@"\pard");
                AppendParagraphStyleControls(document, child, sb);
                sb.Append(@"\b\fs").Append(size).Append(' ');
                AppendInlineText(document, child, sb, context, InlineStyle.Empty);
                sb.Append(@"\b0\par ");
            }
            else if (child.NamespaceUri == OdfNamespaces.Text && child.LocalName == "p")
            {
                sb.Append(@"\pard");
                AppendParagraphStyleControls(document, child, sb);
                sb.Append(@"\fs24 ");
                AppendInlineText(document, child, sb, context, InlineStyle.Empty);
                sb.Append(@"\par ");
            }
            else if (child.NamespaceUri == OdfNamespaces.Text && child.LocalName == "list")
            {
                WriteList(document, child, sb, context);
            }
            else if (child.NamespaceUri == OdfNamespaces.Table && child.LocalName == "table")
            {
                WriteTable(document, child, sb, context);
            }
            else
            {
                WriteBody(document, child, sb, context);
            }
        }
    }

    private static void WriteList(TextDocument document, OdfNode listNode, StringBuilder sb, RtfExportContext context)
    {
        foreach (OdfNode item in listNode.Children)
        {
            if (item.NodeType != OdfNodeType.Element ||
                item.NamespaceUri != OdfNamespaces.Text ||
                item.LocalName != "list-item")
            {
                continue;
            }

            sb.Append(@"\pard\fi-360\li720 \bullet\tab ");
            AppendInlineText(document, item, sb, context, InlineStyle.Empty);
            sb.Append(@"\par ");
        }
    }

    private static void WriteTable(TextDocument document, OdfNode tableNode, StringBuilder sb, RtfExportContext context)
    {
        foreach (OdfNode rowNode in tableNode.Children)
        {
            if (rowNode.NodeType != OdfNodeType.Element ||
                rowNode.NamespaceUri != OdfNamespaces.Table ||
                rowNode.LocalName != "table-row")
            {
                continue;
            }

            List<OdfNode> cells = rowNode.Children
                .Where(child => child.NodeType == OdfNodeType.Element &&
                    child.NamespaceUri == OdfNamespaces.Table &&
                    (child.LocalName == "table-cell" || child.LocalName == "covered-table-cell"))
                .ToList();

            if (cells.Count == 0)
            {
                continue;
            }

            sb.Append(@"\trowd\trautofit1");
            int currentX = 0;
            foreach (OdfNode cell in cells)
            {
                if (cell.LocalName == "covered-table-cell")
                {
                    continue;
                }

                int span = int.TryParse(cell.GetAttribute("number-columns-spanned", OdfNamespaces.Table), out int parsed)
                    ? Math.Max(1, parsed)
                    : 1;

                currentX += span * 2000;
                sb.Append(@"\cellx").Append(currentX);
            }

            foreach (OdfNode cell in cells)
            {
                if (cell.LocalName == "covered-table-cell")
                {
                    continue;
                }

                sb.Append(@"\intbl ");
                AppendInlineText(document, cell, sb, context, InlineStyle.Empty);
                sb.Append(@"\cell ");
            }

            sb.Append(@"\row ");
        }
    }

    private static void AppendParagraphStyleControls(TextDocument document, OdfNode paragraphNode, StringBuilder sb)
    {
        string? styleName = paragraphNode.GetAttribute("style-name", OdfNamespaces.Text);
        if (string.IsNullOrWhiteSpace(styleName))
        {
            return;
        }

        string? textAlign = document.StyleEngine.GetStyleProperty(styleName!, "text-align", OdfNamespaces.Fo, "paragraph");

        switch (textAlign?.Trim().ToLowerInvariant())
        {
            case "center":
                sb.Append(@"\qc");
                break;
            case "end":
            case "right":
                sb.Append(@"\qr");
                break;
            case "justify":
                sb.Append(@"\qj");
                break;
            case "left":
            case "start":
                sb.Append(@"\ql");
                break;
        }

        AppendTwipsControl(sb, "li", ReadParagraphLengthTwips(document, styleName!, "margin-left"));
        AppendTwipsControl(sb, "ri", ReadParagraphLengthTwips(document, styleName!, "margin-right"));
        AppendTwipsControl(sb, "fi", ReadParagraphLengthTwips(document, styleName!, "text-indent"));
        AppendTwipsControl(sb, "sb", ReadParagraphLengthTwips(document, styleName!, "margin-top"));
        AppendTwipsControl(sb, "sa", ReadParagraphLengthTwips(document, styleName!, "margin-bottom"));
        AppendTwipsControl(sb, "sl", ReadParagraphLengthTwips(document, styleName!, "line-height"));
    }

    private static void AppendTwipsControl(StringBuilder sb, string controlName, int? twips)
    {
        if (twips.HasValue)
        {
            sb.Append('\\').Append(controlName).Append(twips.Value);
        }
    }

    private static int? ReadParagraphLengthTwips(TextDocument document, string styleName, string propertyName)
    {
        string? value = document.StyleEngine.GetStyleProperty(styleName, propertyName, OdfNamespaces.Fo, "paragraph");
        if (string.IsNullOrWhiteSpace(value) || !OdfLength.TryParse(value, out OdfLength length))
        {
            return null;
        }

        try
        {
            return (int)Math.Round(length.ToPoints() * 20, MidpointRounding.AwayFromZero);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    private static void AppendInlineText(TextDocument document, OdfNode node, StringBuilder sb, RtfExportContext context, InlineStyle style)
    {
        foreach (OdfNode child in node.Children)
        {
            if (child.NodeType == OdfNodeType.Text)
            {
                AppendStyledRtfText(sb, child.TextContent, style, context);
            }
            else if (child.NamespaceUri == OdfNamespaces.Text && child.LocalName == "span")
            {
                AppendInlineText(document, child, sb, context, style.Merge(ReadInlineStyle(document, child)));
            }
            else if (child.NamespaceUri == OdfNamespaces.Text && child.LocalName == "p")
            {
                AppendInlineText(document, child, sb, context, style);
            }
            else if (child.NamespaceUri == OdfNamespaces.Text && child.LocalName == "line-break")
            {
                sb.Append(@"\line ");
            }
            else if (child.NamespaceUri == OdfNamespaces.Text && child.LocalName == "soft-page-break")
            {
                sb.Append(@"\page ");
            }
            else if (child.NamespaceUri == OdfNamespaces.Text && child.LocalName == "tab")
            {
                sb.Append(@"\tab ");
            }
            else if (child.NamespaceUri == OdfNamespaces.Text && child.LocalName == "s")
            {
                int count = int.TryParse(child.GetAttribute("c", OdfNamespaces.Text), out int parsed)
                    ? Math.Max(1, parsed)
                    : 1;
                sb.Append(' ', count);
            }
            else if (child.NamespaceUri == OdfNamespaces.Text && child.LocalName == "note")
            {
                WriteNote(child, sb);
            }
            else if (child.NamespaceUri == OdfNamespaces.Text && child.LocalName == "a")
            {
                WriteHyperlink(document, child, sb, context, style);
            }
            else if (child.NamespaceUri == OdfNamespaces.Draw && child.LocalName == "frame")
            {
                WriteImageReference(child, sb);
            }
            else if (child.NamespaceUri == OdfNamespaces.Office && child.LocalName == "annotation-start")
            {
                string name = child.GetAttribute("name", OdfNamespaces.Office) ?? string.Empty;
                int id = context.GetAnnotationId(name);
                sb.Append(@"\atnstart").Append(id).Append(' ');
            }
            else if (child.NamespaceUri == OdfNamespaces.Office && child.LocalName == "annotation-end")
            {
                string name = child.GetAttribute("name", OdfNamespaces.Office) ?? string.Empty;
                int id = context.GetAnnotationId(name);
                sb.Append(@"\atnend").Append(id).Append(' ');
            }
            else if (child.NamespaceUri == OdfNamespaces.Office && child.LocalName == "annotation")
            {
                WriteAnnotation(child, sb, context);
            }
            else
            {
                AppendInlineText(document, child, sb, context, style);
            }
        }

        if (node.Children.Count == 0)
        {
            AppendStyledRtfText(sb, node.TextContent, style, context);
        }
    }

    private static void WriteNote(OdfNode noteNode, StringBuilder sb)
    {
        string citation = ReadFirstChildText(noteNode, "note-citation");
        string body = ReadFirstChildText(noteNode, "note-body");
        AppendRtfText(sb, citation);
        sb.Append(@"{\footnote ");
        AppendRtfText(sb, citation);
        if (!string.IsNullOrEmpty(citation) && !string.IsNullOrEmpty(body))
        {
            sb.Append(' ');
        }
        AppendRtfText(sb, body);
        sb.Append('}');
    }

    private static void WriteHyperlink(TextDocument document, OdfNode linkNode, StringBuilder sb, RtfExportContext context, InlineStyle style)
    {
        string? href = linkNode.GetAttribute("href", OdfNamespaces.XLink);
        sb.Append(@"{\field{\*\fldinst HYPERLINK """);
        AppendRtfInstructionText(sb, href);
        sb.Append(@"""}{\fldrslt ");
        AppendInlineText(document, linkNode, sb, context, style);
        sb.Append("}}");
    }

    private static void WriteImageReference(OdfNode frameNode, StringBuilder sb)
    {
        sb.Append("[Image: ");
        AppendRtfText(sb, GetImageAltText(frameNode));
        string? href = GetImageHref(frameNode);
        if (!string.IsNullOrWhiteSpace(href))
        {
            sb.Append(" (");
            AppendRtfText(sb, href);
            sb.Append(')');
        }

        sb.Append(']');
    }

    private static void WriteAnnotation(OdfNode annotationNode, StringBuilder sb, RtfExportContext context)
    {
        string name = annotationNode.GetAttribute("name", OdfNamespaces.Office) ?? string.Empty;
        int id = context.GetAnnotationId(name);
        string idStr = id > 0 ? id.ToString(CultureInfo.InvariantCulture) : name;

        string author = ReadFirstChildText(annotationNode, "creator", OdfNamespaces.Dc);
        string body = ReadAnnotationBody(annotationNode);

        sb.Append(@"{\*\atnid ").Append(idStr).Append(@"}");
        sb.Append(@"{\*\atnauthor ").Append(author).Append(@"}");
        sb.Append(@"{\*\annotation ");
        AppendRtfText(sb, body);
        sb.Append(@"}");

        sb.Append("[Comment");
        if (!string.IsNullOrEmpty(author))
        {
            sb.Append(" by ").Append(author);
        }
        sb.Append(": ").Append(body).Append(']');
    }

    private static void WriteRtfMetadata(TextDocument document, StringBuilder sb)
    {
        var meta = document.Metadata;
        if (!string.IsNullOrEmpty(meta.Title) ||
            !string.IsNullOrEmpty(meta.Creator) ||
            !string.IsNullOrEmpty(meta.Subject) ||
            !string.IsNullOrEmpty(meta.Description) ||
            !string.IsNullOrEmpty(meta.Language))
        {
            sb.Append(@"{\info");
            if (!string.IsNullOrEmpty(meta.Title))
            {
                sb.Append(@"{\title ");
                AppendRtfText(sb, meta.Title);
                sb.Append('}');
            }
            if (!string.IsNullOrEmpty(meta.Creator))
            {
                sb.Append(@"{\author ");
                AppendRtfText(sb, meta.Creator);
                sb.Append('}');
            }
            if (!string.IsNullOrEmpty(meta.Subject))
            {
                sb.Append(@"{\subject ");
                AppendRtfText(sb, meta.Subject);
                sb.Append('}');
            }
            if (!string.IsNullOrEmpty(meta.Description))
            {
                sb.Append(@"{\doccomm ");
                AppendRtfText(sb, meta.Description);
                sb.Append('}');
            }
            if (!string.IsNullOrEmpty(meta.Language))
            {
                sb.Append(@"{\comment language: ");
                AppendRtfText(sb, meta.Language);
                sb.Append('}');
            }
            sb.Append('}');
        }
    }

    private static string ReadFirstChildText(OdfNode node, string localName) =>
        ReadFirstChildText(node, localName, OdfNamespaces.Text);

    private static string ReadFirstChildText(OdfNode node, string localName, string namespaceUri)
    {
        foreach (OdfNode child in node.Children)
        {
            if (child.NodeType == OdfNodeType.Element &&
                child.NamespaceUri == namespaceUri &&
                child.LocalName == localName)
            {
                return child.TextContent ?? string.Empty;
            }
        }

        return string.Empty;
    }

    private static string ReadAnnotationBody(OdfNode annotationNode)
    {
        var sb = new StringBuilder();
        foreach (OdfNode child in annotationNode.Children)
        {
            if (child.NodeType == OdfNodeType.Element &&
                child.NamespaceUri == OdfNamespaces.Text &&
                child.LocalName == "p")
            {
                if (sb.Length > 0)
                {
                    sb.Append('\n');
                }

                sb.Append(child.TextContent);
            }
        }

        return sb.ToString();
    }

    private static string? GetImageHref(OdfNode frameNode)
    {
        OdfNode? image = FindFirstChild(frameNode, "image", OdfNamespaces.Draw);
        return image?.GetAttribute("href", OdfNamespaces.XLink);
    }

    private static string GetImageAltText(OdfNode frameNode)
    {
        string? alt = ReadOptionalChildText(frameNode, "desc", OdfNamespaces.Svg)
            ?? ReadOptionalChildText(frameNode, "title", OdfNamespaces.Svg)
            ?? frameNode.GetAttribute("name", OdfNamespaces.Draw)
            ?? GetImageHref(frameNode);
        return string.IsNullOrWhiteSpace(alt) ? "image" : alt!;
    }

    private static OdfNode? FindFirstChild(OdfNode node, string localName, string namespaceUri)
    {
        foreach (OdfNode child in node.Children)
        {
            if (child.NodeType == OdfNodeType.Element &&
                child.LocalName == localName &&
                child.NamespaceUri == namespaceUri)
            {
                return child;
            }
        }

        return null;
    }

    private static string? ReadOptionalChildText(OdfNode node, string localName, string namespaceUri)
    {
        OdfNode? child = FindFirstChild(node, localName, namespaceUri);
        return string.IsNullOrWhiteSpace(child?.TextContent) ? null : child!.TextContent;
    }

    private static InlineStyle ReadInlineStyle(TextDocument document, OdfNode node)
    {
        string? styleName = node.GetAttribute("style-name", OdfNamespaces.Text);
        if (string.IsNullOrWhiteSpace(styleName))
        {
            return InlineStyle.Empty;
        }

        return new InlineStyle(
            document.StyleEngine.GetStyleProperty(styleName!, "font-weight", OdfNamespaces.Fo, "text"),
            document.StyleEngine.GetStyleProperty(styleName!, "font-style", OdfNamespaces.Fo, "text"),
            document.StyleEngine.GetStyleProperty(styleName!, "text-underline-style", OdfNamespaces.Style, "text"),
            document.StyleEngine.GetStyleProperty(styleName!, "text-line-through-style", OdfNamespaces.Style, "text"),
            document.StyleEngine.GetStyleProperty(styleName!, "text-position", OdfNamespaces.Style, "text"),
            document.StyleEngine.GetStyleProperty(styleName!, "font-size", OdfNamespaces.Fo, "text"),
            document.StyleEngine.GetStyleProperty(styleName!, "color", OdfNamespaces.Fo, "text"));
    }

    private static void AppendStyledRtfText(StringBuilder sb, string? text, InlineStyle style, RtfExportContext context)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        bool hasFormatting = !style.IsEmpty;
        if (hasFormatting)
        {
            sb.Append('{');
            if (style.Bold)
            {
                sb.Append(@"\b ");
            }

            if (style.Italic)
            {
                sb.Append(@"\i ");
            }

            if (style.Underline)
            {
                sb.Append(@"\ul ");
            }

            if (style.Strikethrough)
            {
                sb.Append(@"\strike ");
            }

            if (style.Superscript)
            {
                sb.Append(@"\super ");
            }
            else if (style.Subscript)
            {
                sb.Append(@"\sub ");
            }

            int? halfPoints = ToRtfHalfPoints(style.FontSize);
            if (halfPoints.HasValue)
            {
                sb.Append(@"\fs").Append(halfPoints.Value).Append(' ');
            }

            int colorIndex = context.GetColorIndex(style.Color);
            if (colorIndex > 0)
            {
                sb.Append(@"\cf").Append(colorIndex).Append(' ');
            }
        }

        AppendRtfText(sb, text);
        if (hasFormatting)
        {
            sb.Append('}');
        }
    }

    private static int? ToRtfHalfPoints(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (OdfLength.TryParse(value, out OdfLength length))
        {
            return Math.Max(1, (int)Math.Round(length.ToPoints() * 2, MidpointRounding.AwayFromZero));
        }

        string trimmed = value!.Trim();
        if (trimmed.EndsWith("pt", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed.Substring(0, trimmed.Length - 2);
        }

        return double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out double points)
            ? Math.Max(1, (int)Math.Round(points * 2, MidpointRounding.AwayFromZero))
            : null;
    }

    private static string? NormalizeColor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string color = value!.Trim();
        if (color.StartsWith("#", StringComparison.Ordinal))
        {
            color = color.Substring(1);
        }

        return color.Length == 6 ? color.ToUpperInvariant() : null;
    }

    private static void AppendRtfText(StringBuilder sb, string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        foreach (char c in text!)
        {
            switch (c)
            {
                case '\\':
                case '{':
                case '}':
                    sb.Append('\\').Append(c);
                    break;
                case '\r':
                    break;
                case '\n':
                    sb.Append(@"\line ");
                    break;
                case '\u00A0':
                    sb.Append(@"\~");
                    break;
                case '\u00AD':
                    sb.Append(@"\-");
                    break;
                case '\u2011':
                    sb.Append(@"\_");
                    break;
                case '\u2002':
                    sb.Append(@"\enspace ");
                    break;
                case '\u2003':
                    sb.Append(@"\emspace ");
                    break;
                case '\u2005':
                    sb.Append(@"\qmspace ");
                    break;
                case '\u2022':
                    sb.Append(@"\bullet ");
                    break;
                case '\u2014':
                    sb.Append(@"\emdash ");
                    break;
                case '\u2013':
                    sb.Append(@"\endash ");
                    break;
                case '\u2018':
                    sb.Append(@"\lquote ");
                    break;
                case '\u2019':
                    sb.Append(@"\rquote ");
                    break;
                case '\u201C':
                    sb.Append(@"\ldblquote ");
                    break;
                case '\u201D':
                    sb.Append(@"\rdblquote ");
                    break;
                default:
                    if (c <= 0x7f)
                    {
                        sb.Append(c);
                    }
                    else
                    {
                        sb.Append(@"\u").Append((short)c).Append('?');
                    }
                    break;
            }
        }
    }

    private static void AppendRtfInstructionText(StringBuilder sb, string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        foreach (char c in text!)
        {
            switch (c)
            {
                case '\\':
                case '"':
                    sb.Append('\\').Append(c);
                    break;
                case '\r':
                case '\n':
                    break;
                default:
                    if (c <= 0x7f)
                    {
                        sb.Append(c);
                    }
                    else
                    {
                        sb.Append(@"\u").Append((short)c).Append('?');
                    }
                    break;
            }
        }
    }

    private sealed class RtfExportContext
    {
        private readonly List<string> _colors = [];
        private readonly Dictionary<string, int> _annotationIds = [];
        private int _nextAnnotationId = 1;

        public RtfExportContext(TextDocument document)
        {
            CollectColors(document, document.BodyTextRoot);
        }

        public int GetColorIndex(string? color)
        {
            string? normalized = NormalizeColor(color);
            return normalized is null ? 0 : _colors.IndexOf(normalized) + 1;
        }

        public int GetAnnotationId(string name)
        {
            if (string.IsNullOrEmpty(name))
                return 0;

            if (!_annotationIds.TryGetValue(name, out int id))
            {
                id = _nextAnnotationId++;
                _annotationIds[name] = id;
            }

            return id;
        }

        public void WriteColorTable(StringBuilder sb)
        {
            if (_colors.Count == 0)
            {
                return;
            }

            sb.Append(@"{\colortbl;");
            foreach (string color in _colors)
            {
                int red = int.Parse(color.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                int green = int.Parse(color.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                int blue = int.Parse(color.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                sb.Append(@"\red").Append(red)
                    .Append(@"\green").Append(green)
                    .Append(@"\blue").Append(blue)
                    .Append(';');
            }

            sb.Append('}');
        }

        private void CollectColors(TextDocument document, OdfNode node)
        {
            if (node.NodeType == OdfNodeType.Element &&
                node.NamespaceUri == OdfNamespaces.Text &&
                node.LocalName == "span")
            {
                AddColor(ReadInlineStyle(document, node).Color);
            }

            foreach (OdfNode child in node.Children)
            {
                CollectColors(document, child);
            }
        }

        private void AddColor(string? color)
        {
            string? normalized = NormalizeColor(color);
            if (normalized is not null && !_colors.Contains(normalized))
            {
                _colors.Add(normalized);
            }
        }
    }

    private sealed class InlineStyle
    {
        public static readonly InlineStyle Empty = new(null, null, null, null, null, null, null);

        public InlineStyle(string? fontWeight, string? fontStyle, string? underlineStyle, string? lineThroughStyle, string? textPosition, string? fontSize, string? color)
        {
            Bold = string.Equals(fontWeight, "bold", StringComparison.OrdinalIgnoreCase);
            Italic = string.Equals(fontStyle, "italic", StringComparison.OrdinalIgnoreCase);
            Underline = !string.IsNullOrWhiteSpace(underlineStyle) &&
                !string.Equals(underlineStyle, "none", StringComparison.OrdinalIgnoreCase);
            Strikethrough = !string.IsNullOrWhiteSpace(lineThroughStyle) &&
                !string.Equals(lineThroughStyle, "none", StringComparison.OrdinalIgnoreCase);
            Superscript = string.Equals(textPosition, "super", StringComparison.OrdinalIgnoreCase);
            Subscript = string.Equals(textPosition, "sub", StringComparison.OrdinalIgnoreCase);
            FontSize = string.IsNullOrWhiteSpace(fontSize) ? null : fontSize!.Trim();
            Color = string.IsNullOrWhiteSpace(color) ? null : color!.Trim();
        }

        private InlineStyle(bool bold, bool italic, bool underline, bool strikethrough, bool superscript, bool subscript, string? fontSize, string? color)
        {
            Bold = bold;
            Italic = italic;
            Underline = underline;
            Strikethrough = strikethrough;
            Superscript = superscript;
            Subscript = subscript;
            FontSize = fontSize;
            Color = color;
        }

        public bool Bold { get; }

        public bool Italic { get; }

        public bool Underline { get; }

        public bool Strikethrough { get; }

        public bool Superscript { get; }

        public bool Subscript { get; }

        public string? FontSize { get; }

        public string? Color { get; }

        public bool IsEmpty => !Bold && !Italic && !Underline && !Strikethrough && !Superscript && !Subscript && string.IsNullOrWhiteSpace(FontSize) && string.IsNullOrWhiteSpace(Color);

        public InlineStyle Merge(InlineStyle other) =>
            new(
                Bold || other.Bold,
                Italic || other.Italic,
                Underline || other.Underline,
                Strikethrough || other.Strikethrough,
                other.Superscript || (!other.Subscript && Superscript),
                other.Subscript || (!other.Superscript && Subscript),
                other.FontSize ?? FontSize,
                other.Color ?? Color);
    }
}
