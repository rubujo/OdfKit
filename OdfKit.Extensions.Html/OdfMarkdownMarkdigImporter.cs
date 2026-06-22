using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Markdig;
using Markdig.Extensions.EmphasisExtras;
using Markdig.Extensions.Footnotes;
using Markdig.Extensions.Tables;
using Markdig.Extensions.TaskLists;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using OdfKit.Styles;
using OdfKit.Text;

namespace OdfKit.Export;

internal static class OdfMarkdownMarkdigImporter
{
    internal static bool TryImport(string markdown, OdfMarkdownImportOptions options, out TextDocument? document)
    {
        document = null;
        if (markdown.IndexOf("<!--", StringComparison.Ordinal) >= 0 || markdown.StartsWith("---", StringComparison.Ordinal))
        {
            return false;
        }

        if (options.AcceptInlineHtml && markdown.IndexOf("<span", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return false;
        }

        MarkdownPipeline pipeline = CreatePipeline(options);
        MarkdownDocument markdownDocument = Markdown.Parse(markdown, pipeline);
        document = TextDocument.Create();
        var context = new ImportContext(document, options);

        foreach (Block block in markdownDocument)
        {
            AppendBlock(context, block);
        }

        return true;
    }

    private static MarkdownPipeline CreatePipeline(OdfMarkdownImportOptions options)
    {
        var builder = new MarkdownPipelineBuilder();
        if (options.Flavor is OdfMarkdownFlavor.GitHubFlavored or OdfMarkdownFlavor.GitLabFlavored)
        {
            builder.UsePipeTables()
                .UseFootnotes()
                .UseEmphasisExtras(EmphasisExtraOptions.Strikethrough)
                .UseTaskLists();
        }

        return builder.Build();
    }

    private static void AppendBlock(ImportContext context, Block block)
    {
        switch (block)
        {
            case HeadingBlock heading:
                OdfParagraph headingParagraph = context.Document.AddHeading(string.Empty, Math.Max(1, Math.Min(6, heading.Level)));
                AppendInlines(context, headingParagraph, heading.Inline, InlineImportStyle.Empty);
                break;
            case ParagraphBlock paragraph:
                OdfParagraph odfParagraph = context.Document.AddParagraph();
                AppendInlines(context, odfParagraph, paragraph.Inline, InlineImportStyle.Empty);
                break;
            case ListBlock list:
                AppendList(context, list);
                break;
            case Table table:
                AppendTable(context, table);
                break;
            case FootnoteGroup:
            case ThematicBreakBlock:
                break;
            case ContainerBlock container:
                foreach (Block child in container)
                {
                    AppendBlock(context, child);
                }
                break;
        }
    }

    private static void AppendList(ImportContext context, ListBlock listBlock)
    {
        OdfList list = context.Document.AddList();
        foreach (Block itemBlock in listBlock)
        {
            if (itemBlock is not ListItemBlock item)
            {
                continue;
            }

            OdfListItem listItem = list.AddListItem();
            bool wroteParagraph = false;
            foreach (Block child in item)
            {
                if (child is ParagraphBlock paragraph)
                {
                    OdfParagraph odfParagraph = listItem.AddParagraph();
                    AppendInlines(context, odfParagraph, paragraph.Inline, InlineImportStyle.Empty);
                    wroteParagraph = true;
                }
                else if (child is ListBlock nested)
                {
                    AppendNestedList(context, listItem, nested);
                }
            }

            if (!wroteParagraph)
            {
                listItem.AddParagraph();
            }
        }
    }

    private static void AppendNestedList(ImportContext context, OdfListItem parent, ListBlock nestedBlock)
    {
        OdfList nested = parent.AddNestedList();
        foreach (Block itemBlock in nestedBlock)
        {
            if (itemBlock is not ListItemBlock item)
            {
                continue;
            }

            OdfListItem nestedItem = nested.AddListItem();
            foreach (Block child in item)
            {
                if (child is ParagraphBlock paragraph)
                {
                    OdfParagraph odfParagraph = nestedItem.AddParagraph();
                    AppendInlines(context, odfParagraph, paragraph.Inline, InlineImportStyle.Empty);
                }
            }
        }
    }

    private static void AppendTable(ImportContext context, Table tableBlock)
    {
        var rows = new List<List<string>>();
        foreach (Block rowBlock in tableBlock)
        {
            if (rowBlock is not TableRow row)
            {
                continue;
            }

            var cells = new List<string>();
            foreach (Block cellBlock in row)
            {
                if (cellBlock is TableCell cell)
                {
                    cells.Add(ExtractPlainText(cell).Trim());
                }
            }

            if (cells.Count > 0)
            {
                rows.Add(cells);
            }
        }

        if (rows.Count == 0)
        {
            return;
        }

        int columns = 0;
        foreach (List<string> row in rows)
        {
            columns = Math.Max(columns, row.Count);
        }

        OdfTable table = context.Document.AddTable(rows.Count, columns);
        for (int row = 0; row < rows.Count; row++)
        {
            for (int column = 0; column < rows[row].Count; column++)
            {
                table.GetCell(row, column).AddParagraph(rows[row][column]);
            }
        }
    }

    private static void AppendInlines(ImportContext context, OdfParagraph paragraph, ContainerInline? container, InlineImportStyle style)
    {
        if (container is null)
        {
            return;
        }

        bool trimTaskListSeparator = false;
        for (Inline? inline = container.FirstChild; inline is not null; inline = inline.NextSibling)
        {
            if (inline is TaskList)
            {
                trimTaskListSeparator = true;
                continue;
            }

            if (trimTaskListSeparator && inline is LiteralInline literal)
            {
                string text = literal.Content.ToString();
                AppendText(paragraph, text.Length > 0 && text[0] == ' ' ? text.Substring(1) : text, style);
                trimTaskListSeparator = false;
                continue;
            }

            trimTaskListSeparator = false;
            AppendInline(context, paragraph, inline, style);
        }
    }

    private static void AppendInline(ImportContext context, OdfParagraph paragraph, Inline inline, InlineImportStyle style)
    {
        switch (inline)
        {
            case LiteralInline literal:
                AppendText(paragraph, literal.Content.ToString(), style);
                break;
            case LineBreakInline lineBreak:
                if (lineBreak.IsHard)
                {
                    paragraph.AddLineBreak();
                }
                else
                {
                    AppendText(paragraph, " ", style);
                }

                break;
            case EmphasisInline emphasis:
                AppendInlines(context, paragraph, emphasis, MergeEmphasisStyle(style, emphasis));
                break;
            case LinkInline link when link.IsImage:
                AppendImage(paragraph, link);
                break;
            case LinkInline link:
                paragraph.AddHyperlink(link.GetDynamicUrl?.Invoke() ?? link.Url ?? string.Empty, ExtractPlainText(link));
                break;
            case FootnoteLink footnoteLink when !footnoteLink.IsBackLink:
                AppendFootnote(context, paragraph, footnoteLink);
                break;
            case HtmlEntityInline entity:
                AppendText(paragraph, entity.Transcoded.ToString(), style);
                break;
            case HtmlInline:
            case TaskList:
                break;
            case ContainerInline childContainer:
                AppendInlines(context, paragraph, childContainer, style);
                break;
        }
    }

    private static InlineImportStyle MergeEmphasisStyle(InlineImportStyle style, EmphasisInline emphasis)
    {
        if (emphasis.DelimiterChar == '~')
        {
            return style with { Strikethrough = true };
        }

        return emphasis.DelimiterCount >= 2
            ? style with { Bold = true }
            : style with { Italic = true };
    }

    private static void AppendImage(OdfParagraph paragraph, LinkInline link)
    {
        string altText = ExtractPlainText(link);
        if (string.IsNullOrWhiteSpace(altText))
        {
            altText = "image";
        }

        OdfImage image = paragraph.AddImage(link.GetDynamicUrl?.Invoke() ?? link.Url ?? string.Empty, OdfLength.FromCentimeters(4), OdfLength.FromCentimeters(3), altText);
        image.AltText = altText;
    }

    private static void AppendFootnote(ImportContext context, OdfParagraph paragraph, FootnoteLink link)
    {
        string id = (link.Footnote.Label ?? link.Index.ToString(CultureInfo.InvariantCulture)).TrimStart('^');
        string body = ExtractPlainText(link.Footnote).Trim();
        if (TryReadComment(body, out string? author, out string? commentText))
        {
            paragraph.AddComment(new OdfComment(author!, commentText!));
        }
        else
        {
            paragraph.AddFootnote(id, body);
        }
    }

    private static void AppendText(OdfParagraph paragraph, string text, InlineImportStyle style)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        OdfTextRun run = paragraph.AddTextRun(text);
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
    }

