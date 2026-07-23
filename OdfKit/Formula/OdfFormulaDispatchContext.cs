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
    IOdfBlankCheckableContext,
    IOdfFormulaFunctionDispatchContext
{
    public OdfCellAddress CurrentCell => inner.CurrentCell;

    public object GetCellValue(OdfCellAddress address) => inner.GetCellValue(address);

    public object[,] GetRangeValues(OdfCellRange range) => inner.GetRangeValues(range);

    public string? GetCellFormula(OdfCellAddress address) => inner.GetCellFormula(address);

    public object GetNamedRangeOrExpressionValue(string name) => inner.GetNamedRangeOrExpressionValue(name);

    public IReadOnlyList<string> SheetNames => inner is IOdfFormulaWorkbookContext workbook
        ? workbook.SheetNames
        : string.IsNullOrEmpty(inner.CurrentCell.SheetName)
            ? []
            : [inner.CurrentCell.SheetName!];

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

        result = handler(values, this);
        return true;
    }
}
