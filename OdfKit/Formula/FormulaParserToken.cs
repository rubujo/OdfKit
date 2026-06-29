using System;

namespace OdfKit.Formula;

/// <summary>
/// Represents a token consumed by the formula parser.
/// 代表公式剖析器使用的語彙基元。
/// </summary>
/// <param name="type">The token type. / 語彙基元型別。</param>
/// <param name="span">The character span for the token. / 語彙基元的字元範圍。</param>
/// <param name="numValue">The numeric value. / 數值。</param>
/// <param name="boolValue">The Boolean value. / 布林值。</param>
public readonly ref struct FormulaParserToken(FormulaTokenType type, ReadOnlySpan<char> span, double numValue = 0, bool boolValue = false)
{
    /// <summary>
    /// Gets the token type.
    /// 取得語彙基元的型別。
    /// </summary>
    public FormulaTokenType Type { get; } = type;

    /// <summary>
    /// Gets the character span for the token.
    /// 取得語彙基元的字元範圍。
    /// </summary>
    public ReadOnlySpan<char> Span { get; } = span;

    /// <summary>
    /// Gets the double-precision numeric value for the token.
    /// 取得語彙基元的雙倍精確度浮點數數值。
    /// </summary>
    public double NumberValue { get; } = numValue;

    /// <summary>
    /// Gets the Boolean value for the token.
    /// 取得語彙基元的布林值。
    /// </summary>
    public bool BoolValue { get; } = boolValue;
}
