using System;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.Spreadsheet;

/// <summary>
/// Represents one formatted run in ODS cell rich text.
/// 表示 ODS 儲存格富文字中的一個格式片段。
/// </summary>
public sealed class OdfRichTextRun
{
    /// <summary>
    /// Gets the plain text of the run.
    /// 片段的純文字。
    /// </summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether the run is bold.
    /// 是否粗體。
    /// </summary>
    public bool Bold { get; init; }

    /// <summary>
    /// Gets a value indicating whether the run is italic.
    /// 是否斜體。
    /// </summary>
    public bool Italic { get; init; }

    /// <summary>
    /// Gets a value indicating whether the run is underlined.
    /// 是否底線。
    /// </summary>
    public bool Underline { get; init; }

    /// <summary>
    /// Gets the text color; <see langword="null"/> indicates the default color is inherited.
    /// 文字色彩；<see langword="null"/> 表示繼承預設色彩。
    /// </summary>
    public OdfColor? Color { get; init; }

    /// <summary>
    /// Gets the font family name; <see langword="null"/> indicates inheritance.
    /// 字型名稱；<see langword="null"/> 表示繼承。
    /// </summary>
    public string? FontFamily { get; init; }
}