    private static string ExtractPlainText(MarkdownObject markdownObject)
    {
        var sb = new StringBuilder();
        AppendPlainText(markdownObject, sb);
        return sb.ToString();
    }

    private static void AppendPlainText(MarkdownObject markdownObject, StringBuilder sb)
    {
        switch (markdownObject)
        {
            case LiteralInline literal:
                sb.Append(literal.Content.ToString());
                break;
            case LineBreakInline:
                sb.Append(' ');
                break;
            case HtmlEntityInline entity:
                sb.Append(entity.Transcoded.ToString());
                break;
            case HtmlInline:
                break;
            case LeafBlock { Inline: not null } leaf:
                AppendPlainText(leaf.Inline, sb);
                break;
            case ContainerInline container:
                for (Inline? child = container.FirstChild; child is not null; child = child.NextSibling)
                {
                    AppendPlainText(child, sb);
                }

                break;
            case ContainerBlock container:
                bool needsSeparator = false;
                foreach (Block child in container)
                {
                    if (needsSeparator)
                    {
                        sb.Append(' ');
                    }

                    AppendPlainText(child, sb);
                    needsSeparator = true;
                }

                break;
        }
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

    private sealed class ImportContext
    {
        public ImportContext(TextDocument document, OdfMarkdownImportOptions options)
        {
            Document = document;
            Options = options;
        }

        public TextDocument Document { get; }

        public OdfMarkdownImportOptions Options { get; }
    }

    private readonly record struct InlineImportStyle(
        bool Bold = false,
        bool Italic = false,
        bool Underline = false,
        bool Strikethrough = false)
    {
        public static InlineImportStyle Empty { get; } = new();
    }
}
