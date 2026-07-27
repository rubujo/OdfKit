using System;
using System.Collections.Generic;
using System.Linq;

using OdfKit.DOM;

namespace OdfKit.Formula;
/// <summary>
/// Provides the OdfFormulaDocument API.
/// 提供 OdfFormulaDocument API。
/// </summary>

public partial class OdfFormulaDocument
{
    /// <summary>
    /// LaTeX 來源標註使用的 MathML <c>annotation</c> 編碼，與既有的 LaTeX/MathML 工具
    /// （例如 MathJax、KaTeX）所採用的慣例一致。
    /// </summary>
    private const string LatexAnnotationEncoding = "application/x-tex";

    /// <summary>
    /// Gets the MathML XML string.
    /// 取得 MathML 的 XML 字串。
    /// </summary>
    /// <returns>The MathML XML string. / MathML XML 字串。</returns>
    public string GetMathML() => MathMlXml;

    /// <summary>
    /// Gets the recognizable token summary list in the current MathML row.
    /// 取得目前 MathML row 中可辨識的 token 摘要清單。
    /// </summary>
    /// <returns>The MathML token list. / MathML token 清單。</returns>
    public IReadOnlyList<OdfMathToken> GetMathTokens() => ReadMathTokens();

    /// <summary>
    /// Creates and loads an <see cref="OdfFormulaDocument"/> from the specified LaTeX formula string.
    /// 從指定的 LaTeX 公式字串建立並載入 <see cref="OdfFormulaDocument"/>。
    /// </summary>
    /// <param name="latex">The LaTeX formula string. / LaTeX 公式字串。</param>
    /// <returns>The <see cref="OdfFormulaDocument"/> instance loaded with the LaTeX formula. / 已載入 LaTeX 公式的 <see cref="OdfFormulaDocument"/> 執行個體。</returns>
    /// <exception cref="ArgumentNullException">When <paramref name="latex"/> is <see langword="null"/>. / 當 <paramref name="latex"/> 為 <see langword="null"/> 時擲出。</exception>
    /// <exception cref="ArgumentException">When the LaTeX formula syntax is invalid. / 當 LaTeX 公式語法錯誤時擲出。</exception>
    public static OdfFormulaDocument FromLatex(string latex)
    {
        var doc = Create();
        doc.LoadFromLatex(latex);
        return doc;
    }

