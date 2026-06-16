using System;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Formula.AST;
using OdfKit.Spreadsheet;

namespace OdfKit.Formula;

internal class OdfDomEvaluationContext : IEvaluationContext
{
    public OdfCellAddress CurrentCell { get; set; }
    private readonly Dictionary<OdfCellAddress, OdfNode> _cellNodes = new();
    private readonly Dictionary<OdfCellAddress, string> _cellFormulas = new();
    private readonly Dictionary<OdfCellAddress, object> _cellValues = new();
    private readonly DefaultFormulaEvaluator _evaluator;
    private readonly OdfNode _contentRoot;

    public OdfDomEvaluationContext(OdfNode contentRoot, DefaultFormulaEvaluator evaluator)
    {
        _contentRoot = contentRoot;
        _evaluator = evaluator;
        TraverseTable(contentRoot);
    }

    public Dictionary<OdfCellAddress, OdfNode> CellNodes => _cellNodes;
    public Dictionary<OdfCellAddress, string> CellFormulas => _cellFormulas;
    public Dictionary<OdfCellAddress, object> CellValues => _cellValues;

    private void TraverseTable(OdfNode node)
    {
        if (node.NodeType == OdfNodeType.Element && node.LocalName == "table" && node.NamespaceUri == OdfNamespaces.Table)
        {
            string sheetName = node.GetAttribute("name", OdfNamespaces.Table) ?? "";

            int currentRow = 0;
            foreach (var rowChild in node.Children)
            {
                if (rowChild.NodeType == OdfNodeType.Element && rowChild.LocalName == "table-row" && rowChild.NamespaceUri == OdfNamespaces.Table)
                {
                    int rowRepeated = 1;
                    string? rowRepeatedStr = rowChild.GetAttribute("number-rows-repeated", OdfNamespaces.Table);
                    if (!string.IsNullOrEmpty(rowRepeatedStr) && int.TryParse(rowRepeatedStr, out int rRep) && rRep > 0)
                    {
                        rowRepeated = Math.Min(rRep, OdfSpreadsheetLimits.FormulaMaxRepeat);
                    }

                    bool hasActiveCells = false;
                    foreach (var cellChild in rowChild.Children)
                    {
                        if (cellChild.NodeType == OdfNodeType.Element &&
                            (cellChild.LocalName == "table-cell" || cellChild.LocalName == "covered-table-cell") &&
                            cellChild.NamespaceUri == OdfNamespaces.Table)
                        {
                            if (cellChild.GetAttribute("formula", OdfNamespaces.Table) != null ||
                                cellChild.GetAttribute("value-type", OdfNamespaces.Office) != null ||
                                !string.IsNullOrEmpty(cellChild.TextContent))
                            {
                                hasActiveCells = true;
                                break;
                            }
                        }
                    }

                    if (hasActiveCells)
                    {
                        for (int r = 0; r < rowRepeated; r++)
                        {
                            int currentCol = 0;
                            foreach (var cellChild in rowChild.Children)
                            {
                                if (cellChild.NodeType == OdfNodeType.Element &&
                                    (cellChild.LocalName == "table-cell" || cellChild.LocalName == "covered-table-cell") &&
                                    cellChild.NamespaceUri == OdfNamespaces.Table)
                                {
                                    int colRepeated = 1;
                                    string? colRepeatedStr = cellChild.GetAttribute("number-columns-repeated", OdfNamespaces.Table);
                                    if (!string.IsNullOrEmpty(colRepeatedStr) && int.TryParse(colRepeatedStr, out int cRep) && cRep > 0)
                                    {
                                        colRepeated = Math.Min(cRep, OdfSpreadsheetLimits.FormulaMaxRepeat);
                                    }

                                    bool isActiveCell = cellChild.GetAttribute("formula", OdfNamespaces.Table) != null ||
                                                       cellChild.GetAttribute("value-type", OdfNamespaces.Office) != null ||
                                                       !string.IsNullOrEmpty(cellChild.TextContent);

                                    for (int c = 0; c < colRepeated; c++)
                                    {
                                        var addr = new OdfCellAddress(currentRow + r, currentCol + c, sheetName);
                                        if (isActiveCell)
                                        {
                                            _cellNodes[addr] = cellChild;

                                            string? formula = cellChild.GetAttribute("formula", OdfNamespaces.Table);
                                            if (!string.IsNullOrEmpty(formula))
                                            {
                                                _cellFormulas[addr] = formula!;
                                            }

                                            object cellValue = ParseCellValue(cellChild);
                                            _cellValues[addr] = cellValue;
                                        }
                                    }
                                    currentCol += colRepeated;
                                }
                            }
                        }
                    }
                    currentRow += rowRepeated;
                }
            }
        }
        else
        {
            foreach (var child in node.Children)
            {
                TraverseTable(child);
            }
        }
    }

