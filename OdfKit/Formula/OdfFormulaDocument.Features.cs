using System;
using System.Collections.Generic;

namespace OdfKit.Formula;

public partial class OdfFormulaDocument
{
    /// <summary>
    /// 取得 MathML 的 XML 字串。
    /// </summary>
    /// <returns>MathML XML 字串。</returns>
    public string GetMathML() => MathMlXml;

    /// <summary>
    /// 取得目前 MathML row 中可辨識的 token 摘要清單。
    /// </summary>
    /// <returns>MathML token 清單。</returns>
    public IReadOnlyList<OdfMathToken> GetMathTokens() => ReadMathTokens();

    /// <summary>
    /// 從指定的 LaTeX 公式字串建立並載入 <see cref="OdfFormulaDocument"/>。
    /// </summary>
    /// <param name="latex">LaTeX 公式字串。</param>
    /// <returns>已載入 LaTeX 公式的 <see cref="OdfFormulaDocument"/> 執行個體。</returns>
    /// <exception cref="ArgumentNullException">當 <paramref name="latex"/> 為 <see langword="null"/> 時擲出。</exception>
    /// <exception cref="ArgumentException">當 LaTeX 公式語法錯誤時擲出。</exception>
    public static OdfFormulaDocument FromLatex(string latex)
    {
        var doc = Create();
        doc.LoadFromLatex(latex);
        return doc;
    }

    /// <summary>
    /// 將指定的 LaTeX 公式字串編譯為 MathML 並載入到目前的公式文件中。
    /// </summary>
    /// <param name="latex">LaTeX 公式字串。</param>
    /// <exception cref="ArgumentNullException">當 <paramref name="latex"/> 為 <see langword="null"/> 時擲出。</exception>
    /// <exception cref="ArgumentException">當 LaTeX 公式語法錯誤時擲出。</exception>
    public void LoadFromLatex(string latex)
    {
        if (latex == null)
        {
            throw new ArgumentNullException(nameof(latex));
        }
        var xml = OdfFormulaLatexConverter.Convert(latex);
        SetMathMl(xml);
    }

    /// <summary>
    /// 使用 <see cref="OdfMathBuilder"/> 組合委派建立並載入 <see cref="OdfFormulaDocument"/>。
    /// </summary>
    /// <param name="build">用於組合 MathML token 樹狀結構的委派。</param>
    /// <returns>已載入組合結果的 <see cref="OdfFormulaDocument"/> 執行個體。</returns>
    /// <exception cref="ArgumentNullException">當 <paramref name="build"/> 為 <see langword="null"/> 時擲出。</exception>
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
}
