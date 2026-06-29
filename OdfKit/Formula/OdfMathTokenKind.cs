using System;

namespace OdfKit.Formula;

/// <summary>
/// Represents MathML token kinds that can be written by the high-level formula API.
/// 表示可由高階公式 API 寫入的 MathML token 類型。
/// </summary>
public enum OdfMathTokenKind
{
    /// <summary>
    /// A MathML <c>mi</c> identifier.
    /// MathML <c>mi</c> 識別名稱。
    /// </summary>
    Identifier,

    /// <summary>
    /// A MathML <c>mn</c> number.
    /// MathML <c>mn</c> 數值。
    /// </summary>
    Number,

    /// <summary>
    /// A MathML <c>mo</c> operator.
    /// MathML <c>mo</c> 運算子。
    /// </summary>
    Operator,

    /// <summary>
    /// A MathML <c>mtext</c> text token.
    /// MathML <c>mtext</c> 文字。
    /// </summary>
    Text,

    /// <summary>
    /// A MathML <c>msup</c> superscript token.
    /// MathML <c>msup</c> 上標。
    /// </summary>
    Superscript,

    /// <summary>
    /// A MathML <c>msub</c> subscript token.
    /// MathML <c>msub</c> 下標。
    /// </summary>
    Subscript,

    /// <summary>
    /// A MathML <c>mfrac</c> fraction, where <see cref="OdfMathToken.Base"/> is the numerator and <see cref="OdfMathToken.Script"/> is the denominator.
    /// MathML <c>mfrac</c> 分數，<see cref="OdfMathToken.Base"/> 為分子、<see cref="OdfMathToken.Script"/> 為分母。
    /// </summary>
    Fraction,

    /// <summary>
    /// A MathML <c>msqrt</c> or <c>mroot</c> radical, where <see cref="OdfMathToken.Base"/> is the radicand and <see cref="OdfMathToken.Script"/> is the optional index.
    /// MathML <c>msqrt</c>（無索引）或 <c>mroot</c>（具索引）根號，
    /// <see cref="OdfMathToken.Base"/> 為被開方數、<see cref="OdfMathToken.Script"/> 為選用的根指數。
    /// </summary>
    Radical,

    /// <summary>
    /// A MathML <c>mrow</c> group row whose children are stored in <see cref="OdfMathToken.Children"/>.
    /// MathML <c>mrow</c> 群組列，子專案存放於 <see cref="OdfMathToken.Children"/>。
    /// </summary>
    Row,

    /// <summary>
    /// A MathML <c>mtable</c> matrix or table whose rows are <see cref="Row"/> tokens stored in <see cref="OdfMathToken.Children"/>.
    /// MathML <c>mtable</c> 矩陣／表格，每一列為 <see cref="OdfMathToken.Children"/> 中的一個 <see cref="Row"/> token。
    /// </summary>
    Matrix,

    /// <summary>
    /// A MathML <c>munder</c> token, where <see cref="OdfMathToken.Base"/> is the base and <see cref="OdfMathToken.Script"/> is the underscript.
    /// MathML <c>munder</c> 下方標記，<see cref="OdfMathToken.Base"/> 為底數、<see cref="OdfMathToken.Script"/> 為下方標記。
    /// </summary>
    Under,

    /// <summary>
    /// A MathML <c>mover</c> token, where <see cref="OdfMathToken.Base"/> is the base and <see cref="OdfMathToken.Script"/> is the overscript.
    /// MathML <c>mover</c> 上方標記，<see cref="OdfMathToken.Base"/> 為底數、<see cref="OdfMathToken.Script"/> 為上方標記。
    /// </summary>
    Over,

    /// <summary>
    /// A MathML <c>munderover</c> token whose children are stored in base, under, over order.
    /// MathML <c>munderover</c> 上下方標記，依序存放於 <see cref="OdfMathToken.Children"/>（底數、下方、上方）。
    /// </summary>
    UnderOver,

    /// <summary>
    /// A fenced group serialized as <c>mrow</c> with leading and trailing <c>mo</c> delimiters instead of deprecated <c>mfenced</c>.
    /// 以括號包圍的群組（序列化為 <c>mrow</c> 搭配前後 <c>mo</c> 分隔符號，取代已棄用的 <c>mfenced</c>）。
    /// </summary>
    Fenced,

    /// <summary>
    /// A MathML <c>mstyle</c> style group whose content is stored in <see cref="OdfMathToken.Base"/>.
    /// MathML <c>mstyle</c> 樣式群組，<see cref="OdfMathToken.Base"/> 為內容。
    /// </summary>
    Style,

    /// <summary>
    /// A basic Content MathML <c>apply</c> semantic token whose operator name is stored in <see cref="OdfMathToken.Text"/> and operands are stored in <see cref="OdfMathToken.Children"/>.
    /// Content MathML <c>apply</c> 語意標記（基礎支援）：<see cref="OdfMathToken.Text"/> 為運算子名稱
    /// （例如 <c>plus</c>、<c>times</c>、<c>eq</c>），運算元依序存放於 <see cref="OdfMathToken.Children"/>。
    /// </summary>
    Apply
}
