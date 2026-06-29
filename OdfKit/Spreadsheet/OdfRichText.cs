using System;
using System.Collections.Generic;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.Spreadsheet;

/// <summary>
/// Represents rich text content for an ODS cell, composed of multiple <see cref="OdfRichTextRun"/> instances.
/// 代表 ODS 儲存格的富文字內容，由多個 <see cref="OdfRichTextRun"/> 組成。
/// </summary>
public sealed class OdfRichText
{
    private readonly List<OdfRichTextRun> _runs = new();

    /// <summary>
    /// Gets all formatted runs.
    /// 取得所有格式片段。
    /// </summary>
    public IReadOnlyList<OdfRichTextRun> Runs => _runs;

    /// <summary>
    /// Clears all formatted runs.
    /// 清除所有格式片段。
    /// </summary>
    public void Clear() => _runs.Clear();

    /// <summary>
    /// Adds a formatted run.
    /// 新增一個格式片段。
    /// </summary>
    /// <param name="text">The run text. / 片段文字。</param>
    /// <param name="bold">Whether to apply bold styling. / 是否套用粗體。</param>
    /// <param name="italic">Whether to apply italic styling. / 是否套用斜體。</param>
    /// <param name="color">The text color; <see langword="null"/> inherits the default color. / 文字色彩；<see langword="null"/> 表示繼承預設色彩。</param>
    /// <param name="fontFamily">The font family name; <see langword="null"/> indicates inheritance. / 字型名稱；<see langword="null"/> 表示繼承。</param>
    /// <param name="underline">Whether to apply underline styling. / 是否套用底線。</param>
    /// <returns>The current rich text object for chaining. / 目前富文字物件，方便鏈式呼叫。</returns>
    public OdfRichText AddRun(string text, bool bold = false, bool italic = false,
        OdfColor? color = null, string? fontFamily = null, bool underline = false)
    {
        _runs.Add(new OdfRichTextRun
        {
            Text = text,
            Bold = bold,
            Italic = italic,
            Underline = underline,
            Color = color,
            FontFamily = fontFamily,
        });

        return this;
    }

    /// <summary>
    /// Adds a line break run.
    /// 新增一個換行片段。
    /// </summary>
    /// <returns>The current rich text object for chaining. / 目前富文字物件，方便鏈式呼叫。</returns>
    public OdfRichText AddLineBreak()
    {
        _runs.Add(new OdfRichTextRun { Text = "\n" });
        return this;
    }
}
