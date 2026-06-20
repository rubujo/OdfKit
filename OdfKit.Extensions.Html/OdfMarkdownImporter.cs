using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;
using OdfKit.Text;

namespace OdfKit.Export;

/// <summary>
/// 將 Markdown 匯入為 <see cref="TextDocument"/> 的 managed 淨室轉換器。
/// </summary>
public static class OdfMarkdownImporter
{
    /// <summary>
    /// 從 Markdown 字串建立文字文件。
    /// </summary>
    /// <param name="markdown">來源 Markdown 內容。</param>
    /// <param name="options">Markdown 匯入選項；若為 null 則使用預設值。</param>
    /// <returns>轉換後的文字文件。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="markdown"/> 為 null 時引發。</exception>
    public static TextDocument Import(string markdown, OdfMarkdownImportOptions? options = null)
    {
        if (markdown is null)
            throw new ArgumentNullException(nameof(markdown));

        options ??= new OdfMarkdownImportOptions();
        if (OdfMarkdownMarkdigImporter.TryImport(markdown, options, out TextDocument? markdigDocument))
        {
            return markdigDocument!;
        }

        string[] lines = SplitLines(markdown);
        string? title = null;
        string? author = null;
        string? subject = null;
        string? description = null;
        string? language = null;
        int linesStartIdx = 0;

        if (lines.Length > 0 && lines[0].Trim() == "---")
        {
            int endFrontMatter = -1;
            for (int k = 1; k < lines.Length; k++)
            {
                if (lines[k].Trim() == "---")
                {
                    endFrontMatter = k;
                    break;
                }
            }

            if (endFrontMatter > 0)
            {
                for (int k = 1; k < endFrontMatter; k++)
                {
                    string yamlLine = lines[k];
                    int colon = yamlLine.IndexOf(':');
                    if (colon > 0)
                    {
                        string key = yamlLine.Substring(0, colon).Trim().ToLowerInvariant();
                        string val = yamlLine.Substring(colon + 1).Trim();
                        if (val.Length >= 2 && val[0] == '"' && val[val.Length - 1] == '"')
                        {
                            val = val.Substring(1, val.Length - 2);
                        }
                        else if (val.Length >= 2 && val[0] == '\'' && val[val.Length - 1] == '\'')
                        {
                            val = val.Substring(1, val.Length - 2);
                        }

                        switch (key)
                        {
                            case "title":
                                title = val;
                                break;
                            case "author":
                                author = val;
                                break;
                            case "subject":
                                subject = val;
                                break;
                            case "description":
                                description = val;
                                break;
                            case "language":
                                language = val;
                                break;
                        }
                    }
                }
                linesStartIdx = endFrontMatter + 1;
            }
        }

        Dictionary<string, string> notes = ReadNoteDefinitions(lines);
        var document = TextDocument.Create();
        if (title is not null)
            document.Metadata.Title = title;
        if (author is not null)
            document.Metadata.Creator = author;
        if (subject is not null)
            document.Metadata.Subject = subject;
        if (description is not null)
            document.Metadata.Description = description;
        if (language is not null)
            document.Metadata.Language = language;

        for (int i = linesStartIdx; i < lines.Length; i++)
        {
            string line = lines[i];
            if (IsBlank(line) || IsNoteDefinition(line))
            {
                continue;
            }

            if (TryReadHeading(line, out int headingLevel, out string? headingText))
            {
                document.AddHeading(UnescapeMarkdown(headingText!), headingLevel);
                continue;
            }

            if (TryReadListItem(line, out string? itemText))
            {
                OdfList list = document.AddList();
                while (i < lines.Length && TryReadListItem(lines[i], out itemText))
                {
                    OdfListItem item = list.AddListItem();
                    OdfParagraph itemParagraph = item.AddParagraph();
                    AppendInlineText(itemParagraph, itemText!, options, notes);
                    i++;
                }

                i--;
                continue;
            }

            if (options.AcceptPipeTables && TryReadTable(lines, i, out List<List<string>>? rows, out int consumed))
            {
                OdfTable table = document.AddTable(rows!.Count, rows[0].Count);
                for (int row = 0; row < rows.Count; row++)
                {
                    for (int column = 0; column < rows[row].Count; column++)
                    {
                        table.GetCell(row, column).AddParagraph(UnescapeMarkdown(rows[row][column]));
                    }
                }

                i += consumed - 1;
                continue;
            }

            var paragraphBuilder = new StringBuilder(line.Trim());
            while (i + 1 < lines.Length &&
                !IsBlank(lines[i + 1]) &&
                !IsNoteDefinition(lines[i + 1]) &&
                !TryReadHeading(lines[i + 1], out _, out _) &&
                !TryReadListItem(lines[i + 1], out _) &&
                !(options.AcceptPipeTables && TryReadTable(lines, i + 1, out _, out _)))
            {
                paragraphBuilder.Append(' ').Append(lines[++i].Trim());
            }

            OdfParagraph paragraph = document.AddParagraph();
            AppendInlineText(paragraph, paragraphBuilder.ToString(), options, notes);
        }

        return document;
    }

