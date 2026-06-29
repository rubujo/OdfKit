using System;

namespace OdfKit.Formula;

/// <summary>
/// Represents lexical token types used by the formula tokenizer.
/// 代表公式語彙分析器使用的語彙基元型別。
/// </summary>
public enum TokenType
{
    /// <summary>
    /// An identifier, such as a function name or named range.
    /// 識別碼，例如函式名稱、具名範圍。
    /// </summary>
    Identifier,

    /// <summary>
    /// A cell reference.
    /// 儲存格參照。
    /// </summary>
    CellReference,

    /// <summary>
    /// A string literal.
    /// 字串常值。
    /// </summary>
    StringLiteral,

    /// <summary>
    /// A numeric literal.
    /// 數字。
    /// </summary>
    Number,

    /// <summary>
    /// An operator.
    /// 運算子。
    /// </summary>
    Operator,

    /// <summary>
    /// A separator, such as "," or ";".
    /// 分隔符號，即 「,」 或 「;」。
    /// </summary>
    Separator,

    /// <summary>
    /// An opening parenthesis.
    /// 左括號。
    /// </summary>
    OpenParenthesis,

    /// <summary>
    /// A closing parenthesis.
    /// 右括號。
    /// </summary>
    CloseParenthesis,

    /// <summary>
    /// An opening brace.
    /// 左大括號。
    /// </summary>
    OpenBrace,

    /// <summary>
    /// A closing brace.
    /// 右大括號。
    /// </summary>
    CloseBrace,

    /// <summary>
    /// Whitespace.
    /// 空白字元。
    /// </summary>
    Whitespace,

    /// <summary>
    /// An unknown token type.
    /// 未知型別。
    /// </summary>
    Unknown
}

