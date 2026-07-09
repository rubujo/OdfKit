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
    /// Short overload of FormatInfo that uses default values for all optional parameters and forwards to the full overload.
    /// 便利多載：FormatInfo 的所有可選參數使用預設值並轉呼叫最長多載。
    /// </summary>
    public FormatInfo() : this(FormatType.Number, 0, 1, false, null, null) { }

    /// <summary>
    /// Short overload of FormatInfo that accepts type; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 type；其餘可選參數使用預設值並轉呼叫最長 FormatInfo 多載。
    /// </summary>
    public FormatInfo(FormatType type) : this(type, 0, 1, false, null, null) { }

    /// <summary>
    /// Short overload of FormatInfo that accepts type and decimalPlaces; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 type 與 decimalPlaces；其餘可選參數使用預設值並轉呼叫最長 FormatInfo 多載。
    /// </summary>
    public FormatInfo(FormatType type, int decimalPlaces) : this(type, decimalPlaces, 1, false, null, null) { }

    /// <summary>
    /// Short overload of FormatInfo that accepts type, decimalPlaces, and minIntegerDigits; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 type、decimalPlaces 與 minIntegerDigits；其餘可選參數使用預設值並轉呼叫最長 FormatInfo 多載。
    /// </summary>
    public FormatInfo(FormatType type, int decimalPlaces, int minIntegerDigits) : this(type, decimalPlaces, minIntegerDigits, false, null, null) { }

    /// <summary>
    /// Short overload of FormatInfo that accepts type, decimalPlaces, minIntegerDigits, and grouping; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 type、decimalPlaces、minIntegerDigits 與 grouping；其餘可選參數使用預設值並轉呼叫最長 FormatInfo 多載。
    /// </summary>
    public FormatInfo(FormatType type, int decimalPlaces, int minIntegerDigits, bool grouping) : this(type, decimalPlaces, minIntegerDigits, grouping, null, null) { }

    /// <summary>
    /// Short overload of FormatInfo that accepts type, decimalPlaces, minIntegerDigits, grouping, and currencySymbol; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 type、decimalPlaces、minIntegerDigits、grouping 與 currencySymbol；其餘可選參數使用預設值並轉呼叫最長 FormatInfo 多載。
    /// </summary>
    public FormatInfo(FormatType type, int decimalPlaces, int minIntegerDigits, bool grouping, string? currencySymbol) : this(type, decimalPlaces, minIntegerDigits, grouping, currencySymbol, null) { }


    /// <summary>
    /// Formats info.
    /// 初始化 <see cref="FormatInfo"/> 結構的新執行個體。
    /// </summary>
    /// <param name="type">格式類型</param>
    /// <param name="decimalPlaces">小數位數</param>
    /// <param name="minIntegerDigits">最小整數位數</param>
    /// <param name="grouping">指出是否使用千分位分組</param>
    /// <param name="currencySymbol">貨幣符號</param>
    /// <param name="dateTimeTokens">日期時間格式的語彙基元集合</param>
    public FormatInfo(FormatType type, int decimalPlaces, int minIntegerDigits, bool grouping, string? currencySymbol, IReadOnlyList<DateTimeToken>? dateTimeTokens)
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
    /// Gets the Type value.
    /// 取得格式類型。
    /// </summary>
    public FormatType Type { get; }

    /// <summary>
    /// Gets the DecimalPlaces value.
    /// 取得小數位數。
    /// </summary>
    public int DecimalPlaces { get; }

    /// <summary>
    /// Gets the MinIntegerDigits value.
    /// 取得最小整數位數。
    /// </summary>
    public int MinIntegerDigits { get; }

    /// <summary>
    /// Gets a value indicating the Grouping state.
    /// 取得一個值，指出是否使用千分位分組。
    /// </summary>
    public bool Grouping { get; }

    /// <summary>
    /// Gets the CurrencySymbol value.
    /// 取得貨幣符號。
    /// </summary>
    public string CurrencySymbol { get; }

    /// <summary>
    /// Provides the DateTimeTokens member.
    /// 取得日期時間格式的語彙基元集合。
    /// </summary>
    public IReadOnlyList<DateTimeToken> DateTimeTokens => _dateTimeTokens ?? EmptyDateTimeTokens;
}
