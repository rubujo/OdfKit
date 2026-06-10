using System;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.Spreadsheet;
using OdfKit.Formula.AST;
using OdfKit.DOM;

namespace OdfKit.Formula
{
    public enum OdfFormulaErrorType
    {
        Null,
        Div0,
        Value,
        Ref,
        Name,
        Num,
        NA
    }

    public class OdfFormulaError
    {
        public OdfFormulaErrorType ErrorType { get; }

        public OdfFormulaError(OdfFormulaErrorType errorType)
        {
            ErrorType = errorType;
        }

        public string ToErrorString() => ErrorType switch
        {
            OdfFormulaErrorType.Null => "#NULL!",
            OdfFormulaErrorType.Div0 => "#DIV/0!",
            OdfFormulaErrorType.Value => "#VALUE!",
            OdfFormulaErrorType.Ref => "#REF!",
            OdfFormulaErrorType.Name => "#NAME?",
            OdfFormulaErrorType.Num => "#NUM!",
            OdfFormulaErrorType.NA => "#N/A",
            _ => "#VALUE!"
        };

        public static readonly OdfFormulaError Null = new(OdfFormulaErrorType.Null);
        public static readonly OdfFormulaError Div0 = new(OdfFormulaErrorType.Div0);
        public static readonly OdfFormulaError Value = new(OdfFormulaErrorType.Value);
        public static readonly OdfFormulaError Ref = new(OdfFormulaErrorType.Ref);
        public static readonly OdfFormulaError Name = new(OdfFormulaErrorType.Name);
        public static readonly OdfFormulaError Num = new(OdfFormulaErrorType.Num);
        public static readonly OdfFormulaError NA = new(OdfFormulaErrorType.NA);
    }

    public class DefaultFormulaEvaluator : IOdfFormulaEvaluator
    {
        private readonly Dictionary<OdfCellAddress, object> _cache = new();
        private readonly HashSet<OdfCellAddress> _evaluatingStack = new();

        /// <summary>
        /// Evaluates a specific cell's formula. Uses circular reference checking and caching.
        /// </summary>
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

