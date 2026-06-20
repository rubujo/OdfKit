using System.Globalization;
using System.Net;
using System.Text;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Text;

namespace OdfKit.Export;

/// <summary>
/// 將 TextDocument 匯出為 Markdown 的淨室轉換器。
/// </summary>
public static class OdfMarkdownExporter
{
    /// <summary>
    /// 將 TextDocument 匯出為 Markdown 字串。
    /// </summary>
    /// <param name="document">來源文字文件。</param>
    /// <param name="options">Markdown 匯出選項；若為 null 則使用預設值。</param>
    /// <returns>Markdown 內容字串。</returns>
    /// <exception cref="ArgumentNullException">當 document 為 null 時引發。</exception>
    public static string Export(TextDocument document, OdfMarkdownExportOptions? options = null)
    {
        if (document is null)
            throw new ArgumentNullException(nameof(document));

        options ??= new OdfMarkdownExportOptions();
        var sb = new StringBuilder(2048);

        var meta = document.Metadata;
        if (!string.IsNullOrEmpty(meta.Title) ||
            !string.IsNullOrEmpty(meta.Creator) ||
            !string.IsNullOrEmpty(meta.Subject) ||
            !string.IsNullOrEmpty(meta.Description) ||
            !string.IsNullOrEmpty(meta.Language))
        {
            sb.AppendLine("---");
            if (!string.IsNullOrEmpty(meta.Title))
                sb.AppendLine($"title: \"{meta.Title!.Replace("\"", "\\\"")}\"");
            if (!string.IsNullOrEmpty(meta.Creator))
                sb.AppendLine($"author: \"{meta.Creator!.Replace("\"", "\\\"")}\"");
            if (!string.IsNullOrEmpty(meta.Subject))
                sb.AppendLine($"subject: \"{meta.Subject!.Replace("\"", "\\\"")}\"");
            if (!string.IsNullOrEmpty(meta.Description))
                sb.AppendLine($"description: \"{meta.Description!.Replace("\"", "\\\"")}\"");
            if (!string.IsNullOrEmpty(meta.Language))
                sb.AppendLine($"language: \"{meta.Language!.Replace("\"", "\\\"")}\"");
            sb.AppendLine("---");
            sb.AppendLine();
        }

        List<MarkdownNote> notes = [];
        WriteBody(document, document.BodyTextRoot, sb, options, notes);
        AppendNotes(sb, options, notes);
        return sb.ToString().TrimEnd();
    }

    private static void WriteBody(TextDocument document, OdfNode node, StringBuilder sb, OdfMarkdownExportOptions options, List<MarkdownNote> notes)
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
                AppendBlockSeparator(sb, options);
                sb.Append('#', level).Append(' ');
                AppendInlineText(document, child, sb, options, notes, InlineStyle.Empty);
                sb.AppendLine();
            }
            else if (child.NamespaceUri == OdfNamespaces.Text && child.LocalName == "p")
            {
                AppendBlockSeparator(sb, options);
                AppendInlineText(document, child, sb, options, notes, InlineStyle.Empty);
                sb.AppendLine();
            }
            else if (child.NamespaceUri == OdfNamespaces.Text && child.LocalName == "list")
            {
                AppendBlockSeparator(sb, options);
                WriteList(document, child, sb, options, notes);
            }
            else if (child.NamespaceUri == OdfNamespaces.Table && child.LocalName == "table")
            {
                AppendBlockSeparator(sb, options);
                WriteTable(document, child, sb, options, notes);
            }
            else
            {
                WriteBody(document, child, sb, options, notes);
            }
        }
    }

    private static void WriteList(TextDocument document, OdfNode listNode, StringBuilder sb, OdfMarkdownExportOptions options, List<MarkdownNote> notes)
    {
        foreach (OdfNode item in listNode.Children)
        {
            if (item.NodeType != OdfNodeType.Element ||
                item.NamespaceUri != OdfNamespaces.Text ||
                item.LocalName != "list-item")
            {
                continue;
            }

            sb.Append("- ");
            AppendInlineText(document, item, sb, options, notes, InlineStyle.Empty);
            sb.AppendLine();
        }
    }

    private static void WriteTable(TextDocument document, OdfNode tableNode, StringBuilder sb, OdfMarkdownExportOptions options, List<MarkdownNote> notes)
    {
        List<List<string>> rows = ReadTableRows(document, tableNode, options, notes);
        if (rows.Count == 0)
        {
            return;
        }

        int columns = rows.Max(row => row.Count);
        if (columns == 0)
        {
            return;
        }

        if (!options.UsePipeTables)
        {
            foreach (List<string> row in rows)
            {
                sb.AppendLine(string.Join("\t", row));
            }

            return;
        }

        foreach (List<string> row in rows)
        {
            while (row.Count < columns)
            {
                row.Add(string.Empty);
            }
        }

        WriteMarkdownTableRow(sb, rows[0]);
        sb.Append('|');
        for (int i = 0; i < columns; i++)
        {
            sb.Append(" --- |");
        }
        sb.AppendLine();

        for (int i = 1; i < rows.Count; i++)
        {
            WriteMarkdownTableRow(sb, rows[i]);
        }
    }

    private static List<List<string>> ReadTableRows(TextDocument document, OdfNode tableNode, OdfMarkdownExportOptions options, List<MarkdownNote> notes)
    {
        List<List<string>> rows = [];
        foreach (OdfNode rowNode in tableNode.Children)
        {
            if (rowNode.NodeType != OdfNodeType.Element ||
                rowNode.NamespaceUri != OdfNamespaces.Table ||
                rowNode.LocalName != "table-row")
            {
                continue;
            }

            var row = new List<string>();
            foreach (OdfNode cellNode in rowNode.Children)
            {
                if (cellNode.NodeType != OdfNodeType.Element ||
                    cellNode.NamespaceUri != OdfNamespaces.Table ||
                    (cellNode.LocalName != "table-cell" && cellNode.LocalName != "covered-table-cell"))
                {
                    continue;
                }

                var cellText = new StringBuilder();
                AppendTableCellText(document, cellNode, cellText, options, notes, InlineStyle.Empty);
                row.Add(NormalizeTableCell(cellText.ToString()));
            }

            rows.Add(row);
        }

        return rows;
    }

    private static void WriteMarkdownTableRow(StringBuilder sb, IReadOnlyList<string> row)
    {
        sb.Append('|');
        foreach (string cell in row)
        {
            sb.Append(' ').Append(cell).Append(" |");
        }
        sb.AppendLine();
    }

    private static void AppendInlineText(TextDocument document, OdfNode node, StringBuilder sb, OdfMarkdownExportOptions options, List<MarkdownNote> notes, InlineStyle style)
    {
        foreach (OdfNode child in node.Children)
        {
            if (child.NodeType == OdfNodeType.Text)
            {
                AppendStyledMarkdown(sb, child.TextContent, style, options);
            }
            else if (child.NamespaceUri == OdfNamespaces.Text && child.LocalName == "span")
            {
                AppendInlineText(document, child, sb, options, notes, style.Merge(ReadInlineStyle(document, child)));
            }
            else if (child.NamespaceUri == OdfNamespaces.Text && child.LocalName == "p")
            {
                AppendInlineText(document, child, sb, options, notes, style);
            }
            else if (child.NamespaceUri == OdfNamespaces.Text && child.LocalName == "line-break")
            {
                sb.AppendLine("  ");
            }
            else if (child.NamespaceUri == OdfNamespaces.Text && child.LocalName == "tab")
            {
                sb.Append('\t');
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
                MarkdownNote note = ReadNote(child, notes.Count + 1);
                notes.Add(note);
                sb.Append("[^");
                AppendFootnoteId(sb, note.Id);
                sb.Append(']');
            }
            else if (child.NamespaceUri == OdfNamespaces.Text && child.LocalName == "a")
            {
                WriteLink(document, child, sb, options, notes, style);
            }
            else if (child.NamespaceUri == OdfNamespaces.Draw && child.LocalName == "frame")
            {
                WriteImageReference(child, sb);
            }
            else if (child.NamespaceUri == OdfNamespaces.Office && child.LocalName == "annotation-start")
            {
                string name = child.GetAttribute("name", OdfNamespaces.Office) ?? string.Empty;
                OdfNode? annoNode = FindAnnotationNodeByName(document, name);
                string creator = string.Empty;
                string date = string.Empty;
                string text = string.Empty;
                if (annoNode is not null)
                {
                    creator = annoNode.GetAttribute("creator", OdfNamespaces.Dc) ?? ReadFirstChildText(annoNode, "creator", OdfNamespaces.Dc);
                    date = annoNode.GetAttribute("date", OdfNamespaces.Dc) ?? ReadFirstChildText(annoNode, "date", OdfNamespaces.Dc);
                    text = ReadAnnotationBody(annoNode);
                }
                sb.Append("<!-- annotation-start: name=\"").Append(EscapeCommentAttribute(name))
                  .Append("\" creator=\"").Append(EscapeCommentAttribute(creator))
                  .Append("\" date=\"").Append(EscapeCommentAttribute(date))
                  .Append("\" text=\"").Append(EscapeCommentAttribute(text))
                  .Append("\" -->");
            }
            else if (child.NamespaceUri == OdfNamespaces.Office && child.LocalName == "annotation-end")
            {
                string name = child.GetAttribute("name", OdfNamespaces.Office) ?? string.Empty;
                sb.Append("<!-- annotation-end: name=\"").Append(EscapeCommentAttribute(name)).Append("\" -->");
            }
            else if (child.NamespaceUri == OdfNamespaces.Office && child.LocalName == "annotation")
            {
                string name = child.GetAttribute("name", OdfNamespaces.Office) ?? string.Empty;
                if (!HasAnnotationStart(document, name))
                {
                    string creator = child.GetAttribute("creator", OdfNamespaces.Dc) ?? ReadFirstChildText(child, "creator", OdfNamespaces.Dc);
                    string date = child.GetAttribute("date", OdfNamespaces.Dc) ?? ReadFirstChildText(child, "date", OdfNamespaces.Dc);
                    string text = ReadAnnotationBody(child);
                    sb.Append("<!-- annotation: name=\"").Append(EscapeCommentAttribute(name))
                      .Append("\" creator=\"").Append(EscapeCommentAttribute(creator))
                      .Append("\" date=\"").Append(EscapeCommentAttribute(date))
                      .Append("\" text=\"").Append(EscapeCommentAttribute(text))
                      .Append("\" -->");

                    MarkdownNote note = ReadAnnotation(child, notes.Count + 1);
                    notes.Add(note);
                    sb.Append("[^");
                    AppendFootnoteId(sb, note.Id);
                    sb.Append(']');
                }
            }
            else if (child.NamespaceUri == OdfNamespaces.Table && child.LocalName == "table")
            {
                if (sb.Length > 0)
                {
                    sb.Append(' ');
                }
                sb.Append("[table]");
            }
            else
            {
                AppendInlineText(document, child, sb, options, notes, style);
            }
        }

        if (node.Children.Count == 0)
        {
            AppendStyledTableCellMarkdown(sb, node.TextContent, style, options);
        }
    }

    private static void AppendTableCellText(TextDocument document, OdfNode node, StringBuilder sb, OdfMarkdownExportOptions options, List<MarkdownNote> notes, InlineStyle style)
    {
        foreach (OdfNode child in node.Children)
        {
            if (child.NodeType == OdfNodeType.Text)
            {
                AppendStyledTableCellMarkdown(sb, child.TextContent, style, options);
            }
            else if (child.NamespaceUri == OdfNamespaces.Text && child.LocalName == "span")
            {
                AppendTableCellText(document, child, sb, options, notes, style.Merge(ReadInlineStyle(document, child)));
            }
            else if (child.NamespaceUri == OdfNamespaces.Text &&
                child.LocalName == "p")
            {
                AppendTableCellText(document, child, sb, options, notes, style);
            }
            else if (child.NamespaceUri == OdfNamespaces.Text && child.LocalName == "line-break")
            {
                sb.Append(' ');
            }
            else if (child.NamespaceUri == OdfNamespaces.Text && child.LocalName == "tab")
            {
                sb.Append('\t');
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
                MarkdownNote note = ReadNote(child, notes.Count + 1);
                notes.Add(note);
                sb.Append("[^").Append(note.Id).Append(']');
            }
            else if (child.NamespaceUri == OdfNamespaces.Text && child.LocalName == "a")
            {
                sb.Append('[');
                AppendTableCellText(document, child, sb, options, notes, style);
                sb.Append("](");
                AppendLinkDestination(sb, child.GetAttribute("href", OdfNamespaces.XLink));
                sb.Append(')');
            }
            else if (child.NamespaceUri == OdfNamespaces.Draw && child.LocalName == "frame")
            {
                AppendRawImageReference(child, sb);
            }
            else if (child.NamespaceUri == OdfNamespaces.Office && child.LocalName == "annotation-start")
            {
                string name = child.GetAttribute("name", OdfNamespaces.Office) ?? string.Empty;
                OdfNode? annoNode = FindAnnotationNodeByName(document, name);
                string creator = string.Empty;
                string date = string.Empty;
                string text = string.Empty;
                if (annoNode is not null)
                {
                    creator = annoNode.GetAttribute("creator", OdfNamespaces.Dc) ?? ReadFirstChildText(annoNode, "creator", OdfNamespaces.Dc);
                    date = annoNode.GetAttribute("date", OdfNamespaces.Dc) ?? ReadFirstChildText(annoNode, "date", OdfNamespaces.Dc);
                    text = ReadAnnotationBody(annoNode);
                }
                sb.Append("<!-- annotation-start: name=\"").Append(EscapeCommentAttribute(name))
                  .Append("\" creator=\"").Append(EscapeCommentAttribute(creator))
                  .Append("\" date=\"").Append(EscapeCommentAttribute(date))
                  .Append("\" text=\"").Append(EscapeCommentAttribute(text))
                  .Append("\" -->");
            }
            else if (child.NamespaceUri == OdfNamespaces.Office && child.LocalName == "annotation-end")
            {
                string name = child.GetAttribute("name", OdfNamespaces.Office) ?? string.Empty;
                sb.Append("<!-- annotation-end: name=\"").Append(EscapeCommentAttribute(name)).Append("\" -->");
            }
            else if (child.NamespaceUri == OdfNamespaces.Office && child.LocalName == "annotation")
            {
                string name = child.GetAttribute("name", OdfNamespaces.Office) ?? string.Empty;
                if (!HasAnnotationStart(document, name))
                {
                    string creator = child.GetAttribute("creator", OdfNamespaces.Dc) ?? ReadFirstChildText(child, "creator", OdfNamespaces.Dc);
                    string date = child.GetAttribute("date", OdfNamespaces.Dc) ?? ReadFirstChildText(child, "date", OdfNamespaces.Dc);
                    string text = ReadAnnotationBody(child);
                    sb.Append("<!-- annotation: name=\"").Append(EscapeCommentAttribute(name))
                      .Append("\" creator=\"").Append(EscapeCommentAttribute(creator))
                      .Append("\" date=\"").Append(EscapeCommentAttribute(date))
                      .Append("\" text=\"").Append(EscapeCommentAttribute(text))
                      .Append("\" -->");

                    MarkdownNote note = ReadAnnotation(child, notes.Count + 1);
                    notes.Add(note);
                    sb.Append("[^").Append(note.Id).Append(']');
                }
            }
            else if (child.NamespaceUri == OdfNamespaces.Table && child.LocalName == "table")
            {
                sb.Append("[table]");
            }
            else
            {
                AppendTableCellText(document, child, sb, options, notes, style);
            }
        }

        if (node.Children.Count == 0)
        {
            AppendStyledMarkdown(sb, node.TextContent, style, options);
        }
    }

    private static void WriteLink(TextDocument document, OdfNode linkNode, StringBuilder sb, OdfMarkdownExportOptions options, List<MarkdownNote> notes, InlineStyle style)
    {
        sb.Append('[');
        AppendInlineText(document, linkNode, sb, options, notes, style);
        sb.Append("](");
        AppendLinkDestination(sb, linkNode.GetAttribute("href", OdfNamespaces.XLink));
        sb.Append(')');
    }

    private static void WriteImageReference(OdfNode frameNode, StringBuilder sb)
    {
        sb.Append("![");
        AppendEscaped(sb, GetImageAltText(frameNode));
        sb.Append("](");
        AppendLinkDestination(sb, GetImageHref(frameNode));
        sb.Append(')');
    }

    private static void AppendRawImageReference(OdfNode frameNode, StringBuilder sb)
    {
        sb.Append("![")
            .Append(GetImageAltText(frameNode))
            .Append("](");
        AppendLinkDestination(sb, GetImageHref(frameNode));
        sb.Append(')');
    }

    private static void AppendNotes(StringBuilder sb, OdfMarkdownExportOptions options, List<MarkdownNote> notes)
    {
        if (notes.Count == 0)
        {
            return;
        }

        AppendBlockSeparator(sb, options);
        foreach (MarkdownNote note in notes)
        {
            sb.Append("[^");
            AppendFootnoteId(sb, note.Id);
            sb.Append("]: ");
            AppendEscaped(sb, note.BodyText);
            sb.AppendLine();
        }
    }

    private static MarkdownNote ReadNote(OdfNode noteNode, int fallbackIndex)
    {
        string citation = ReadFirstChildText(noteNode, "note-citation");
        string body = ReadFirstChildText(noteNode, "note-body");
        string id = !string.IsNullOrWhiteSpace(citation)
            ? citation
            : noteNode.GetAttribute("id", OdfNamespaces.Text) ?? fallbackIndex.ToString(CultureInfo.InvariantCulture);

        return new MarkdownNote(id, body);
    }

    private static MarkdownNote ReadAnnotation(OdfNode annotationNode, int fallbackIndex)
    {
        string author = ReadFirstChildText(annotationNode, "creator", OdfNamespaces.Dc);
        string body = ReadAnnotationBody(annotationNode);
        string prefix = !string.IsNullOrWhiteSpace(author)
            ? "Comment by " + author + ": "
            : "Comment: ";
        string id = "comment-" + fallbackIndex.ToString(CultureInfo.InvariantCulture);
        return new MarkdownNote(id, prefix + body);
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

    private static string NormalizeTableCell(string value)
    {
        string normalized = value.Replace("\r", " ")
            .Replace("\n", " ")
            .Trim();
        return normalized.Replace("|", "\\|");
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
            document.StyleEngine.GetStyleProperty(styleName!, "font-size", OdfNamespaces.Fo, "text"),
            document.StyleEngine.GetStyleProperty(styleName!, "color", OdfNamespaces.Fo, "text"));
    }

    private static void AppendStyledMarkdown(StringBuilder sb, string? text, InlineStyle style, OdfMarkdownExportOptions options)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        if (style.RequiresHtml(options))
        {
            if (!options.AllowInlineHtml)
            {
                AppendEscaped(sb, text);
                return;
            }

            sb.Append("<span style=\"");
            AppendCssDeclarations(sb, style);
            sb.Append("\">").Append(WebUtility.HtmlEncode(text)).Append("</span>");
            return;
        }

        string marker = style.Bold && style.Italic ? "***" : style.Bold ? "**" : style.Italic ? "*" : string.Empty;
        string strikeMarker = style.Strikethrough ? "~~" : string.Empty;
        sb.Append(strikeMarker);
        sb.Append(marker);
        AppendEscaped(sb, text);
        sb.Append(marker);
        sb.Append(strikeMarker);
    }

    private static void AppendStyledTableCellMarkdown(StringBuilder sb, string? text, InlineStyle style, OdfMarkdownExportOptions options)
    {
        if (style.RequiresHtml(options) || style.Bold || style.Italic || style.Strikethrough)
        {
            AppendStyledMarkdown(sb, text, style, options);
            return;
        }

        sb.Append(text);
    }

    private static void AppendCssDeclarations(StringBuilder sb, InlineStyle style)
    {
        bool needsSeparator = false;
        AppendCssDeclaration(sb, "font-weight", style.Bold ? "bold" : null, ref needsSeparator);
        AppendCssDeclaration(sb, "font-style", style.Italic ? "italic" : null, ref needsSeparator);
        AppendCssDeclaration(sb, "text-decoration", style.TextDecorationCss, ref needsSeparator);
        AppendCssDeclaration(sb, "font-size", style.FontSize, ref needsSeparator);
        AppendCssDeclaration(sb, "color", NormalizeCssColor(style.Color), ref needsSeparator);
    }

    private static void AppendCssDeclaration(StringBuilder sb, string name, string? value, ref bool needsSeparator)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (needsSeparator)
        {
            sb.Append("; ");
        }

        sb.Append(name).Append(':').Append(value);
        needsSeparator = true;
    }

    private static string? NormalizeCssColor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string color = value!.Trim();
        return color.StartsWith("#", StringComparison.Ordinal) ? color : null;
    }

    private static void AppendEscaped(StringBuilder sb, string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        foreach (char c in text!)
        {
            if (c is '\\' or '`' or '*' or '_' or '{' or '}' or '[' or ']' or '(' or ')' or '#' or '+' or '-' or '.' or '!' or '|' or '>')
            {
                sb.Append('\\');
            }

            sb.Append(c);
        }
    }

    private static void AppendFootnoteId(StringBuilder sb, string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        foreach (char c in text!)
        {
            if (c is '\\' or ']')
            {
                sb.Append('\\');
            }

            sb.Append(c);
        }
    }

    private static void AppendLinkDestination(StringBuilder sb, string? href)
    {
        sb.Append('<');
        if (!string.IsNullOrEmpty(href))
        {
            sb.Append(href!.Replace(">", "%3E"));
        }

        sb.Append('>');
    }

    private static void AppendBlockSeparator(StringBuilder sb, OdfMarkdownExportOptions options)
    {
        if (!options.BlankLineBetweenBlocks || sb.Length == 0)
        {
            return;
        }

        if (sb.Length >= 2 && sb[sb.Length - 1] == '\n' && sb[sb.Length - 2] == '\n')
        {
            return;
        }

        if (sb[sb.Length - 1] == '\n')
        {
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine().AppendLine();
        }
    }

    private static OdfNode? FindAnnotationNodeByName(TextDocument document, string name)
    {
        if (string.IsNullOrEmpty(name))
            return null;

        return FindNode(document.BodyTextRoot, n => n.NodeType == OdfNodeType.Element && n.NamespaceUri == OdfNamespaces.Office && n.LocalName == "annotation" && n.GetAttribute("name", OdfNamespaces.Office) == name);
    }

    private static bool HasAnnotationStart(TextDocument document, string name)
    {
        if (string.IsNullOrEmpty(name))
            return false;

        return FindNode(document.BodyTextRoot, n => n.NodeType == OdfNodeType.Element && n.NamespaceUri == OdfNamespaces.Office && n.LocalName == "annotation-start" && n.GetAttribute("name", OdfNamespaces.Office) == name) is not null;
    }

    private static OdfNode? FindNode(OdfNode root, Func<OdfNode, bool> predicate)
    {
        if (predicate(root))
            return root;

        foreach (OdfNode child in root.Children)
        {
            OdfNode? found = FindNode(child, predicate);
            if (found is not null)
                return found;
        }

        return null;
    }

    private static string EscapeCommentAttribute(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return WebUtility.HtmlEncode(value);
    }

    private sealed class MarkdownNote
    {
        public MarkdownNote(string id, string bodyText)
        {
            Id = id;
            BodyText = bodyText;
        }

        public string Id { get; }

        public string BodyText { get; }
    }

    private sealed class InlineStyle
    {
        public static readonly InlineStyle Empty = new(null, null, null, null, null, null);

        public InlineStyle(string? fontWeight, string? fontStyle, string? underlineStyle, string? lineThroughStyle, string? fontSize, string? color)
        {
            Bold = string.Equals(fontWeight, "bold", StringComparison.OrdinalIgnoreCase);
            Italic = string.Equals(fontStyle, "italic", StringComparison.OrdinalIgnoreCase);
            Underline = !string.IsNullOrWhiteSpace(underlineStyle) &&
                !string.Equals(underlineStyle, "none", StringComparison.OrdinalIgnoreCase);
            Strikethrough = !string.IsNullOrWhiteSpace(lineThroughStyle) &&
                !string.Equals(lineThroughStyle, "none", StringComparison.OrdinalIgnoreCase);
            FontSize = string.IsNullOrWhiteSpace(fontSize) ? null : fontSize!.Trim();
            Color = string.IsNullOrWhiteSpace(color) ? null : color!.Trim();
        }

        private InlineStyle(bool bold, bool italic, bool underline, bool strikethrough, string? fontSize, string? color)
        {
            Bold = bold;
            Italic = italic;
            Underline = underline;
            Strikethrough = strikethrough;
            FontSize = fontSize;
            Color = color;
        }

        public bool Bold { get; }

        public bool Italic { get; }

        public bool Underline { get; }

        public bool Strikethrough { get; }

        public string? FontSize { get; }

        public string? Color { get; }

        public string? TextDecorationCss
        {
            get
            {
                if (Underline && Strikethrough)
                {
                    return "underline line-through";
                }

                return Underline ? "underline" : Strikethrough ? "line-through" : null;
            }
        }

        public bool RequiresHtml(OdfMarkdownExportOptions options) =>
            Underline ||
            !string.IsNullOrWhiteSpace(FontSize) ||
            !string.IsNullOrWhiteSpace(Color) ||
            (Strikethrough && !options.UseTildeStrikethrough);

        public InlineStyle Merge(InlineStyle other) =>
            new(
                Bold || other.Bold,
                Italic || other.Italic,
                Underline || other.Underline,
                Strikethrough || other.Strikethrough,
                other.FontSize ?? FontSize,
                other.Color ?? Color);
    }
}
