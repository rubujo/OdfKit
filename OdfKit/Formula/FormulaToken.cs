using System;

namespace OdfKit.Formula;

/// <summary>
/// Represents a lexical token in a formula.
/// 代表公式中的一個語彙基元。
/// </summary>
/// <param name="type">The token type. / 語彙基元型別。</param>
/// <param name="value">The token content. / 語彙基元內容。</param>
/// <param name="startIndex">The starting index in the original formula string. / 原始公式字串中的起始索引位置。</param>
public class FormulaToken(TokenType type, string value, int startIndex)
{
    /// <summary>
    /// Gets the token type.
    /// 取得語彙基元的型別。
    /// </summary>
    public TokenType Type { get; } = type;

    /// <summary>
    /// Gets the token string value.
    /// 取得語彙基元的字串值。
    /// </summary>
    public string Value { get; } = value;

    /// <summary>
    /// Gets the starting index of the token in the original formula string.
    /// 取得語彙基元在原始公式字串中的起始索引。
    /// </summary>
    public int StartIndex { get; } = startIndex;
}

