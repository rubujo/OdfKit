using System;
using System.Collections.Generic;

using OdfKit.Compliance;
namespace OdfKit.Formula;

/// <summary>
/// Composes MathML token trees with nested delegates.
/// 提供以巢狀委派組合 MathML token 樹狀結構的 Fluent API。
/// </summary>
public sealed class OdfMathBuilder
{
    private readonly List<OdfMathToken> _tokens = [];

    /// <summary>
    /// Appends a MathML <c>mi</c> identifier.
    /// 附加 MathML <c>mi</c> 識別名稱。
    /// </summary>
    /// <param name="text">The identifier text. / 識別名稱文字。</param>
    /// <returns>The current builder instance. / 目前 builder 執行個體。</returns>
    public OdfMathBuilder Identifier(string text)
    {
        _tokens.Add(OdfMathToken.Identifier(text));
        return this;
    }

    /// <summary>
    /// Appends a MathML <c>mn</c> number.
    /// 附加 MathML <c>mn</c> 數值。
    /// </summary>
    /// <param name="text">The number text. / 數值文字。</param>
    /// <returns>The current builder instance. / 目前 builder 執行個體。</returns>
    public OdfMathBuilder Number(string text)
    {
        _tokens.Add(OdfMathToken.Number(text));
        return this;
    }

    /// <summary>
    /// Appends a MathML <c>mo</c> operator.
    /// 附加 MathML <c>mo</c> 運算子。
    /// </summary>
    /// <param name="text">The operator text. / 運算子文字。</param>
    /// <returns>The current builder instance. / 目前 builder 執行個體。</returns>
    public OdfMathBuilder Operator(string text)
    {
        _tokens.Add(OdfMathToken.Operator(text));
        return this;
    }

    /// <summary>
    /// Appends MathML <c>mtext</c> text.
    /// 附加 MathML <c>mtext</c> 文字。
    /// </summary>
    /// <param name="text">The text content. / 文字內容。</param>
    /// <returns>The current builder instance. / 目前 builder 執行個體。</returns>
    public OdfMathBuilder Text(string text)
    {
        _tokens.Add(OdfMathToken.TextToken(text));
        return this;
    }

    /// <summary>
    /// Appends a fraction (<c>mfrac</c>).
    /// 附加分數（<c>mfrac</c>）。
    /// </summary>
    /// <param name="numerator">The numerator composition delegate. / 分子組合委派。</param>
    /// <param name="denominator">The denominator composition delegate. / 分母組合委派。</param>
    /// <returns>The current builder instance. / 目前 builder 執行個體。</returns>
    public OdfMathBuilder Fraction(Action<OdfMathBuilder> numerator, Action<OdfMathBuilder> denominator)
    {
        _tokens.Add(OdfMathToken.Fraction(BuildSingle(numerator), BuildSingle(denominator)));
        return this;
    }

    /// <summary>
    /// Appends a square root (<c>msqrt</c>).
    /// 附加平方根（<c>msqrt</c>）。
    /// </summary>
    /// <param name="radicand">The radicand composition delegate. / 被開方數組合委派。</param>
    /// <returns>The current builder instance. / 目前 builder 執行個體。</returns>
    public OdfMathBuilder Sqrt(Action<OdfMathBuilder> radicand)
    {
        _tokens.Add(OdfMathToken.Radical(BuildSingle(radicand)));
        return this;
    }

    /// <summary>
    /// Appends a root with an index (<c>mroot</c>).
    /// 附加具根指數的根號（<c>mroot</c>）。
    /// </summary>
    /// <param name="radicand">The radicand composition delegate. / 被開方數組合委派。</param>
    /// <param name="index">The root-index composition delegate. / 根指數組合委派。</param>
    /// <returns>The current builder instance. / 目前 builder 執行個體。</returns>
    public OdfMathBuilder Root(Action<OdfMathBuilder> radicand, Action<OdfMathBuilder> index)
    {
        _tokens.Add(OdfMathToken.Radical(BuildSingle(radicand), BuildSingle(index)));
        return this;
    }

    /// <summary>
    /// Appends a grouped row (<c>mrow</c>).
    /// 附加群組列（<c>mrow</c>）。
    /// </summary>
    /// <param name="content">The grouped-content composition delegate. / 群組內容組合委派。</param>
    /// <returns>The current builder instance. / 目前 builder 執行個體。</returns>
    public OdfMathBuilder Row(Action<OdfMathBuilder> content)
    {
        _tokens.Add(BuildRow(content));
        return this;
    }

