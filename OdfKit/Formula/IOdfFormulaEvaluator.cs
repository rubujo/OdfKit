using OdfKit.Spreadsheet;

namespace OdfKit.Formula
{
    /// <summary>
    /// Represents an evaluator capable of calculating formula results.
    /// </summary>
    public interface IOdfFormulaEvaluator
    {
        /// <summary>
        /// Evaluates a formula string in the context of the spreadsheet.
        /// </summary>
        /// <param name="formula">The normalized Excel-style formula to evaluate (e.g., "SUM(A1:B10)").</param>
        /// <param name="context">The evaluation context providing access to cell and range data.</param>
        /// <returns>The result of the evaluation (double, string, bool, or OdfFormulaError).</returns>
        object Evaluate(string formula, IEvaluationContext context);
    }

    /// <summary>
    /// Provides access to the spreadsheet data required for formula evaluation.
    /// </summary>
    public interface IEvaluationContext
    {
        /// <summary>
        /// Gets the absolute address of the cell currently being evaluated.
        /// Used for circular dependency tracking.
        /// </summary>
        OdfCellAddress CurrentCell { get; }

        /// <summary>
        /// Gets the raw or evaluated value of a single cell.
        /// If the cell has an uncalculated formula, this triggers its evaluation.
        /// </summary>
        object GetCellValue(OdfCellAddress address);

        /// <summary>
        /// Gets the values of a range of cells as a 2D array.
        /// </summary>
        object[,] GetRangeValues(OdfCellRange range);

        /// <summary>
        /// Gets the formula string of a cell (returns null or empty if it's a static value cell).
        /// </summary>
        string? GetCellFormula(OdfCellAddress address);

        /// <summary>
        /// Resolves and returns the value of a named range or named expression.
        /// </summary>
        object GetNamedRangeOrExpressionValue(string name);
    }
}
