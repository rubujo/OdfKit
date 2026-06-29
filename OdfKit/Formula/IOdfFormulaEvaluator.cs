using OdfKit.Spreadsheet;

namespace OdfKit.Formula;

/// <summary>
/// Represents an evaluator that can calculate formula results.
/// 代表能夠計算公式結果的評估器。
/// </summary>
public interface IOdfFormulaEvaluator
{
    /// <summary>
    /// Evaluates a formula string within a spreadsheet content model.
    /// 在試算表的內容模型中評估公式字串。
    /// </summary>
    /// <param name="formula">The normalized Excel-style formula string to evaluate, such as "SUM(A1:B10)". / 要評估的標準化 Excel 樣式公式字串，例如 "SUM(A1:B10)"。</param>
    /// <param name="context">The evaluation context that provides cell and range data access. / 提供儲存格與範圍資料存取的評估內容模型。</param>
    /// <returns>The evaluated result, whose type may be double, string, bool, or OdfFormulaError. / 評估後的結果，其型別可能為 double、string、bool 或 OdfFormulaError。</returns>
    object Evaluate(string formula, IEvaluationContext context);
}

/// <summary>
/// Supplies spreadsheet data required by formula evaluation.
/// 提供公式評估所需的試算表資料存取介面。
/// </summary>
public interface IEvaluationContext
{
    /// <summary>
    /// Gets the absolute address of the cell currently being evaluated.
    /// 取得目前正在評估的儲存格絕對位址。
    /// </summary>
    /// <remarks>
    /// This value is used for circular dependency tracking.
    /// 此值用於循環相依性追蹤。
    /// </remarks>
    OdfCellAddress CurrentCell { get; }

    /// <summary>
    /// Gets the raw or evaluated value of a single cell.
    /// 取得單一儲存格的原始值或已評估的值。
    /// </summary>
    /// <remarks>
    /// If the cell contains an unevaluated formula, this call triggers evaluation.
    /// 如果該儲存格包含未計算的公式，這將觸發其評估。
    /// </remarks>
    /// <param name="address">The cell address. / 儲存格位址。</param>
    /// <returns>The cell value. / 儲存格的值。</returns>
    object GetCellValue(OdfCellAddress address);

    /// <summary>
    /// Gets values for a cell range as a two-dimensional array.
    /// 以二維陣列形式取得儲存格範圍的值。
    /// </summary>
    /// <param name="range">The cell range. / 儲存格範圍。</param>
    /// <returns>A two-dimensional array containing the values in the range. / 包含範圍內儲存格值的二維陣列。</returns>
    object[,] GetRangeValues(OdfCellRange range);

    /// <summary>
    /// Gets the formula string for a cell, or null or an empty string for static-value cells.
    /// 取得儲存格的公式字串，若為靜態值儲存格則傳回 null 或空字串。
    /// </summary>
    /// <param name="address">The cell address. / 儲存格位址。</param>
    /// <returns>The formula string, or null. / 公式字串，或為 null。</returns>
    string? GetCellFormula(OdfCellAddress address);

    /// <summary>
    /// Resolves and returns the value of a named range or named expression.
    /// 解析並傳回具名範圍或具名運算式的值。
    /// </summary>
    /// <param name="name">The named range or named expression name. / 具名範圍或具名運算式的名稱。</param>
    /// <returns>The value of the named range or expression. / 具名範圍或運算式的值。</returns>
    object GetNamedRangeOrExpressionValue(string name);
}