    /// <summary>
    /// Appends a superscript (<c>msup</c>).
    /// 附加上標（<c>msup</c>）。
    /// </summary>
    /// <param name="baseExpr">The base-expression composition delegate. / 底數組合委派。</param>
    /// <param name="script">The superscript composition delegate. / 上標組合委派。</param>
    /// <returns>The current builder instance. / 目前 builder 執行個體。</returns>
    public OdfMathBuilder Superscript(Action<OdfMathBuilder> baseExpr, Action<OdfMathBuilder> script)
    {
        _tokens.Add(OdfMathToken.Superscript(BuildSingle(baseExpr), BuildSingle(script)));
        return this;
    }

    /// <summary>
    /// Appends a subscript (<c>msub</c>).
    /// 附加下標（<c>msub</c>）。
    /// </summary>
    /// <param name="baseExpr">The base-expression composition delegate. / 底數組合委派。</param>
    /// <param name="script">The subscript composition delegate. / 下標組合委派。</param>
    /// <returns>The current builder instance. / 目前 builder 執行個體。</returns>
    public OdfMathBuilder Subscript(Action<OdfMathBuilder> baseExpr, Action<OdfMathBuilder> script)
    {
        _tokens.Add(OdfMathToken.Subscript(BuildSingle(baseExpr), BuildSingle(script)));
        return this;
    }

    /// <summary>
    /// Appends an underscript marker (<c>munder</c>).
    /// 附加下方標記（<c>munder</c>）。
    /// </summary>
    /// <param name="baseExpr">The base-expression composition delegate. / 底數組合委派。</param>
    /// <param name="under">The underscript-marker composition delegate. / 下方標記組合委派。</param>
    /// <returns>The current builder instance. / 目前 builder 執行個體。</returns>
    public OdfMathBuilder Under(Action<OdfMathBuilder> baseExpr, Action<OdfMathBuilder> under)
    {
        _tokens.Add(OdfMathToken.Under(BuildSingle(baseExpr), BuildSingle(under)));
        return this;
    }

    /// <summary>
    /// Appends an overscript marker (<c>mover</c>).
    /// 附加上方標記（<c>mover</c>）。
    /// </summary>
    /// <param name="baseExpr">The base-expression composition delegate. / 底數組合委派。</param>
    /// <param name="over">The overscript-marker composition delegate. / 上方標記組合委派。</param>
    /// <returns>The current builder instance. / 目前 builder 執行個體。</returns>
    public OdfMathBuilder Over(Action<OdfMathBuilder> baseExpr, Action<OdfMathBuilder> over)
    {
        _tokens.Add(OdfMathToken.Over(BuildSingle(baseExpr), BuildSingle(over)));
        return this;
    }

    /// <summary>
    /// Appends combined under and over markers (<c>munderover</c>).
    /// 附加上下方標記（<c>munderover</c>）。
    /// </summary>
    /// <param name="baseExpr">The base-expression composition delegate. / 底數組合委派。</param>
    /// <param name="under">The underscript-marker composition delegate. / 下方標記組合委派。</param>
    /// <param name="over">The overscript-marker composition delegate. / 上方標記組合委派。</param>
    /// <returns>The current builder instance. / 目前 builder 執行個體。</returns>
    public OdfMathBuilder UnderOver(Action<OdfMathBuilder> baseExpr, Action<OdfMathBuilder> under, Action<OdfMathBuilder> over)
    {
        _tokens.Add(OdfMathToken.UnderOver(BuildSingle(baseExpr), BuildSingle(under), BuildSingle(over)));
        return this;
    }

    /// <summary>
    /// Appends an accent marker, modeled as an overscript marker with <c>accent="true"</c>.
    /// 附加重音標記（語意上的上方標記，序列化為 <c>mover</c> 並設定 <c>accent="true"</c>，
    /// 用於向量符號、變音符號等裝飾性記號，與一般 <see cref="Over"/> 的極限記號語意有別）。
    /// </summary>
    /// <param name="baseExpr">The base-expression composition delegate. / 底數組合委派。</param>
    /// <param name="accentMark">The accent-marker composition delegate. / 重音標記組合委派。</param>
    /// <returns>The current builder instance. / 目前 builder 執行個體。</returns>
    public OdfMathBuilder Accent(Action<OdfMathBuilder> baseExpr, Action<OdfMathBuilder> accentMark)
    {
        _tokens.Add(OdfMathToken.Over(BuildSingle(baseExpr), BuildSingle(accentMark)).WithAttribute("accent", "true"));
        return this;
    }

