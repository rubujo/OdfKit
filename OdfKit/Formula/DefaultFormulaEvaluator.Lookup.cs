using System.Collections.Generic;
using OdfKit.Formula.AST;
using OdfKit.Spreadsheet;

namespace OdfKit.Formula;

public partial class DefaultFormulaEvaluator
{
    #region Lookup Functions

    private static object EvaluateVLookup(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count < 3 || arguments.Count > 4)
            return OdfFormulaError.Value;

        object lookupValue = arguments[0].Evaluate(context);
        if (lookupValue is OdfFormulaError err)
            return err;

        if (arguments[1] is not RangeReferenceNode rangeNode)
            return OdfFormulaError.Value;
        OdfCellRange range = rangeNode.Range;

        object colIndexVal = arguments[2].Evaluate(context);
        if (!FormulaCoercion.TryCoerceDouble(colIndexVal, out double colD))
            return OdfFormulaError.Value;
        int colIndex = (int)colD;

        bool rangeLookup = true;
        if (arguments.Count == 4)
        {
            object lookupTypeVal = arguments[3].Evaluate(context);
            rangeLookup = FormulaCoercion.CoerceToBool(lookupTypeVal);
        }

        object[,] table = context.GetRangeValues(range);
        int tableRows = table.GetLength(0);
        int tableCols = table.GetLength(1);

        if (colIndex < 1 || colIndex > tableCols)
            return OdfFormulaError.Ref;

        int targetCol = colIndex - 1;

        if (!rangeLookup)
        {
            for (int r = 0; r < tableRows; r++)
            {
                if (FormulaCoercion.CompareValues(table[r, 0], lookupValue) == 0)
                    return table[r, targetCol] ?? string.Empty;
            }

            return OdfFormulaError.NA;
        }

        int matchedRow = -1;
        for (int r = 0; r < tableRows; r++)
        {
            object cellVal = table[r, 0];
            int comp = FormulaCoercion.CompareValues(cellVal, lookupValue);
            if (comp == 0)
                return table[r, targetCol] ?? string.Empty;
            if (comp < 0)
                matchedRow = r;
            else
                break;
        }

        if (matchedRow == -1)
            return OdfFormulaError.NA;
        return table[matchedRow, targetCol] ?? string.Empty;
    }

    #endregion
}
