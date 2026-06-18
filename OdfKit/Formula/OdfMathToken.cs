using System;

namespace OdfKit.Formula;

/// <summary>
/// 表示一個簡易 MathML token。
/// </summary>
public sealed class OdfMathToken
{
    private OdfMathToken(OdfMathTokenKind kind, string text, OdfMathToken? baseToken, OdfMathToken? scriptToken)
    {
        Kind = kind;
        Text = text ?? string.Empty;
        Base = baseToken;
        Script = scriptToken;
    }

    /// <summary>
    /// 取得 token 類型。
    /// </summary>
    public OdfMathTokenKind Kind { get; }

    /// <summary>
    /// 取得葉節點 token 文字；複合 token 為空字串。
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// 取得上標或下標的底數 token。
    /// </summary>
    public OdfMathToken? Base { get; }

    /// <summary>
    /// 取得上標或下標的指數 token。
    /// </summary>
    public OdfMathToken? Script { get; }

    /// <summary>
    /// 建立 MathML <c>mi</c> 識別名稱 token。
    /// </summary>
    /// <param name="text">識別名稱文字。</param>
    /// <returns>新的 <see cref="OdfMathToken"/>。</returns>
    public static OdfMathToken Identifier(string text) => new(OdfMathTokenKind.Identifier, text, null, null);

    /// <summary>
    /// 建立 MathML <c>mn</c> 數值 token。
    /// </summary>
    /// <param name="text">數值文字。</param>
    /// <returns>新的 <see cref="OdfMathToken"/>。</returns>
    public static OdfMathToken Number(string text) => new(OdfMathTokenKind.Number, text, null, null);

    /// <summary>
    /// 建立 MathML <c>mo</c> 運算子 token。
    /// </summary>
    /// <param name="text">運算子文字。</param>
    /// <returns>新的 <see cref="OdfMathToken"/>。</returns>
    public static OdfMathToken Operator(string text) => new(OdfMathTokenKind.Operator, text, null, null);

    /// <summary>
    /// 建立 MathML <c>mtext</c> 文字 token。
    /// </summary>
    /// <param name="text">文字內容。</param>
    /// <returns>新的 <see cref="OdfMathToken"/>。</returns>
    public static OdfMathToken TextToken(string text) => new(OdfMathTokenKind.Text, text, null, null);

    /// <summary>
    /// 建立 MathML <c>msup</c> 上標 token。
    /// </summary>
    /// <param name="baseToken">底數 token。</param>
    /// <param name="scriptToken">上標 token。</param>
    /// <returns>新的 <see cref="OdfMathToken"/>。</returns>
    public static OdfMathToken Superscript(OdfMathToken baseToken, OdfMathToken scriptToken)
    {
        if (baseToken is null)
        {
            throw new ArgumentNullException(nameof(baseToken));
        }

        if (scriptToken is null)
        {
            throw new ArgumentNullException(nameof(scriptToken));
        }

        return new OdfMathToken(OdfMathTokenKind.Superscript, string.Empty, baseToken, scriptToken);
    }

    /// <summary>
    /// 建立 MathML <c>msub</c> 下標 token。
    /// </summary>
    /// <param name="baseToken">底數 token。</param>
    /// <param name="scriptToken">下標 token。</param>
    /// <returns>新的 <see cref="OdfMathToken"/>。</returns>
    public static OdfMathToken Subscript(OdfMathToken baseToken, OdfMathToken scriptToken)
    {
        if (baseToken is null)
        {
            throw new ArgumentNullException(nameof(baseToken));
        }

        if (scriptToken is null)
        {
            throw new ArgumentNullException(nameof(scriptToken));
        }

        return new OdfMathToken(OdfMathTokenKind.Subscript, string.Empty, baseToken, scriptToken);
    }
}
