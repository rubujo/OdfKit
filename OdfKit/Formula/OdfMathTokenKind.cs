using System;

namespace OdfKit.Formula;

/// <summary>
/// 表示可由高階公式 API 寫入的 MathML token 類型。
/// </summary>
public enum OdfMathTokenKind
{
    /// <summary>
    /// MathML <c>mi</c> 識別名稱。
    /// </summary>
    Identifier,

    /// <summary>
    /// MathML <c>mn</c> 數值。
    /// </summary>
    Number,

    /// <summary>
    /// MathML <c>mo</c> 運算子。
    /// </summary>
    Operator,

    /// <summary>
    /// MathML <c>mtext</c> 文字。
    /// </summary>
    Text,

    /// <summary>
    /// MathML <c>msup</c> 上標。
    /// </summary>
    Superscript,

    /// <summary>
    /// MathML <c>msub</c> 下標。
    /// </summary>
    Subscript,

    /// <summary>
    /// MathML <c>mfrac</c> 分數，<see cref="OdfMathToken.Base"/> 為分子、<see cref="OdfMathToken.Script"/> 為分母。
    /// </summary>
    Fraction,

    /// <summary>
    /// MathML <c>msqrt</c>（無索引）或 <c>mroot</c>（具索引）根號，
    /// <see cref="OdfMathToken.Base"/> 為被開方數、<see cref="OdfMathToken.Script"/> 為選用的根指數。
    /// </summary>
    Radical,

    /// <summary>
    /// MathML <c>mrow</c> 群組列，子專案存放於 <see cref="OdfMathToken.Children"/>。
    /// </summary>
    Row,

    /// <summary>
    /// MathML <c>mtable</c> 矩陣／表格，每一列為 <see cref="OdfMathToken.Children"/> 中的一個 <see cref="Row"/> token。
    /// </summary>
    Matrix,

    /// <summary>
    /// MathML <c>munder</c> 下方標記，<see cref="OdfMathToken.Base"/> 為底數、<see cref="OdfMathToken.Script"/> 為下方標記。
    /// </summary>
    Under,

    /// <summary>
    /// MathML <c>mover</c> 上方標記，<see cref="OdfMathToken.Base"/> 為底數、<see cref="OdfMathToken.Script"/> 為上方標記。
    /// </summary>
    Over,

    /// <summary>
    /// MathML <c>munderover</c> 上下方標記，依序存放於 <see cref="OdfMathToken.Children"/>（底數、下方、上方）。
    /// </summary>
    UnderOver,

    /// <summary>
    /// 以括號包圍的群組（序列化為 <c>mrow</c> 搭配前後 <c>mo</c> 分隔符號，取代已棄用的 <c>mfenced</c>）。
    /// </summary>
    Fenced,

    /// <summary>
    /// MathML <c>mstyle</c> 樣式群組，<see cref="OdfMathToken.Base"/> 為內容。
    /// </summary>
    Style
}
