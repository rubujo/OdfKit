using System;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Formula.AST;
using OdfKit.Spreadsheet;

namespace OdfKit.Formula;

/// <summary>
/// 查詢與參照內建公式函式處理常式（內部協作者）。
/// </summary>
internal static class FormulaLookupFunctionHandlers
{


    internal static object EvaluateVLookup(List<AstNode> arguments, IEvaluationContext context)
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



    internal static object EvaluateHLookup(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count < 3 || arguments.Count > 4)
            return OdfFormulaError.Value;

        var lookupValue = arguments[0].Evaluate(context);
        if (lookupValue is OdfFormulaError err)
            return err;

        if (arguments[1] is not RangeReferenceNode rangeNode)
            return OdfFormulaError.Value;
        var range = rangeNode.Range;

        var rowIndexVal = arguments[2].Evaluate(context);
        if (!FormulaCoercion.TryCoerceDouble(rowIndexVal, out double rowD))
            return OdfFormulaError.Value;
        int rowIndex = (int)rowD;

        bool rangeLookup = true;
        if (arguments.Count == 4)
        {
            var lookupTypeVal = arguments[3].Evaluate(context);
            rangeLookup = FormulaCoercion.CoerceToBool(lookupTypeVal);
        }

        object[,] table = context.GetRangeValues(range);
        int tableRows = table.GetLength(0);
        int tableCols = table.GetLength(1);

        if (rowIndex < 1 || rowIndex > tableRows)
            return OdfFormulaError.Ref;

        int targetRow = rowIndex - 1;

