using System;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Styles;

/// <summary>
/// 表示日期時間格式化語彙基元的唯讀值型別。
/// </summary>
/// <param name="token">格式化語彙基元字串</param>
/// <param name="isLiteral">指出該語彙基元是否為字面值</param>
public readonly struct DateTimeToken(string token, bool isLiteral)
{
    /// <summary>
    /// 取得格式化語彙基元字串。
    /// </summary>
    public string Token { get; } = token;

    /// <summary>
    /// 取得一個值，指出該語彙基元是否為字面值。
    /// </summary>
    public bool IsLiteral { get; } = isLiteral;
}