    /// <summary>
    /// Compiles the specified LaTeX formula string to MathML and loads it into the current formula document.
    /// 將指定的 LaTeX 公式字串編譯為 MathML 並載入到目前的公式文件中。
    /// </summary>
    /// <param name="latex">The LaTeX formula string. / LaTeX 公式字串。</param>
    /// <exception cref="ArgumentNullException">When <paramref name="latex"/> is <see langword="null"/>. / 當 <paramref name="latex"/> 為 <see langword="null"/> 時擲出。</exception>
    /// <exception cref="ArgumentException">When the LaTeX formula syntax is invalid. / 當 LaTeX 公式語法錯誤時擲出。</exception>
    public void LoadFromLatex(string latex)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(latex, nameof(latex));
        var xml = OdfFormulaLatexConverter.Convert(latex);
        SetMathMl(xml);
        SetAnnotation(LatexAnnotationEncoding, latex);
    }

    /// <summary>
    /// Converts the current MathML formula content back to a LaTeX formula string.
    /// 將目前 MathML 公式內容反向轉換為 LaTeX 公式字串。若公式以
    /// <see cref="LoadFromLatex"/>／<see cref="FromLatex"/> 建立（或曾以
    /// <see cref="SetAnnotation"/> 附加 <c>application/x-tex</c> 標註），會優先傳回該原始
    /// LaTeX 來源以達成精確往返；否則改採 best-effort 由 MathML token 重建（因 LaTeX 與
    /// MathML 並非一對一對應，部分語意可能無法完整保留）。
    /// </summary>
    /// <returns>The LaTeX formula string. / LaTeX 公式字串。</returns>
    public string ToLatex() => FindAnnotation(LatexAnnotationEncoding) ?? OdfFormulaLatexConverter.ToLatex(GetMathTokens());

    /// <summary>
    /// Creates and loads an <see cref="OdfFormulaDocument"/> by using an <see cref="OdfMathBuilder"/> composition delegate.
    /// 使用 <see cref="OdfMathBuilder"/> 組合委派建立並載入 <see cref="OdfFormulaDocument"/>。
    /// </summary>
    /// <param name="build">The delegate used to compose the MathML token tree. / 用於組合 MathML token 樹狀結構的委派。</param>
    /// <returns>The <see cref="OdfFormulaDocument"/> instance loaded with the composed result. / 已載入組合結果的 <see cref="OdfFormulaDocument"/> 執行個體。</returns>
    /// <exception cref="ArgumentNullException">When <paramref name="build"/> is <see langword="null"/>. / 當 <paramref name="build"/> 為 <see langword="null"/> 時擲出。</exception>
    public static OdfFormulaDocument FromBuilder(Action<OdfMathBuilder> build)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(build, nameof(build));

        var mathBuilder = new OdfMathBuilder();
        build(mathBuilder);
        OdfMathToken root = mathBuilder.Build();

        OdfFormulaDocument doc = Create();
        doc.SetMathRow(root);
        return doc;
    }

    /// <summary>
    /// Replaces the first token of the specified kind in the current formula tree.
    /// 將目前公式樹中第一個符合種類的 token 替換為指定 token。
    /// </summary>
    /// <param name="kind">The target token kind. / 目標 token 種類。</param>
    /// <param name="replacement">The replacement token. / 替換後的新 token。</param>
    /// <returns><see langword="true"/> if a token was replaced; <see langword="false"/> when no target token was found. / 若成功替換則為 <see langword="true"/>；找不到目標時為 <see langword="false"/>。</returns>
    /// <exception cref="ArgumentNullException">When <paramref name="replacement"/> is <see langword="null"/>. / 當 <paramref name="replacement"/> 為 <see langword="null"/> 時擲出。</exception>
    public bool ReplaceFirst(OdfMathTokenKind kind, OdfMathToken replacement)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(replacement, nameof(replacement));

        System.Collections.ObjectModel.ReadOnlyCollection<OdfMathToken> tokens = ReadMathTokens();
        if (tokens.Count == 0)
        {
            return false;
        }

        var rewritten = new OdfMathToken[tokens.Count];
        for (int index = 0; index < tokens.Count; index++)
        {
            rewritten[index] = tokens[index];
        }

        for (int index = 0; index < rewritten.Length; index++)
        {
            OdfMathToken root = rewritten[index];
            if (root.FindFirst(kind) is null)
            {
                continue;
            }

            rewritten[index] = root.ReplaceFirst(kind, replacement);
            SetMathRow(rewritten);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Removes the first token of the specified kind from the current formula tree.
    /// 從目前公式樹移除第一個符合指定種類的 token。
    /// </summary>
    /// <param name="kind">The target token kind. / 目標 token 種類。</param>
    /// <returns><see langword="true"/> if a token was removed; otherwise <see langword="false"/>. / 若成功移除 token 則為 <see langword="true"/>；否則為 <see langword="false"/>。</returns>
    /// <remarks>
    /// Removing a required child of a composite MathML construct leaves an empty row in that slot so the containing construct remains structurally valid.
    /// 若移除的是 MathML 複合結構的必要子節點，該位置會保留空白 row，使外層結構維持有效。
    /// </remarks>
    public bool RemoveFirst(OdfMathTokenKind kind)
    {
        System.Collections.ObjectModel.ReadOnlyCollection<OdfMathToken> tokens = ReadMathTokens();
        for (int index = 0; index < tokens.Count; index++)
        {
            OdfMathToken root = tokens[index];
            if (root.Kind == kind)
            {
                var rewritten = new List<OdfMathToken>(tokens);
                rewritten.RemoveAt(index);
                if (rewritten.Count == 0)
                {
                    ClearMathTokens();
                }
                else
                {
                    SetMathRow(rewritten.ToArray());
                }

                return true;
            }

            OdfMathToken? target = root.FindFirst(kind);
            if (target is null)
            {
                continue;
            }

            OdfMathToken rewrittenRoot = root.ReplaceFirst(
                token => ReferenceEquals(token, target),
                _ => CreateEmptyRow());
            var rewrittenTokens = new OdfMathToken[tokens.Count];
            for (int tokenIndex = 0; tokenIndex < tokens.Count; tokenIndex++)
            {
                rewrittenTokens[tokenIndex] = tokenIndex == index ? rewrittenRoot : tokens[tokenIndex];
            }

            SetMathRow(rewrittenTokens);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Replaces every token of the specified kind in the current formula tree.
    /// 替換目前公式樹中所有指定種類的 token。
    /// </summary>
    /// <param name="kind">The target token kind. / 目標 token 種類。</param>
    /// <param name="replacement">The replacement token. / 替換後的新 token。</param>
    /// <returns>The number of replaced tokens. / 已替換的 token 數量。</returns>
    /// <remarks>
    /// Matching is evaluated against the original tree; newly inserted replacement subtrees are not searched again.
    /// 比對以原始樹為準；不會再次搜尋新插入的替換子樹。
    /// </remarks>
    /// <exception cref="ArgumentNullException">When <paramref name="replacement"/> is <see langword="null"/>. / 當 <paramref name="replacement"/> 為 <see langword="null"/> 時擲出。</exception>
    public int ReplaceAll(OdfMathTokenKind kind, OdfMathToken replacement)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(replacement, nameof(replacement));

        return ReplaceAll(token => token.Kind == kind, _ => replacement);
    }

    /// <summary>
    /// Replaces every token matching the specified predicate in the current formula tree.
    /// 替換目前公式樹中所有符合指定條件的 token。
    /// </summary>
    /// <param name="predicate">The delegate that selects replacement targets. / 選取替換目標的委派。</param>
    /// <param name="replacementFactory">The delegate that creates a replacement from each matched token. / 根據每個命中 token 建立替換 token 的委派。</param>
    /// <returns>The number of replaced tokens. / 已替換的 token 數量。</returns>
    /// <remarks>
    /// Matching is evaluated against the original tree; newly inserted replacement subtrees are not searched again.
    /// 比對以原始樹為準；不會再次搜尋新插入的替換子樹。
    /// </remarks>
    /// <exception cref="ArgumentNullException">When <paramref name="predicate"/> or <paramref name="replacementFactory"/> is <see langword="null"/>, or the factory returns <see langword="null"/>. / 當 <paramref name="predicate"/> 或 <paramref name="replacementFactory"/> 為 <see langword="null"/>，或 factory 回傳 <see langword="null"/> 時擲出。</exception>
    public int ReplaceAll(
        Func<OdfMathToken, bool> predicate,
        Func<OdfMathToken, OdfMathToken> replacementFactory)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(predicate, nameof(predicate));

        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(replacementFactory, nameof(replacementFactory));

        System.Collections.ObjectModel.ReadOnlyCollection<OdfMathToken> tokens = ReadMathTokens();
        int replacedCount = 0;
        var rewritten = new OdfMathToken[tokens.Count];
        for (int index = 0; index < tokens.Count; index++)
        {
            OdfMathToken token = tokens[index];
            rewritten[index] = token.ReplaceAll(
                predicate,
                matched =>
                {
                    replacedCount++;
                    return replacementFactory(matched);
                });
        }

        if (replacedCount > 0)
        {
            SetMathRow(rewritten);
        }

        return replacedCount;
    }

    /// <summary>
    /// Removes every token of the specified kind from the current formula tree.
    /// 從目前公式樹移除所有指定種類的 token。
    /// </summary>
    /// <param name="kind">The target token kind. / 目標 token 種類。</param>
    /// <returns>The number of removed tokens. / 已移除的 token 數量。</returns>
    /// <remarks>
    /// Required children of composite MathML constructs are replaced with empty rows so their containers remain structurally valid.
    /// MathML 複合結構的必要子節點會替換為空白 row，使其容器維持結構有效。
    /// </remarks>
    public int RemoveAll(OdfMathTokenKind kind)
    {
        return RemoveAll(token => token.Kind == kind);
    }

    /// <summary>
    /// Removes every token matching the specified predicate from the current formula tree.
    /// 從目前公式樹移除所有符合指定條件的 token。
    /// </summary>
    /// <param name="predicate">The delegate that selects removal targets. / 選取移除目標的委派。</param>
    /// <returns>The number of matched tokens removed from the original tree. / 從原始樹移除的符合 token 數量。</returns>
    /// <remarks>
    /// Required children of composite MathML constructs are replaced with empty rows so their containers remain structurally valid.
    /// MathML 複合結構的必要子節點會替換為空白 row，使其容器維持結構有效。
    /// </remarks>
    /// <exception cref="ArgumentNullException">When <paramref name="predicate"/> is <see langword="null"/>. / 當 <paramref name="predicate"/> 為 <see langword="null"/> 時擲出。</exception>
    public int RemoveAll(Func<OdfMathToken, bool> predicate)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(predicate, nameof(predicate));

        IReadOnlyList<OdfMathToken> tokens = ReadMathTokens();
        int removedCount = 0;
        List<OdfMathToken> rewritten = [];
        foreach (OdfMathToken token in tokens)
        {
            List<OdfMathToken> matches = token.GetAll(predicate).ToList();
            if (matches.Count == 0)
            {
                rewritten.Add(token);
                continue;
            }

            removedCount += matches.Count;
            if (ReferenceEquals(matches[0], token))
            {
                continue;
            }

            var targets = new HashSet<OdfMathToken>(matches);
            rewritten.Add(token.ReplaceAll(targets.Contains, _ => CreateEmptyRow()));
        }

        if (removedCount == 0)
        {
            return 0;
        }

        if (rewritten.Count == 0)
        {
            ClearMathTokens();
        }
        else
        {
            SetMathRow(rewritten.ToArray());
        }

        return removedCount;
    }

    private static OdfMathToken CreateEmptyRow() =>
        OdfMathToken.Row(OdfMathToken.TextToken(string.Empty));

    /// <summary>
    /// Clears all presentation tokens while retaining a valid MathML row.
    /// 清除所有呈現 token，同時保留有效的 MathML row。
    /// </summary>
    public void ClearMathTokens()
    {
        OdfNode math = OdfNodeFactory.CreateElement("math", MathMlNamespace, "math");
        math.AppendChild(OdfNodeFactory.CreateElement("mrow", MathMlNamespace, "math"));

        OdfNode formula = GetFormulaNode();
        foreach (OdfNode child in new List<OdfNode>(formula.Children))
        {
            formula.RemoveChild(child);
        }

        formula.AppendChild(math);
    }
}
