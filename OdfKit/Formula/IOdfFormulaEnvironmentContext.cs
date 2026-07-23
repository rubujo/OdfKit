namespace OdfKit.Formula;

/// <summary>
/// Supplies host-controlled values for the OpenFormula INFO function.
/// 提供由主機控制的 OpenFormula INFO 函式值。
/// </summary>
public interface IOdfFormulaEnvironmentContext : IEvaluationContext
{
    /// <summary>
    /// Attempts to resolve an OpenFormula environment-information category.
    /// 嘗試解析 OpenFormula 環境資訊類別。
    /// </summary>
    /// <param name="category">The case-insensitive INFO category. / 不區分大小寫的 INFO 類別。</param>
    /// <param name="result">The resolved environment value. / 解析後的環境值。</param>
    /// <returns><see langword="true"/> when the category was resolved; otherwise, <see langword="false"/>. / 已解析類別時為 <see langword="true"/>，否則為 <see langword="false"/>。</returns>
    bool TryGetFormulaEnvironmentInfo(string category, out object result);
}
