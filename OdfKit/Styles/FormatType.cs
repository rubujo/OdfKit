using System;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Styles;

/// <summary>
/// 表示格式類型的列舉。
/// </summary>
public enum FormatType
{
    /// <summary>
    /// 數值格式。
    /// </summary>
    Number,

    /// <summary>
    /// 貨幣格式。
    /// </summary>
    Currency,

    /// <summary>
    /// 百分比格式。
    /// </summary>
    Percentage,

    /// <summary>
    /// 日期格式。
    /// </summary>
    Date,

    /// <summary>
    /// 時間格式。
    /// </summary>
    Time
}
