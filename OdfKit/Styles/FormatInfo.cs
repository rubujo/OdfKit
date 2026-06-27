using System;
using System.Collections.Generic;

namespace OdfKit.Styles;

/// <summary>
/// 儲存格式化資訊的唯讀值型別。
/// </summary>
public readonly struct FormatInfo
{
    private static readonly DateTimeToken[] EmptyDateTimeTokens = [];
    private readonly DateTimeToken[]? _dateTimeTokens;

    /// <summary>
    /// 初始化 <see cref="FormatInfo"/> 結構的新執行個體。
    /// </summary>
    /// <param name="type">格式類型</param>
    /// <param name="decimalPlaces">小數位數</param>
    /// <param name="minIntegerDigits">最小整數位數</param>
    /// <param name="grouping">指出是否使用千分位分組</param>
    /// <param name="currencySymbol">貨幣符號</param>
    /// <param name="dateTimeTokens">日期時間格式的語彙基元集合</param>
    public FormatInfo(
        FormatType type = FormatType.Number,
        int decimalPlaces = 0,
        int minIntegerDigits = 1,
        bool grouping = false,
        string? currencySymbol = null,
        IReadOnlyList<DateTimeToken>? dateTimeTokens = null)
    {
        Type = type;
        DecimalPlaces = decimalPlaces;
        MinIntegerDigits = Math.Max(1, minIntegerDigits);
        Grouping = grouping;
        CurrencySymbol = string.IsNullOrEmpty(currencySymbol) ? "$" : currencySymbol!;

        if (dateTimeTokens is null || dateTimeTokens.Count == 0)
        {
            _dateTimeTokens = null;
        }
        else
        {
            _dateTimeTokens = new DateTimeToken[dateTimeTokens.Count];
            for (int i = 0; i < dateTimeTokens.Count; i++)
            {
                _dateTimeTokens[i] = dateTimeTokens[i];
            }
        }
    }

    /// <summary>
    /// 取得格式類型。
    /// </summary>
    public FormatType Type { get; }

    /// <summary>
    /// 取得小數位數。
    /// </summary>
    public int DecimalPlaces { get; }

    /// <summary>
    /// 取得最小整數位數。
    /// </summary>
    public int MinIntegerDigits { get; }

    /// <summary>
    /// 取得一個值，指出是否使用千分位分組。
    /// </summary>
    public bool Grouping { get; }

    /// <summary>
    /// 取得貨幣符號。
    /// </summary>
    public string CurrencySymbol { get; }

    /// <summary>
    /// 取得日期時間格式的語彙基元集合。
    /// </summary>
    public IReadOnlyList<DateTimeToken> DateTimeTokens => _dateTimeTokens ?? EmptyDateTimeTokens;
}
