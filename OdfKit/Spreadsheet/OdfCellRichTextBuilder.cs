using System;
using OdfKit.DOM;

namespace OdfKit.Spreadsheet;

/// <summary>
/// Builds rich text runs for spreadsheet cells with a fluent API.
/// 提供儲存格富文字的鏈式建構 API。
/// </summary>
public sealed class OdfCellRichTextBuilder
{
    private readonly OdfCell _cell;
    private readonly OdfRichText _richText;

    internal OdfCellRichTextBuilder(OdfCell cell)
    {
        _cell = cell;
        _richText = cell.GetRichText() ?? new OdfRichText();
    }

    /// <summary>
    /// Clears the current rich text content of the cell.
    /// 清除目前儲存格富文字內容。
    /// </summary>
    /// <returns>The current builder for chaining. / 目前建構器，方便鏈式呼叫。</returns>
    public OdfCellRichTextBuilder Clear()
    {
        _richText.Clear();
        Commit();
        return this;
    }

    /// <summary>
    /// Appends a plain text run with default formatting.
    /// 以預設格式追加一段文字。
    /// </summary>
    /// <param name="text">The text to append. / 要追加的文字。</param>
    /// <returns>The current builder for chaining. / 目前建構器，方便鏈式呼叫。</returns>
    public OdfCellRichTextBuilder Append(string text) => Append(text, OdfRichTextRunOptions.Default);

    /// <summary>
    /// Appends a text run using an options object.
    /// 以 options 物件追加一段文字。
    /// </summary>
    /// <param name="text">The text to append. / 要追加的文字。</param>
    /// <param name="options">The run formatting options. / 片段格式選項。</param>
    /// <returns>The current builder for chaining. / 目前建構器，方便鏈式呼叫。</returns>
    public OdfCellRichTextBuilder Append(string text, OdfRichTextRunOptions options)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(text, nameof(text));
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(options, nameof(options));

        _richText.AddRun(text, options);
        Commit();
        return this;
    }

    /// <summary>
    /// Appends a line break.
    /// 追加一個換行。
    /// </summary>
    /// <returns>The current builder for chaining. / 目前建構器，方便鏈式呼叫。</returns>
    public OdfCellRichTextBuilder LineBreak()
    {
        _richText.AddLineBreak();
        Commit();
        return this;
    }

    /// <summary>
    /// Replaces the current cell text with the specified rich text content.
    /// 以指定富文字內容取代目前儲存格文字。
    /// </summary>
    /// <param name="richText">The new rich text content. / 新的富文字內容。</param>
    /// <returns>The current builder for chaining. / 目前建構器，方便鏈式呼叫。</returns>
    public OdfCellRichTextBuilder Set(OdfRichText richText)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(richText, nameof(richText));

        _richText.Clear();
        foreach (OdfRichTextRun run in richText.Runs)
        {
            _richText.AddRun(
                run.Text,
                new OdfRichTextRunOptions
                {
                    Bold = run.Bold,
                    Italic = run.Italic,
                    Color = run.Color,
                    FontFamily = run.FontFamily,
                    Underline = run.Underline,
                });
        }

        Commit();
        return this;
    }

    private void Commit() => _cell.SetRichText(_richText);
}
