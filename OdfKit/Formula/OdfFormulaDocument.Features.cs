using System.Collections.Generic;

namespace OdfKit.Formula;

public partial class OdfFormulaDocument
{
    /// <summary>
    /// 取得目前 MathML row 中可辨識的 token 摘要清單。
    /// </summary>
    /// <returns>MathML token 清單。</returns>
    public IReadOnlyList<OdfMathToken> GetMathTokens() => ReadMathTokens();
}
