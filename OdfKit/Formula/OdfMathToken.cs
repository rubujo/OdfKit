using System;
using System.Collections.Generic;
using System.Linq;

using OdfKit.Compliance;
namespace OdfKit.Formula;

/// <summary>
/// Represents a lightweight MathML token.
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
    /// Gets the token kind.
    /// 取得 token 類型。
    /// </summary>
    public OdfMathTokenKind Kind { get; }

    /// <summary>
    /// Gets leaf-token text, or auxiliary data for composite tokens.
    /// 取得葉節點 token 文字，或複合 token 的輔助資料。
    /// </summary>
    /// <remarks>
    /// Composite tokens usually use an empty string, except for auxiliary data such as delimiters for <see cref="OdfMathTokenKind.Fenced"/> or the displaystyle setting for <see cref="OdfMathTokenKind.Style"/>.
    /// 複合 token 通常使用空字串，例外為 <see cref="OdfMathTokenKind.Fenced"/> 的開閉括號或 <see cref="OdfMathTokenKind.Style"/> 的 displaystyle 設定等輔助資料。
    /// </remarks>
    public string Text { get; }

    /// <summary>
    /// Gets the first child token for binary composite tokens.
    /// 取得二元複合 token 的第一個子 token。
    /// </summary>
    /// <remarks>
    /// This applies to superscript, subscript, fraction, radical, and upper/lower mark tokens.
    /// 這適用於上標、下標、分數、根號與上下方標記 token。
    /// </remarks>
    public OdfMathToken? Base { get; }

    /// <summary>
    /// Gets the second child token for binary composite tokens.
    /// 取得二元複合 token 的第二個子 token。
    /// </summary>
    /// <remarks>
    /// This applies to superscript, subscript, fraction, radical, and upper/lower mark tokens.
    /// 這適用於上標、下標、分數、根號與上下方標記 token。
    /// </remarks>
    public OdfMathToken? Script { get; }

    /// <summary>
    /// Gets the child token list for multi-child composite tokens.
    /// 取得多元複合 token 的子 token 清單。
    /// </summary>
    /// <remarks>
    /// This applies to <see cref="OdfMathTokenKind.Row"/>, <see cref="OdfMathTokenKind.Matrix"/>, and <see cref="OdfMathTokenKind.UnderOver"/>.
    /// 這適用於 <see cref="OdfMathTokenKind.Row"/>、<see cref="OdfMathTokenKind.Matrix"/> 與 <see cref="OdfMathTokenKind.UnderOver"/>。
    /// </remarks>
    public IReadOnlyList<OdfMathToken>? Children { get; }

    /// <summary>
    /// Gets the common MathML attributes set on this token.
    /// 取得此 token 上設定的 MathML 通用屬性。
    /// </summary>
    /// <remarks>
    /// Examples include <c>mathvariant</c>, <c>displaystyle</c>, <c>mathsize</c>, <c>mathcolor</c>, <c>mathbackground</c>, <c>stretchy</c>, <c>lspace</c>, and <c>rspace</c>.
    /// 例如 <c>mathvariant</c>、<c>displaystyle</c>、<c>mathsize</c>、<c>mathcolor</c>、<c>mathbackground</c>、<c>stretchy</c>、<c>lspace</c> 與 <c>rspace</c>。
    /// </remarks>
    public IReadOnlyDictionary<string, string>? Attributes { get; }

    /// <summary>
    /// Creates a new token with the specified MathML attribute added, leaving the original token unchanged.
    /// 建立一個附加指定 MathML 屬性的新 token（原 token 不會被修改）。
    /// </summary>
    /// <param name="name">The attribute name, such as <c>mathvariant</c>. / 屬性名稱，例如 <c>mathvariant</c>。</param>
    /// <param name="value">The attribute value. / 屬性值。</param>
    /// <returns>A new <see cref="OdfMathToken"/> with the added attribute. / 附加屬性後的新 <see cref="OdfMathToken"/>。</returns>
    /// <exception cref="ArgumentException">When <paramref name="name"/> is blank. / 當 <paramref name="name"/> 為空白時擲出。</exception>
    /// <exception cref="ArgumentNullException">When <paramref name="value"/> is <see langword="null"/>. / 當 <paramref name="value"/> 為 <see langword="null"/> 時擲出。</exception>
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
    /// Recursively finds the first token matching the specified kind.
    /// 遞迴尋找第一個符合指定種類的 token。
    /// </summary>
    /// <param name="kind">The target token kind. / 目標 token 種類。</param>
    /// <returns>The first matching token, or <see langword="null"/> when none exists. / 找到的第一個 token；若不存在則為 <see langword="null"/>。</returns>
    public OdfMathToken? FindFirst(OdfMathTokenKind kind)
    {
        if (Kind == kind)
        {
            return this;
        }

        int childCount = GetChildCount();
        for (int index = 0; index < childCount; index++)
        {
            OdfMathToken? found = GetChild(index).FindFirst(kind);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>
    /// Recursively enumerates all tokens matching the specified kind.
    /// 遞迴列舉所有符合指定種類的 token。
    /// </summary>
    /// <param name="kind">The target token kind. / 目標 token 種類。</param>
    /// <returns>The matching token sequence. / 符合條件的 token 序列。</returns>
    public IEnumerable<OdfMathToken> GetAll(OdfMathTokenKind kind)
    {
        if (Kind == kind)
        {
            yield return this;
        }

        int childCount = GetChildCount();
        for (int index = 0; index < childCount; index++)
        {
            foreach (OdfMathToken match in GetChild(index).GetAll(kind))
            {
                yield return match;
            }
        }
    }

    /// <summary>
    /// Returns a new token with the specified child replaced, leaving the original token unchanged.
    /// 回傳替換指定子節點後的新 token（原 token 不會被修改）。
    /// </summary>
    /// <param name="index">The child index to replace. / 要替換的子節點索引。</param>
    /// <param name="replacement">The replacement child token. / 替換後的新子節點。</param>
    /// <returns>The new <see cref="OdfMathToken"/> with the replacement applied. / 替換完成的新 <see cref="OdfMathToken"/>。</returns>
    /// <exception cref="ArgumentNullException">When <paramref name="replacement"/> is <see langword="null"/>. / 當 <paramref name="replacement"/> 為 <see langword="null"/> 時擲出。</exception>
    /// <exception cref="ArgumentOutOfRangeException">When <paramref name="index"/> is outside the available child range. / 當 <paramref name="index"/> 超出可用子節點範圍時擲出。</exception>
    public OdfMathToken WithChild(int index, OdfMathToken replacement)
    {
        if (replacement is null)
        {
            throw new ArgumentNullException(nameof(replacement));
        }

        int childCount = GetChildCount();
        if (index < 0 || index >= childCount)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        return Kind switch
        {
            OdfMathTokenKind.Row or OdfMathTokenKind.Matrix or OdfMathTokenKind.UnderOver or OdfMathTokenKind.Apply
                => ReplaceListChild(index, replacement),
            _ => ReplacePairedChild(index, replacement)
        };
    }

    /// <summary>
    /// Recursively finds the first token matching the predicate and replaces it with a token created by the specified factory.
    /// 遞迴尋找第一個符合條件的 token，並以指定 factory 建立的新 token 取代。
    /// </summary>
    /// <param name="predicate">The delegate that determines whether a token is the replacement target. / 判斷 token 是否為替換目標的委派。</param>
    /// <param name="replacementFactory">The delegate that creates a replacement token from the matched token. / 根據命中的 token 建立替換 token 的委派。</param>
    /// <returns>The new token after replacement, or the current token when no token is matched. / 替換後的新 token；若未命中任何 token，則回傳目前 token。</returns>
    /// <exception cref="ArgumentNullException">When <paramref name="predicate"/> or <paramref name="replacementFactory"/> is <see langword="null"/>. / 當 <paramref name="predicate"/> 或 <paramref name="replacementFactory"/> 為 <see langword="null"/> 時擲出。</exception>
    public OdfMathToken ReplaceFirst(Func<OdfMathToken, bool> predicate, Func<OdfMathToken, OdfMathToken> replacementFactory)
    {
        if (predicate is null)
        {
            throw new ArgumentNullException(nameof(predicate));
        }

        if (replacementFactory is null)
        {
            throw new ArgumentNullException(nameof(replacementFactory));
        }

        if (predicate(this))
        {
            OdfMathToken replacement = replacementFactory(this);
            return replacement ?? throw new ArgumentNullException(nameof(replacementFactory));
        }

        int childCount = GetChildCount();
        for (int index = 0; index < childCount; index++)
        {
            OdfMathToken child = GetChild(index);
            OdfMathToken replaced = child.ReplaceFirst(predicate, replacementFactory);
            if (!ReferenceEquals(child, replaced))
            {
                return WithChild(index, replaced);
            }
        }

        return this;
    }

    /// <summary>
    /// Recursively finds the first token with the specified kind and replaces it with the specified token.
    /// 遞迴尋找第一個指定種類的 token，並以指定 token 取代。
    /// </summary>
    /// <param name="kind">The target token kind. / 目標 token 種類。</param>
    /// <param name="replacement">The replacement token. / 替換後的新 token。</param>
    /// <returns>The new token after replacement, or the current token when no token is matched. / 替換後的新 token；若未命中任何 token，則回傳目前 token。</returns>
    /// <exception cref="ArgumentNullException">When <paramref name="replacement"/> is <see langword="null"/>. / 當 <paramref name="replacement"/> 為 <see langword="null"/> 時擲出。</exception>
    public OdfMathToken ReplaceFirst(OdfMathTokenKind kind, OdfMathToken replacement)
    {
        if (replacement is null)
        {
            throw new ArgumentNullException(nameof(replacement));
        }

        return ReplaceFirst(token => token.Kind == kind, _ => replacement);
    }

    /// <summary>
    /// Creates a MathML <c>mi</c> identifier token.
    /// 建立 MathML <c>mi</c> 識別名稱 token。
    /// </summary>
    /// <param name="text">The identifier text. / 識別名稱文字。</param>
    /// <returns>A new <see cref="OdfMathToken"/>. / 新的 <see cref="OdfMathToken"/>。</returns>
    public static OdfMathToken Identifier(string text) => new(OdfMathTokenKind.Identifier, text, null, null);

    /// <summary>
    /// Creates a MathML <c>mn</c> number token.
    /// 建立 MathML <c>mn</c> 數值 token。
    /// </summary>
    /// <param name="text">The number text. / 數值文字。</param>
    /// <returns>A new <see cref="OdfMathToken"/>. / 新的 <see cref="OdfMathToken"/>。</returns>
    public static OdfMathToken Number(string text) => new(OdfMathTokenKind.Number, text, null, null);

    /// <summary>
    /// Creates a MathML <c>mo</c> operator token.
    /// 建立 MathML <c>mo</c> 運算子 token。
    /// </summary>
    /// <param name="text">The operator text. / 運算子文字。</param>
    /// <returns>A new <see cref="OdfMathToken"/>. / 新的 <see cref="OdfMathToken"/>。</returns>
    public static OdfMathToken Operator(string text) => new(OdfMathTokenKind.Operator, text, null, null);

    /// <summary>
    /// Creates a MathML <c>mtext</c> text token.
    /// 建立 MathML <c>mtext</c> 文字 token。
    /// </summary>
    /// <param name="text">The text content. / 文字內容。</param>
    /// <returns>A new <see cref="OdfMathToken"/>. / 新的 <see cref="OdfMathToken"/>。</returns>
    public static OdfMathToken TextToken(string text) => new(OdfMathTokenKind.Text, text, null, null);

    /// <summary>
    /// Creates a MathML <c>msup</c> superscript token.
    /// 建立 MathML <c>msup</c> 上標 token。
    /// </summary>
    /// <param name="baseToken">The base token. / 底數 token。</param>
    /// <param name="scriptToken">The superscript token. / 上標 token。</param>
    /// <returns>A new <see cref="OdfMathToken"/>. / 新的 <see cref="OdfMathToken"/>。</returns>
    public static OdfMathToken Superscript(OdfMathToken baseToken, OdfMathToken scriptToken)
    {
        RequireNotNull(baseToken, nameof(baseToken));
        RequireNotNull(scriptToken, nameof(scriptToken));
        return new OdfMathToken(OdfMathTokenKind.Superscript, string.Empty, baseToken, scriptToken);
    }

    /// <summary>
    /// Creates a MathML <c>msub</c> subscript token.
    /// 建立 MathML <c>msub</c> 下標 token。
    /// </summary>
    /// <param name="baseToken">The base token. / 底數 token。</param>
    /// <param name="scriptToken">The subscript token. / 下標 token。</param>
    /// <returns>A new <see cref="OdfMathToken"/>. / 新的 <see cref="OdfMathToken"/>。</returns>
    public static OdfMathToken Subscript(OdfMathToken baseToken, OdfMathToken scriptToken)
    {
        RequireNotNull(baseToken, nameof(baseToken));
        RequireNotNull(scriptToken, nameof(scriptToken));
        return new OdfMathToken(OdfMathTokenKind.Subscript, string.Empty, baseToken, scriptToken);
    }

    /// <summary>
    /// Creates a MathML <c>mfrac</c> fraction token.
    /// 建立 MathML <c>mfrac</c> 分數 token。
    /// </summary>
    /// <param name="numerator">The numerator token. / 分子 token。</param>
    /// <param name="denominator">The denominator token. / 分母 token。</param>
    /// <returns>A new <see cref="OdfMathToken"/>. / 新的 <see cref="OdfMathToken"/>。</returns>
    public static OdfMathToken Fraction(OdfMathToken numerator, OdfMathToken denominator)
    {
        RequireNotNull(numerator, nameof(numerator));
        RequireNotNull(denominator, nameof(denominator));
        return new OdfMathToken(OdfMathTokenKind.Fraction, string.Empty, numerator, denominator);
    }

    /// <summary>
    /// Creates a MathML <c>msqrt</c> or <c>mroot</c> radical token.
    /// 建立 MathML <c>msqrt</c>（無索引）或 <c>mroot</c>（具索引）根號 token。
    /// </summary>
    /// <param name="radicand">The radicand token. / 被開方數 token。</param>
    /// <param name="index">The optional root index token; <see langword="null"/> means square root. / 選用的根指數 token；<see langword="null"/> 表示平方根。</param>
    /// <returns>A new <see cref="OdfMathToken"/>. / 新的 <see cref="OdfMathToken"/>。</returns>
    public static OdfMathToken Radical(OdfMathToken radicand, OdfMathToken? index = null)
    {
        RequireNotNull(radicand, nameof(radicand));
        return new OdfMathToken(OdfMathTokenKind.Radical, string.Empty, radicand, index);
    }

    /// <summary>
    /// Creates a MathML <c>mrow</c> row token.
    /// 建立 MathML <c>mrow</c> 群組列 token。
    /// </summary>
    /// <param name="children">The child token list in the row. / 群組中的子 token 清單。</param>
    /// <returns>A new <see cref="OdfMathToken"/>. / 新的 <see cref="OdfMathToken"/>。</returns>
    public static OdfMathToken Row(params OdfMathToken[] children) =>
        new(OdfMathTokenKind.Row, string.Empty, null, null, RequireChildren(children, nameof(children)));

    /// <summary>
    /// Creates a MathML <c>mtable</c> matrix token.
    /// 建立 MathML <c>mtable</c> 矩陣 token。
    /// </summary>
    /// <param name="rows">The matrix rows, each created by <see cref="Row(OdfMathToken[])"/>. / 矩陣的每一列，須為 <see cref="Row(OdfMathToken[])"/> 所建立的 token。</param>
    /// <returns>A new <see cref="OdfMathToken"/>. / 新的 <see cref="OdfMathToken"/>。</returns>
    public static OdfMathToken Matrix(params OdfMathToken[] rows) =>
        new(OdfMathTokenKind.Matrix, string.Empty, null, null, RequireChildren(rows, nameof(rows)));

    /// <summary>
    /// Creates a MathML <c>munder</c> underscript token.
    /// 建立 MathML <c>munder</c> 下方標記 token。
    /// </summary>
    /// <param name="baseToken">The base token. / 底數 token。</param>
    /// <param name="under">The underscript token. / 下方標記 token。</param>
    /// <returns>A new <see cref="OdfMathToken"/>. / 新的 <see cref="OdfMathToken"/>。</returns>
    public static OdfMathToken Under(OdfMathToken baseToken, OdfMathToken under)
    {
        RequireNotNull(baseToken, nameof(baseToken));
        RequireNotNull(under, nameof(under));
        return new OdfMathToken(OdfMathTokenKind.Under, string.Empty, baseToken, under);
    }

    /// <summary>
    /// Creates a MathML <c>mover</c> overscript token.
    /// 建立 MathML <c>mover</c> 上方標記 token。
    /// </summary>
    /// <param name="baseToken">The base token. / 底數 token。</param>
    /// <param name="over">The overscript token. / 上方標記 token。</param>
    /// <returns>A new <see cref="OdfMathToken"/>. / 新的 <see cref="OdfMathToken"/>。</returns>
    public static OdfMathToken Over(OdfMathToken baseToken, OdfMathToken over)
    {
        RequireNotNull(baseToken, nameof(baseToken));
        RequireNotNull(over, nameof(over));
        return new OdfMathToken(OdfMathTokenKind.Over, string.Empty, baseToken, over);
    }

    /// <summary>
    /// Creates a MathML <c>munderover</c> under-over token.
    /// 建立 MathML <c>munderover</c> 上下方標記 token。
    /// </summary>
    /// <param name="baseToken">The base token. / 底數 token。</param>
    /// <param name="under">The underscript token. / 下方標記 token。</param>
    /// <param name="over">The overscript token. / 上方標記 token。</param>
    /// <returns>A new <see cref="OdfMathToken"/>. / 新的 <see cref="OdfMathToken"/>。</returns>
    public static OdfMathToken UnderOver(OdfMathToken baseToken, OdfMathToken under, OdfMathToken over)
    {
        RequireNotNull(baseToken, nameof(baseToken));
        RequireNotNull(under, nameof(under));
        RequireNotNull(over, nameof(over));
        return new OdfMathToken(OdfMathTokenKind.UnderOver, string.Empty, null, null, [baseToken, under, over]);
    }

    /// <summary>
    /// Creates a fenced group token serialized as <c>mrow</c> with leading and trailing <c>mo</c> delimiters.
    /// 建立以括號包圍內容的群組 token（序列化為 <c>mrow</c> 搭配前後 <c>mo</c> 分隔符號）。
    /// </summary>
    /// <param name="inner">The token content inside the delimiters. / 括號內的內容 token。</param>
    /// <param name="open">The opening delimiter text; defaults to <c>(</c>. / 開括號文字，預設為 <c>(</c>。</param>
    /// <param name="close">The closing delimiter text; defaults to <c>)</c>. / 閉括號文字，預設為 <c>)</c>。</param>
    /// <returns>A new <see cref="OdfMathToken"/>. / 新的 <see cref="OdfMathToken"/>。</returns>
    public static OdfMathToken Fenced(OdfMathToken inner, string open = "(", string close = ")")
    {
        RequireNotNull(inner, nameof(inner));
        return new OdfMathToken(OdfMathTokenKind.Fenced, $"{open}|{close}", inner, null);
    }

    /// <summary>
    /// Creates a MathML <c>mstyle</c> style group token.
    /// 建立 MathML <c>mstyle</c> 樣式群組 token。
    /// </summary>
    /// <param name="inner">The style group content token. / 樣式群組的內容 token。</param>
    /// <param name="displayStyle">The optional <c>displaystyle</c> setting. / 選用的 <c>displaystyle</c> 設定。</param>
    /// <returns>A new <see cref="OdfMathToken"/>. / 新的 <see cref="OdfMathToken"/>。</returns>
    public static OdfMathToken Style(OdfMathToken inner, bool? displayStyle = null)
    {
        RequireNotNull(inner, nameof(inner));
        string text = displayStyle.HasValue ? (displayStyle.Value ? "true" : "false") : string.Empty;
        return new OdfMathToken(OdfMathTokenKind.Style, text, inner, null);
    }

    /// <summary>
    /// Creates a basic Content MathML <c>apply</c> semantic token.
    /// 建立 Content MathML <c>apply</c> 語意標記 token（基礎支援）。
    /// </summary>
    /// <param name="operatorName">The operator name, such as <c>plus</c>, <c>times</c>, or <c>eq</c>; it is serialized as the corresponding empty element. / 運算子名稱，例如 <c>plus</c>、<c>times</c> 或 <c>eq</c>；序列化為對應的空元素。</param>
    /// <param name="operands">The operand token list. / 運算元 token 清單。</param>
    /// <returns>A new <see cref="OdfMathToken"/>. / 新的 <see cref="OdfMathToken"/>。</returns>
    /// <exception cref="ArgumentException">When <paramref name="operatorName"/> is blank or <paramref name="operands"/> is empty. / 當 <paramref name="operatorName"/> 為空白，或 <paramref name="operands"/> 為空時擲出。</exception>
    public static OdfMathToken Apply(string operatorName, params OdfMathToken[] operands)
    {
        if (string.IsNullOrWhiteSpace(operatorName))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfMathToken_PropertyCannotBeEmpty"), nameof(operatorName));
        }

        return new OdfMathToken(OdfMathTokenKind.Apply, operatorName, null, null, RequireChildren(operands, nameof(operands)));
    }

    /// <summary>
    /// Gets the numerator of a <see cref="OdfMathTokenKind.Fraction"/> token (the <c>mfrac</c> first child).
    /// 取得分數（<see cref="OdfMathTokenKind.Fraction"/>，對應 <c>mfrac</c>）token 的分子。
    /// </summary>
    /// <exception cref="InvalidOperationException">When this token is not a Fraction. / 當此 token 不是分數時擲出。</exception>
    public OdfMathToken Numerator
    {
        get
        {
            RequireKind(OdfMathTokenKind.Fraction);
            return Base!;
        }
    }

    /// <summary>
    /// Gets the denominator of a <see cref="OdfMathTokenKind.Fraction"/> token (the <c>mfrac</c> second child).
    /// 取得分數（<see cref="OdfMathTokenKind.Fraction"/>，對應 <c>mfrac</c>）token 的分母。
    /// </summary>
    /// <exception cref="InvalidOperationException">When this token is not a Fraction. / 當此 token 不是分數時擲出。</exception>
    public OdfMathToken Denominator
    {
        get
        {
            RequireKind(OdfMathTokenKind.Fraction);
            return Script!;
        }
    }

    /// <summary>
    /// Returns a new Fraction token with the numerator replaced (the original token is not modified).
    /// 回傳替換分子後的新分數 token（原 token 不會被修改）。
    /// </summary>
    /// <param name="numerator">The new numerator token. / 新的分子 token。</param>
    /// <returns>The new <see cref="OdfMathToken"/> with the replaced numerator. / 替換分子後的新 <see cref="OdfMathToken"/>。</returns>
    /// <exception cref="InvalidOperationException">When this token is not a Fraction. / 當此 token 不是分數時擲出。</exception>
    /// <exception cref="ArgumentNullException">When <paramref name="numerator"/> is null. / 當 <paramref name="numerator"/> 為 null 時擲出。</exception>
    public OdfMathToken WithNumerator(OdfMathToken numerator)
    {
        RequireKind(OdfMathTokenKind.Fraction);
        RequireNotNull(numerator, nameof(numerator));
        return Fraction(numerator, Denominator);
    }

    /// <summary>
    /// Returns a new Fraction token with the denominator replaced (the original token is not modified).
    /// 回傳替換分母後的新分數 token（原 token 不會被修改）。
    /// </summary>
    /// <param name="denominator">The new denominator token. / 新的分母 token。</param>
    /// <returns>The new <see cref="OdfMathToken"/> with the replaced denominator. / 替換分母後的新 <see cref="OdfMathToken"/>。</returns>
    /// <exception cref="InvalidOperationException">When this token is not a Fraction. / 當此 token 不是分數時擲出。</exception>
    /// <exception cref="ArgumentNullException">When <paramref name="denominator"/> is null. / 當 <paramref name="denominator"/> 為 null 時擲出。</exception>
    public OdfMathToken WithDenominator(OdfMathToken denominator)
    {
        RequireKind(OdfMathTokenKind.Fraction);
        RequireNotNull(denominator, nameof(denominator));
        return Fraction(Numerator, denominator);
    }

    /// <summary>
    /// Gets the radicand of a <see cref="OdfMathTokenKind.Radical"/> token (the value under the root).
    /// 取得根號（<see cref="OdfMathTokenKind.Radical"/>）token 的被開方數。
    /// </summary>
    /// <exception cref="InvalidOperationException">When this token is not a Radical. / 當此 token 不是根號時擲出。</exception>
    public OdfMathToken Radicand
    {
        get
        {
            RequireKind(OdfMathTokenKind.Radical);
            return Base!;
        }
    }

    /// <summary>
    /// Gets the root index of a <see cref="OdfMathTokenKind.Radical"/> token; null for a plain square root.
    /// 取得根號（<see cref="OdfMathTokenKind.Radical"/>）token 的根指數；平方根時為 null。
    /// </summary>
    /// <exception cref="InvalidOperationException">When this token is not a Radical. / 當此 token 不是根號時擲出。</exception>
    public OdfMathToken? RootIndex
    {
        get
        {
            RequireKind(OdfMathTokenKind.Radical);
            return Script;
        }
    }

    /// <summary>
    /// Returns a new Radical token with the radicand replaced (the original token is not modified).
    /// 回傳替換被開方數後的新根號 token（原 token 不會被修改）。
    /// </summary>
    /// <param name="radicand">The new radicand token. / 新的被開方數 token。</param>
    /// <returns>The new <see cref="OdfMathToken"/> with the replaced radicand. / 替換被開方數後的新 <see cref="OdfMathToken"/>。</returns>
    /// <exception cref="InvalidOperationException">When this token is not a Radical. / 當此 token 不是根號時擲出。</exception>
    /// <exception cref="ArgumentNullException">When <paramref name="radicand"/> is null. / 當 <paramref name="radicand"/> 為 null 時擲出。</exception>
    public OdfMathToken WithRadicand(OdfMathToken radicand)
    {
        RequireKind(OdfMathTokenKind.Radical);
        RequireNotNull(radicand, nameof(radicand));
        return Radical(radicand, RootIndex);
    }

    /// <summary>
    /// Returns a new Radical token with the root index replaced; pass null to turn it back into a plain square root.
    /// 回傳替換根指數後的新根號 token；傳入 null 可還原為平方根（原 token 不會被修改）。
    /// </summary>
    /// <param name="rootIndex">The new root index token, or null for a square root. / 新的根指數 token，或 null 表示平方根。</param>
    /// <returns>The new <see cref="OdfMathToken"/> with the replaced root index. / 替換根指數後的新 <see cref="OdfMathToken"/>。</returns>
    /// <exception cref="InvalidOperationException">When this token is not a Radical. / 當此 token 不是根號時擲出。</exception>
    public OdfMathToken WithRootIndex(OdfMathToken? rootIndex)
    {
        RequireKind(OdfMathTokenKind.Radical);
        return Radical(Radicand, rootIndex);
    }

    /// <summary>
    /// Gets the exponent of a <see cref="OdfMathTokenKind.Superscript"/> token (the <c>msup</c> second child).
    /// 取得上標（<see cref="OdfMathTokenKind.Superscript"/>，對應 <c>msup</c>）token 的指數。
    /// </summary>
    /// <exception cref="InvalidOperationException">When this token is not a Superscript. / 當此 token 不是上標時擲出。</exception>
    public OdfMathToken Exponent
    {
        get
        {
            RequireKind(OdfMathTokenKind.Superscript);
            return Script!;
        }
    }

    /// <summary>
    /// Returns a new Superscript token with the exponent replaced (the original token is not modified).
    /// 回傳替換指數後的新上標 token（原 token 不會被修改）。
    /// </summary>
    /// <param name="exponent">The new exponent token. / 新的指數 token。</param>
    /// <returns>The new <see cref="OdfMathToken"/> with the replaced exponent. / 替換指數後的新 <see cref="OdfMathToken"/>。</returns>
    /// <exception cref="InvalidOperationException">When this token is not a Superscript. / 當此 token 不是上標時擲出。</exception>
    /// <exception cref="ArgumentNullException">When <paramref name="exponent"/> is null. / 當 <paramref name="exponent"/> 為 null 時擲出。</exception>
    public OdfMathToken WithExponent(OdfMathToken exponent)
    {
        RequireKind(OdfMathTokenKind.Superscript);
        RequireNotNull(exponent, nameof(exponent));
        return Superscript(Base!, exponent);
    }

    /// <summary>
    /// Gets the subscript index of a <see cref="OdfMathTokenKind.Subscript"/> token (the <c>msub</c> second child).
    /// 取得下標（<see cref="OdfMathTokenKind.Subscript"/>，對應 <c>msub</c>）token 的下標索引。
    /// </summary>
    /// <exception cref="InvalidOperationException">When this token is not a Subscript. / 當此 token 不是下標時擲出。</exception>
    public OdfMathToken SubscriptIndex
    {
        get
        {
            RequireKind(OdfMathTokenKind.Subscript);
            return Script!;
        }
    }

    /// <summary>
    /// Returns a new Subscript token with the subscript index replaced (the original token is not modified).
    /// 回傳替換下標索引後的新下標 token（原 token 不會被修改）。
    /// </summary>
    /// <param name="subscriptIndex">The new subscript index token. / 新的下標索引 token。</param>
    /// <returns>The new <see cref="OdfMathToken"/> with the replaced subscript index. / 替換下標索引後的新 <see cref="OdfMathToken"/>。</returns>
    /// <exception cref="InvalidOperationException">When this token is not a Subscript. / 當此 token 不是下標時擲出。</exception>
    /// <exception cref="ArgumentNullException">When <paramref name="subscriptIndex"/> is null. / 當 <paramref name="subscriptIndex"/> 為 null 時擲出。</exception>
    public OdfMathToken WithSubscriptIndex(OdfMathToken subscriptIndex)
    {
        RequireKind(OdfMathTokenKind.Subscript);
        RequireNotNull(subscriptIndex, nameof(subscriptIndex));
        return Subscript(Base!, subscriptIndex);
    }

    /// <summary>
    /// Gets the number of rows of a <see cref="OdfMathTokenKind.Matrix"/> token (the <c>mtable</c> row count).
    /// 取得矩陣（<see cref="OdfMathTokenKind.Matrix"/>，對應 <c>mtable</c>）token 的列數。
    /// </summary>
    /// <exception cref="InvalidOperationException">When this token is not a Matrix. / 當此 token 不是矩陣時擲出。</exception>
    public int RowCount
    {
        get
        {
            RequireKind(OdfMathTokenKind.Matrix);
            return Children?.Count ?? 0;
        }
    }

    /// <summary>
    /// Gets the row at the specified index of a Matrix token (a <see cref="OdfMathTokenKind.Row"/> token).
    /// 取得矩陣 token 指定索引的一列（一個 <see cref="OdfMathTokenKind.Row"/> token）。
    /// </summary>
    /// <param name="rowIndex">The zero-based row index. / 以零起始的列索引。</param>
    /// <returns>The row token at the specified index. / 指定索引的列 token。</returns>
    /// <exception cref="InvalidOperationException">When this token is not a Matrix. / 當此 token 不是矩陣時擲出。</exception>
    /// <exception cref="ArgumentOutOfRangeException">When <paramref name="rowIndex"/> is out of range. / 當 <paramref name="rowIndex"/> 超出範圍時擲出。</exception>
    public OdfMathToken GetRow(int rowIndex)
    {
        RequireKind(OdfMathTokenKind.Matrix);
        return GetChild(rowIndex);
    }

    /// <summary>
    /// Gets the cell at the specified row and column of a Matrix token.
    /// 取得矩陣 token 指定列、欄位置的儲存格 token。
    /// </summary>
    /// <param name="rowIndex">The zero-based row index. / 以零起始的列索引。</param>
    /// <param name="columnIndex">The zero-based column index. / 以零起始的欄索引。</param>
    /// <returns>The cell token at the specified position. / 指定位置的儲存格 token。</returns>
    /// <exception cref="InvalidOperationException">When this token is not a Matrix. / 當此 token 不是矩陣時擲出。</exception>
    /// <exception cref="ArgumentOutOfRangeException">When <paramref name="rowIndex"/> or <paramref name="columnIndex"/> is out of range. / 當 <paramref name="rowIndex"/> 或 <paramref name="columnIndex"/> 超出範圍時擲出。</exception>
    public OdfMathToken GetCell(int rowIndex, int columnIndex)
    {
        OdfMathToken row = GetRow(rowIndex);
        return row.GetChild(columnIndex);
    }

    /// <summary>
    /// Returns a new Matrix token with the row at the specified index replaced (the original token is not modified).
    /// 回傳替換指定列後的新矩陣 token（原 token 不會被修改）。
    /// </summary>
    /// <param name="rowIndex">The zero-based row index to replace. / 要替換的以零起始列索引。</param>
    /// <param name="row">The new row token. / 新的列 token。</param>
    /// <returns>The new <see cref="OdfMathToken"/> with the replaced row. / 替換指定列後的新 <see cref="OdfMathToken"/>。</returns>
    /// <exception cref="InvalidOperationException">When this token is not a Matrix. / 當此 token 不是矩陣時擲出。</exception>
    /// <exception cref="ArgumentNullException">When <paramref name="row"/> is null. / 當 <paramref name="row"/> 為 null 時擲出。</exception>
    /// <exception cref="ArgumentOutOfRangeException">When <paramref name="rowIndex"/> is out of range. / 當 <paramref name="rowIndex"/> 超出範圍時擲出。</exception>
    public OdfMathToken WithRow(int rowIndex, OdfMathToken row)
    {
        RequireKind(OdfMathTokenKind.Matrix);
        return WithChild(rowIndex, row);
    }

    /// <summary>
    /// Returns a new Matrix token with the cell at the specified row and column replaced (the original token is not modified).
    /// 回傳替換指定列、欄儲存格後的新矩陣 token（原 token 不會被修改）。
    /// </summary>
    /// <param name="rowIndex">The zero-based row index. / 以零起始的列索引。</param>
    /// <param name="columnIndex">The zero-based column index. / 以零起始的欄索引。</param>
    /// <param name="cell">The new cell token. / 新的儲存格 token。</param>
    /// <returns>The new <see cref="OdfMathToken"/> with the replaced cell. / 替換指定儲存格後的新 <see cref="OdfMathToken"/>。</returns>
    /// <exception cref="InvalidOperationException">When this token is not a Matrix. / 當此 token 不是矩陣時擲出。</exception>
    /// <exception cref="ArgumentNullException">When <paramref name="cell"/> is null. / 當 <paramref name="cell"/> 為 null 時擲出。</exception>
    /// <exception cref="ArgumentOutOfRangeException">When <paramref name="rowIndex"/> or <paramref name="columnIndex"/> is out of range. / 當 <paramref name="rowIndex"/> 或 <paramref name="columnIndex"/> 超出範圍時擲出。</exception>
    public OdfMathToken WithCell(int rowIndex, int columnIndex, OdfMathToken cell)
    {
        OdfMathToken row = GetRow(rowIndex);
        OdfMathToken updatedRow = row.WithChild(columnIndex, cell);
        return WithRow(rowIndex, updatedRow);
    }

    /// <summary>
    /// Returns a new Matrix token with the specified row appended at the end (the original token is not modified).
    /// 回傳在尾端新增一列後的新矩陣 token（原 token 不會被修改）。
    /// </summary>
    /// <param name="row">The row token to append, built with <see cref="Row(OdfMathToken[])"/>. / 要新增的列 token，須以 <see cref="Row(OdfMathToken[])"/> 建立。</param>
    /// <returns>The new <see cref="OdfMathToken"/> with the appended row. / 新增列後的新 <see cref="OdfMathToken"/>。</returns>
    /// <exception cref="InvalidOperationException">When this token is not a Matrix. / 當此 token 不是矩陣時擲出。</exception>
    /// <exception cref="ArgumentNullException">When <paramref name="row"/> is null. / 當 <paramref name="row"/> 為 null 時擲出。</exception>
    public OdfMathToken AddRow(OdfMathToken row)
    {
        RequireKind(OdfMathTokenKind.Matrix);
        RequireNotNull(row, nameof(row));
        var rows = new List<OdfMathToken>(Children!) { row };
        return new OdfMathToken(Kind, Text, Base, Script, rows, Attributes);
    }

    /// <summary>
    /// Returns a new Matrix token with the row at the specified index removed (the original token is not modified).
    /// 回傳移除指定列後的新矩陣 token（原 token 不會被修改）。
    /// </summary>
    /// <param name="rowIndex">The zero-based row index to remove. / 要移除的以零起始列索引。</param>
    /// <returns>The new <see cref="OdfMathToken"/> with the row removed. / 移除指定列後的新 <see cref="OdfMathToken"/>。</returns>
    /// <exception cref="InvalidOperationException">When this token is not a Matrix, or when removing would leave the matrix without any row. / 當此 token 不是矩陣，或移除後矩陣將不剩任何列時擲出。</exception>
    /// <exception cref="ArgumentOutOfRangeException">When <paramref name="rowIndex"/> is out of range. / 當 <paramref name="rowIndex"/> 超出範圍時擲出。</exception>
    public OdfMathToken RemoveRow(int rowIndex)
    {
        RequireKind(OdfMathTokenKind.Matrix);
        int childCount = Children?.Count ?? 0;
        if (rowIndex < 0 || rowIndex >= childCount)
        {
            throw new ArgumentOutOfRangeException(nameof(rowIndex));
        }

        if (childCount <= 1)
        {
            throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_OdfMathToken_SubtokenCannotBeEmpty"));
        }

        var rows = new List<OdfMathToken>(Children!);
        rows.RemoveAt(rowIndex);
        return new OdfMathToken(Kind, Text, Base, Script, rows, Attributes);
    }

    private void RequireKind(OdfMathTokenKind expected)
    {
        if (Kind != expected)
        {
            throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_OdfMathToken_UnexpectedKind", expected, Kind));
        }
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

    private int GetChildCount() =>
        Kind switch
        {
            OdfMathTokenKind.Row or OdfMathTokenKind.Matrix or OdfMathTokenKind.UnderOver or OdfMathTokenKind.Apply
                => Children?.Count ?? 0,
            OdfMathTokenKind.Identifier or OdfMathTokenKind.Number or OdfMathTokenKind.Operator or OdfMathTokenKind.Text
                => 0,
            OdfMathTokenKind.Fenced or OdfMathTokenKind.Style
                => Base is null ? 0 : 1,
            _ => Base is null
                ? 0
                : Script is null ? 1 : 2
        };

    private OdfMathToken GetChild(int index)
    {
        if (Kind is OdfMathTokenKind.Row or OdfMathTokenKind.Matrix or OdfMathTokenKind.UnderOver or OdfMathTokenKind.Apply)
        {
            return Children![index];
        }

        if (index == 0 && Base is not null)
        {
            return Base;
        }

        if (index == 1 && Script is not null)
        {
            return Script;
        }

        throw new ArgumentOutOfRangeException(nameof(index));
    }

    private OdfMathToken ReplaceListChild(int index, OdfMathToken replacement)
    {
        var children = new List<OdfMathToken>(Children!);
        children[index] = replacement;
        return new OdfMathToken(Kind, Text, Base, Script, children, Attributes);
    }

    private OdfMathToken ReplacePairedChild(int index, OdfMathToken replacement)
    {
        OdfMathToken? baseToken = Base;
        OdfMathToken? scriptToken = Script;
        if (index == 0)
        {
            baseToken = replacement;
        }
        else
        {
            scriptToken = replacement;
        }

        return new OdfMathToken(Kind, Text, baseToken, scriptToken, Children, Attributes);
    }
}
