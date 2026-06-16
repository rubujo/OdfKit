using System;
using OdfKit.DOM;

namespace OdfKit.Presentation;

/// <summary>
/// 表示預留位置（Placeholder）型態的列舉。
/// </summary>
public enum OdfPlaceholderType
{
    /// <summary>
    /// 標題。
    /// </summary>
    Title,

    /// <summary>
    /// 副標題。
    /// </summary>
    Subtitle,

    /// <summary>
    /// 大綱。
    /// </summary>
    Outline,

    /// <summary>
    /// 文字。
    /// </summary>
    Text,

    /// <summary>
    /// 圖形。
    /// </summary>
    Graphic,

    /// <summary>
    /// 物件。
    /// </summary>
    Object,

    /// <summary>
    /// 圖表。
    /// </summary>
    Chart,

    /// <summary>
    /// 表格。
    /// </summary>
    Table,

    /// <summary>
    /// 組織圖。
    /// </summary>
    Orgchart,

    /// <summary>
    /// 頁碼。
    /// </summary>
    PageNumber,

    /// <summary>
    /// 頁首。
    /// </summary>
    Header,

    /// <summary>
    /// 頁尾。
    /// </summary>
    Footer,

    /// <summary>
    /// 日期與時間。
    /// </summary>
    DateTime,

    /// <summary>
    /// 備忘錄。
    /// </summary>
    Notes,

    /// <summary>
    /// 講義。
    /// </summary>
    Handout
}
