using System;
using OdfKit.DOM;

namespace OdfKit.Presentation;

/// <summary>
/// Represents placeholder types.
/// 表示預留位置（Placeholder）型態的列舉。
/// </summary>
public enum OdfPlaceholderType
{
    /// <summary>
    /// A title placeholder.
    /// 標題。
    /// </summary>
    Title,

    /// <summary>
    /// A subtitle placeholder.
    /// 副標題。
    /// </summary>
    Subtitle,

    /// <summary>
    /// An outline placeholder.
    /// 大綱。
    /// </summary>
    Outline,

    /// <summary>
    /// A text placeholder.
    /// 文字。
    /// </summary>
    Text,

    /// <summary>
    /// A graphic placeholder.
    /// 圖形。
    /// </summary>
    Graphic,

    /// <summary>
    /// An object placeholder.
    /// 物件。
    /// </summary>
    Object,

    /// <summary>
    /// A chart placeholder.
    /// 圖表。
    /// </summary>
    Chart,

    /// <summary>
    /// A table placeholder.
    /// 表格。
    /// </summary>
    Table,

    /// <summary>
    /// An organization chart placeholder.
    /// 組織圖。
    /// </summary>
    Orgchart,

    /// <summary>
    /// A page number placeholder.
    /// 頁碼。
    /// </summary>
    PageNumber,

    /// <summary>
    /// A header placeholder.
    /// 頁首。
    /// </summary>
    Header,

    /// <summary>
    /// A footer placeholder.
    /// 頁尾。
    /// </summary>
    Footer,

    /// <summary>
    /// A date and time placeholder.
    /// 日期與時間。
    /// </summary>
    DateTime,

    /// <summary>
    /// A notes placeholder.
    /// 備忘錄。
    /// </summary>
    Notes,

    /// <summary>
    /// A handout placeholder.
    /// 講義。
    /// </summary>
    Handout
}