    private object ParseCellValue(OdfNode cellNode)
    {
        string? valType = cellNode.GetAttribute("value-type", OdfNamespaces.Office);
        if (string.IsNullOrEmpty(valType))
        {
            string text = cellNode.TextContent;
            if (double.TryParse(text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double d))
                return d;
            return text;
        }

        if (valType == "float" || valType == "percentage" || valType == "currency")
        {
            string? val = cellNode.GetAttribute("value", OdfNamespaces.Office);
            if (!string.IsNullOrEmpty(val) && double.TryParse(val, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double d))
                return d;
            return 0.0;
        }
        if (valType == "boolean")
        {
            string? val = cellNode.GetAttribute("boolean-value", OdfNamespaces.Office);
            return !string.IsNullOrEmpty(val) && val!.Equals("true", StringComparison.OrdinalIgnoreCase);
        }
        if (valType == "string")
        {
            return cellNode.GetAttribute("string-value", OdfNamespaces.Office) ?? cellNode.TextContent;
        }
        return cellNode.GetAttribute("date-value", OdfNamespaces.Office) ??
               cellNode.GetAttribute("time-value", OdfNamespaces.Office) ??
               cellNode.TextContent;
    }

    public object GetCellValue(OdfCellAddress address)
    {
        if (string.IsNullOrEmpty(address.SheetName) && !string.IsNullOrEmpty(CurrentCell.SheetName))
        {
            address = new OdfCellAddress(address.Row, address.Column, CurrentCell.SheetName,
                address.IsRowAbsolute, address.IsColumnAbsolute, address.IsSheetAbsolute);
        }

        if (_cellFormulas.TryGetValue(address, out var formula))
        {
            var oldCell = CurrentCell;
            CurrentCell = address;
            try
            {
                return _evaluator.EvaluateCell(address, this);
            }
            finally
            {
                CurrentCell = oldCell;
            }
        }
        if (_cellValues.TryGetValue(address, out var val))
            return val;
        return 0.0;
    }

    public object[,] GetRangeValues(OdfCellRange range)
    {
        string? sheetName = range.StartAddress.SheetName;
        if (string.IsNullOrEmpty(sheetName))
        {
            sheetName = CurrentCell.SheetName;
        }

        int minRow = Math.Min(range.StartAddress.Row, range.EndAddress.Row);
        int maxRow = Math.Max(range.StartAddress.Row, range.EndAddress.Row);
        int minCol = Math.Min(range.StartAddress.Column, range.EndAddress.Column);
        int maxCol = Math.Max(range.StartAddress.Column, range.EndAddress.Column);

