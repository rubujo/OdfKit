using System;

namespace OdfKit.Formula;

/// <summary>
/// 代表公式剖析器的語彙基元型別。
/// </summary>
public enum FormulaTokenType
{
    /// <summary>
    /// 數字。
    /// </summary>
    Number,

    /// <summary>
    /// 字串。
    /// </summary>
    String,

    /// <summary>
    /// 布林值。
    /// </summary>
    Bool,

    /// <summary>
    /// 識別碼（例如函式名稱、儲存格或範圍名稱）。
    /// </summary>
    Identifier,

    /// <summary>
    /// 運算子，例如 +, -, *, /, ^, &amp;, =, &lt;, &gt;, &lt;=, &gt;=, &lt;&gt; 等。
    /// </summary>
    Operator,

    /// <summary>
    /// 左括號 (。
    /// </summary>
    OpenParen,

    /// <summary>
    /// 右括號 )。
    /// </summary>
    CloseParen,

    /// <summary>
    /// 分隔符號，即 「,」 或 「;」。
    /// </summary>
    Separator,

    /// <summary>
    /// 冒號 :。
    /// </summary>
    Colon,

    /// <summary>
    /// 公式結尾。
    /// </summary>
    EndOfFormula
}
