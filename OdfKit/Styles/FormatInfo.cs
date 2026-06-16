using System;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Styles;

/// <summary>
/// 儲存格式化資訊的類別。
/// </summary>
public class FormatInfo
{
    /// <summary>
    /// 取得或設定格式類型。
    /// </summary>
    public FormatType Type { get; set; } = FormatType.Number;

    /// <summary>
    /// 取得或設定小數位數。
    /// </summary>
    public int DecimalPlaces { get; set; }

    /// <summary>
    /// 取得或設定最小整數位數。
    /// </summary>
    public int MinIntegerDigits { get; set; } = 1;

    /// <summary>
    /// 取得或設定一個值，指出是否使用千分位分組。
    /// </summary>
    public bool Grouping { get; set; }

    /// <summary>
    /// 取得或設定貨幣符號。
    /// </summary>
    public string CurrencySymbol { get; set; } = "$";

    /// <summary>
    /// 取得或設定日期時間格式的語彙基元清單。
    /// </summary>
    public List<DateTimeToken> DateTimeTokens { get; set; } = [];
}