        int rows = maxRow - minRow + 1;
        int cols = maxCol - minCol + 1;
        var arr = new object[rows, cols];

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                var addr = new OdfCellAddress(minRow + r, minCol + c, sheetName);
                arr[r, c] = GetCellValue(addr);
            }
        }
        return arr;
    }

    public string? GetCellFormula(OdfCellAddress address)
    {
        if (string.IsNullOrEmpty(address.SheetName) && !string.IsNullOrEmpty(CurrentCell.SheetName))
        {
            address = new OdfCellAddress(address.Row, address.Column, CurrentCell.SheetName,
                address.IsRowAbsolute, address.IsColumnAbsolute, address.IsSheetAbsolute);
        }
        return _cellFormulas.TryGetValue(address, out var formula) ? formula : null;
    }

    public object GetNamedRangeOrExpressionValue(string name)
    {
        string? currentSheet = CurrentCell.SheetName;
        OdfNode? targetNode = null;

        if (!string.IsNullOrEmpty(currentSheet))
        {
            var sheetNode = FindSheetNode(_contentRoot, currentSheet);
            if (sheetNode != null)
            {
                targetNode = FindNamedNodeUnderParent(sheetNode, name);
            }
        }

        if (targetNode == null)
        {
            targetNode = FindGlobalNamedNode(_contentRoot, name);
        }

        if (targetNode == null)
        {
            return OdfFormulaError.Name;
        }

        if (targetNode.LocalName == "named-range")
        {
            string? cellRangeAddress = targetNode.GetAttribute("cell-range-address", OdfNamespaces.Table);
            if (string.IsNullOrEmpty(cellRangeAddress))
            {
                return OdfFormulaError.Value;
            }

            if (OdfCellRange.TryParse(cellRangeAddress!, out var range))
            {
                return GetRangeValues(range);
            }
            return OdfFormulaError.Value;
        }
        else if (targetNode.LocalName == "named-expression")
        {
            string? expression = targetNode.GetAttribute("expression", OdfNamespaces.Table);
            if (string.IsNullOrEmpty(expression))
            {
                return OdfFormulaError.Value;
            }

            if (expression!.StartsWith("oooc:=", StringComparison.OrdinalIgnoreCase) ||
                expression.StartsWith("of:=", StringComparison.OrdinalIgnoreCase))
            {
                expression = OdfFormulaTranslator.OdfToExcelFormula(expression);
            }

            if (expression!.StartsWith("oooc:=", StringComparison.OrdinalIgnoreCase))
                expression = expression.Substring(6);
            else if (expression.StartsWith("of:=", StringComparison.OrdinalIgnoreCase))
                expression = expression.Substring(4);
            else if (expression.StartsWith("="))
                expression = expression.Substring(1);

            return _evaluator.Evaluate(expression!, this);
        }

        return OdfFormulaError.Name;
    }

    private OdfNode? FindSheetNode(OdfNode node, string? sheetName)
    {
        if (string.IsNullOrEmpty(sheetName))
            return null;
        if (node.NodeType == OdfNodeType.Element && node.LocalName == "table" && node.NamespaceUri == OdfNamespaces.Table)
        {
            if (node.GetAttribute("name", OdfNamespaces.Table) == sheetName)
                return node;
        }
        foreach (var child in node.Children)
        {
            var match = FindSheetNode(child, sheetName);
            if (match != null)
                return match;
        }
        return null;
    }

    private OdfNode? FindNamedNodeUnderParent(OdfNode parent, string name)
    {
        foreach (var child in parent.Children)
        {
            if (child.NodeType == OdfNodeType.Element && child.LocalName == "named-expressions" && child.NamespaceUri == OdfNamespaces.Table)
            {
                foreach (var exprChild in child.Children)
                {
                    if (exprChild.NodeType == OdfNodeType.Element &&
                        (exprChild.LocalName == "named-range" || exprChild.LocalName == "named-expression") &&
                        exprChild.NamespaceUri == OdfNamespaces.Table &&
                        exprChild.GetAttribute("name", OdfNamespaces.Table) == name)
                    {
                        return exprChild;
                    }
                }
            }
        }
        return null;
    }

    private OdfNode? FindGlobalNamedNode(OdfNode root, string name)
    {
        if (root.NodeType == OdfNodeType.Element && root.LocalName == "table" && root.NamespaceUri == OdfNamespaces.Table)
        {
            return null;
        }

        if (root.NodeType == OdfNodeType.Element && root.LocalName == "named-expressions" && root.NamespaceUri == OdfNamespaces.Table)
        {
            foreach (var exprChild in root.Children)
            {
                if (exprChild.NodeType == OdfNodeType.Element &&
                    (exprChild.LocalName == "named-range" || exprChild.LocalName == "named-expression") &&
                    exprChild.NamespaceUri == OdfNamespaces.Table &&
                    exprChild.GetAttribute("name", OdfNamespaces.Table) == name)
                {
                    return exprChild;
                }
            }
        }

        foreach (var child in root.Children)
        {
            var match = FindGlobalNamedNode(child, name);
            if (match != null)
                return match;
        }

        return null;
    }
}
