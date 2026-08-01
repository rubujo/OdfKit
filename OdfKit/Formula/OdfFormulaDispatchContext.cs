using System.Collections.Generic;
using OdfKit.Formula.AST;
using OdfKit.Spreadsheet;

namespace OdfKit.Formula;

internal interface IOdfFormulaFunctionDispatchContext
{
    bool TryEvaluateFunction(string name, List<AstNode> arguments, out object result);
}

internal sealed class OdfFormulaDispatchContext(
    IEvaluationContext inner,
    OdfFormulaFunctionRegistry functions) :
    IEvaluationContext,
    IOdfFormulaWorkbookContext,
    IOdfFormulaEnvironmentContext,
    IOdfFormulaVolatileContext,
    IOdfFormulaReferenceContext,
    IOdfBlankCheckableContext,
    IOdfFormulaFunctionDispatchContext
{
    internal IEvaluationContext InnerContext => inner;

    private readonly DateTime _evaluationTimestamp = DateTime.Now;
    private readonly Random _random = new();

    public OdfCellAddress CurrentCell => inner.CurrentCell;

    public object GetCellValue(OdfCellAddress address) => inner.GetCellValue(address);

    public object[,] GetRangeValues(OdfCellRange range) => inner.GetRangeValues(range);

    public string? GetCellFormula(OdfCellAddress address) => inner.GetCellFormula(address);

    public object GetNamedRangeOrExpressionValue(string name) => inner.GetNamedRangeOrExpressionValue(name);

    public bool TryGetNamedRanges(
        string name,
        out IReadOnlyList<OdfCellRange> ranges)
    {
        if (inner is IOdfFormulaReferenceContext reference &&
            reference.TryGetNamedRanges(name, out ranges))
        {
            return true;
        }

        ranges = [];
        return false;
    }

    public bool TryGetFormulaEnvironmentInfo(
        string category,
        out object result)
    {
        if (inner is IOdfFormulaEnvironmentContext environment &&
            environment.TryGetFormulaEnvironmentInfo(category, out result))
        {
            return true;
        }

        result = OdfFormulaError.NA;
        return false;
    }

    public IReadOnlyList<string> SheetNames => inner is IOdfFormulaWorkbookContext workbook
        ? workbook.SheetNames
        : string.IsNullOrEmpty(inner.CurrentCell.SheetName)
            ? []
            : [inner.CurrentCell.SheetName!];

    public DateTime EvaluationTimestamp => inner is IOdfFormulaVolatileContext volatileContext
        ? volatileContext.EvaluationTimestamp
        : _evaluationTimestamp;

    public double NextRandomDouble()
    {
        if (inner is IOdfFormulaVolatileContext volatileContext)
            return volatileContext.NextRandomDouble();
        lock (_random)
            return _random.NextDouble();
    }

    public bool TryGetPivotData(
        string dataField,
        OdfCellAddress pivotAnchor,
        IReadOnlyDictionary<string, object> filters,
        out object result)
    {
        if (inner is IOdfFormulaWorkbookContext workbook)
            return workbook.TryGetPivotData(dataField, pivotAnchor, filters, out result);
        result = OdfFormulaError.NA;
        return false;
    }

    public bool TryEvaluateMultipleOperations(
        IReadOnlyList<object> arguments,
        out object result)
    {
        if (inner is IOdfFormulaWorkbookContext workbook)
            return workbook.TryEvaluateMultipleOperations(arguments, out result);
        result = OdfFormulaError.NA;
        return false;
    }

    public bool IsBlank(OdfCellAddress address) => inner is IOdfBlankCheckableContext blankCheckable
        ? blankCheckable.IsBlank(address)
        : inner.GetCellValue(address) is null;

    public bool TryEvaluateFunction(string name, List<AstNode> arguments, out object result)
    {
        if (inner is OdfDomEvaluationContext domContext)
            domContext.Budget?.ChargeOperation();

        if (FormulaBuiltinFunctionRegistry.TryEvaluate(name, arguments, this, out result))
        {
            return true;
        }

        if (!functions.TryGetHandler(name, out OdfFormulaFunctionHandler? handler) || handler is null)
        {
            result = OdfFormulaError.Name;
            return false;
        }

        var values = new object[arguments.Count];
        for (int index = 0; index < arguments.Count; index++)
        {
            values[index] = arguments[index].Evaluate(this);
        }

        result = FormulaNumericResult.Normalize(handler(values, this));
        return true;
    }
}
