using System;
using System.Collections.Generic;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.Spreadsheet;

/// <summary>
/// 代表 ODS 儲存格的富文字內容，由多個 <see cref="OdfRichTextRun"/> 組成。
/// </summary>
public sealed class OdfRichText
{
    private readonly List<OdfRichTextRun> _runs = new();

    /// <summary>
    /// 取得所有格式片段
    /// </summary>
    public IReadOnlyList<OdfRichTextRun> Runs => _runs;

    /// <summary>
    /// 清除所有格式片段。
    /// </summary>
    public void Clear() => _runs.Clear();

    /// <summary>
    /// 新增一個格式片段。
    /// </summary>
    /// <param name="text">片段文字</param>
    /// <param name="bold">是否套用粗體</param>
    /// <param name="italic">是否套用斜體</param>
    /// <param name="color">文字色彩；null 表示繼承預設色彩</param>
    /// <param name="fontFamily">字型名稱；null 表示繼承</param>
    /// <param name="underline">是否套用底線</param>
    /// <returns>目前富文字物件，方便鏈式呼叫</returns>
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
    /// 新增一個換行片段。
    /// </summary>
    /// <returns>目前富文字物件，方便鏈式呼叫</returns>
    public OdfRichText AddLineBreak()
    {
        _runs.Add(new OdfRichTextRun { Text = "\n" });
        return this;
    }
}
