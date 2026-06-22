using System;
using System.Collections.Generic;
using System.Linq;

using OdfKit.Compliance;
namespace OdfKit.Formula;

/// <summary>
/// 表示一個簡易 MathML token。
/// </summary>
public sealed class OdfMathToken
{
    private OdfMathToken(
        OdfMathTokenKind kind,
        string text,
        OdfMathToken? baseToken,
        OdfMathToken? scriptToken,
        IReadOnlyList<OdfMathToken>? children = null,
        IReadOnlyDictionary<string, string>? attributes = null)
    {
        Kind = kind;
        Text = text ?? string.Empty;
        Base = baseToken;
        Script = scriptToken;
        Children = children;
        Attributes = attributes;
    }

    /// <summary>
    /// 取得 token 類型。
    /// </summary>
    public OdfMathTokenKind Kind { get; }

    /// <summary>
    /// 取得葉節點 token 文字；複合 token 為空字串，或用於儲存複合 token 的輔助資料
    /// （例如 <see cref="OdfMathTokenKind.Fenced"/> 的開閉括號、<see cref="OdfMathTokenKind.Style"/> 的 displaystyle 設定）。
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// 取得二元複合 token（上標、下標、分數、根號、上下方標記）的第一個子 token。
    /// </summary>
    public OdfMathToken? Base { get; }

    /// <summary>
    /// 取得二元複合 token（上標、下標、分數、根號、上下方標記）的第二個子 token。
    /// </summary>
    public OdfMathToken? Script { get; }

    /// <summary>
    /// 取得多元複合 token（<see cref="OdfMathTokenKind.Row"/>、<see cref="OdfMathTokenKind.Matrix"/>、
    /// <see cref="OdfMathTokenKind.UnderOver"/>）的子 token 清單。
    /// </summary>
    public IReadOnlyList<OdfMathToken>? Children { get; }

    /// <summary>
    /// 取得此 token 上設定的 MathML 通用屬性（例如 <c>mathvariant</c>、<c>displaystyle</c>、
    /// <c>mathsize</c>、<c>mathcolor</c>、<c>mathbackground</c>、<c>stretchy</c>、<c>lspace</c>、<c>rspace</c>）。
    /// </summary>
    public IReadOnlyDictionary<string, string>? Attributes { get; }