                formula = CleanFormulaPrefix(formula!);

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
            }
        }

        private static string CleanFormulaPrefix(string formula)
        {
            if (formula.StartsWith("oooc:=", StringComparison.OrdinalIgnoreCase))
                return formula.Substring(6);
            if (formula.StartsWith("of:=", StringComparison.OrdinalIgnoreCase))
                return formula.Substring(4);
            if (formula.StartsWith("="))
                return formula.Substring(1);
            return formula;
        }

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

        public void ClearCache()
        {
            _cache.Clear();
            _evaluatingStack.Clear();
        }

        public void EvaluateFormulasInDocument(OdfNode contentRoot)
        {
            var context = new OdfDomEvaluationContext(contentRoot, this);
            var addresses = new List<OdfCellAddress>(context.CellFormulas.Keys);
            
            foreach (var addr in addresses)
            {
                object result = context.GetCellValue(addr);
                if (context.CellNodes.TryGetValue(addr, out var cellNode))
                {
                    if (result is OdfFormulaError err)
                    {
                        string errStr = err.ToErrorString();
                        OdfKitDiagnostics.Warn($"Formula evaluation error at {addr.ToExcelString()}: {errStr}");
                        
                        cellNode.SetAttribute("value-type", OdfNamespaces.Office, "string", "office");
                        cellNode.SetAttribute("string-value", OdfNamespaces.Office, errStr, "office");
                        cellNode.RemoveAttribute("value", OdfNamespaces.Office);
                        cellNode.RemoveAttribute("boolean-value", OdfNamespaces.Office);
                        UpdateCellDisplayText(cellNode, errStr);
                    }
                    else if (result is double d)
                    {
                        cellNode.SetAttribute("value-type", OdfNamespaces.Office, "float", "office");
                        cellNode.SetAttribute("value", OdfNamespaces.Office, d.ToString(System.Globalization.CultureInfo.InvariantCulture), "office");
                        cellNode.RemoveAttribute("string-value", OdfNamespaces.Office);
                        cellNode.RemoveAttribute("boolean-value", OdfNamespaces.Office);
                        UpdateCellDisplayText(cellNode, d.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    }
                    else if (result is bool b)
                    {
                        cellNode.SetAttribute("value-type", OdfNamespaces.Office, "boolean", "office");
                        cellNode.SetAttribute("boolean-value", OdfNamespaces.Office, b ? "true" : "false", "office");
                        cellNode.RemoveAttribute("value", OdfNamespaces.Office);
                        cellNode.RemoveAttribute("string-value", OdfNamespaces.Office);
                        UpdateCellDisplayText(cellNode, b ? "TRUE" : "FALSE");
                    }
                    else
                    {
                        string str = result?.ToString() ?? "";
                        cellNode.SetAttribute("value-type", OdfNamespaces.Office, "string", "office");
                        cellNode.SetAttribute("string-value", OdfNamespaces.Office, str, "office");
                        cellNode.RemoveAttribute("value", OdfNamespaces.Office);
                        cellNode.RemoveAttribute("boolean-value", OdfNamespaces.Office);
                        UpdateCellDisplayText(cellNode, str);
                    }
                }
            }
        }

        private void UpdateCellDisplayText(OdfNode cellNode, string text)
        {
            OdfNode? pNode = null;
            foreach (var child in cellNode.Children)
            {
                if (child.NodeType == OdfNodeType.Element && child.LocalName == "p" && child.NamespaceUri == OdfNamespaces.Text)
                {
                    pNode = child;
                    break;
                }
            }

            if (pNode == null)
            {
                pNode = new OdfNode(OdfNodeType.Element, "p", OdfNamespaces.Text, "text");
                cellNode.AppendChild(pNode);
            }
            pNode.TextContent = text;
        }

        /// <summary>
        /// Central method to evaluate all supported functions.
        /// </summary>
        internal static object EvaluateFunction(string name, List<AstNode> arguments, IEvaluationContext context)
        {
            string upperName = name.ToUpperInvariant();
            try
            {
                return upperName switch
                {
                    // 1. Logical Functions
                    "IF" => EvaluateIf(arguments, context),
                    "AND" => EvaluateAnd(arguments, context),
                    "OR" => EvaluateOr(arguments, context),

                    // 2. String Functions
                    "CONCAT" => EvaluateConcat(arguments, context),
                    "LEFT" => EvaluateLeft(arguments, context),
                    "RIGHT" => EvaluateRight(arguments, context),
                    "MID" => EvaluateMid(arguments, context),

                    // 3. Statistical Functions
                    "SUM" => EvaluateSum(arguments, context),
                    "AVERAGE" => EvaluateAverage(arguments, context),
                    "COUNT" => EvaluateCount(arguments, context),
                    "SUMIF" => EvaluateSumIf(arguments, context),
                    "COUNTIF" => EvaluateCountIf(arguments, context),

                    // 4. Lookup Functions
                    "VLOOKUP" => EvaluateVLookup(arguments, context),

                    _ => OdfFormulaError.Name
                };
            }
            catch
            {
                return OdfFormulaError.Value;
            }
        }

        #region Logical Functions

        private static object EvaluateIf(List<AstNode> arguments, IEvaluationContext context)
        {
            if (arguments.Count < 2 || arguments.Count > 3) return OdfFormulaError.Value;

            object testVal = arguments[0].Evaluate(context);
            if (testVal is OdfFormulaError) return testVal;

            bool test = CoerceToBool(testVal);

            if (test)
            {
                return arguments[1].Evaluate(context);
            }
            else
            {
                return arguments.Count == 3 ? arguments[2].Evaluate(context) : false;
            }
        }

        private static object EvaluateAnd(List<AstNode> arguments, IEvaluationContext context)
        {
            if (arguments.Count == 0) return OdfFormulaError.Value;

            bool hasLogical = false;
            bool result = true;

            foreach (var node in arguments)
            {
                var val = node.Evaluate(context);
                if (val is OdfFormulaError err) return err;

                foreach (var innerVal in FlattenValues(val))
                {
                    if (TryCoerceToBool(innerVal, out bool b))
                    {
                        hasLogical = true;
                        result &= b;
                    }
                }
            }

            return hasLogical ? result : OdfFormulaError.Value;
        }

        private static object EvaluateOr(List<AstNode> arguments, IEvaluationContext context)
        {
            if (arguments.Count == 0) return OdfFormulaError.Value;

            bool hasLogical = false;
            bool result = false;

            foreach (var node in arguments)
            {
                var val = node.Evaluate(context);
                if (val is OdfFormulaError err) return err;

                foreach (var innerVal in FlattenValues(val))
                {
                    if (TryCoerceToBool(innerVal, out bool b))
                    {
                        hasLogical = true;
                        result |= b;
                    }
                }
            }

            return hasLogical ? result : OdfFormulaError.Value;
        }

        #endregion

        #region String Functions

        private static object EvaluateConcat(List<AstNode> arguments, IEvaluationContext context)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var arg in arguments)
            {
                var val = arg.Evaluate(context);
                if (val is OdfFormulaError err) return err;

                foreach (var item in FlattenValues(val))
                {
                    sb.Append(item?.ToString() ?? "");
                }
            }
            return sb.ToString();
        }

        private static object EvaluateLeft(List<AstNode> arguments, IEvaluationContext context)
        {
            if (arguments.Count < 1 || arguments.Count > 2) return OdfFormulaError.Value;

            var val = arguments[0].Evaluate(context);
            if (val is OdfFormulaError err) return err;

            int count = 1;
            if (arguments.Count == 2)
            {
                var countVal = arguments[1].Evaluate(context);
                if (!TryCoerceDouble(countVal, out double d) || d < 0) return OdfFormulaError.Value;
                count = (int)d;
            }

            string str = val.ToString() ?? "";
            return count >= str.Length ? str : str.Substring(0, count);
        }

        private static object EvaluateRight(List<AstNode> arguments, IEvaluationContext context)
        {
            if (arguments.Count < 1 || arguments.Count > 2) return OdfFormulaError.Value;

            var val = arguments[0].Evaluate(context);
            if (val is OdfFormulaError err) return err;

            int count = 1;
            if (arguments.Count == 2)
            {
                var countVal = arguments[1].Evaluate(context);
                if (!TryCoerceDouble(countVal, out double d) || d < 0) return OdfFormulaError.Value;
                count = (int)d;
            }

            string str = val.ToString() ?? "";
            return count >= str.Length ? str : str.Substring(str.Length - count, count);
        }

        private static object EvaluateMid(List<AstNode> arguments, IEvaluationContext context)
        {
            if (arguments.Count != 3) return OdfFormulaError.Value;

            var val = arguments[0].Evaluate(context);
            if (val is OdfFormulaError err) return err;

            var startVal = arguments[1].Evaluate(context);
            var lenVal = arguments[2].Evaluate(context);

            if (!TryCoerceDouble(startVal, out double startD) || !TryCoerceDouble(lenVal, out double lenD))
                return OdfFormulaError.Value;

            int start = (int)startD;
            int len = (int)lenD;

            if (start < 1 || len < 0) return OdfFormulaError.Value;

            string str = val.ToString() ?? "";
            int zeroIndexStart = start - 1;

            if (zeroIndexStart >= str.Length) return string.Empty;
            return zeroIndexStart + len >= str.Length ? str.Substring(zeroIndexStart) : str.Substring(zeroIndexStart, len);
        }

        #endregion

        #region Statistical Functions

        private static object EvaluateSum(List<AstNode> arguments, IEvaluationContext context)
        {
            double sum = 0;
            foreach (var arg in arguments)
            {
                var val = arg.Evaluate(context);
                if (val is OdfFormulaError err) return err;

                foreach (var innerVal in FlattenValues(val))
                {
                    if (TryCoerceDouble(innerVal, out double d))
                    {
                        sum += d;
                    }
                }
            }
            return sum;
        }

        private static object EvaluateAverage(List<AstNode> arguments, IEvaluationContext context)
        {
            double sum = 0;
            int count = 0;

            foreach (var arg in arguments)
            {
                var val = arg.Evaluate(context);
                if (val is OdfFormulaError err) return err;

                foreach (var innerVal in FlattenValues(val))
                {
                    if (TryCoerceDouble(innerVal, out double d))
                    {
                        sum += d;
                        count++;
                    }
                }
            }

            return count == 0 ? OdfFormulaError.Div0 : sum / count;
        }

        private static object EvaluateCount(List<AstNode> arguments, IEvaluationContext context)
        {
            int count = 0;
            foreach (var arg in arguments)
            {
                var val = arg.Evaluate(context);
                if (val is OdfFormulaError) continue;

                foreach (var innerVal in FlattenValues(val))
                {
                    if (innerVal is double || (innerVal is string s && double.TryParse(s, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out _)))
                    {
                        count++;
                    }
                }
            }
            return (double)count;
        }

        private static object EvaluateSumIf(List<AstNode> arguments, IEvaluationContext context)
        {
            if (arguments.Count < 2 || arguments.Count > 3) return OdfFormulaError.Value;

            if (arguments[0] is not RangeReferenceNode rangeNode) return OdfFormulaError.Value;
            var range = rangeNode.Range;

            var criteriaVal = arguments[1].Evaluate(context);
            if (criteriaVal is OdfFormulaError err) return err;

            OdfCellRange sumRange = range;
            if (arguments.Count == 3)
            {
                if (arguments[2] is not RangeReferenceNode sumRangeNode) return OdfFormulaError.Value;
                sumRange = sumRangeNode.Range;
            }

            object[,] rangeValues = context.GetRangeValues(range);
            object[,] sumValues = context.GetRangeValues(sumRange);

            int rows = rangeValues.GetLength(0);
            int cols = rangeValues.GetLength(1);

            double sum = 0;
            var criteria = new CriteriaMatcher(criteriaVal);

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    object cellVal = rangeValues[r, c];
                    if (criteria.Matches(cellVal))
                    {
                        if (r < sumValues.GetLength(0) && c < sumValues.GetLength(1))
                        {
                            object sumVal = sumValues[r, c];
                            if (TryCoerceDouble(sumVal, out double num))
                            {
                                sum += num;
                            }
                        }
                    }
                }
            }

            return sum;
        }

        private static object EvaluateCountIf(List<AstNode> arguments, IEvaluationContext context)
        {
            if (arguments.Count != 2) return OdfFormulaError.Value;

            if (arguments[0] is not RangeReferenceNode rangeNode) return OdfFormulaError.Value;
            var range = rangeNode.Range;

            var criteriaVal = arguments[1].Evaluate(context);
            if (criteriaVal is OdfFormulaError err) return err;

            object[,] rangeValues = context.GetRangeValues(range);
            int rows = rangeValues.GetLength(0);
            int cols = rangeValues.GetLength(1);

            int count = 0;
            var criteria = new CriteriaMatcher(criteriaVal);

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    if (criteria.Matches(rangeValues[r, c]))
                    {
                        count++;
                    }
                }
            }

            return (double)count;
        }

        #endregion

        #region Lookup Functions

        private static object EvaluateVLookup(List<AstNode> arguments, IEvaluationContext context)
        {
            if (arguments.Count < 3 || arguments.Count > 4) return OdfFormulaError.Value;

            var lookupValue = arguments[0].Evaluate(context);
            if (lookupValue is OdfFormulaError err) return err;

            if (arguments[1] is not RangeReferenceNode rangeNode) return OdfFormulaError.Value;
            var range = rangeNode.Range;

            var colIndexVal = arguments[2].Evaluate(context);
            if (!TryCoerceDouble(colIndexVal, out double colD)) return OdfFormulaError.Value;
            int colIndex = (int)colD;

            bool rangeLookup = true; // Default to approximate match
            if (arguments.Count == 4)
            {
                var lookupTypeVal = arguments[3].Evaluate(context);
                rangeLookup = CoerceToBool(lookupTypeVal);
            }

            object[,] table = context.GetRangeValues(range);
            int tableRows = table.GetLength(0);
            int tableCols = table.GetLength(1);

            if (colIndex < 1 || colIndex > tableCols) return OdfFormulaError.Ref;

            int targetCol = colIndex - 1;

            if (!rangeLookup)
            {
                for (int r = 0; r < tableRows; r++)
                {
                    if (CompareValues(table[r, 0], lookupValue) == 0)
                    {
                        return table[r, targetCol] ?? string.Empty;
                    }
                }
                return OdfFormulaError.NA;
            }
            else
            {
                int matchedRow = -1;
                for (int r = 0; r < tableRows; r++)
                {
                    object cellVal = table[r, 0];
                    int comp = CompareValues(cellVal, lookupValue);
                    if (comp == 0)
                    {
                        return table[r, targetCol] ?? string.Empty;
                    }
                    if (comp < 0)
                    {
                        matchedRow = r;
                    }
                    else
                    {
                        break;
                    }
                }

                if (matchedRow == -1) return OdfFormulaError.NA;
                return table[matchedRow, targetCol] ?? string.Empty;
            }
        }

        #endregion

        #region Helpers & Coercion

        private static IEnumerable<object> FlattenValues(object val)
        {
            if (val is object[,] arr)
            {
                foreach (var item in arr) yield return item;
            }
            else
            {
                yield return val;
            }
        }

        private static bool TryCoerceDouble(object val, out double result)
        {
            if (val is double d) { result = d; return true; }
            if (val is bool b) { result = b ? 1.0 : 0.0; return true; }
            if (val is string s) return double.TryParse(s, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out result);
            result = 0;
            return false;
        }

        private static bool CoerceToBool(object val)
        {
            if (val is bool b) return b;
            if (val is double d) return d != 0;
            if (val is string s)
            {
                if (s.Equals("TRUE", StringComparison.OrdinalIgnoreCase)) return true;
                if (s.Equals("FALSE", StringComparison.OrdinalIgnoreCase)) return false;
            }
            return false;
        }

        private static bool TryCoerceToBool(object val, out bool result)
        {
            if (val is bool b) { result = b; return true; }
            if (val is double d) { result = d != 0; return true; }
            if (val is string s)
            {
                if (s.Equals("TRUE", StringComparison.OrdinalIgnoreCase)) { result = true; return true; }
                if (s.Equals("FALSE", StringComparison.OrdinalIgnoreCase)) { result = false; return true; }
            }
            result = false;
            return false;
        }

        private static int CompareValues(object val1, object val2)
        {
            if (val1 is double d1 && val2 is double d2)
                return d1.CompareTo(d2);
            if (val1 is bool b1 && val2 is bool b2)
                return b1.CompareTo(b2);
            
            if (TryCoerceDouble(val1, out double num1) && TryCoerceDouble(val2, out double num2))
                return num1.CompareTo(num2);

            string s1 = val1?.ToString() ?? "";
            string s2 = val2?.ToString() ?? "";
            return string.Compare(s1, s2, StringComparison.OrdinalIgnoreCase);
        }

        #endregion
    }

    internal class CriteriaMatcher
    {
        private readonly string? _op;
        private readonly object _operand;

        public CriteriaMatcher(object criteria)
        {
            if (criteria is string strCriteria)
            {
                strCriteria = strCriteria.Trim();
                if (strCriteria.StartsWith("<=")) { _op = "<="; _operand = ParseOperand(strCriteria.Substring(2)); }
                else if (strCriteria.StartsWith(">=")) { _op = ">="; _operand = ParseOperand(strCriteria.Substring(2)); }
                else if (strCriteria.StartsWith("<>")) { _op = "<>"; _operand = ParseOperand(strCriteria.Substring(2)); }
                else if (strCriteria.StartsWith("<")) { _op = "<"; _operand = ParseOperand(strCriteria.Substring(1)); }
                else if (strCriteria.StartsWith(">")) { _op = ">"; _operand = ParseOperand(strCriteria.Substring(1)); }
                else if (strCriteria.StartsWith("=")) { _op = "="; _operand = ParseOperand(strCriteria.Substring(1)); }
                else
                {
                    _op = "=";
                    _operand = ParseOperand(strCriteria);
                }
            }
            else
            {
                _op = "=";
                _operand = criteria;
            }
        }

        private static object ParseOperand(string val)
        {
            if (double.TryParse(val, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double num))
                return num;
            if (bool.TryParse(val, out bool b))
                return b;
            return val;
        }

        public bool Matches(object cellVal)
        {
            if (cellVal == null || cellVal is OdfFormulaError) return false;

            int comp = Compare(cellVal, _operand);

            return _op switch
            {
                "=" => comp == 0,
                "<" => comp < 0,
                ">" => comp > 0,
                "<=" => comp <= 0,
                ">=" => comp >= 0,
                "<>" => comp != 0,
                _ => false
            };
        }

        private static int Compare(object val1, object val2)
        {
            if (val1 is double d1 && val2 is double d2)
                return d1.CompareTo(d2);
            if (val1 is bool b1 && val2 is bool b2)
                return b1.CompareTo(b2);

            if (double.TryParse(val1.ToString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double n1) &&
                double.TryParse(val2.ToString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double n2))
            {
                return n1.CompareTo(n2);
            }

            return string.Compare(val1.ToString(), val2.ToString(), StringComparison.OrdinalIgnoreCase);
        }
    }

    internal class OdfDomEvaluationContext : IEvaluationContext
    {
        public OdfCellAddress CurrentCell { get; set; }
        private readonly Dictionary<OdfCellAddress, OdfNode> _cellNodes = new();
        private readonly Dictionary<OdfCellAddress, string> _cellFormulas = new();
        private readonly Dictionary<OdfCellAddress, object> _cellValues = new();
        private readonly DefaultFormulaEvaluator _evaluator;

        public OdfDomEvaluationContext(OdfNode contentRoot, DefaultFormulaEvaluator evaluator)
        {
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
                        if (!string.IsNullOrEmpty(rowRepeatedStr) && int.TryParse(rowRepeatedStr, out int rRep))
                        {
                            rowRepeated = rRep;
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
                                        if (!string.IsNullOrEmpty(colRepeatedStr) && int.TryParse(colRepeatedStr, out int cRep))
                                        {
                                            colRepeated = cRep;
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
            if (_cellValues.TryGetValue(address, out var val)) return val;
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
    }
}
