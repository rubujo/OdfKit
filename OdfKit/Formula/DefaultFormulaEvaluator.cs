using System;
using System.Collections.Generic;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Formula.AST;
using OdfKit.Spreadsheet;

namespace OdfKit.Formula;

/// <summary>
/// Evaluates parsed ODF formulas with the default spreadsheet function set.
/// 提供預設的 ODF 公式評估器實作。
/// </summary>
public partial class DefaultFormulaEvaluator : IOdfFormulaEvaluator
{
    private readonly Dictionary<OdfCellAddress, object> _cache = new();
    private readonly HashSet<OdfCellAddress> _evaluatingStack = new();

    // 以公式字串為鍵的剖析樹快取；AST 節點於剖析後不可變，可安全跨次評估重用。
    private readonly Dictionary<string, AstNode> _astCache = new(StringComparer.Ordinal);

    // 剖析樹快取的筆數上限；達到上限時整批清除，避免長時間執行時無上限累積。
    private const int AstCacheCapacity = 4096;

    /// <summary>
    /// Initializes an evaluator with an empty application-defined function registry.
    /// 使用空白的應用程式自訂函式註冊表初始化評估器。
    /// </summary>
    public DefaultFormulaEvaluator() : this(new OdfFormulaFunctionRegistry(), null)
    {
    }

    /// <summary>
    /// Initializes an evaluator with application-defined functions.
    /// 使用應用程式自訂函式初始化評估器。
    /// </summary>
    /// <param name="functions">The instance-scoped function registry. / 執行個體範圍的函式註冊表。</param>
    public DefaultFormulaEvaluator(OdfFormulaFunctionRegistry functions) : this(functions, null)
    {
    }

    /// <summary>
    /// Initializes an evaluator with application-defined functions and an unsupported-formula fallback.
    /// 使用應用程式自訂函式與不受支援公式的後援初始化評估器。
    /// </summary>
    /// <param name="functions">The instance-scoped function registry. / 執行個體範圍的函式註冊表。</param>
    /// <param name="fallback">The unsupported-formula fallback, or <see langword="null"/>. / 不受支援公式的後援，或為 <see langword="null"/>。</param>
    public DefaultFormulaEvaluator(
        OdfFormulaFunctionRegistry functions,
        IOdfFormulaEvaluationFallback? fallback)
    {
        Functions = functions ?? throw new ArgumentNullException(
            nameof(functions),
            OdfLocalizer.GetMessage("Err_DefaultFormulaEvaluator_FunctionRegistryNull"));
        Fallback = fallback;
    }

    /// <summary>
    /// Gets the instance-scoped application-defined function registry.
    /// 取得執行個體範圍的應用程式自訂函式註冊表。
    /// </summary>
    public OdfFormulaFunctionRegistry Functions { get; }

    /// <summary>
    /// Gets the fallback used when the in-process evaluator returns an unsupported-name error.
    /// 取得處理程序內評估器傳回不受支援名稱錯誤時使用的後援。
    /// </summary>
    public IOdfFormulaEvaluationFallback? Fallback { get; }

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
        OdfCellAddress oldCell = default;
        if (domCtx is not null)
        {
            oldCell = domCtx.CurrentCell;
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
            if (domCtx is not null)
            {
                domCtx.CurrentCell = oldCell;
            }
        }
    }

    /// <summary>
    /// Evaluates a formula string and returns the result.
    /// 評估公式字串並傳回結果。
    /// </summary>
    /// <remarks>
    /// Parsed syntax trees are cached by formula text, so repeated evaluation of the same formula skips tokenizing and parsing.
    /// 剖析後的語法樹會以公式字串為鍵快取，重複評估相同公式時可略過語彙分析與剖析階段。
    /// </remarks>
    /// <param name="formula">The formula string. / 公式字串。</param>
    /// <param name="context">The evaluation context. / 評估內容模型。</param>
    /// <returns>The formula calculation result. / 公式計算後的結果。</returns>
    public object Evaluate(string formula, IEvaluationContext context)
    {
        try
        {
            if (formula.StartsWith("oooc:=", StringComparison.OrdinalIgnoreCase) ||
                formula.StartsWith("of:=", StringComparison.OrdinalIgnoreCase))
            {
                formula = OdfFormulaTranslator.OdfToExcelFormula(formula);
            }

            formula = FormulaPrefixNormalizer.RemovePrefix(formula);
            if (!_astCache.TryGetValue(formula, out var ast))
            {
                var parser = new FormulaParser(formula);
                ast = parser.Parse();
                if (_astCache.Count >= AstCacheCapacity)
                {
                    _astCache.Clear();
                }
                _astCache[formula] = ast;
            }

            var dispatchContext = new OdfFormulaDispatchContext(context, Functions);
            object result = ast.Evaluate(dispatchContext);
            if (result is OdfFormulaError { ErrorType: OdfFormulaErrorType.Name } &&
                Fallback is not null &&
                Fallback.TryEvaluate(formula, context, out object fallbackResult))
            {
                return fallbackResult;
            }

            return result;
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