    /// <summary>
    /// 從 Markdown reader 建立文字文件。
    /// </summary>
    public static TextDocument Import(TextReader reader, OdfMarkdownImportOptions? options = null)
    {
        if (reader is null)
            throw new ArgumentNullException(nameof(reader));

        return Import(reader.ReadToEnd(), options);
    }

    /// <summary>
    /// 從 Markdown 檔案建立文字文件。
    /// </summary>
    public static TextDocument Load(string path, OdfMarkdownImportOptions? options = null)
    {
        if (string.IsNullOrEmpty(path))
            throw new ArgumentNullException(nameof(path));

        using var reader = File.OpenText(path);
        return Import(reader, options);
    }

    private static string[] SplitLines(string markdown) =>
        markdown.Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Split('\n');

    private static Dictionary<string, string> ReadNoteDefinitions(string[] lines)
    {
        var notes = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string line in lines)
        {
            if (TryReadNoteDefinition(line, out string? id, out string? body))
            {
                notes[id!] = UnescapeMarkdown(body!);
            }
        }

        return notes;
    }

    private static void AppendInlineText(OdfParagraph paragraph, string text, OdfMarkdownImportOptions options, Dictionary<string, string> notes)
    {
        int index = 0;
        while (index < text.Length)
        {
            int marker = FindNextInlineMarker(text, index);
            if (marker < 0)
            {
                AppendTextRun(paragraph, text.Substring(index));
                return;
            }

            AppendTextRun(paragraph, text.Substring(index, marker - index));
            if (TryAppendNoteReference(paragraph, text, marker, notes, out int nextIndex) ||
                TryAppendImage(paragraph, text, marker, out nextIndex) ||
                TryAppendLink(paragraph, text, marker, out nextIndex) ||
                (options.AcceptInlineHtml && TryAppendHtmlSpan(paragraph, text, marker, out nextIndex)) ||
                TryAppendAnnotationMarker(paragraph, text, marker, out nextIndex) ||
                (options.AcceptTildeStrikethrough && TryAppendStrikethrough(paragraph, text, marker, out nextIndex)) ||
                TryAppendEmphasis(paragraph, text, marker, out nextIndex))
            {
                index = nextIndex;
                continue;
            }

            AppendTextRun(paragraph, text.Substring(marker, 1));
            index = marker + 1;
        }
    }

    private static int FindNextInlineMarker(string text, int start)
    {
        int best = -1;
        ReadOnlySpan<string> markers = ["<!--", "![", "[^", "[", "<span", "~~", "***", "**", "*"];
        foreach (string marker in markers)
        {
            int index = text.IndexOf(marker, start, StringComparison.Ordinal);
            if (index >= 0 && (best < 0 || index < best))
            {
                best = index;
            }
        }

        return best;
    }

    private static bool TryAppendNoteReference(
        OdfParagraph paragraph,
        string text,
        int marker,
        Dictionary<string, string> notes,
        out int nextIndex)
    {
        nextIndex = marker;
        if (!StartsWithAt(text, marker, "[^"))
        {
            return false;
        }

        int idStart = marker + 2;
        int idEnd = text.IndexOf(']', idStart);
        if (idEnd < 0)
        {
            return false;
        }

        string id = text.Substring(idStart, idEnd - idStart);
        if (notes.TryGetValue(id, out string? body))
        {
            AppendNote(paragraph, id, body);
        }
        else
        {
            AppendTextRun(paragraph, text.Substring(marker, idEnd - marker + 1));
        }

        nextIndex = idEnd + 1;
        return true;
    }

    private static bool TryAppendImage(OdfParagraph paragraph, string text, int marker, out int nextIndex)
    {
        nextIndex = marker;
        if (!StartsWithAt(text, marker, "!["))
        {
            return false;
        }

        if (!TryReadBracketAndDestination(text, marker + 1, out string? altText, out string? destination, out nextIndex))
        {
            return false;
        }

        OdfImage image = paragraph.AddImage(UnwrapDestination(destination!), OdfLength.FromCentimeters(4), OdfLength.FromCentimeters(3), altText);
        image.AltText = UnescapeMarkdown(altText!);
        return true;
    }

    private static bool TryAppendLink(OdfParagraph paragraph, string text, int marker, out int nextIndex)
    {
        nextIndex = marker;
        if (text[marker] != '[' || StartsWithAt(text, marker, "[^"))
        {
            return false;
        }

        if (!TryReadBracketAndDestination(text, marker, out string? linkText, out string? destination, out nextIndex))
        {
            return false;
        }

        paragraph.AddHyperlink(UnwrapDestination(destination!), UnescapeMarkdown(linkText!));
        return true;
    }

    private static bool TryAppendHtmlSpan(OdfParagraph paragraph, string text, int marker, out int nextIndex)
    {
        nextIndex = marker;
        if (!StartsWithAt(text, marker, "<span"))
        {
            return false;
        }

        int startTagEnd = text.IndexOf('>', marker);
        if (startTagEnd < 0)
        {
            return false;
        }

        const string endTag = "</span>";
        int endTagStart = text.IndexOf(endTag, startTagEnd + 1, StringComparison.OrdinalIgnoreCase);
        if (endTagStart < 0)
        {
            return false;
        }

        string startTag = text.Substring(marker, startTagEnd - marker + 1);
        string spanText = WebUtility.HtmlDecode(text.Substring(startTagEnd + 1, endTagStart - startTagEnd - 1));
        ApplyInlineStyle(paragraph.AddTextRun(spanText), ReadSpanStyle(startTag));
        nextIndex = endTagStart + endTag.Length;
        return true;
    }

    private static bool TryAppendEmphasis(OdfParagraph paragraph, string text, int marker, out int nextIndex)
    {
        nextIndex = marker;
        if (StartsWithAt(text, marker, "***"))
        {
            return TryAppendDelimitedRun(paragraph, text, marker, "***", new InlineImportStyle(Bold: true, Italic: true), out nextIndex);
        }

        if (StartsWithAt(text, marker, "**"))
        {
            return TryAppendDelimitedRun(paragraph, text, marker, "**", new InlineImportStyle(Bold: true), out nextIndex);
        }

        if (text[marker] == '*')
        {
            return TryAppendDelimitedRun(paragraph, text, marker, "*", new InlineImportStyle(Italic: true), out nextIndex);
        }

        return false;
    }

    private static bool TryAppendStrikethrough(OdfParagraph paragraph, string text, int marker, out int nextIndex) =>
        TryAppendDelimitedRun(paragraph, text, marker, "~~", new InlineImportStyle(Strikethrough: true), out nextIndex);

    private static bool TryAppendDelimitedRun(
        OdfParagraph paragraph,
        string text,
        int marker,
        string delimiter,
        InlineImportStyle style,
        out int nextIndex)
    {
        nextIndex = marker;
        int end = text.IndexOf(delimiter, marker + delimiter.Length, StringComparison.Ordinal);
        if (end < 0)
        {
            return false;
        }

        ApplyInlineStyle(
            paragraph.AddTextRun(UnescapeMarkdown(text.Substring(marker + delimiter.Length, end - marker - delimiter.Length))),
            style);
        nextIndex = end + delimiter.Length;
        return true;
    }

    private static InlineImportStyle ReadSpanStyle(string startTag)
    {
        string? styleValue = ReadAttributeValue(startTag, "style");
        if (string.IsNullOrWhiteSpace(styleValue))
        {
            return InlineImportStyle.Empty;
        }

        bool bold = false;
        bool italic = false;
        bool underline = false;
        bool strikethrough = false;
        string? fontSize = null;
        string? color = null;

        string[] declarations = styleValue!.Split(';');
        foreach (string declaration in declarations)
        {
            int separator = declaration.IndexOf(':');
            if (separator <= 0)
            {
                continue;
            }

            string name = declaration.Substring(0, separator).Trim();
            string value = declaration.Substring(separator + 1).Trim();
            if (string.Equals(name, "font-weight", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(value, "bold", StringComparison.OrdinalIgnoreCase))
            {
                bold = true;
            }
            else if (string.Equals(name, "font-style", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(value, "italic", StringComparison.OrdinalIgnoreCase))
            {
                italic = true;
            }
            else if (string.Equals(name, "text-decoration", StringComparison.OrdinalIgnoreCase))
            {
                underline = value.IndexOf("underline", StringComparison.OrdinalIgnoreCase) >= 0;
                strikethrough = value.IndexOf("line-through", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            else if (string.Equals(name, "font-size", StringComparison.OrdinalIgnoreCase))
            {
                fontSize = value;
            }
            else if (string.Equals(name, "color", StringComparison.OrdinalIgnoreCase))
            {
                color = value;
            }
        }

        return new InlineImportStyle(bold, italic, underline, strikethrough, fontSize, color);
    }

    private static string? ReadAttributeValue(string tag, string attributeName)
    {
        string prefix = attributeName + "=\"";
        int start = tag.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
        {
            return null;
        }

        start += prefix.Length;
        int end = tag.IndexOf('"', start);
        return end < 0 ? null : WebUtility.HtmlDecode(tag.Substring(start, end - start));
    }

    private static void ApplyInlineStyle(OdfTextRun run, InlineImportStyle style)
    {
        if (style.Bold)
        {
            run.IsBold = true;
        }

        if (style.Italic)
        {
            run.IsItalic = true;
        }

        if (style.Underline)
        {
            run.IsUnderline = true;
        }

        if (style.Strikethrough)
        {
            run.IsStrikethrough = true;
        }

        if (!string.IsNullOrWhiteSpace(style.FontSize))
        {
            run.SetFontSize(style.FontSize!);
        }

        if (!string.IsNullOrWhiteSpace(style.Color))
        {
            run.Color = style.Color;
        }
    }

    private static bool TryReadBracketAndDestination(string text, int bracketStart, out string? label, out string? destination, out int nextIndex)
    {
        label = null;
        destination = null;
        nextIndex = bracketStart;
        if (bracketStart >= text.Length || text[bracketStart] != '[')
        {
            return false;
        }

        int labelEnd = text.IndexOf(']', bracketStart + 1);
        if (labelEnd < 0 || labelEnd + 1 >= text.Length || text[labelEnd + 1] != '(')
        {
            return false;
        }

        int destinationEnd = text.IndexOf(')', labelEnd + 2);
        if (destinationEnd < 0)
        {
            return false;
        }

        label = text.Substring(bracketStart + 1, labelEnd - bracketStart - 1);
        destination = text.Substring(labelEnd + 2, destinationEnd - labelEnd - 2);
        nextIndex = destinationEnd + 1;
        return true;
    }

    private static bool StartsWithAt(string text, int index, string value)
    {
        if (index < 0 || index + value.Length > text.Length)
        {
            return false;
        }

        return string.CompareOrdinal(text, index, value, 0, value.Length) == 0;
    }

    private static string UnwrapDestination(string destination)
    {
        string trimmed = destination.Trim();
        if (trimmed.Length >= 2 && trimmed[0] == '<' && trimmed[trimmed.Length - 1] == '>')
        {
            return trimmed.Substring(1, trimmed.Length - 2);
        }

        return UnescapeMarkdown(trimmed);
    }

    private static void AppendTextRun(OdfParagraph paragraph, string text)
    {
        if (!string.IsNullOrEmpty(text))
        {
            paragraph.AddTextRun(UnescapeMarkdown(text));
        }
    }

    private static void AppendNote(OdfParagraph paragraph, string id, string body)
    {
        if (TryReadComment(body, out string? author, out string? commentText))
        {
            string canonicalName = id;
            if (HasAnnotationNodeInParagraph(paragraph, canonicalName))
            {
                return;
            }

            paragraph.AddComment(new OdfComment(author!, commentText!));
            return;
        }

        paragraph.AddFootnote(id, body);
    }

    private static bool HasAnnotationNodeInParagraph(OdfParagraph paragraph, string name)
    {
        return FindNode(paragraph.Node, n => n.NodeType == OdfNodeType.Element && n.NamespaceUri == OdfNamespaces.Office && (n.LocalName == "annotation" || n.LocalName == "annotation-start") && n.GetAttribute("name", OdfNamespaces.Office) == name) is not null;
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

    private static bool TryAppendAnnotationMarker(OdfParagraph paragraph, string text, int marker, out int nextIndex)
    {
        nextIndex = marker;
        if (!StartsWithAt(text, marker, "<!--"))
        {
            return false;
        }

        int endComment = text.IndexOf("-->", marker);
        if (endComment < 0)
        {
            return false;
        }

        string commentBody = text.Substring(marker + 4, endComment - marker - 4).Trim();
        nextIndex = endComment + 3;

        if (commentBody.StartsWith("annotation-start:", StringComparison.Ordinal))
        {
            string name = ReadAttributeValue(commentBody, "name") ?? string.Empty;
            string creator = ReadAttributeValue(commentBody, "creator") ?? string.Empty;
            string date = ReadAttributeValue(commentBody, "date") ?? string.Empty;
            string commentText = ReadAttributeValue(commentBody, "text") ?? string.Empty;

            var startNode = new OdfNode(OdfNodeType.Element, "annotation-start", OdfNamespaces.Office, "office");
            startNode.SetAttribute("name", OdfNamespaces.Office, name, "office");
            paragraph.Node.AppendChild(startNode);

            var annoNode = new OdfNode(OdfNodeType.Element, "annotation", OdfNamespaces.Office, "office");
            annoNode.SetAttribute("name", OdfNamespaces.Office, name, "office");

            if (!string.IsNullOrEmpty(creator))
            {
                var creatorNode = new OdfNode(OdfNodeType.Element, "creator", OdfNamespaces.Dc, "dc") { TextContent = creator };
                annoNode.AppendChild(creatorNode);
            }
            if (!string.IsNullOrEmpty(date))
            {
                var dateNode = new OdfNode(OdfNodeType.Element, "date", OdfNamespaces.Dc, "dc") { TextContent = date };
                annoNode.AppendChild(dateNode);
            }
            if (!string.IsNullOrEmpty(commentText))
            {
                var pNode = new OdfNode(OdfNodeType.Element, "p", OdfNamespaces.Text, "text") { TextContent = commentText };
                annoNode.AppendChild(pNode);
            }
            paragraph.Node.AppendChild(annoNode);

            return true;
        }
        else if (commentBody.StartsWith("annotation-end:", StringComparison.Ordinal))
        {
            string name = ReadAttributeValue(commentBody, "name") ?? string.Empty;

            var endNode = new OdfNode(OdfNodeType.Element, "annotation-end", OdfNamespaces.Office, "office");
            endNode.SetAttribute("name", OdfNamespaces.Office, name, "office");
            paragraph.Node.AppendChild(endNode);

            return true;
        }
        else if (commentBody.StartsWith("annotation:", StringComparison.Ordinal))
        {
            string name = ReadAttributeValue(commentBody, "name") ?? string.Empty;
            string creator = ReadAttributeValue(commentBody, "creator") ?? string.Empty;
            string date = ReadAttributeValue(commentBody, "date") ?? string.Empty;
            string commentText = ReadAttributeValue(commentBody, "text") ?? string.Empty;

            var annoNode = new OdfNode(OdfNodeType.Element, "annotation", OdfNamespaces.Office, "office");
            annoNode.SetAttribute("name", OdfNamespaces.Office, name, "office");

            if (!string.IsNullOrEmpty(creator))
            {
                var creatorNode = new OdfNode(OdfNodeType.Element, "creator", OdfNamespaces.Dc, "dc") { TextContent = creator };
                annoNode.AppendChild(creatorNode);
            }
            if (!string.IsNullOrEmpty(date))
            {
                var dateNode = new OdfNode(OdfNodeType.Element, "date", OdfNamespaces.Dc, "dc") { TextContent = date };
                annoNode.AppendChild(dateNode);
            }
            if (!string.IsNullOrEmpty(commentText))
            {
                var pNode = new OdfNode(OdfNodeType.Element, "p", OdfNamespaces.Text, "text") { TextContent = commentText };
                annoNode.AppendChild(pNode);
            }
            paragraph.Node.AppendChild(annoNode);

            return true;
        }

        return false;
    }

    private static bool TryReadComment(string body, out string? author, out string? commentText)
    {
        const string commentByPrefix = "Comment by ";
        const string commentPrefix = "Comment: ";

        if (body.StartsWith(commentByPrefix, StringComparison.Ordinal))
        {
            int separator = body.IndexOf(": ", commentByPrefix.Length, StringComparison.Ordinal);
            if (separator > commentByPrefix.Length)
            {
                author = body.Substring(commentByPrefix.Length, separator - commentByPrefix.Length);
                commentText = body.Substring(separator + 2);
                return true;
            }
        }

        if (body.StartsWith(commentPrefix, StringComparison.Ordinal))
        {
            author = string.Empty;
            commentText = body.Substring(commentPrefix.Length);
            return true;
        }

        author = null;
        commentText = null;
        return false;
    }

    private static bool TryReadHeading(string line, out int level, out string? text)
    {
        level = 0;
        text = null;
        string trimmed = line.TrimStart();
        int count = 0;
        while (count < trimmed.Length && trimmed[count] == '#')
        {
            count++;
        }

        if (count is < 1 or > 6 || count >= trimmed.Length || trimmed[count] != ' ')
        {
            return false;
        }

        level = count;
        text = trimmed.Substring(count + 1).Trim();
        return true;
    }

    private static bool TryReadListItem(string line, out string? text)
    {
        string trimmed = line.TrimStart();
        if (trimmed.Length < 2 || trimmed[0] != '-' || trimmed[1] != ' ')
        {
            text = null;
            return false;
        }

        text = trimmed.Substring(2);
        return true;
    }

    private static bool TryReadTable(string[] lines, int start, out List<List<string>>? rows, out int consumed)
    {
        rows = null;
        consumed = 0;
        if (start + 1 >= lines.Length ||
            !TryReadTableRow(lines[start], out List<string>? header) ||
            !IsTableSeparator(lines[start + 1], header!.Count))
        {
            return false;
        }

        rows = [header];
        consumed = 2;
        int index = start + 2;
        while (index < lines.Length && TryReadTableRow(lines[index], out List<string>? row))
        {
            while (row!.Count < header.Count)
            {
                row.Add(string.Empty);
            }

            if (row.Count > header.Count)
            {
                row.RemoveRange(header.Count, row.Count - header.Count);
            }

            rows.Add(row);
            consumed++;
            index++;
        }

        return true;
    }

    private static bool TryReadTableRow(string line, out List<string>? cells)
    {
        cells = null;
        string trimmed = line.Trim();
        if (trimmed.Length < 2 || trimmed[0] != '|' || trimmed[trimmed.Length - 1] != '|')
        {
            return false;
        }

        cells = [];
        var cell = new StringBuilder();
        for (int i = 1; i < trimmed.Length - 1; i++)
        {
            char c = trimmed[i];
            if (c == '\\' && i + 1 < trimmed.Length - 1)
            {
                cell.Append(trimmed[++i]);
            }
            else if (c == '|')
            {
                cells.Add(cell.ToString().Trim());
                cell.Clear();
            }
            else
            {
                cell.Append(c);
            }
        }

        cells.Add(cell.ToString().Trim());
        return cells.Count > 0;
    }

    private static bool IsTableSeparator(string line, int columns)
    {
        if (!TryReadTableRow(line, out List<string>? cells) || cells!.Count != columns)
        {
            return false;
        }

        foreach (string cell in cells)
        {
            string trimmed = cell.Trim();
            if (trimmed.Length < 3)
            {
                return false;
            }

            foreach (char c in trimmed)
            {
                if (c != '-' && c != ':')
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool IsNoteDefinition(string line) =>
        TryReadNoteDefinition(line, out _, out _);

    private static bool TryReadNoteDefinition(string line, out string? id, out string? body)
    {
        id = null;
        body = null;
        string trimmed = line.TrimStart();
        if (!trimmed.StartsWith("[^", StringComparison.Ordinal))
        {
            return false;
        }

        int idEnd = trimmed.IndexOf("]:", 2, StringComparison.Ordinal);
        if (idEnd < 0)
        {
            return false;
        }

        id = trimmed.Substring(2, idEnd - 2);
        body = trimmed.Substring(idEnd + 2).TrimStart();
        return id.Length > 0;
    }

    private static bool IsBlank(string line) =>
        string.IsNullOrWhiteSpace(line);

    private static string UnescapeMarkdown(string text)
    {
        if (text.IndexOf('\\') < 0)
        {
            return text;
        }

        var sb = new StringBuilder(text.Length);
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '\\' && i + 1 < text.Length)
            {
                sb.Append(text[++i]);
            }
            else
            {
                sb.Append(text[i]);
            }
        }

        return sb.ToString();
    }

    private readonly struct InlineImportStyle
    {
        public InlineImportStyle(
            bool Bold = false,
            bool Italic = false,
            bool Underline = false,
            bool Strikethrough = false,
            string? FontSize = null,
            string? Color = null)
        {
            this.Bold = Bold;
            this.Italic = Italic;
            this.Underline = Underline;
            this.Strikethrough = Strikethrough;
            this.FontSize = FontSize;
            this.Color = Color;
        }

        public static InlineImportStyle Empty { get; } = new();

        public bool Bold { get; }

        public bool Italic { get; }

        public bool Underline { get; }

        public bool Strikethrough { get; }

        public string? FontSize { get; }

        public string? Color { get; }
    }
}
