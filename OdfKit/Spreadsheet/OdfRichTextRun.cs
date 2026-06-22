using System;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.Spreadsheet;

/// <summary>
/// 表示 ODS 儲存格富文字中的一個格式片段。
/// </summary>
public sealed class OdfRichTextRun
{
    /// <summary>
    /// 片段的純文字
    /// </summary>
    public string Text { get; init; } = string.Empty;
    /// <summary>
    /// 是否粗體
    /// </summary>
    public bool Bold { get; init; }
    /// <summary>
    /// 是否斜體
    /// </summary>
    public bool Italic { get; init; }
    /// <summary>
    /// 是否底線
    /// </summary>
    public bool Underline { get; init; }
    /// <summary>
    /// 文字色彩；null 表示繼承預設色彩
    /// </summary>
    public OdfColor? Color { get; init; }
    /// <summary>
    /// 字型名稱；null 表示繼承
    /// </summary>
    public string? FontFamily { get; init; }
}
