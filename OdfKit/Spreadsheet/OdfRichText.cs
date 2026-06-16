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

    /// <summary>取得所有格式片段。</summary>
    public IReadOnlyList<OdfRichTextRun> Runs => _runs;

    /// <summary>新增一個格式片段。</summary>
    public void AddRun(string text, bool bold = false, bool italic = false,
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
    }
}
