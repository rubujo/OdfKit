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
    Text
}
