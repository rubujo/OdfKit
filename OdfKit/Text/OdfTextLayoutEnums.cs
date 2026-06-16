using System;
using OdfKit.DOM;

namespace OdfKit.Text;

/// <summary>
/// 表示浮動文字框的文字環繞方式。
/// </summary>
public enum OdfTextWrap
{
    /// <summary>
    /// 不環繞。
    /// </summary>
    None,

    /// <summary>
    /// 平行環繞。
    /// </summary>
    Parallel,

    /// <summary>
    /// 只允許左側環繞。
    /// </summary>
    Left,

    /// <summary>
    /// 只允許右側環繞。
    /// </summary>
    Right,

    /// <summary>
    /// 文字穿越物件。
    /// </summary>
    Through
}

/// <summary>
/// 表示浮動物件的錨定類型。
/// </summary>
public enum OdfAnchorType
{
    /// <summary>
    /// 錨定到頁面。
    /// </summary>
    Page,

    /// <summary>
    /// 錨定到段落。
    /// </summary>
    Paragraph,

    /// <summary>
    /// 錨定到字元。
    /// </summary>
    Character,

    /// <summary>
    /// 視為字元。
    /// </summary>
    AsChar
}
