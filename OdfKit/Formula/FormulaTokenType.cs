using System;

namespace OdfKit.Formula;

/// <summary>
/// Represents token types produced by the formula parser.
/// 代表公式剖析器產生的語彙基元型別。
/// </summary>
public enum FormulaTokenType
{
    /// <summary>
    /// A numeric token.
    /// 數字。
    /// </summary>
    Number,

    /// <summary>
    /// A string token.
    /// 字串。
    /// </summary>
    String,

    /// <summary>
    /// A Boolean token.
    /// 布林值。
    /// </summary>
    Bool,

    /// <summary>
    /// An identifier, such as a function name, cell reference, or range name.
    /// 識別碼（例如函式名稱、儲存格或範圍名稱）。
    /// </summary>
    Identifier,

    /// <summary>
    /// An operator, such as +, -, *, /, ^, &amp;, =, &lt;, &gt;, &lt;=, &gt;=, or &lt;&gt;.
    /// 運算子，例如 +, -, *, /, ^, &amp;, =, &lt;, &gt;, &lt;=, &gt;=, &lt;&gt; 等。
    /// </summary>
    Operator,

    /// <summary>
    /// An opening parenthesis.
    /// 左括號 (。
    /// </summary>
    OpenParen,

    /// <summary>
    /// A closing parenthesis.
    /// 右括號 )。
    /// </summary>
    CloseParen,

    /// <summary>
    /// A separator, such as "," or ";".
    /// 分隔符號，即 「,」 或 「;」。
    /// </summary>
    Separator,

    /// <summary>
    /// A colon token.
    /// 冒號 :。
    /// </summary>
    Colon,

    /// <summary>
    /// The end of the formula.
    /// 公式結尾。
    /// </summary>
    EndOfFormula
}
