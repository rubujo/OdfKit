using System;

namespace OdfKit.Formula;

/// <summary>
/// 代表公式語彙基元的型別。
/// </summary>
public enum TokenType
{
    /// <summary>
    /// 識別碼，例如函式名稱、具名範圍。
    /// </summary>
    Identifier,

    /// <summary>
    /// 儲存格參照。
    /// </summary>
    CellReference,

    /// <summary>
    /// 字串常值。
    /// </summary>
    StringLiteral,

    /// <summary>
    /// 數字。
    /// </summary>
    Number,

    /// <summary>
    /// 運算子。
    /// </summary>
    Operator,

    /// <summary>
    /// 分隔符號，即 「,」 或 「;」。
    /// </summary>
    Separator,

    /// <summary>
    /// 左括號。
    /// </summary>
    OpenParenthesis,

    /// <summary>
    /// 右括號。
    /// </summary>
    CloseParenthesis,

    /// <summary>
    /// 左大括號。
    /// </summary>
    OpenBrace,

    /// <summary>
    /// 右大括號。
    /// </summary>
    CloseBrace,

    /// <summary>
    /// 空白字元。
    /// </summary>
    Whitespace,

    /// <summary>
    /// 未知型別。
    /// </summary>
    Unknown
}

