namespace OdfKit.WebFonts;

/// <summary>
/// Identifies a supported standalone WebFont output format.
/// 識別支援的獨立 WebFont 輸出格式。
/// </summary>
public enum WebFontFormat
{
    /// <summary>
    /// Represents WOFF 2.0.
    /// 代表 WOFF 2.0。
    /// </summary>
    Woff2,

    /// <summary>
    /// Represents WOFF 1.0.
    /// 代表 WOFF 1.0。
    /// </summary>
    Woff,

    /// <summary>
    /// Represents a standalone TrueType sfnt.
    /// 代表獨立的 TrueType sfnt。
    /// </summary>
    TrueType,

    /// <summary>
    /// Represents a standalone OpenType CFF or CFF2 sfnt.
    /// 代表獨立的 OpenType CFF 或 CFF2 sfnt。
    /// </summary>
    OpenType
}