    /// <summary>
    /// 建立一個附加指定 MathML 屬性的新 token（原 token 不會被修改）。
    /// </summary>
    /// <param name="name">屬性名稱（例如 <c>mathvariant</c>）。</param>
    /// <param name="value">屬性值。</param>
    /// <returns>附加屬性後的新 <see cref="OdfMathToken"/>。</returns>
    /// <exception cref="ArgumentException">當 <paramref name="name"/> 為空白時擲出。</exception>
    /// <exception cref="ArgumentNullException">當 <paramref name="value"/> 為 <see langword="null"/> 時擲出。</exception>
    public OdfMathToken WithAttribute(string name, string value)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfMathToken_PropertyCannotBeEmpty"), nameof(name));
        }

        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        var merged = new Dictionary<string, string>();
        if (Attributes is not null)
        {
            foreach (KeyValuePair<string, string> existing in Attributes)
            {
                merged[existing.Key] = existing.Value;
            }
        }

        merged[name] = value;
        return new OdfMathToken(Kind, Text, Base, Script, Children, merged);
    }

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
        RequireNotNull(baseToken, nameof(baseToken));
        RequireNotNull(scriptToken, nameof(scriptToken));
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
        RequireNotNull(baseToken, nameof(baseToken));
        RequireNotNull(scriptToken, nameof(scriptToken));
        return new OdfMathToken(OdfMathTokenKind.Subscript, string.Empty, baseToken, scriptToken);
    }

    /// <summary>
    /// 建立 MathML <c>mfrac</c> 分數 token。
    /// </summary>
    /// <param name="numerator">分子 token。</param>
    /// <param name="denominator">分母 token。</param>
    /// <returns>新的 <see cref="OdfMathToken"/>。</returns>
    public static OdfMathToken Fraction(OdfMathToken numerator, OdfMathToken denominator)
    {
        RequireNotNull(numerator, nameof(numerator));
        RequireNotNull(denominator, nameof(denominator));
        return new OdfMathToken(OdfMathTokenKind.Fraction, string.Empty, numerator, denominator);
    }

    /// <summary>
    /// 建立 MathML <c>msqrt</c>（無索引）或 <c>mroot</c>（具索引）根號 token。
    /// </summary>
    /// <param name="radicand">被開方數 token。</param>
    /// <param name="index">選用的根指數 token；<see langword="null"/> 表示平方根。</param>
    /// <returns>新的 <see cref="OdfMathToken"/>。</returns>
    public static OdfMathToken Radical(OdfMathToken radicand, OdfMathToken? index = null)
    {
        RequireNotNull(radicand, nameof(radicand));
        return new OdfMathToken(OdfMathTokenKind.Radical, string.Empty, radicand, index);
    }

    /// <summary>
    /// 建立 MathML <c>mrow</c> 群組列 token。
    /// </summary>
    /// <param name="children">群組中的子 token 清單。</param>
    /// <returns>新的 <see cref="OdfMathToken"/>。</returns>
    public static OdfMathToken Row(params OdfMathToken[] children) =>
        new(OdfMathTokenKind.Row, string.Empty, null, null, RequireChildren(children, nameof(children)));

    /// <summary>
    /// 建立 MathML <c>mtable</c> 矩陣 token。
    /// </summary>
    /// <param name="rows">矩陣的每一列，須為 <see cref="Row(OdfMathToken[])"/> 所建立的 token。</param>
    /// <returns>新的 <see cref="OdfMathToken"/>。</returns>
    public static OdfMathToken Matrix(params OdfMathToken[] rows) =>
        new(OdfMathTokenKind.Matrix, string.Empty, null, null, RequireChildren(rows, nameof(rows)));

    /// <summary>
    /// 建立 MathML <c>munder</c> 下方標記 token。
    /// </summary>
    /// <param name="baseToken">底數 token。</param>
    /// <param name="under">下方標記 token。</param>
    /// <returns>新的 <see cref="OdfMathToken"/>。</returns>
    public static OdfMathToken Under(OdfMathToken baseToken, OdfMathToken under)
    {
        RequireNotNull(baseToken, nameof(baseToken));
        RequireNotNull(under, nameof(under));
        return new OdfMathToken(OdfMathTokenKind.Under, string.Empty, baseToken, under);
    }

    /// <summary>
    /// 建立 MathML <c>mover</c> 上方標記 token。
    /// </summary>
    /// <param name="baseToken">底數 token。</param>
    /// <param name="over">上方標記 token。</param>
    /// <returns>新的 <see cref="OdfMathToken"/>。</returns>
    public static OdfMathToken Over(OdfMathToken baseToken, OdfMathToken over)
    {
        RequireNotNull(baseToken, nameof(baseToken));
        RequireNotNull(over, nameof(over));
        return new OdfMathToken(OdfMathTokenKind.Over, string.Empty, baseToken, over);
    }

    /// <summary>
    /// 建立 MathML <c>munderover</c> 上下方標記 token。
    /// </summary>
    /// <param name="baseToken">底數 token。</param>
    /// <param name="under">下方標記 token。</param>
    /// <param name="over">上方標記 token。</param>
    /// <returns>新的 <see cref="OdfMathToken"/>。</returns>
    public static OdfMathToken UnderOver(OdfMathToken baseToken, OdfMathToken under, OdfMathToken over)
    {
        RequireNotNull(baseToken, nameof(baseToken));
        RequireNotNull(under, nameof(under));
        RequireNotNull(over, nameof(over));
        return new OdfMathToken(OdfMathTokenKind.UnderOver, string.Empty, null, null, [baseToken, under, over]);
    }

    /// <summary>
    /// 建立以括號包圍內容的群組 token（序列化為 <c>mrow</c> 搭配前後 <c>mo</c> 分隔符號）。
    /// </summary>
    /// <param name="inner">括號內的內容 token。</param>
    /// <param name="open">開括號文字，預設為 <c>(</c>。</param>
    /// <param name="close">閉括號文字，預設為 <c>)</c>。</param>
    /// <returns>新的 <see cref="OdfMathToken"/>。</returns>
    public static OdfMathToken Fenced(OdfMathToken inner, string open = "(", string close = ")")
    {
        RequireNotNull(inner, nameof(inner));
        return new OdfMathToken(OdfMathTokenKind.Fenced, $"{open}|{close}", inner, null);
    }

    /// <summary>
    /// 建立 MathML <c>mstyle</c> 樣式群組 token。
    /// </summary>
    /// <param name="inner">樣式群組的內容 token。</param>
    /// <param name="displayStyle">選用的 <c>displaystyle</c> 設定。</param>
    /// <returns>新的 <see cref="OdfMathToken"/>。</returns>
    public static OdfMathToken Style(OdfMathToken inner, bool? displayStyle = null)
    {
        RequireNotNull(inner, nameof(inner));
        string text = displayStyle.HasValue ? (displayStyle.Value ? "true" : "false") : string.Empty;
        return new OdfMathToken(OdfMathTokenKind.Style, text, inner, null);
    }

    private static void RequireNotNull(OdfMathToken? token, string paramName)
    {
        if (token is null)
        {
            throw new ArgumentNullException(paramName);
        }
    }

    private static IReadOnlyList<OdfMathToken> RequireChildren(OdfMathToken[]? children, string paramName)
    {
        if (children is null || children.Length == 0)
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfMathToken_SubtokenCannotBeEmpty"), paramName);
        }

        if (children.Any(static child => child is null))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfMathToken_ChildTokenListsCannot"), paramName);
        }

        return children.ToList();
    }
}
