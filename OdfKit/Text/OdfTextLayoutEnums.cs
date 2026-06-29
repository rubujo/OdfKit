using System;
using OdfKit.DOM;

namespace OdfKit.Text;

/// <summary>
/// Represents the text wrap mode of a floating text box.
/// 表示浮動文字框的文字環繞方式。
/// </summary>
public enum OdfTextWrap
{
    /// <summary>
    /// No wrapping.
    /// 不環繞。
    /// </summary>
    None,

    /// <summary>
    /// Parallel wrapping.
    /// 平行環繞。
    /// </summary>
    Parallel,

    /// <summary>
    /// Wrapping is allowed only on the left side.
    /// 只允許左側環繞。
    /// </summary>
    Left,

    /// <summary>
    /// Wrapping is allowed only on the right side.
    /// 只允許右側環繞。
    /// </summary>
    Right,

    /// <summary>
    /// Text runs through the object.
    /// 文字穿越物件。
    /// </summary>
    Through
}

/// <summary>
/// Represents the anchor type of a floating object.
/// 表示浮動物件的錨定類型。
/// </summary>
public enum OdfAnchorType
{
    /// <summary>
    /// Anchored to the page.
    /// 錨定到頁面。
    /// </summary>
    Page,

    /// <summary>
    /// Anchored to the paragraph.
    /// 錨定到段落。
    /// </summary>
    Paragraph,

    /// <summary>
    /// Anchored to the character.
    /// 錨定到字元。
    /// </summary>
    Character,

    /// <summary>
    /// Treated as a character.
    /// 視為字元。
    /// </summary>
    AsChar
}