        if (!rangeLookup)
        {
            for (int c = 0; c < tableCols; c++)
            {
                if (FormulaCoercion.CompareValues(table[0, c], lookupValue) == 0)
                {
                    return table[targetRow, c] ?? string.Empty;
                }
            }
            return OdfFormulaError.NA;
        }
        else
        {
            int matchedCol = -1;
            for (int c = 0; c < tableCols; c++)
            {
                object cellVal = table[0, c];
                int comp = FormulaCoercion.CompareValues(cellVal, lookupValue);
                if (comp == 0)
                {
                    return table[targetRow, c] ?? string.Empty;
                }
                if (comp < 0)
                {
                    matchedCol = c;
                }
                else
                {
                    break;
                }
            }

            if (matchedCol == -1)
                return OdfFormulaError.NA;
            return table[targetRow, matchedCol] ?? string.Empty;
        }
    }

    internal static object EvaluateIndex(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count < 2 || arguments.Count > 3)
            return OdfFormulaError.Value;

        if (arguments[0] is not RangeReferenceNode rangeNode)
            return OdfFormulaError.Value;
        var range = rangeNode.Range;
        object[,] table = context.GetRangeValues(range);
        int tableRows = table.GetLength(0);
        int tableCols = table.GetLength(1);

        var rowVal = arguments[1].Evaluate(context);
        if (!FormulaCoercion.TryCoerceDouble(rowVal, out double rowD))
            return OdfFormulaError.Value;
        int rowNum = (int)rowD;

        int colNum = 1;
        if (arguments.Count == 3)
        {
            var colVal = arguments[2].Evaluate(context);
            if (!FormulaCoercion.TryCoerceDouble(colVal, out double colD))
                return OdfFormulaError.Value;
            colNum = (int)colD;
        }
        else
        {
            if (tableRows == 1 && tableCols > 1)
            {
                colNum = rowNum;
                rowNum = 1;
            }
        }

        if (rowNum < 0 || rowNum > tableRows || colNum < 0 || colNum > tableCols)
            return OdfFormulaError.Ref;
        if (rowNum == 0 || colNum == 0)
            return OdfFormulaError.Ref;

        return table[rowNum - 1, colNum - 1] ?? string.Empty;
    }

    internal static object EvaluateMatch(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count < 2 || arguments.Count > 3)
            return OdfFormulaError.Value;

        var lookupValue = arguments[0].Evaluate(context);
        if (lookupValue is OdfFormulaError err)
            return err;

        if (arguments[1] is not RangeReferenceNode rangeNode)
            return OdfFormulaError.Value;
        var range = rangeNode.Range;
        object[,] table = context.GetRangeValues(range);
        int rows = table.GetLength(0);
        int cols = table.GetLength(1);

        if (rows > 1 && cols > 1)
            return OdfFormulaError.Value;
        int length = Math.Max(rows, cols);

        int matchType = 1;
        if (arguments.Count == 3)
        {
            var mtVal = arguments[2].Evaluate(context);
            if (!FormulaCoercion.TryCoerceDouble(mtVal, out double mtD))
                return OdfFormulaError.Value;
            matchType = (int)mtD;
        }

        object GetElement(int i)
        {
            return rows > 1 ? table[i, 0] : table[0, i];
        }

        if (matchType == 0)
        {
            for (int i = 0; i < length; i++)
            {
                if (FormulaCoercion.CompareValues(GetElement(i), lookupValue) == 0)
                {
                    return (double)(i + 1);
                }
            }
            return OdfFormulaError.NA;
        }
        else if (matchType == 1)
        {
            int matchedIdx = -1;
            for (int i = 0; i < length; i++)
            {
                int comp = FormulaCoercion.CompareValues(GetElement(i), lookupValue);
                if (comp == 0)
                {
                    return (double)(i + 1);
                }
                if (comp < 0)
                {
                    matchedIdx = i;
                }
                else
                {
                    break;
                }
            }
            if (matchedIdx == -1)
                return OdfFormulaError.NA;
            return (double)(matchedIdx + 1);
        }
        else if (matchType == -1)
        {
            int matchedIdx = -1;
            for (int i = 0; i < length; i++)
            {
                int comp = FormulaCoercion.CompareValues(GetElement(i), lookupValue);
                if (comp == 0)
                {
                    return (double)(i + 1);
                }
                if (comp > 0)
                {
                    matchedIdx = i;
                }
                else
                {
                    break;
                }
            }
            if (matchedIdx == -1)
                return OdfFormulaError.NA;
            return (double)(matchedIdx + 1);
        }

        return OdfFormulaError.Value;
    }

    internal static object EvaluateOffset(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count < 3 || arguments.Count > 5)
            return OdfFormulaError.Value;

        OdfCellRange baseRange;
        string? sheetName = null;
        var baseNode = arguments[0];
        if (baseNode is CellAddressNode cellNode)
        {
            baseRange = new OdfCellRange(cellNode.Address, cellNode.Address);
            sheetName = cellNode.Address.SheetName;
        }
        else if (baseNode is RangeReferenceNode rangeNode)
        {
            baseRange = rangeNode.Range;
            sheetName = rangeNode.Range.StartAddress.SheetName;
        }
        else
        {
            return OdfFormulaError.Value;
        }

        var rowsVal = arguments[1].Evaluate(context);
        var colsVal = arguments[2].Evaluate(context);
        if (!FormulaCoercion.TryCoerceDouble(rowsVal, out double rowsD) || !FormulaCoercion.TryCoerceDouble(colsVal, out double colsD))
            return OdfFormulaError.Value;

        int rowOffset = (int)rowsD;
        int colOffset = (int)colsD;

        int height = baseRange.EndAddress.Row - baseRange.StartAddress.Row + 1;
        if (arguments.Count >= 4)
        {
            var hVal = arguments[3].Evaluate(context);
            if (!FormulaCoercion.TryCoerceDouble(hVal, out double hD) || hD <= 0)
                return OdfFormulaError.Value;
            height = (int)hD;
        }

        int width = baseRange.EndAddress.Column - baseRange.StartAddress.Column + 1;
        if (arguments.Count == 5)
        {
            var wVal = arguments[4].Evaluate(context);
            if (!FormulaCoercion.TryCoerceDouble(wVal, out double wD) || wD <= 0)
                return OdfFormulaError.Value;
            width = (int)wD;
        }

        int startRow = baseRange.StartAddress.Row + rowOffset;
        int startCol = baseRange.StartAddress.Column + colOffset;

        if (startRow < 0 || startCol < 0)
            return OdfFormulaError.Ref;

        var newStart = new OdfCellAddress(startRow, startCol, sheetName);
        var newEnd = new OdfCellAddress(startRow + height - 1, startCol + width - 1, sheetName);
        var newRange = new OdfCellRange(newStart, newEnd);

        if (height == 1 && width == 1)
        {
            return context.GetCellValue(newStart);
        }

        return context.GetRangeValues(newRange);
    }

    internal static object EvaluateIndirect(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count < 1 || arguments.Count > 2)
            return OdfFormulaError.Value;
        var refTextVal = arguments[0].Evaluate(context);
        if (refTextVal is not string refText)
            return OdfFormulaError.Value;

        refText = refText.Trim();

        try
        {
            if (refText.Contains(':'))
            {
                var range = OdfCellRange.ParseOdf(refText);
                return context.GetRangeValues(range);
            }
            else
            {
                if (OdfCellAddress.TryParse(refText, out var addr))
                {
                    return context.GetCellValue(addr);
                }
                var addrOdf = OdfCellAddress.ParseOdf(refText);
                return context.GetCellValue(addrOdf);
            }
        }
        catch
        {
            return OdfFormulaError.Ref;
        }
    }

    internal static object EvaluateRow(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count == 0)
        {
            return (double)(context.CurrentCell.Row + 1);
        }
        if (arguments.Count == 1)
        {
            var node = arguments[0];
            if (node is CellAddressNode cellNode)
            {
                return (double)(cellNode.Address.Row + 1);
            }
            if (node is RangeReferenceNode rangeNode)
            {
                return (double)(rangeNode.Range.StartAddress.Row + 1);
            }
            return OdfFormulaError.Value;
        }
        return OdfFormulaError.Value;
    }

    internal static object EvaluateColumn(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count == 0)
        {
            return (double)(context.CurrentCell.Column + 1);
        }
        if (arguments.Count == 1)
        {
            var node = arguments[0];
            if (node is CellAddressNode cellNode)
            {
                return (double)(cellNode.Address.Column + 1);
            }
            if (node is RangeReferenceNode rangeNode)
            {
                return (double)(rangeNode.Range.StartAddress.Column + 1);
            }
            return OdfFormulaError.Value;
        }
        return OdfFormulaError.Value;
    }

    internal static object EvaluateRows(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 1)
            return OdfFormulaError.Value;
        var node = arguments[0];
        if (node is CellAddressNode)
            return 1.0;
        if (node is RangeReferenceNode rangeNode)
        {
            return (double)(rangeNode.Range.EndAddress.Row - rangeNode.Range.StartAddress.Row + 1);
        }
        return OdfFormulaError.Value;
    }

    internal static object EvaluateColumns(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 1)
            return OdfFormulaError.Value;
        var node = arguments[0];
        if (node is CellAddressNode)
            return 1.0;
        if (node is RangeReferenceNode rangeNode)
        {
            return (double)(rangeNode.Range.EndAddress.Column - rangeNode.Range.StartAddress.Column + 1);
        }
        return OdfFormulaError.Value;
    }

    internal static object EvaluateChoose(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count < 2)
            return OdfFormulaError.Value;
        var indexVal = arguments[0].Evaluate(context);
        if (!FormulaCoercion.TryCoerceDouble(indexVal, out double idxD))
            return OdfFormulaError.Value;
        int idx = (int)idxD;
        if (idx < 1 || idx >= arguments.Count)
            return OdfFormulaError.Value;
        return arguments[idx].Evaluate(context);
    }
}

