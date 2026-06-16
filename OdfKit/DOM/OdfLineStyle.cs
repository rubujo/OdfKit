namespace OdfKit.DOM;

/// <summary>
/// 表示 ODF schema 中名為 <c>lineStyle</c> 的線條樣式 token。
/// </summary>
public enum OdfLineStyle
{
    /// <summary>
    /// 無線條。
    /// </summary>
    None,

    /// <summary>
    /// 實線。
    /// </summary>
    Solid,

    /// <summary>
    /// 點線。
    /// </summary>
    Dotted,

    /// <summary>
    /// 虛線。
    /// </summary>
    Dash,

    /// <summary>
    /// 長虛線。
    /// </summary>
    LongDash,

    /// <summary>
    /// 點虛線。
    /// </summary>
    DotDash,

    /// <summary>
    /// 雙點虛線。
    /// </summary>
    DotDotDash,

    /// <summary>
    /// 波浪線。
    /// </summary>
    Wave
}
