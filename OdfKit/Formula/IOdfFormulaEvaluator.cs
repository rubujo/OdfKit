using OdfKit.Spreadsheet;

namespace OdfKit.Formula;

/// <summary>
/// 代表能夠計算公式結果的評估器。
/// </summary>
public interface IOdfFormulaEvaluator
{
    /// <summary>
    /// 在試算表的內容模型中評估公式字串。
    /// </summary>
    /// <param name="formula">要評估的標準化 Excel 樣式公式字串，例如 "SUM(A1:B10)"</param>
    /// <param name="context">提供儲存格與範圍資料存取的評估內容模型</param>
    /// <returns>評估後的結果，其型別可能為 double、string、bool 或 OdfFormulaError</returns>
    object Evaluate(string formula, IEvaluationContext context);
}

/// <summary>
/// 提供公式評估所需的試算表資料存取介面。
/// </summary>
public interface IEvaluationContext
{
    /// <summary>
    /// 取得目前正在評估的儲存格絕對位址。
    /// 用於循環相依性追蹤。
    /// </summary>
    OdfCellAddress CurrentCell { get; }

    /// <summary>
    /// 取得單一儲存格的原始值或已評估的值。
    /// 如果該儲存格包含未計算的公式，這將觸發其評估。
    /// </summary>
    /// <param name="address">儲存格位址</param>
    /// <returns>儲存格的值</returns>
    object GetCellValue(OdfCellAddress address);

    /// <summary>
    /// 以二維陣列形式取得儲存格範圍的值。
    /// </summary>
    /// <param name="range">儲存格範圍</param>
    /// <returns>包含範圍內儲存格值的二維陣列</returns>
    object[,] GetRangeValues(OdfCellRange range);

    /// <summary>
    /// 取得儲存格的公式字串，若為靜態值儲存格則傳回 null 或空字串。
    /// </summary>
    /// <param name="address">儲存格位址</param>
    /// <returns>公式字串，或為 null</returns>
    string? GetCellFormula(OdfCellAddress address);

    /// <summary>
    /// 解析並傳回具名範圍或具名運算式的值。
    /// </summary>
    /// <param name="name">具名範圍或具名運算式的名稱</param>
    /// <returns>具名範圍或運算式的值</returns>
    object GetNamedRangeOrExpressionValue(string name);
}
