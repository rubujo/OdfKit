using System;
using OdfKit.DOM;

namespace OdfKit.Spreadsheet;

/// <summary>
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
    /// 清除目前儲存格富文字內容。
    /// </summary>
    /// <returns>目前建構器，方便鏈式呼叫</returns>
    public OdfCellRichTextBuilder Clear()
    {
        _richText.Clear();
        Commit();
        return this;
    }

    /// <summary>
    /// 追加一段文字。
    /// </summary>
    /// <param name="text">要追加的文字</param>
    /// <param name="bold">是否套用粗體</param>
    /// <param name="italic">是否套用斜體</param>
    /// <param name="color">文字色彩；null 表示繼承預設色彩</param>
    /// <param name="fontFamily">字型名稱；null 表示繼承</param>
    /// <param name="underline">是否套用底線</param>
    /// <returns>目前建構器，方便鏈式呼叫</returns>
    public OdfCellRichTextBuilder Append(
        string text,
        bool bold = false,
        bool italic = false,
        OdfColor? color = null,
        string? fontFamily = null,
        bool underline = false)
    {
        if (text is null)
        {
            throw new ArgumentNullException(nameof(text));
        }

        _richText.AddRun(text, bold, italic, color, fontFamily, underline);
        Commit();
        return this;
    }

    /// <summary>
    /// 追加一個換行。
    /// </summary>
    /// <returns>目前建構器，方便鏈式呼叫</returns>
    public OdfCellRichTextBuilder LineBreak()
    {
        _richText.AddLineBreak();
        Commit();
        return this;
    }

    /// <summary>
    /// 以指定富文字內容取代目前儲存格文字。
    /// </summary>
    /// <param name="richText">新的富文字內容</param>
    /// <returns>目前建構器，方便鏈式呼叫</returns>
    public OdfCellRichTextBuilder Set(OdfRichText richText)
    {
        if (richText is null)
        {
            throw new ArgumentNullException(nameof(richText));
        }

        _richText.Clear();
        foreach (OdfRichTextRun run in richText.Runs)
        {
            _richText.AddRun(run.Text, run.Bold, run.Italic, run.Color, run.FontFamily, run.Underline);
        }

        Commit();
        return this;
    }

    private void Commit() => _cell.SetRichText(_richText);
}
