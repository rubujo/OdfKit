using System;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Formula.AST;
using OdfKit.Spreadsheet;

namespace OdfKit.Formula;

/// <summary>
/// Provides the default ODF formula evaluator implementation.
/// 提供預設的 ODF 公式評估器實作。
/// </summary>
public partial class DefaultFormulaEvaluator : IOdfFormulaEvaluator
{
    private readonly Dictionary<OdfCellAddress, object> _cache = new();
    private readonly HashSet<OdfCellAddress> _evaluatingStack = new();

    /// <summary>
    /// Evaluates the formula for a specific cell with circular-reference checks and caching.
    /// 評估特定儲存格的公式，並使用循環參照檢查與快取機制。
    /// </summary>
    /// <param name="cellAddress">The cell address. / 儲存格位址。</param>
    /// <param name="context">The evaluation context. / 評估內容模型。</param>
    /// <returns>The evaluated cell value. / 評估後的儲存格值。</returns>
    public object EvaluateCell(OdfCellAddress cellAddress, IEvaluationContext context)
    {
        if (_cache.TryGetValue(cellAddress, out var cachedValue))
        {
            return cachedValue;
        }

        if (_evaluatingStack.Contains(cellAddress))
        {
            OdfKitDiagnostics.Warn($"Circular dependency detected at cell {cellAddress.ToExcelString()}.");
            return OdfFormulaError.Ref;
        }

        var domCtx = context as OdfDomEvaluationContext;
        var oldCell = domCtx?.CurrentCell;
        if (domCtx != null)
        {
            domCtx.CurrentCell = cellAddress;
        }

        _evaluatingStack.Add(cellAddress);
        try
        {
            string? formula = context.GetCellFormula(cellAddress);
            if (string.IsNullOrEmpty(formula))
            {
                return context.GetCellValue(cellAddress);
            }

            if (formula!.StartsWith("oooc:=", StringComparison.OrdinalIgnoreCase) ||
                formula.StartsWith("of:=", StringComparison.OrdinalIgnoreCase))
            {
                formula = OdfFormulaTranslator.OdfToExcelFormula(formula);
            }

            formula = FormulaPrefixNormalizer.RemovePrefix(formula!);

            object result = Evaluate(formula!, context);
            _cache[cellAddress] = result;
            return result;
        }
        catch (Exception ex)
        {
            OdfKitDiagnostics.Error($"Evaluation failed for cell {cellAddress.ToExcelString()}: {ex.Message}", ex);
            return OdfFormulaError.Value;
        }
        finally
        {
            _evaluatingStack.Remove(cellAddress);
            if (domCtx != null && oldCell.HasValue)
            {
                domCtx.CurrentCell = oldCell.Value;
            }
        }
    }

    /// <summary>
    /// Evaluates a formula string and returns the result.
    /// 評估公式字串並傳回結果。
    /// </summary>
    /// <param name="formula">The formula string. / 公式字串。</param>
    /// <param name="context">The evaluation context. / 評估內容模型。</param>
    /// <returns>The formula calculation result. / 公式計算後的結果。</returns>
    public object Evaluate(string formula, IEvaluationContext context)
    {
        try
        {
            var parser = new FormulaParser(formula);
            var ast = parser.Parse();
            return ast.Evaluate(context);
        }
        catch (Exception ex)
        {
            OdfKitDiagnostics.Warn($"Parser failed on formula '{formula}': {ex.Message}");
            return OdfFormulaError.Value;
        }
    }

    /// <summary>
    /// Clears the evaluation cache and circular-dependency tracking stack.
    /// 清除評估快取與循環相依性追蹤堆疊。
    /// </summary>
    public void ClearCache()
    {
        _cache.Clear();
        _evaluatingStack.Clear();
    }

    internal void SetCachedValue(OdfCellAddress cellAddress, object value)
    {
        _cache[cellAddress] = value;
    }

    /// <summary>
    /// Evaluates all document formulas under the specified content root and updates their display text and attributes.
    /// 評估指定內容根節點下的所有文件公式，並更新其顯示文字與屬性。
    /// </summary>
    /// <param name="contentRoot">The document content root node. / 文件的內容根節點。</param>
    public void EvaluateFormulasInDocument(OdfNode contentRoot)
        => FormulaDocumentEvaluationEngine.EvaluateFormulasInDocument(contentRoot, this);

    /// <summary>
    /// Evaluates all document formulas under the specified content root and resolves cross-document references through an external link manager.
    /// 評估指定內容根節點下的所有文件公式，並使用外部連結管理器解析跨文件參照。
    /// </summary>
    /// <param name="contentRoot">The document content root node. / 文件的內容根節點。</param>
    /// <param name="externalLinks">The external link manager. / 外部連結管理器。</param>
    public void EvaluateFormulasInDocument(OdfNode contentRoot, OdfExternalLinkManager? externalLinks)
        => FormulaDocumentEvaluationEngine.EvaluateFormulasInDocument(contentRoot, this, externalLinks);

    /// <summary>
    /// 評估所有支援之公式函式的中央分派方法。
    /// </summary>
    internal static object EvaluateFunction(string name, List<AstNode> arguments, IEvaluationContext context)
        => FormulaBuiltinFunctionRegistry.Evaluate(name, arguments, context);
}
