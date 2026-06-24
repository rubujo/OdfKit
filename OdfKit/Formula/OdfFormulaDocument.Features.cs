using System;
using System.Collections.Generic;

namespace OdfKit.Formula;

public partial class OdfFormulaDocument
{
    /// <summary>
    /// LaTeX 來源標註使用的 MathML <c>annotation</c> 編碼，與既有的 LaTeX/MathML 工具
    /// （例如 MathJax、KaTeX）所採用的慣例一致。
    /// </summary>
    private const string LatexAnnotationEncoding = "application/x-tex";

    /// <summary>
    /// 取得 MathML 的 XML 字串。
    /// </summary>
    /// <returns>MathML XML 字串</returns>
    public string GetMathML() => MathMlXml;

    /// <summary>
    /// 取得目前 MathML row 中可辨識的 token 摘要清單。
    /// </summary>
    /// <returns>MathML token 清單</returns>
    public IReadOnlyList<OdfMathToken> GetMathTokens() => ReadMathTokens();

    /// <summary>
    /// 從指定的 LaTeX 公式字串建立並載入 <see cref="OdfFormulaDocument"/>。
    /// </summary>
    /// <param name="latex">LaTeX 公式字串</param>
    /// <returns>已載入 LaTeX 公式的 <see cref="OdfFormulaDocument"/> 執行個體</returns>
    /// <exception cref="ArgumentNullException">當 <paramref name="latex"/> 為 <see langword="null"/> 時擲出</exception>
    /// <exception cref="ArgumentException">當 LaTeX 公式語法錯誤時擲出</exception>
    public static OdfFormulaDocument FromLatex(string latex)
    {
        var doc = Create();
        doc.LoadFromLatex(latex);
        return doc;
    }

    /// <summary>
    /// 將指定的 LaTeX 公式字串編譯為 MathML 並載入到目前的公式文件中。
    /// </summary>
    /// <param name="latex">LaTeX 公式字串</param>
    /// <exception cref="ArgumentNullException">當 <paramref name="latex"/> 為 <see langword="null"/> 時擲出</exception>
    /// <exception cref="ArgumentException">當 LaTeX 公式語法錯誤時擲出</exception>
    public void LoadFromLatex(string latex)
    {
        if (latex == null)
        {
            throw new ArgumentNullException(nameof(latex));
        }
        var xml = OdfFormulaLatexConverter.Convert(latex);
        SetMathMl(xml);
        SetAnnotation(LatexAnnotationEncoding, latex);
    }

    /// <summary>
    /// 將目前 MathML 公式內容反向轉換為 LaTeX 公式字串。若公式以
    /// <see cref="LoadFromLatex"/>／<see cref="FromLatex"/> 建立（或曾以
    /// <see cref="SetAnnotation"/> 附加 <c>application/x-tex</c> 標註），會優先傳回該原始
    /// LaTeX 來源以達成精確往返；否則改採 best-effort 由 MathML token 重建（因 LaTeX 與
    /// MathML 並非一對一對應，部分語意可能無法完整保留）。
    /// </summary>
    /// <returns>LaTeX 公式字串</returns>
    public string ToLatex() => GetAnnotation(LatexAnnotationEncoding) ?? OdfFormulaLatexConverter.ToLatex(GetMathTokens());

    /// <summary>
    /// 使用 <see cref="OdfMathBuilder"/> 組合委派建立並載入 <see cref="OdfFormulaDocument"/>。
    /// </summary>
    /// <param name="build">用於組合 MathML token 樹狀結構的委派</param>
    /// <returns>已載入組合結果的 <see cref="OdfFormulaDocument"/> 執行個體</returns>
    /// <exception cref="ArgumentNullException">當 <paramref name="build"/> 為 <see langword="null"/> 時擲出</exception>
    public static OdfFormulaDocument FromBuilder(Action<OdfMathBuilder> build)
    {
        if (build is null)
        {
            throw new ArgumentNullException(nameof(build));
        }

        var mathBuilder = new OdfMathBuilder();
        build(mathBuilder);
        OdfMathToken root = mathBuilder.Build();

        OdfFormulaDocument doc = Create();
        doc.SetMathRow(root);
        return doc;
    }

    /// <summary>
    /// 將目前公式樹中第一個符合種類的 token 替換為指定 token。
    /// </summary>
    /// <param name="kind">目標 token 種類</param>
    /// <param name="replacement">替換後的新 token</param>
    /// <returns>若成功替換則為 <see langword="true"/>；找不到目標時為 <see langword="false"/></returns>
    /// <exception cref="ArgumentNullException">當 <paramref name="replacement"/> 為 <see langword="null"/> 時擲出</exception>
    public bool ReplaceFirst(OdfMathTokenKind kind, OdfMathToken replacement)
    {
        if (replacement is null)
        {
            throw new ArgumentNullException(nameof(replacement));
        }

        IReadOnlyList<OdfMathToken> tokens = ReadMathTokens();
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
            OdfMathToken? target = root.FindFirst(kind);
            if (target is null)
            {
                continue;
            }

            if (ReferenceEquals(root, target))
            {
                rewritten[index] = replacement;
            }
            else if (TryReplaceToken(root, target, replacement, out OdfMathToken updated))
            {
                rewritten[index] = updated;
            }
            else
            {
                continue;
            }

            SetMathRow(rewritten);
            return true;
        }

        return false;
    }

    private static bool TryReplaceToken(
        OdfMathToken current,
        OdfMathToken target,
        OdfMathToken replacement,
        out OdfMathToken updated)
    {
        if (ReferenceEquals(current, target))
        {
            updated = replacement;
            return true;
        }

        int childCount = GetChildCount(current);
        for (int childIndex = 0; childIndex < childCount; childIndex++)
        {
            OdfMathToken child = GetChild(current, childIndex);
            if (!TryReplaceToken(child, target, replacement, out OdfMathToken childUpdated))
            {
                continue;
            }

            updated = current.WithChild(childIndex, childUpdated);
            return true;
        }

        updated = current;
        return false;
    }

    private static int GetChildCount(OdfMathToken token) =>
        token.Kind switch
        {
            OdfMathTokenKind.Row or OdfMathTokenKind.Matrix or OdfMathTokenKind.UnderOver or OdfMathTokenKind.Apply
                => token.Children?.Count ?? 0,
            OdfMathTokenKind.Identifier or OdfMathTokenKind.Number or OdfMathTokenKind.Operator or OdfMathTokenKind.Text
                => 0,
            OdfMathTokenKind.Fenced or OdfMathTokenKind.Style
                => token.Base is null ? 0 : 1,
            _ => token.Base is null
                ? 0
                : token.Script is null ? 1 : 2
        };

    private static OdfMathToken GetChild(OdfMathToken token, int index)
    {
        if (token.Kind is OdfMathTokenKind.Row or OdfMathTokenKind.Matrix or OdfMathTokenKind.UnderOver or OdfMathTokenKind.Apply)
        {
            return token.Children![index];
        }

        if (index == 0 && token.Base is not null)
        {
            return token.Base;
        }

        if (index == 1 && token.Script is not null)
        {
            return token.Script;
        }

        throw new ArgumentOutOfRangeException(nameof(index));
    }
}
