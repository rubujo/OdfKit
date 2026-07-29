using OdfKit.DOM;

namespace OdfKit.Spreadsheet;

/// <summary>
/// Configures formatting for a single rich-text run.
/// 設定單一富文字片段的格式。
/// </summary>
/// <remarks>
/// Prefer this options object over multi-optional parameter lists for new call sites.
/// 新呼叫端請優先使用此 options 物件，避免多個尾端可選參數。
/// </remarks>
public sealed class OdfRichTextRunOptions
{
    /// <summary>
    /// Gets the default formatting options (no emphasis).
    /// 取得預設格式選項（無強調樣式）。
    /// </summary>
    public static OdfRichTextRunOptions Default { get; } = new();

    /// <summary>
    /// Gets or sets whether the run is bold.
    /// 取得或設定是否套用粗體。
    /// </summary>
    public bool Bold { get; set; }

    /// <summary>
    /// Gets or sets whether the run is italic.
    /// 取得或設定是否套用斜體。
    /// </summary>
    public bool Italic { get; set; }

    /// <summary>
    /// Gets or sets whether the run is underlined.
    /// 取得或設定是否套用底線。
    /// </summary>
    public bool Underline { get; set; }

    /// <summary>
    /// Gets or sets the text color; <see langword="null"/> inherits the default color.
    /// 取得或設定文字色彩；<see langword="null"/> 表示繼承預設色彩。
    /// </summary>
    public OdfColor? Color { get; set; }

    /// <summary>
    /// Gets or sets the font family name; <see langword="null"/> indicates inheritance.
    /// 取得或設定字型家族名稱；<see langword="null"/> 表示繼承。
    /// </summary>
    public string? FontFamily { get; set; }

    /// <summary>
    /// Gets or sets the font size in points; <see langword="null"/> indicates inheritance.
    /// 取得或設定以點為單位的字型大小；<see langword="null"/> 表示繼承。
    /// </summary>
    public double? FontSizePoints { get; set; }
}
