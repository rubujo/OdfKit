using System.Globalization;
using System;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Formula.AST;
using OdfKit.Spreadsheet;

namespace OdfKit.Formula;

internal class OdfDomEvaluationContext :
    IOdfFormulaWorkbookContext,
    IOdfFormulaVolatileContext,
    IOdfFormulaReferenceContext,
    IOdfBlankCheckableContext
{
    /// <summary>
    /// Short overload of IsBlank that accepts address; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 address；其餘可選參數使用預設值並轉呼叫最長 IsBlank 多載。
    /// </summary>
    public bool IsBlank(OdfCellAddress address) =>
        !_cellValues.ContainsKey(address) && !_cellFormulas.ContainsKey(address);

    public OdfCellAddress CurrentCell { get; set; }
    private readonly Dictionary<OdfCellAddress, OdfNode> _cellNodes = new();
    private readonly Dictionary<OdfCellAddress, string> _cellFormulas = new();
    private readonly Dictionary<OdfCellAddress, object> _cellValues = new();
    private readonly HashSet<string> _evaluatingNames =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _sheetNames = [];
    private readonly DefaultFormulaEvaluator _evaluator;
    private readonly OdfNode _contentRoot;
    private readonly OdfExternalLinkManager? _externalLinks;
    private readonly IOdfFormulaVolatileContext _volatileContext;

    public DateTime EvaluationTimestamp => _volatileContext.EvaluationTimestamp;
    /// <summary>
    /// Short overload of OdfDomEvaluationContext that accepts contentRoot and evaluator; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 contentRoot 與 evaluator；其餘可選參數使用預設值並轉呼叫最長 OdfDomEvaluationContext 多載。
    /// </summary>
    public OdfDomEvaluationContext(OdfNode contentRoot, DefaultFormulaEvaluator evaluator) : this(contentRoot, evaluator, null) { }


    /// <summary>
    /// Full overload of OdfDomEvaluationContext that accepts contentRoot, evaluator, and externalLinks.
    /// OdfDomEvaluationContext 完整多載：接受 contentRoot、evaluator 與 externalLinks。
    /// </summary>
    public OdfDomEvaluationContext(OdfNode contentRoot, DefaultFormulaEvaluator evaluator, OdfExternalLinkManager? externalLinks)
        : this(contentRoot, evaluator, externalLinks, new OdfFormulaVolatileSession())
    {
    }

    internal OdfDomEvaluationContext(
        OdfNode contentRoot,
        DefaultFormulaEvaluator evaluator,
        OdfExternalLinkManager? externalLinks,
        IOdfFormulaVolatileContext volatileContext)
    {
        _contentRoot = contentRoot;
        _evaluator = evaluator;
        _externalLinks = externalLinks;
        _volatileContext = volatileContext;
        TraverseTable(contentRoot);
    }


    public Dictionary<OdfCellAddress, OdfNode> CellNodes => _cellNodes;
    public Dictionary<OdfCellAddress, string> CellFormulas => _cellFormulas;
    public Dictionary<OdfCellAddress, object> CellValues => _cellValues;

    public IReadOnlyList<string> SheetNames => _sheetNames;

    public double NextRandomDouble() => _volatileContext.NextRandomDouble();

    private void TraverseTable(OdfNode node)
    {
        if (node.NodeType == OdfNodeType.Element && node.LocalName == "table" && node.NamespaceUri == OdfNamespaces.Table)
        {
            string sheetName = node.GetAttribute("name", OdfNamespaces.Table) ?? "";
            if (!_sheetNames.Contains(sheetName))
                _sheetNames.Add(sheetName);

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

    private static object ParseCellValue(OdfNode cellNode)
    {
        string? valType = cellNode.GetAttribute("value-type", OdfNamespaces.Office);
        if (string.IsNullOrEmpty(valType))
        {
            string text = cellNode.TextContent;
            if (double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out double d))
                return d;
            return text;
        }

        if (valType == "float" || valType == "percentage" || valType == "currency")
        {
            string? val = cellNode.GetAttribute("value", OdfNamespaces.Office);
            if (!string.IsNullOrEmpty(val) && double.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out double d))
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

    /// <summary>
    /// Full overload of GetCellValue that accepts address.
    /// GetCellValue 完整多載：接受 address。
    /// </summary>
    public object GetCellValue(OdfCellAddress address)
    {
        if (_externalLinks is not null && _externalLinks.TryGetCellValue(address, out object? externalValue))
        {
            return externalValue ?? 0.0;
        }

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

    /// <summary>
    /// Gets the values of the specified cell range as a two-dimensional array.
    /// 取得指定儲存格範圍的值，並以二維陣列傳回。
    /// </summary>
    /// <param name="range">The cell range to read. / 要讀取的儲存格範圍。</param>
    /// <returns>A two-dimensional array of cell values. / 儲存格值的二維陣列。</returns>
    public object[,] GetRangeValues(OdfCellRange range)
    {
        if (_externalLinks is not null && _externalLinks.TryGetRangeValues(range, out object[,] externalValues))
        {
            return externalValues;
        }

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

    /// <summary>
    /// Full overload of GetCellFormula that accepts address.
    /// GetCellFormula 完整多載：接受 address。
    /// </summary>
    public string? GetCellFormula(OdfCellAddress address)
    {
        if (string.IsNullOrEmpty(address.SheetName) && !string.IsNullOrEmpty(CurrentCell.SheetName))
        {
            address = new OdfCellAddress(address.Row, address.Column, CurrentCell.SheetName,
                address.IsRowAbsolute, address.IsColumnAbsolute, address.IsSheetAbsolute);
        }
        return _cellFormulas.TryGetValue(address, out var formula) ? formula : null;
    }

    /// <summary>
    /// Full overload of GetNamedRangeOrExpressionValue that accepts name.
    /// GetNamedRangeOrExpressionValue 完整多載：接受 name。
    /// </summary>
    public object GetNamedRangeOrExpressionValue(string name)
    {
        ParseNamedExpression(
            name,
            out string? source,
            out string? qualifiedSheet,
            out string simpleName);
        if (!string.IsNullOrEmpty(source))
        {
            return _externalLinks is not null &&
                _externalLinks.TryGetNamedExpressionValue(
                    source!,
                    qualifiedSheet,
                    simpleName,
                    _evaluator,
                    out object externalResult)
                ? externalResult
                : OdfFormulaError.Name;
        }

        string? currentSheet = qualifiedSheet ?? CurrentCell.SheetName;
        OdfNode? targetNode = null;

        if (!string.IsNullOrEmpty(currentSheet))
        {
            var sheetNode = FindSheetNode(_contentRoot, currentSheet);
            if (sheetNode != null)
            {
                targetNode = FindNamedNodeUnderParent(sheetNode, simpleName);
            }
        }

        if (targetNode == null)
        {
            targetNode = FindGlobalNamedNode(_contentRoot, simpleName);
        }

        if (targetNode == null)
        {
            return IsSimpleQuotedLabel(name) &&
                TryResolveLabelRange(simpleName, currentSheet, out OdfCellRange labelRange)
                ? GetRangeValues(labelRange)
                : OdfFormulaError.Name;
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
            else if (global::OdfKit.Internal.OdfStringHelper.StartsWith(expression, '='))
                expression = expression.Substring(1);

            string scopeKey = string.Concat(currentSheet, "\n", simpleName);
            if (!_evaluatingNames.Add(scopeKey))
                return OdfFormulaError.Ref;

            OdfCellAddress previousCell = CurrentCell;
            try
            {
                string? baseAddress = targetNode.GetAttribute(
                    "base-cell-address",
                    OdfNamespaces.Table);
                if (!string.IsNullOrEmpty(baseAddress) &&
                    OdfCellAddress.TryParse(baseAddress!, out OdfCellAddress parsedBase))
                {
                    CurrentCell = parsedBase;
                }

                return _evaluator.Evaluate(expression!, this);
            }
            finally
            {
                CurrentCell = previousCell;
                _evaluatingNames.Remove(scopeKey);
            }
        }

        return OdfFormulaError.Name;
    }

    private static void ParseNamedExpression(
        string value,
        out string? source,
        out string? sheetName,
        out string name)
    {
        source = null;
        sheetName = null;
        value = value.Trim();
        int sourceMarker = FindUnquoted(value, '#');
        if (sourceMarker >= 0)
        {
            source = UnquoteName(value.Substring(0, sourceMarker));
            value = value.Substring(sourceMarker + 1);
        }

        int sheetMarker = FindUnquoted(value, '.');
        if (sheetMarker >= 0)
        {
            sheetName = UnquoteName(value.Substring(0, sheetMarker));
            value = value.Substring(sheetMarker + 1);
        }

        if (value.StartsWith("$$", StringComparison.Ordinal))
            value = value.Substring(2);
        name = UnquoteName(value);
    }

    private static int FindUnquoted(string value, char target)
    {
        bool quoted = false;
        for (int index = 0; index < value.Length; index++)
        {
            if (value[index] == '\'')
            {
                if (quoted &&
                    index + 1 < value.Length &&
                    value[index + 1] == '\'')
                {
                    index++;
                    continue;
                }

                quoted = !quoted;
            }
            else if (!quoted && value[index] == target)
            {
                return index;
            }
        }

        return -1;
    }

    private static string UnquoteName(string value)
    {
        value = value.Trim();
        if (global::OdfKit.Internal.OdfStringHelper.StartsWith(value, '$'))
            value = value.Substring(1);
        if (value.Length >= 2 &&
            value[0] == '\'' &&
            value[value.Length - 1] == '\'')
        {
            return value.Substring(1, value.Length - 2).Replace("''", "'");
        }

        return value;
    }

    public bool TryGetNamedRanges(
        string name,
        out IReadOnlyList<OdfCellRange> ranges)
    {
        ParseNamedExpression(
            name,
            out string? source,
            out string? qualifiedSheet,
            out string simpleName);
        if (!string.IsNullOrEmpty(source))
        {
            ranges = [];
            return false;
        }

        string? currentSheet = qualifiedSheet ?? CurrentCell.SheetName;
        OdfNode? target = null;
        if (!string.IsNullOrEmpty(currentSheet))
        {
            OdfNode? sheet = FindSheetNode(_contentRoot, currentSheet);
            if (sheet is not null)
                target = FindNamedNodeUnderParent(sheet, simpleName);
        }

        target ??= FindGlobalNamedNode(_contentRoot, simpleName);
        if (target is null &&
            IsSimpleQuotedLabel(name) &&
            TryResolveLabelRange(
                simpleName,
                currentSheet,
                out OdfCellRange labelRange))
        {
            ranges = [labelRange];
            return true;
        }

        string? address = target?.LocalName == "named-range"
            ? target.GetAttribute("cell-range-address", OdfNamespaces.Table)
            : null;
        if (!string.IsNullOrEmpty(address) &&
            OdfCellRange.TryParse(address!, out OdfCellRange range))
        {
            ranges = [range];
            return true;
        }

        ranges = [];
        return false;
    }

    private static bool IsSimpleQuotedLabel(string value)
    {
        value = value.Trim();
        return value.Length >= 2 &&
            value[0] == '\'' &&
            value[value.Length - 1] == '\'' &&
            FindUnquoted(value, '#') < 0 &&
            FindUnquoted(value, '.') < 0;
    }

    private bool TryResolveLabelRange(
        string label,
        string? sheetName,
        out OdfCellRange range)
    {
        foreach (KeyValuePair<OdfCellAddress, object> cell in _cellValues)
        {
            if (!string.Equals(
                    cell.Key.SheetName,
                    sheetName,
                    StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(
                    Convert.ToString(cell.Value, CultureInfo.InvariantCulture),
                    label,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            int columnEnd = FindLabelExtent(cell.Key, rowStep: 1, columnStep: 0);
            int rowEnd = FindLabelExtent(cell.Key, rowStep: 0, columnStep: 1);
            int columnLength = columnEnd - cell.Key.Row;
            int rowLength = rowEnd - cell.Key.Column;
            if (columnLength <= 0 && rowLength <= 0)
                continue;

            range = columnLength >= rowLength
                ? new OdfCellRange(
                    cell.Key.Row + 1,
                    cell.Key.Column,
                    columnEnd,
                    cell.Key.Column,
                    cell.Key.SheetName)
                : new OdfCellRange(
                    cell.Key.Row,
                    cell.Key.Column + 1,
                    cell.Key.Row,
                    rowEnd,
                    cell.Key.SheetName);
            return true;
        }

        range = default;
        return false;
    }

    private int FindLabelExtent(
        OdfCellAddress label,
        int rowStep,
        int columnStep)
    {
        int maxRow = label.Row;
        int maxColumn = label.Column;
        foreach (OdfCellAddress address in _cellValues.Keys)
        {
            if (!string.Equals(
                address.SheetName,
                label.SheetName,
                StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            maxRow = Math.Max(maxRow, address.Row);
            maxColumn = Math.Max(maxColumn, address.Column);
        }

        int row = label.Row + rowStep;
        int column = label.Column + columnStep;
        bool skippedInitialBlank = false;
        bool foundValue = false;
        int end = rowStep == 1 ? label.Row : label.Column;
        while (row <= maxRow && column <= maxColumn)
        {
            var address = new OdfCellAddress(row, column, label.SheetName);
            bool hasValue = _cellValues.TryGetValue(address, out object? value) &&
                value is not null &&
                !string.IsNullOrEmpty(
                    Convert.ToString(value, CultureInfo.InvariantCulture));
            if (!hasValue)
            {
                if (!foundValue && !skippedInitialBlank)
                {
                    skippedInitialBlank = true;
                    row += rowStep;
                    column += columnStep;
                    continue;
                }

                break;
            }

            foundValue = true;
            end = rowStep == 1 ? row : column;
            row += rowStep;
            column += columnStep;
        }

        return foundValue
            ? end
            : rowStep == 1 ? label.Row : label.Column;
    }

    public bool TryGetPivotData(
        string dataField,
        OdfCellAddress pivotAnchor,
        IReadOnlyDictionary<string, object> filters,
        out object result)
    {
        List<OdfNode> pivots =
        [
            .. EnumerateElements(
                _contentRoot,
                "data-pilot-table",
                OdfNamespaces.Table)
        ];
        for (int pivotIndex = pivots.Count - 1; pivotIndex >= 0; pivotIndex--)
        {
            OdfNode pivot = pivots[pivotIndex];
            string? targetText = pivot.GetAttribute(
                "target-range-address",
                OdfNamespaces.Table);
            if (string.IsNullOrEmpty(targetText) ||
                !OdfCellRange.TryParse(targetText!, out OdfCellRange target) ||
                !ContainsResolved(target, pivotAnchor))
            {
                continue;
            }

            if (TryLoadPivotSource(pivot, out object[,] values) &&
                TryAggregatePivot(
                    pivot,
                    values,
                    dataField,
                    filters,
                    null,
                    out result))
            {
                return true;
            }
        }

        result = OdfFormulaError.NA;
        return false;
    }

    internal bool TryGetPivotDataAlternative(
        OdfCellAddress pivotAnchor,
        string constraints,
        out object result)
    {
        if (!TryTokenizePivotConstraints(constraints, out IReadOnlyList<string> tokens))
        {
            result = OdfFormulaError.Value;
            return false;
        }

        List<OdfNode> pivots =
        [
            .. EnumerateElements(
                _contentRoot,
                "data-pilot-table",
                OdfNamespaces.Table)
        ];
        for (int pivotIndex = pivots.Count - 1; pivotIndex >= 0; pivotIndex--)
        {
            OdfNode pivot = pivots[pivotIndex];
            string? targetText = pivot.GetAttribute(
                "target-range-address",
                OdfNamespaces.Table);
            if (string.IsNullOrEmpty(targetText) ||
                !OdfCellRange.TryParse(targetText!, out OdfCellRange target) ||
                !ContainsResolved(target, pivotAnchor) ||
                !TryLoadPivotSource(pivot, out object[,] values) ||
                !TryResolveAlternativePivotQuery(
                    pivot,
                    values,
                    tokens,
                    out string dataField,
                    out IReadOnlyDictionary<string, object> filters,
                    out string? subtotalFunction))
            {
                continue;
            }

            if (TryAggregatePivot(
                pivot,
                values,
                dataField,
                filters,
                subtotalFunction,
                out result))
            {
                return true;
            }
        }

        result = OdfFormulaError.NA;
        return false;
    }

    public bool TryEvaluateMultipleOperations(
        IReadOnlyList<object> arguments,
        out object result)
    {
        result = OdfFormulaError.NA;
        return false;
    }

    internal bool TryEvaluateMultipleOperations(
        IReadOnlyList<AstNode> arguments,
        out object result)
    {
        result = OdfFormulaError.NA;
        if (arguments.Count is not (3 or 5) ||
            arguments[0] is not CellAddressNode formulaNode ||
            arguments[1] is not CellAddressNode rowInputNode ||
            arguments.Count == 5 && arguments[3] is not CellAddressNode)
        {
            return false;
        }

        OdfCellAddress formulaAddress = ResolveAddress(formulaNode.Address);
        OdfCellAddress rowInput = ResolveAddress(rowInputNode.Address);
        OdfCellAddress? columnInput = arguments.Count == 5
            ? ResolveAddress(((CellAddressNode)arguments[3]).Address)
            : null;
        object rowReplacement = arguments[2].Evaluate(this);
        if (rowReplacement is OdfFormulaError)
        {
            result = rowReplacement;
            return true;
        }

        object? columnReplacement = null;
        if (arguments.Count == 5)
        {
            columnReplacement = arguments[4].Evaluate(this);
            if (columnReplacement is OdfFormulaError)
            {
                result = columnReplacement;
                return true;
            }
        }

        var backups = new List<CellOverrideBackup>(2);
        OdfCellAddress previousCell = CurrentCell;
        try
        {
            backups.Add(OverrideCell(rowInput, rowReplacement));
            if (columnInput.HasValue)
                backups.Add(OverrideCell(columnInput.Value, columnReplacement ?? 0d));
            _evaluator.ClearCache();
            string? formula = GetOriginalFormula(formulaAddress);
            if (string.IsNullOrEmpty(formula))
            {
                result = OdfFormulaError.NA;
                return true;
            }

            CurrentCell = formulaAddress;
            result = _evaluator.Evaluate(formula!, this);
            return true;
        }
        finally
        {
            CurrentCell = previousCell;
            for (int index = backups.Count - 1; index >= 0; index--)
                RestoreCell(backups[index]);
            _evaluator.ClearCache();
        }
    }

    private string? GetOriginalFormula(OdfCellAddress address)
    {
        if (_cellFormulas.TryGetValue(address, out string? formula))
            return formula;
        return _cellNodes.TryGetValue(address, out OdfNode? node)
            ? node.GetAttribute("formula", OdfNamespaces.Table)
            : null;
    }

    private OdfCellAddress ResolveAddress(OdfCellAddress address) =>
        string.IsNullOrEmpty(address.SheetName)
            ? new OdfCellAddress(
                address.Row,
                address.Column,
                CurrentCell.SheetName,
                address.IsRowAbsolute,
                address.IsColumnAbsolute,
                address.IsSheetAbsolute)
            : address;

    private CellOverrideBackup OverrideCell(OdfCellAddress address, object value)
    {
        bool hadValue = _cellValues.TryGetValue(address, out object? oldValue);
        bool hadFormula = _cellFormulas.TryGetValue(address, out string? oldFormula);
        _cellValues[address] = value;
        _cellFormulas.Remove(address);
        return new CellOverrideBackup(
            address,
            hadValue,
            oldValue,
            hadFormula,
            oldFormula);
    }

    private void RestoreCell(CellOverrideBackup backup)
    {
        if (backup.HadValue)
            _cellValues[backup.Address] = backup.Value ?? 0d;
        else
            _cellValues.Remove(backup.Address);
        if (backup.HadFormula)
            _cellFormulas[backup.Address] = backup.Formula!;
        else
            _cellFormulas.Remove(backup.Address);
    }

    private static IEnumerable<OdfNode> EnumerateElements(
        OdfNode node,
        string localName,
        string namespaceUri)
    {
        if (node.NodeType == OdfNodeType.Element &&
            node.LocalName == localName &&
            node.NamespaceUri == namespaceUri)
        {
            yield return node;
        }

        foreach (OdfNode child in node.Children)
        {
            foreach (OdfNode match in EnumerateElements(
                child,
                localName,
                namespaceUri))
            {
                yield return match;
            }
        }
    }

    private static OdfNode? FindDirectChild(
        OdfNode parent,
        string localName,
        string namespaceUri)
    {
        foreach (OdfNode child in parent.Children)
        {
            if (child.NodeType == OdfNodeType.Element &&
                child.LocalName == localName &&
                child.NamespaceUri == namespaceUri)
            {
                return child;
            }
        }

        return null;
    }

    private static bool ContainsResolved(
        OdfCellRange range,
        OdfCellAddress address)
    {
        string? rangeSheet = range.StartAddress.SheetName;
        string? addressSheet = address.SheetName ?? rangeSheet;
        if (!string.Equals(
            rangeSheet,
            addressSheet,
            StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return address.Row >= Math.Min(
                range.StartAddress.Row,
                range.EndAddress.Row) &&
            address.Row <= Math.Max(
                range.StartAddress.Row,
                range.EndAddress.Row) &&
            address.Column >= Math.Min(
                range.StartAddress.Column,
                range.EndAddress.Column) &&
            address.Column <= Math.Max(
                range.StartAddress.Column,
                range.EndAddress.Column);
    }

    private static bool TryFindHeader(
        object[,] values,
        string name,
        out int column)
    {
        for (int index = 0; index < values.GetLength(1); index++)
        {
            if (string.Equals(
                Convert.ToString(values[0, index], CultureInfo.InvariantCulture),
                name,
                StringComparison.OrdinalIgnoreCase))
            {
                column = index;
                return true;
            }
        }

        column = -1;
        return false;
    }

    private bool TryLoadPivotSource(OdfNode pivot, out object[,] values)
    {
        OdfNode? sourceNode = FindDirectChild(
            pivot,
            "source-cell-range",
            OdfNamespaces.Table);
        string? sourceText = sourceNode?.GetAttribute(
            "cell-range-address",
            OdfNamespaces.Table);
        if (!string.IsNullOrEmpty(sourceText) &&
            OdfCellRange.TryParse(sourceText!, out OdfCellRange source))
        {
            values = GetRangeValues(source);
            return values.GetLength(0) >= 2;
        }

        values = new object[0, 0];
        return false;
    }

    private static bool TryAggregatePivot(
        OdfNode pivot,
        object[,] values,
        string dataField,
        IReadOnlyDictionary<string, object> filters,
        string? requiredAggregation,
        out object result)
    {
        if (!TryFindHeader(values, dataField, out int dataColumn))
        {
            result = OdfFormulaError.NA;
            return false;
        }

        string aggregation = FindPivotAggregation(pivot, dataField);
        if (!string.IsNullOrEmpty(requiredAggregation) &&
            !string.Equals(
                aggregation,
                requiredAggregation,
                StringComparison.OrdinalIgnoreCase))
        {
            result = OdfFormulaError.NA;
            return false;
        }

        var filterColumns = new List<KeyValuePair<int, object>>();
        foreach (KeyValuePair<string, object> filter in filters)
        {
            if (!TryFindHeader(values, filter.Key, out int filterColumn))
            {
                result = OdfFormulaError.NA;
                return false;
            }

            filterColumns.Add(new KeyValuePair<int, object>(
                filterColumn,
                filter.Value));
        }

        var matched = new List<object>();
        for (int row = 1; row < values.GetLength(0); row++)
        {
            bool matches = true;
            foreach (KeyValuePair<int, object> filter in filterColumns)
            {
                if (FormulaCoercion.CompareValues(
                    values[row, filter.Key],
                    filter.Value) != 0)
                {
                    matches = false;
                    break;
                }
            }

            if (matches)
                matched.Add(values[row, dataColumn]);
        }

        result = AggregatePivotValues(matched, aggregation);
        return result is not OdfFormulaError;
    }

    private static bool TryResolveAlternativePivotQuery(
        OdfNode pivot,
        object[,] values,
        IReadOnlyList<string> tokens,
        out string dataField,
        out IReadOnlyDictionary<string, object> filters,
        out string? subtotalFunction)
    {
        var dataFields = new List<string>();
        var pivotFields = new List<string>();
        foreach (OdfNode field in pivot.Children)
        {
            if (field.NodeType is not OdfNodeType.Element ||
                field.LocalName != "data-pilot-field" ||
                field.NamespaceUri != OdfNamespaces.Table)
            {
                continue;
            }

            string? fieldName = field.GetAttribute(
                "source-field-name",
                OdfNamespaces.Table);
            if (string.IsNullOrEmpty(fieldName))
                continue;
            pivotFields.Add(fieldName!);
            if (string.Equals(
                field.GetAttribute("orientation", OdfNamespaces.Table),
                "data",
                StringComparison.OrdinalIgnoreCase))
            {
                dataFields.Add(fieldName!);
            }
        }

        string? selectedDataField = null;
        string? selectedFunction = null;
        var resolvedFilters = new Dictionary<string, object>(
            StringComparer.OrdinalIgnoreCase);
        foreach (string token in tokens)
        {
            if (TryParsePivotFieldConstraint(
                token,
                out string fieldName,
                out string member,
                out string? function))
            {
                if (!ContainsIgnoreCase(pivotFields, fieldName) ||
                    (resolvedFilters.TryGetValue(fieldName, out object? previous) &&
                        !string.Equals(
                            Convert.ToString(previous, CultureInfo.InvariantCulture),
                            member,
                            StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrEmpty(function) &&
                        !string.IsNullOrEmpty(selectedFunction) &&
                        !string.Equals(
                            selectedFunction,
                            function,
                            StringComparison.OrdinalIgnoreCase)))
                {
                    dataField = string.Empty;
                    filters = resolvedFilters;
                    subtotalFunction = null;
                    return false;
                }

                resolvedFilters[fieldName] = member;
                if (!string.IsNullOrEmpty(function))
                    selectedFunction = function;
                continue;
            }

            if (ContainsIgnoreCase(dataFields, token))
            {
                if (!string.IsNullOrEmpty(selectedDataField) &&
                    !string.Equals(
                        selectedDataField,
                        token,
                        StringComparison.OrdinalIgnoreCase))
                {
                    dataField = string.Empty;
                    filters = resolvedFilters;
                    subtotalFunction = null;
                    return false;
                }

                selectedDataField = token;
                continue;
            }

            string? memberField = null;
            foreach (string pivotField in pivotFields)
            {
                if (ContainsIgnoreCase(dataFields, pivotField) ||
                    !TryFindHeader(values, pivotField, out int column) ||
                    !ColumnContains(values, column, token))
                {
                    continue;
                }

                if (memberField is not null)
                {
                    dataField = string.Empty;
                    filters = resolvedFilters;
                    subtotalFunction = null;
                    return false;
                }

                memberField = pivotField;
            }

            if (memberField is null)
            {
                dataField = string.Empty;
                filters = resolvedFilters;
                subtotalFunction = null;
                return false;
            }

            resolvedFilters[memberField] = token;
        }

        if (string.IsNullOrEmpty(selectedDataField))
        {
            if (dataFields.Count != 1)
            {
                dataField = string.Empty;
                filters = resolvedFilters;
                subtotalFunction = null;
                return false;
            }

            selectedDataField = dataFields[0];
        }

        dataField = selectedDataField!;
        filters = resolvedFilters;
        subtotalFunction = selectedFunction;
        return true;
    }

    private static bool TryTokenizePivotConstraints(
        string constraints,
        out IReadOnlyList<string> tokens)
    {
        var result = new List<string>();
        var token = new System.Text.StringBuilder();
        bool quoted = false;
        for (int index = 0; index < constraints.Length; index++)
        {
            char current = constraints[index];
            if (current == '\'')
            {
                if (quoted &&
                    index + 1 < constraints.Length &&
                    constraints[index + 1] == '\'')
                {
                    token.Append('\'');
                    index++;
                }
                else
                {
                    quoted = !quoted;
                }

                continue;
            }
            if (char.IsWhiteSpace(current) && !quoted)
            {
                if (token.Length > 0)
                {
                    result.Add(token.ToString());
                    token.Clear();
                }

                continue;
            }

            token.Append(current);
        }

        if (quoted)
        {
            tokens = [];
            return false;
        }
        if (token.Length > 0)
            result.Add(token.ToString());
        tokens = result;
        return true;
    }

    private static bool TryParsePivotFieldConstraint(
        string token,
        out string fieldName,
        out string member,
        out string? function)
    {
        int opening = token.IndexOf('[');
        int closing = token.LastIndexOf(']');
        if (opening <= 0 || closing != token.Length - 1 || closing <= opening + 1)
        {
            fieldName = string.Empty;
            member = string.Empty;
            function = null;
            return false;
        }

        fieldName = token.Substring(0, opening);
        string content = token.Substring(opening + 1, closing - opening - 1);
        int separator = content.LastIndexOf(';');
        if (separator < 0)
        {
            member = content;
            function = null;
        }
        else
        {
            member = content.Substring(0, separator);
            function = content.Substring(separator + 1);
        }

        return fieldName.Length > 0 &&
            member.Length > 0 &&
            (function is null || function.Length > 0);
    }

    private static bool ContainsIgnoreCase(
        IReadOnlyList<string> values,
        string candidate)
    {
        foreach (string value in values)
        {
            if (string.Equals(value, candidate, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool ColumnContains(object[,] values, int column, string candidate)
    {
        for (int row = 1; row < values.GetLength(0); row++)
        {
            if (string.Equals(
                Convert.ToString(values[row, column], CultureInfo.InvariantCulture),
                candidate,
                StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string FindPivotAggregation(
        OdfNode pivot,
        string dataField)
    {
        foreach (OdfNode field in pivot.Children)
        {
            if (field.NodeType == OdfNodeType.Element &&
                field.LocalName == "data-pilot-field" &&
                field.NamespaceUri == OdfNamespaces.Table &&
                string.Equals(
                    field.GetAttribute("source-field-name", OdfNamespaces.Table),
                    dataField,
                    StringComparison.OrdinalIgnoreCase))
            {
                return field.GetAttribute("function", OdfNamespaces.Table) ??
                    "sum";
            }
        }

        return "sum";
    }

    private static object AggregatePivotValues(
        List<object> values,
        string aggregation)
    {
        var numbers = new List<double>();
        foreach (object value in values)
        {
            if (FormulaCoercion.TryCoerceDouble(value, out double number))
                numbers.Add(number);
        }

        string normalized = aggregation.ToLowerInvariant();
        if (normalized is "count")
            return (double)values.Count;
        if (normalized is "countnums")
            return (double)numbers.Count;
        if (numbers.Count == 0)
            return OdfFormulaError.NA;
        if (normalized is "max")
            return Max(numbers);
        if (normalized is "min")
            return Min(numbers);
        if (normalized is "average")
            return Sum(numbers) / numbers.Count;
        if (normalized is "product")
            return Product(numbers);
        if (normalized is "stdev" or "stdevp" or "var" or "varp")
        {
            bool population = global::OdfKit.Internal.OdfStringHelper.EndsWith(normalized, 'p');
            if (!population && numbers.Count < 2)
                return OdfFormulaError.Div0;
            double mean = Sum(numbers) / numbers.Count;
            double sumSquares = 0;
            foreach (double number in numbers)
                sumSquares += (number - mean) * (number - mean);
            double variance = sumSquares /
                (population ? numbers.Count : numbers.Count - 1);
            return normalized.StartsWith("stdev", StringComparison.Ordinal)
                ? Math.Sqrt(variance)
                : variance;
        }

        return Sum(numbers);
    }

    private static double Sum(IReadOnlyList<double> values)
    {
        double result = 0;
        foreach (double value in values)
            result += value;
        return result;
    }

    private static double Product(IReadOnlyList<double> values)
    {
        double result = 1;
        foreach (double value in values)
            result *= value;
        return result;
    }

    private static double Min(List<double> values)
    {
        double result = values[0];
        for (int index = 1; index < values.Count; index++)
            result = Math.Min(result, values[index]);
        return result;
    }

    private static double Max(List<double> values)
    {
        double result = values[0];
        for (int index = 1; index < values.Count; index++)
            result = Math.Max(result, values[index]);
        return result;
    }

    private readonly struct CellOverrideBackup(
        OdfCellAddress address,
        bool hadValue,
        object? value,
        bool hadFormula,
        string? formula)
    {
        internal OdfCellAddress Address { get; } = address;

        internal bool HadValue { get; } = hadValue;

        internal object? Value { get; } = value;

        internal bool HadFormula { get; } = hadFormula;

        internal string? Formula { get; } = formula;
    }

    private static OdfNode? FindSheetNode(OdfNode node, string? sheetName)
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

    private static OdfNode? FindNamedNodeUnderParent(OdfNode parent, string name)
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
                        string.Equals(
                            exprChild.GetAttribute("name", OdfNamespaces.Table),
                            name,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return exprChild;
                    }
                }
            }
        }
        return null;
    }

    private static OdfNode? FindGlobalNamedNode(OdfNode root, string name)
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
                    string.Equals(
                        exprChild.GetAttribute("name", OdfNamespaces.Table),
                        name,
                        StringComparison.OrdinalIgnoreCase))
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
