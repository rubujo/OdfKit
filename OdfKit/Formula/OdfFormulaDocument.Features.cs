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
}
