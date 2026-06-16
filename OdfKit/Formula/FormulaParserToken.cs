using System;

namespace OdfKit.Formula;

/// <summary>
/// 代表公式剖析器的語彙基元。
/// </summary>
/// <param name="type">語彙基元型別</param>
/// <param name="span">語彙基元的字元範圍</param>
/// <param name="numValue">數值</param>
/// <param name="boolValue">布林值</param>
public readonly ref struct FormulaParserToken(FormulaTokenType type, ReadOnlySpan<char> span, double numValue = 0, bool boolValue = false)
{
    /// <summary>
    /// 取得語彙基元的型別。
    /// </summary>
    public FormulaTokenType Type { get; } = type;

    /// <summary>
    /// 取得語彙基元的字元範圍。
    /// </summary>
    public ReadOnlySpan<char> Span { get; } = span;

    /// <summary>
    /// 取得語彙基元的雙倍精確度浮點數數值。
    /// </summary>
    public double NumberValue { get; } = numValue;

    /// <summary>
    /// 取得語彙基元的布林值。
    /// </summary>
    public bool BoolValue { get; } = boolValue;
}