    /// <summary>
    /// Appends a Content MathML <c>apply</c> semantic token (basic support).
    /// 附加 Content MathML <c>apply</c> 語意標記（基礎支援）。
    /// </summary>
    /// <param name="operatorName">The operator name, such as <c>plus</c>, <c>times</c>, or <c>eq</c>. / 運算子名稱（例如 <c>plus</c>、<c>times</c>、<c>eq</c>）。</param>
    /// <param name="operands">The operand composition delegates. / 運算元組合委派清單。</param>
    /// <returns>The current builder instance. / 目前 builder 執行個體。</returns>
    /// <exception cref="ArgumentException">When no operands are provided. / 當未提供任何運算元時擲出。</exception>
    public OdfMathBuilder Apply(string operatorName, params Action<OdfMathBuilder>[] operands)
    {
        if (operands is null || operands.Length == 0)
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfMathToken_SubtokenCannotBeEmpty"), nameof(operands));
        }

        var operandTokens = new OdfMathToken[operands.Length];
        for (int i = 0; i < operands.Length; i++)
        {
            operandTokens[i] = BuildSingle(operands[i]);
        }

        _tokens.Add(OdfMathToken.Apply(operatorName, operandTokens));
        return this;
    }

    /// <summary>
    /// Appends a fenced group.
    /// 附加以括號包圍的群組。
    /// </summary>
    /// <param name="inner">The fenced-content composition delegate. / 括號內容組合委派。</param>
    /// <param name="open">The opening delimiter text; defaults to <c>(</c>. / 開括號文字，預設為 <c>(</c>。</param>
    /// <param name="close">The closing delimiter text; defaults to <c>)</c>. / 閉括號文字，預設為 <c>)</c>。</param>
    /// <returns>The current builder instance. / 目前 builder 執行個體。</returns>
    public OdfMathBuilder Fenced(Action<OdfMathBuilder> inner, string open = "(", string close = ")")
    {
        _tokens.Add(OdfMathToken.Fenced(BuildSingle(inner), open, close));
        return this;
    }

    /// <summary>
    /// Appends a matrix (<c>mtable</c>).
    /// 附加矩陣（<c>mtable</c>）。
    /// </summary>
    /// <param name="rows">The cell composition delegates for each row. / 每一列的儲存格組合委派。</param>
    /// <returns>The current builder instance. / 目前 builder 執行個體。</returns>
    /// <exception cref="ArgumentException">When no rows are provided. / 當未提供任何列時擲出。</exception>
    public OdfMathBuilder Matrix(params Action<OdfMathBuilder>[] rows)
    {
        if (rows is null || rows.Length == 0)
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfMathBuilder_MatrixContainLeastOne"), nameof(rows));
        }

        var rowTokens = new OdfMathToken[rows.Length];
        for (int i = 0; i < rows.Length; i++)
        {
            rowTokens[i] = BuildRow(rows[i]);
        }

        _tokens.Add(OdfMathToken.Matrix(rowTokens));
        return this;
    }

    /// <summary>
    /// Builds the accumulated tokens as a single token, wrapping multiple tokens in <c>mrow</c>.
    /// 將目前累積的 token 建立為單一 token（多個 token 會包裝為 <c>mrow</c>）。
    /// </summary>
    /// <returns>The built <see cref="OdfMathToken"/>. / 建立完成的 <see cref="OdfMathToken"/>。</returns>
    /// <exception cref="InvalidOperationException">When no token has been appended. / 當尚未附加任何 token 時擲出。</exception>
    public OdfMathToken Build()
    {
        if (_tokens.Count == 0)
        {
            throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_OdfMathBuilder_NoMathmlTokenAttached"));
        }

        return _tokens.Count == 1 ? _tokens[0] : OdfMathToken.Row(_tokens.ToArray());
    }

    private static OdfMathToken BuildSingle(Action<OdfMathBuilder> build)
    {
        if (build is null)
        {
            throw new ArgumentNullException(nameof(build));
        }

        var builder = new OdfMathBuilder();
        build(builder);
        return builder.Build();
    }

    private static OdfMathToken BuildRow(Action<OdfMathBuilder> build)
    {
        if (build is null)
        {
            throw new ArgumentNullException(nameof(build));
        }

        var builder = new OdfMathBuilder();
        build(builder);
        if (builder._tokens.Count == 0)
        {
            throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_OdfMathBuilder_GroupCannotBeEmpty"));
        }

        return OdfMathToken.Row(builder._tokens.ToArray());
    }
}
