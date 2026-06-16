using System;

namespace OdfKit.Formula;

/// <summary>
/// 代表公式中的一個語彙基元。
/// </summary>
/// <param name="type">語彙基元型別</param>
/// <param name="value">語彙基元內容</param>
/// <param name="startIndex">起始索引位置</param>
public class FormulaToken(TokenType type, string value, int startIndex)
{
    /// <summary>
    /// 取得語彙基元的型別。
    /// </summary>
    public TokenType Type { get; } = type;

    /// <summary>
    /// 取得語彙基元的字串值。
    /// </summary>
    public string Value { get; } = value;

    /// <summary>
    /// 取得語彙基元在原始公式字串中的起始索引。
    /// </summary>
    public int StartIndex { get; } = startIndex;
}

