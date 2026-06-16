using System;

namespace OdfKit.Formula;

/// <summary>
/// 表示一個簡易 MathML token。
/// </summary>
/// <param name="kind">token 類型。</param>
/// <param name="text">token 文字。</param>
public sealed class OdfMathToken(OdfMathTokenKind kind, string text)
{
    /// <summary>
    /// 取得 token 類型。
    /// </summary>
    public OdfMathTokenKind Kind { get; } = kind;

    /// <summary>
    /// 取得 token 文字。
    /// </summary>
    public string Text { get; } = text ?? throw new ArgumentNullException(nameof(text));

    /// <summary>
    /// 建立 MathML <c>mi</c> 識別名稱 token。
    /// </summary>
    /// <param name="text">識別名稱文字。</param>
    /// <returns>新的 <see cref="OdfMathToken"/>。</returns>
    public static OdfMathToken Identifier(string text) => new(OdfMathTokenKind.Identifier, text);

    /// <summary>
    /// 建立 MathML <c>mn</c> 數值 token。
    /// </summary>
    /// <param name="text">數值文字。</param>
    /// <returns>新的 <see cref="OdfMathToken"/>。</returns>
    public static OdfMathToken Number(string text) => new(OdfMathTokenKind.Number, text);

    /// <summary>
    /// 建立 MathML <c>mo</c> 運算子 token。
    /// </summary>
    /// <param name="text">運算子文字。</param>
    /// <returns>新的 <see cref="OdfMathToken"/>。</returns>
    public static OdfMathToken Operator(string text) => new(OdfMathTokenKind.Operator, text);

    /// <summary>
    /// 建立 MathML <c>mtext</c> 文字 token。
    /// </summary>
    /// <param name="text">文字內容。</param>
    /// <returns>新的 <see cref="OdfMathToken"/>。</returns>
    public static OdfMathToken TextToken(string text) => new(OdfMathTokenKind.Text, text);
}
