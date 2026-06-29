using System;
using System.Collections.Generic;
using OdfKit.DOM;
using OdfKit.Text;

namespace OdfKit.Spreadsheet;

public partial class OdfCell
{
    /// <summary>
    /// Parses and appends an HTML inline rich text fragment to the end of the cell text.
    /// 在儲存格文字結尾解析並追加 HTML 行內富文字片段。
    /// </summary>
    /// <param name="html">The HTML inline fragment to parse. / 要解析的 HTML 行內片段。</param>
    /// <returns>The current cell for chaining. / 目前儲存格，方便鏈式呼叫。</returns>
    public OdfCell AppendHtml(string html)
    {
        AppendInlineHtml(html);
        return this;
    }

    /// <summary>
    /// Parses and appends a Markdown inline rich text fragment to the end of the cell text.
    /// 在儲存格文字結尾解析並追加 Markdown 行內富文字片段。
    /// </summary>
    /// <param name="markdown">The Markdown inline text to parse. / 要解析的 Markdown 行內文字。</param>
    /// <returns>The current cell for chaining. / 目前儲存格，方便鏈式呼叫。</returns>
    public OdfCell AppendMarkdown(string markdown)
    {
        AppendInlineHtml(TextDocumentHtmlFragmentEngine.ConvertMarkdownInlineToHtml(markdown));
        return this;
    }

    private void AppendInlineHtml(string html)
    {
        if (html is null)
            throw new ArgumentNullException(nameof(html));

        IReadOnlyList<OdfInlineTextSegment> segments = TextDocumentHtmlFragmentEngine.ParseHtmlSegments(html);
        if (segments.Count == 0)
            return;

        OdfRichText richText = GetRichText() ?? new OdfRichText();
        if (richText.Runs.Count == 0 && !string.IsNullOrEmpty(DisplayText))
            richText.AddRun(DisplayText);

        foreach (OdfInlineTextSegment segment in segments)
        {
            if (segment.IsLineBreak)
            {
                richText.AddLineBreak();
                continue;
            }

            OdfColor? color = null;
            if (segment.Color is not null && OdfColor.TryParse(segment.Color, out OdfColor parsed))
                color = parsed;

            richText.AddRun(segment.Text, segment.Bold, segment.Italic, color, segment.FontFamily, segment.Underline);
        }

        SetRichText(richText);
    }
}
