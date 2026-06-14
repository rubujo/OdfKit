using System;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.Spreadsheet;
using OdfKit.Formula.AST;
using OdfKit.DOM;

namespace OdfKit.Formula;

/// <summary>
/// 代表 ODF 公式錯誤的型別。
/// </summary>
public enum OdfFormulaErrorType
{
    /// <summary>
    /// 無交集錯誤 (#NULL!)。
    /// </summary>
    Null,

    /// <summary>
    /// 除以零錯誤 (#DIV/0!)。
    /// </summary>
    Div0,

    /// <summary>
    /// 值錯誤 (#VALUE!)。
    /// </summary>
    Value,

    /// <summary>
    /// 參照無效錯誤 (#REF!)。
    /// </summary>
    Ref,

    /// <summary>
    /// 名稱未識別錯誤 (#NAME?)。
    /// </summary>
    Name,

    /// <summary>
    /// 數字錯誤 (#NUM!)。
    /// </summary>
    Num,

    /// <summary>
    /// 值無法使用錯誤 (#N/A)。
    /// </summary>
    NA
}

/// <summary>
/// 代表 ODF 公式評估錯誤。
/// </summary>
/// <param name="errorType">公式錯誤型別</param>
public class OdfFormulaError(OdfFormulaErrorType errorType)
{
    /// <summary>
    /// 取得公式錯誤的型別。
    /// </summary>
    public OdfFormulaErrorType ErrorType { get; } = errorType;

    /// <summary>
    /// 將公式錯誤轉換為對應的錯誤字串。
    /// </summary>
    /// <returns>公式錯誤字串</returns>
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

    /// <summary>
    /// 代表無交集錯誤的靜態執行個體。
    /// </summary>
    public static readonly OdfFormulaError Null = new(OdfFormulaErrorType.Null);

    /// <summary>
    /// 代表除以零錯誤的靜態執行個體。
    /// </summary>
    public static readonly OdfFormulaError Div0 = new(OdfFormulaErrorType.Div0);

    /// <summary>
    /// 代表值錯誤的靜態執行個體。
    /// </summary>
    public static readonly OdfFormulaError Value = new(OdfFormulaErrorType.Value);

    /// <summary>
    /// 代表參照無效錯誤的靜態執行個體。
    /// </summary>
    public static readonly OdfFormulaError Ref = new(OdfFormulaErrorType.Ref);

    /// <summary>
    /// 代表名稱未識別錯誤的靜態執行個體。
    /// </summary>
    public static readonly OdfFormulaError Name = new(OdfFormulaErrorType.Name);

    /// <summary>
    /// 代表數字錯誤的靜態執行個體。
    /// </summary>
    public static readonly OdfFormulaError Num = new(OdfFormulaErrorType.Num);

    /// <summary>
    /// 代表值無法使用錯誤的靜態執行個體。
    /// </summary>
    public static readonly OdfFormulaError NA = new(OdfFormulaErrorType.NA);
}

/// <summary>
/// 預設的 ODF 公式評估器實現。
/// </summary>
public class DefaultFormulaEvaluator : IOdfFormulaEvaluator
{
    private readonly Dictionary<OdfCellAddress, object> _cache = new();
    private readonly HashSet<OdfCellAddress> _evaluatingStack = new();

    /// <summary>
    /// 評估特定儲長格的公式。使用循環參照檢查與快取機制。
    /// </summary>
    /// <param name="cellAddress">儲存格位址</param>
    /// <param name="context">評估內容模型</param>
    /// <returns>評估後的儲存格值</returns>
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

    /// <summary>
    /// 評估公式字串並傳回結果。
    /// </summary>
    /// <param name="formula">公式字串</param>
    /// <param name="context">評估內容模型</param>
    /// <returns>公式計算後的結果</returns>
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
    /// 清除評估快取與循環相依性追蹤堆疊。
    /// </summary>
    public void ClearCache()
    {
        _cache.Clear();
        _evaluatingStack.Clear();
    }

    /// <summary>
    /// 評估指定內容根節點下的所有文件公式，並更新其顯示文字與屬性。
    /// </summary>
    /// <param name="contentRoot">文件的內容根節點</param>
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
                "TRUE" => true,
                "FALSE" => false,

                // 2. String Functions
                "CONCAT" => EvaluateConcat(arguments, context),
                "LEFT" => EvaluateLeft(arguments, context),
                "RIGHT" => EvaluateRight(arguments, context),
                "MID" => EvaluateMid(arguments, context),
                "LEN" => EvaluateLen(arguments, context),
                "LOWER" => EvaluateLower(arguments, context),
                "UPPER" => EvaluateUpper(arguments, context),
                "TRIM" => EvaluateTrim(arguments, context),
                "REPLACE" => EvaluateReplace(arguments, context),

                // 3. Statistical Functions
                "SUM" => EvaluateSum(arguments, context),
                "AVERAGE" => EvaluateAverage(arguments, context),
                "COUNT" => EvaluateCount(arguments, context),
                "SUMIF" => EvaluateSumIf(arguments, context),
                "COUNTIF" => EvaluateCountIf(arguments, context),
                "MAX" => EvaluateMax(arguments, context),
                "MIN" => EvaluateMin(arguments, context),

                // 4. Lookup Functions
                "VLOOKUP" => EvaluateVLookup(arguments, context),

                // 5. Math Functions
                "ABS" => EvaluateAbs(arguments, context),
                "SQRT" => EvaluateSqrt(arguments, context),
                "ROUND" => EvaluateRound(arguments, context),
                "MOD" => EvaluateMod(arguments, context),
                "POWER" => EvaluatePower(arguments, context),
                "LN" => EvaluateLn(arguments, context),
                "LOG" => EvaluateLog(arguments, context),
                "EXP" => EvaluateExp(arguments, context),
                "CEILING" => EvaluateCeiling(arguments, context),
                "FLOOR" => EvaluateFloor(arguments, context),
                "PI" => EvaluatePi(arguments, context),
                "DEGREES" => EvaluateDegrees(arguments, context),
                "RADIANS" => EvaluateRadians(arguments, context),
                "SIN" => EvaluateSin(arguments, context),
                "COS" => EvaluateCos(arguments, context),
                "TAN" => EvaluateTan(arguments, context),
                "TRUNC" => EvaluateTrunc(arguments, context),

                // 6. Date/Time Functions
                "DATE" => EvaluateDate(arguments, context),
                "DAY" => EvaluateDay(arguments, context),
                "HOUR" => EvaluateHour(arguments, context),
                "MINUTE" => EvaluateMinute(arguments, context),
                "MONTH" => EvaluateMonth(arguments, context),
                "NOW" => EvaluateNow(arguments, context),
                "SECOND" => EvaluateSecond(arguments, context),
                "TIME" => EvaluateTime(arguments, context),
                "TODAY" => EvaluateToday(arguments, context),
                "YEAR" => EvaluateYear(arguments, context),

                // 7. Matrix Functions
                "TRANSPOSE" => EvaluateTranspose(arguments, context),

                // 8. Database Functions
                "DSUM" => EvaluateDSum(arguments, context),
                "DAVERAGE" => EvaluateDAverage(arguments, context),
                "DCOUNT" => EvaluateDCount(arguments, context),
                "DMAX" => EvaluateDMax(arguments, context),
                "DMIN" => EvaluateDMin(arguments, context),

                // 9. Financial Functions
                "PMT" => EvaluatePmt(arguments, context),
                "FV" => EvaluateFv(arguments, context),
                "PV" => EvaluatePv(arguments, context),
                "NPER" => EvaluateNper(arguments, context),
                "RATE" => EvaluateRate(arguments, context),
                "IPMT" => EvaluateIpmt(arguments, context),
                "PPMT" => EvaluatePpmt(arguments, context),

                _ => OdfFormulaError.Name
            };
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            OdfKitDiagnostics.Warn($"Formula function '{name}' threw unexpected exception: {ex.GetType().Name}");
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

    #region Math Functions

    private static object EvaluateAbs(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 1) return OdfFormulaError.Value;
        var val = arguments[0].Evaluate(context);
        if (val is OdfFormulaError err) return err;
        if (!TryCoerceDouble(val, out double d)) return OdfFormulaError.Value;
        return Math.Abs(d);
    }

    private static object EvaluateSqrt(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 1) return OdfFormulaError.Value;
        var val = arguments[0].Evaluate(context);
        if (val is OdfFormulaError err) return err;
        if (!TryCoerceDouble(val, out double d)) return OdfFormulaError.Value;
        if (d < 0) return OdfFormulaError.Num;
        return Math.Sqrt(d);
    }

    private static object EvaluateRound(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count < 1 || arguments.Count > 2) return OdfFormulaError.Value;
        var val = arguments[0].Evaluate(context);
        if (val is OdfFormulaError err) return err;
        if (!TryCoerceDouble(val, out double num)) return OdfFormulaError.Value;

        double digits = 0;
        if (arguments.Count == 2)
        {
            var digitsVal = arguments[1].Evaluate(context);
            if (digitsVal is OdfFormulaError err2) return err2;
            if (!TryCoerceDouble(digitsVal, out digits)) return OdfFormulaError.Value;
        }

        int count = (int)digits;
        if (count >= 0)
        {
            return Math.Round(num, count, MidpointRounding.AwayFromZero);
        }
        else
        {
            double factor = Math.Pow(10, -count);
            return Math.Round(num / factor, 0, MidpointRounding.AwayFromZero) * factor;
        }
    }

    private static object EvaluateMod(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 2) return OdfFormulaError.Value;
        var val1 = arguments[0].Evaluate(context);
        var val2 = arguments[1].Evaluate(context);
        if (val1 is OdfFormulaError err1) return err1;
        if (val2 is OdfFormulaError err2) return err2;

        if (!TryCoerceDouble(val1, out double n) || !TryCoerceDouble(val2, out double d))
            return OdfFormulaError.Value;

        if (d == 0) return OdfFormulaError.Div0;
        return n - d * Math.Floor(n / d);
    }

    private static object EvaluatePower(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 2) return OdfFormulaError.Value;
        var val1 = arguments[0].Evaluate(context);
        var val2 = arguments[1].Evaluate(context);
        if (val1 is OdfFormulaError err1) return err1;
        if (val2 is OdfFormulaError err2) return err2;

        if (!TryCoerceDouble(val1, out double b) || !TryCoerceDouble(val2, out double e))
            return OdfFormulaError.Value;

        if (b < 0 && Math.Abs(e - (int)e) > 1e-9) return OdfFormulaError.Num;
        if (b == 0 && e < 0) return OdfFormulaError.Div0;
        return Math.Pow(b, e);
    }

    private static object EvaluateLn(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 1) return OdfFormulaError.Value;
        var val = arguments[0].Evaluate(context);
        if (val is OdfFormulaError err) return err;
        if (!TryCoerceDouble(val, out double d)) return OdfFormulaError.Value;
        if (d <= 0) return OdfFormulaError.Num;
        return Math.Log(d);
    }

    private static object EvaluateLog(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count < 1 || arguments.Count > 2) return OdfFormulaError.Value;
        var val = arguments[0].Evaluate(context);
        if (val is OdfFormulaError err) return err;
        if (!TryCoerceDouble(val, out double num)) return OdfFormulaError.Value;
        if (num <= 0) return OdfFormulaError.Num;

        double baseVal = 10;
        if (arguments.Count == 2)
        {
            var baseObject = arguments[1].Evaluate(context);
            if (baseObject is OdfFormulaError err2) return err2;
            if (!TryCoerceDouble(baseObject, out baseVal)) return OdfFormulaError.Value;
            if (baseVal <= 0 || baseVal == 1) return OdfFormulaError.Num;
        }

        return Math.Log(num, baseVal);
    }

    private static object EvaluateExp(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 1) return OdfFormulaError.Value;
        var val = arguments[0].Evaluate(context);
        if (val is OdfFormulaError err) return err;
        if (!TryCoerceDouble(val, out double d)) return OdfFormulaError.Value;
        return Math.Exp(d);
    }

    private static object EvaluateCeiling(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count < 1 || arguments.Count > 3) return OdfFormulaError.Value;
        var val = arguments[0].Evaluate(context);
        if (val is OdfFormulaError err) return err;
        if (!TryCoerceDouble(val, out double num)) return OdfFormulaError.Value;

        if (arguments.Count == 1)
        {
            return Math.Ceiling(num);
        }

        var sigVal = arguments[1].Evaluate(context);
        if (sigVal is OdfFormulaError err2) return err2;
        if (!TryCoerceDouble(sigVal, out double significance)) return OdfFormulaError.Value;
        if (significance == 0.0) return 0.0;

        int mode = 1;
        if (arguments.Count == 3)
        {
            var modeVal = arguments[2].Evaluate(context);
            if (modeVal is OdfFormulaError err3) return err3;
            if (!TryCoerceDouble(modeVal, out double m)) return OdfFormulaError.Value;
            mode = (int)m;
        }

        if (num > 0 && significance < 0) return OdfFormulaError.Num;
        if (num == 0.0) return 0.0;

        if (mode == 0)
        {
            return Math.Ceiling(num / significance) * significance;
        }
        else
        {
            if (num < 0)
                return Math.Floor(num / significance) * significance;
            else
                return Math.Ceiling(num / significance) * significance;
        }
    }

    private static object EvaluateFloor(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count < 1 || arguments.Count > 3) return OdfFormulaError.Value;
        var val = arguments[0].Evaluate(context);
        if (val is OdfFormulaError err) return err;
        if (!TryCoerceDouble(val, out double num)) return OdfFormulaError.Value;

        if (arguments.Count == 1)
        {
            return Math.Floor(num);
        }

        var sigVal = arguments[1].Evaluate(context);
        if (sigVal is OdfFormulaError err2) return err2;
        if (!TryCoerceDouble(sigVal, out double significance)) return OdfFormulaError.Value;
        if (significance == 0.0) return 0.0;

        int mode = 1;
        if (arguments.Count == 3)
        {
            var modeVal = arguments[2].Evaluate(context);
            if (modeVal is OdfFormulaError err3) return err3;
            if (!TryCoerceDouble(modeVal, out double m)) return OdfFormulaError.Value;
            mode = (int)m;
        }

        if (num > 0 && significance < 0) return OdfFormulaError.Num;
        if (num == 0.0) return 0.0;

        if (mode == 0)
        {
            return Math.Floor(num / significance) * significance;
        }
        else
        {
            if (num < 0)
                return Math.Ceiling(num / significance) * significance;
            else
                return Math.Floor(num / significance) * significance;
        }
    }

    private static object EvaluatePi(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 0) return OdfFormulaError.Value;
        return Math.PI;
    }

    private static object EvaluateDegrees(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 1) return OdfFormulaError.Value;
        var val = arguments[0].Evaluate(context);
        if (val is OdfFormulaError err) return err;
        if (!TryCoerceDouble(val, out double d)) return OdfFormulaError.Value;
        return d * 180.0 / Math.PI;
    }

    private static object EvaluateRadians(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 1) return OdfFormulaError.Value;
        var val = arguments[0].Evaluate(context);
        if (val is OdfFormulaError err) return err;
        if (!TryCoerceDouble(val, out double d)) return OdfFormulaError.Value;
        return d * Math.PI / 180.0;
    }

    private static object EvaluateSin(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 1) return OdfFormulaError.Value;
        var val = arguments[0].Evaluate(context);
        if (val is OdfFormulaError err) return err;
        if (!TryCoerceDouble(val, out double d)) return OdfFormulaError.Value;
        return Math.Sin(d);
    }

    private static object EvaluateCos(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 1) return OdfFormulaError.Value;
        var val = arguments[0].Evaluate(context);
        if (val is OdfFormulaError err) return err;
        if (!TryCoerceDouble(val, out double d)) return OdfFormulaError.Value;
        return Math.Cos(d);
    }

    private static object EvaluateTan(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 1) return OdfFormulaError.Value;
        var val = arguments[0].Evaluate(context);
        if (val is OdfFormulaError err) return err;
        if (!TryCoerceDouble(val, out double d)) return OdfFormulaError.Value;
        return Math.Tan(d);
    }

    private static object EvaluateTrunc(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count < 1 || arguments.Count > 2) return OdfFormulaError.Value;
        var val = arguments[0].Evaluate(context);
        if (val is OdfFormulaError err) return err;
        if (!TryCoerceDouble(val, out double num)) return OdfFormulaError.Value;

        double digits = 0;
        if (arguments.Count == 2)
        {
            var digitsVal = arguments[1].Evaluate(context);
            if (digitsVal is OdfFormulaError err2) return err2;
            if (!TryCoerceDouble(digitsVal, out digits)) return OdfFormulaError.Value;
        }

        int count = (int)digits;
        double factor = Math.Pow(10, count);
        if (num < 0)
            return Math.Ceiling(num * factor) / factor;
        else
            return Math.Floor(num * factor) / factor;
    }

    #endregion

    #region Additional String Functions

    private static object EvaluateLen(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 1) return OdfFormulaError.Value;
        var val = arguments[0].Evaluate(context);
        if (val is OdfFormulaError err) return err;
        string str = val.ToString() ?? "";
        return (double)str.Length;
    }

    private static object EvaluateLower(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 1) return OdfFormulaError.Value;
        var val = arguments[0].Evaluate(context);
        if (val is OdfFormulaError err) return err;
        string str = val.ToString() ?? "";
        return str.ToLowerInvariant();
    }

    private static object EvaluateUpper(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 1) return OdfFormulaError.Value;
        var val = arguments[0].Evaluate(context);
        if (val is OdfFormulaError err) return err;
        string str = val.ToString() ?? "";
        return str.ToUpperInvariant();
    }

    private static object EvaluateTrim(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 1) return OdfFormulaError.Value;
        var val = arguments[0].Evaluate(context);
        if (val is OdfFormulaError err) return err;
        string str = val.ToString() ?? "";
        str = str.Trim();
        var sb = new System.Text.StringBuilder();
        bool lastWasSpace = false;
        foreach (char c in str)
        {
            if (c == ' ')
            {
                if (!lastWasSpace)
                {
                    sb.Append(c);
                    lastWasSpace = true;
                }
            }
            else
            {
                sb.Append(c);
                lastWasSpace = false;
            }
        }
        return sb.ToString();
    }

    private static object EvaluateReplace(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 4) return OdfFormulaError.Value;
        var valText = arguments[0].Evaluate(context);
        var valStart = arguments[1].Evaluate(context);
        var valLen = arguments[2].Evaluate(context);
        var valNewText = arguments[3].Evaluate(context);

        if (valText is OdfFormulaError err1) return err1;
        if (valStart is OdfFormulaError err2) return err2;
        if (valLen is OdfFormulaError err3) return err3;
        if (valNewText is OdfFormulaError err4) return err4;

        string oldText = valText.ToString() ?? "";
        if (!TryCoerceDouble(valStart, out double startD) || !TryCoerceDouble(valLen, out double lenD))
            return OdfFormulaError.Value;

        int startIndex = (int)startD - 1;
        int len = (int)lenD;
        string newText = valNewText.ToString() ?? "";

        if (startIndex < 0 || len < 0) return OdfFormulaError.Value;
        if (startIndex > oldText.Length) startIndex = oldText.Length;
        int end = startIndex + len;
        if (end > oldText.Length) end = oldText.Length;

        string part1 = oldText.Substring(0, startIndex);
        string part2 = oldText.Substring(end);
        return part1 + newText + part2;
    }

    #endregion

    #region Additional Statistical Functions

    private static object EvaluateMax(List<AstNode> arguments, IEvaluationContext context)
    {
        double max = double.MinValue;
        bool hasNumber = false;
        foreach (var arg in arguments)
        {
            var val = arg.Evaluate(context);
            if (val is OdfFormulaError err) return err;
            foreach (var innerVal in FlattenValues(val))
            {
                if (TryCoerceDouble(innerVal, out double d))
                {
                    if (d > max) max = d;
                    hasNumber = true;
                }
            }
        }
        return hasNumber ? max : 0.0;
    }

    private static object EvaluateMin(List<AstNode> arguments, IEvaluationContext context)
    {
        double min = double.MaxValue;
        bool hasNumber = false;
        foreach (var arg in arguments)
        {
            var val = arg.Evaluate(context);
            if (val is OdfFormulaError err) return err;
            foreach (var innerVal in FlattenValues(val))
            {
                if (TryCoerceDouble(innerVal, out double d))
                {
                    if (d < min) min = d;
                    hasNumber = true;
                }
            }
        }
        return hasNumber ? min : 0.0;
    }

    #endregion

    #region Database and Financial Functions

    private static object EvaluateDatabaseFunction(string name, List<AstNode> arguments, IEvaluationContext context, Func<List<double>, object> aggregator)
    {
        if (arguments.Count != 3) return OdfFormulaError.Value;

        var dbVal = arguments[0].Evaluate(context);
        var fieldVal = arguments[1].Evaluate(context);
        var criteriaVal = arguments[2].Evaluate(context);

        if (dbVal is OdfFormulaError err1) return err1;
        if (fieldVal is OdfFormulaError err2) return err2;
        if (criteriaVal is OdfFormulaError err3) return err3;

        if (dbVal is not object[,] db || criteriaVal is not object[,] crit) return OdfFormulaError.Value;

        int dbRows = db.GetLength(0);
        int dbCols = db.GetLength(1);
        int critRows = crit.GetLength(0);
        int critCols = crit.GetLength(1);

        if (dbRows < 2 || dbCols < 1 || critRows < 2 || critCols < 1) return OdfFormulaError.Value;

        // Resolve field column index
        int fieldCol = -1;
        if (TryCoerceDouble(fieldVal, out double colD))
        {
            fieldCol = (int)colD - 1;
        }
        else
        {
            string fieldStr = fieldVal.ToString() ?? "";
            for (int c = 0; c < dbCols; c++)
            {
                if (string.Equals(db[0, c]?.ToString(), fieldStr, StringComparison.OrdinalIgnoreCase))
                {
                    fieldCol = c;
                    break;
                }
            }
        }

        if (fieldCol < 0 || fieldCol >= dbCols) return OdfFormulaError.Value;

        // Map criteria columns
        var critColMap = new Dictionary<int, int>(); // critCol -> dbCol
        for (int c = 0; c < critCols; c++)
        {
            string header = crit[0, c]?.ToString() ?? "";
            if (string.IsNullOrEmpty(header)) continue;

            int mappedCol = -1;
            for (int dc = 0; dc < dbCols; dc++)
            {
                if (string.Equals(db[0, dc]?.ToString(), header, StringComparison.OrdinalIgnoreCase))
                {
                    mappedCol = dc;
                    break;
                }
            }
            critColMap[c] = mappedCol;
        }

        var selectedValues = new List<double>();

        // 針對資料庫中的每一列（不含標頭列）
        for (int r = 1; r < dbRows; r++)
        {
            bool rowMatches = false;

            // 比對條件列（不含標頭列）
            // 若有任一條件列符合，則 rowMatches = true（各列之間為 OR 邏輯）
            for (int cr = 1; cr < critRows; cr++)
            {
                bool critRowMatches = true;
                bool hasConditions = false;

                // 條件列中的所有條件均必須符合（同一列的各欄之間為 AND 邏輯）
                for (int cc = 0; cc < critCols; cc++)
                {
                    object critCell = crit[cr, cc];
                    if (critCell == null || string.IsNullOrEmpty(critCell.ToString())) continue;

                    hasConditions = true;
                    int dbCol = critColMap.TryGetValue(cc, out int mapped) ? mapped : -1;
                    if (dbCol < 0)
                    {
                        critRowMatches = false;
                        break;
                    }

                    object dbCell = db[r, dbCol];
                    var matcher = new CriteriaMatcher(critCell);
                    if (!matcher.Matches(dbCell))
                    {
                        critRowMatches = false;
                        break;
                    }
                }

                if (hasConditions && critRowMatches)
                {
                    rowMatches = true;
                    break;
                }
            }

            if (rowMatches)
            {
                object cellVal = db[r, fieldCol];
                if (TryCoerceDouble(cellVal, out double val))
                {
                    selectedValues.Add(val);
                }
            }
        }

        return aggregator(selectedValues);
    }

    private static object EvaluateDSum(List<AstNode> arguments, IEvaluationContext context)
    {
        return EvaluateDatabaseFunction("DSUM", arguments, context, list =>
        {
            double sum = 0;
            foreach (var d in list) sum += d;
            return sum;
        });
    }

    private static object EvaluateDAverage(List<AstNode> arguments, IEvaluationContext context)
    {
        return EvaluateDatabaseFunction("DAVERAGE", arguments, context, list =>
        {
            if (list.Count == 0) return OdfFormulaError.Div0;
            double sum = 0;
            foreach (var d in list) sum += d;
            return sum / list.Count;
        });
    }

    private static object EvaluateDCount(List<AstNode> arguments, IEvaluationContext context)
    {
        return EvaluateDatabaseFunction("DCOUNT", arguments, context, list => (double)list.Count);
    }

    private static object EvaluateDMax(List<AstNode> arguments, IEvaluationContext context)
    {
        return EvaluateDatabaseFunction("DMAX", arguments, context, list =>
        {
            if (list.Count == 0) return 0.0;
            double max = double.MinValue;
            foreach (var d in list) if (d > max) max = d;
            return max;
        });
    }

    private static object EvaluateDMin(List<AstNode> arguments, IEvaluationContext context)
    {
        return EvaluateDatabaseFunction("DMIN", arguments, context, list =>
        {
            if (list.Count == 0) return 0.0;
            double min = double.MaxValue;
            foreach (var d in list) if (d < min) min = d;
            return min;
        });
    }

    private static object EvaluatePmt(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count < 3 || arguments.Count > 5) return OdfFormulaError.Value;
        var valRate = arguments[0].Evaluate(context);
        var valNper = arguments[1].Evaluate(context);
        var valPv = arguments[2].Evaluate(context);

        if (valRate is OdfFormulaError err1) return err1;
        if (valNper is OdfFormulaError err2) return err2;
        if (valPv is OdfFormulaError err3) return err3;

        if (!TryCoerceDouble(valRate, out double rate) ||
            !TryCoerceDouble(valNper, out double nper) ||
            !TryCoerceDouble(valPv, out double pv))
            return OdfFormulaError.Value;

        double fv = 0;
        if (arguments.Count >= 4)
        {
            var valFv = arguments[3].Evaluate(context);
            if (valFv is OdfFormulaError err4) return err4;
            if (!TryCoerceDouble(valFv, out fv)) return OdfFormulaError.Value;
        }

        double type = 0;
        if (arguments.Count == 5)
        {
            var valType = arguments[4].Evaluate(context);
            if (valType is OdfFormulaError err5) return err5;
            if (!TryCoerceDouble(valType, out type)) return OdfFormulaError.Value;
        }

        if (nper <= 0) return OdfFormulaError.Value;

        if (rate == 0)
        {
            return -(pv + fv) / nper;
        }
        else
        {
            double p = Math.Pow(1 + rate, nper);
            if (type != 0)
            {
                return -(pv * p + fv) * rate / ((p - 1) * (1 + rate));
            }
            else
            {
                return -(pv * p + fv) * rate / (p - 1);
            }
        }
    }

    private static object EvaluateFv(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count < 3 || arguments.Count > 5) return OdfFormulaError.Value;
        var valRate = arguments[0].Evaluate(context);
        var valNper = arguments[1].Evaluate(context);
        var valPmt = arguments[2].Evaluate(context);

        if (valRate is OdfFormulaError err1) return err1;
        if (valNper is OdfFormulaError err2) return err2;
        if (valPmt is OdfFormulaError err3) return err3;

        if (!TryCoerceDouble(valRate, out double rate) ||
            !TryCoerceDouble(valNper, out double nper) ||
            !TryCoerceDouble(valPmt, out double pmt))
            return OdfFormulaError.Value;

        double pv = 0;
        if (arguments.Count >= 4)
        {
            var valPv = arguments[3].Evaluate(context);
            if (valPv is OdfFormulaError err4) return err4;
            if (!TryCoerceDouble(valPv, out pv)) return OdfFormulaError.Value;
        }

        double type = 0;
        if (arguments.Count == 5)
        {
            var valType = arguments[4].Evaluate(context);
            if (valType is OdfFormulaError err5) return err5;
            if (!TryCoerceDouble(valType, out type)) return OdfFormulaError.Value;
        }

        if (rate == 0)
        {
            return -(pv + pmt * nper);
        }
        else
        {
            double p = Math.Pow(1 + rate, nper);
            if (type != 0)
            {
                return -pv * p - pmt * (1 + rate) * (p - 1) / rate;
            }
            else
            {
                return -pv * p - pmt * (p - 1) / rate;
            }
        }
    }

    private static object EvaluatePv(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count < 3 || arguments.Count > 5) return OdfFormulaError.Value;
        var valRate = arguments[0].Evaluate(context);
        var valNper = arguments[1].Evaluate(context);
        var valPmt = arguments[2].Evaluate(context);

        if (valRate is OdfFormulaError err1) return err1;
        if (valNper is OdfFormulaError err2) return err2;
        if (valPmt is OdfFormulaError err3) return err3;

        if (!TryCoerceDouble(valRate, out double rate) ||
            !TryCoerceDouble(valNper, out double nper) ||
            !TryCoerceDouble(valPmt, out double pmt))
            return OdfFormulaError.Value;

        double fv = 0;
        if (arguments.Count >= 4)
        {
            var valFv = arguments[3].Evaluate(context);
            if (valFv is OdfFormulaError err4) return err4;
            if (!TryCoerceDouble(valFv, out fv)) return OdfFormulaError.Value;
        }

        double type = 0;
        if (arguments.Count == 5)
        {
            var valType = arguments[4].Evaluate(context);
            if (valType is OdfFormulaError err5) return err5;
            if (!TryCoerceDouble(valType, out type)) return OdfFormulaError.Value;
        }

        if (rate == 0)
        {
            return -(fv + pmt * nper);
        }
        else
        {
            double p = Math.Pow(1 + rate, nper);
            if (type != 0)
            {
                return (-fv - pmt * (1 + rate) * (p - 1) / rate) / p;
            }
            else
            {
                return (-fv - pmt * (p - 1) / rate) / p;
            }
        }
    }

    private static object EvaluateNper(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count < 3 || arguments.Count > 5) return OdfFormulaError.Value;
        var valRate = arguments[0].Evaluate(context);
        var valPmt = arguments[1].Evaluate(context);
        var valPv = arguments[2].Evaluate(context);

        if (valRate is OdfFormulaError err1) return err1;
        if (valPmt is OdfFormulaError err2) return err2;
        if (valPv is OdfFormulaError err3) return err3;

        if (!TryCoerceDouble(valRate, out double rate) ||
            !TryCoerceDouble(valPmt, out double pmt) ||
            !TryCoerceDouble(valPv, out double pv))
            return OdfFormulaError.Value;

        double fv = 0;
        if (arguments.Count >= 4)
        {
            var valFv = arguments[3].Evaluate(context);
            if (valFv is OdfFormulaError err4) return err4;
            if (!TryCoerceDouble(valFv, out fv)) return OdfFormulaError.Value;
        }

        double type = 0;
        if (arguments.Count == 5)
        {
            var valType = arguments[4].Evaluate(context);
            if (valType is OdfFormulaError err5) return err5;
            if (!TryCoerceDouble(valType, out type)) return OdfFormulaError.Value;
        }

        if (pmt == 0) return OdfFormulaError.Value;

        if (rate == 0)
        {
            return -(pv + fv) / pmt;
        }
        else
        {
            double num = pmt * (1 + rate * (type != 0 ? 1 : 0)) - fv * rate;
            double den = pmt * (1 + rate * (type != 0 ? 1 : 0)) + pv * rate;
            if (num * den <= 0) return OdfFormulaError.Num;
            return Math.Log(num / den) / Math.Log(1 + rate);
        }
    }

    private static object EvaluateRate(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count < 3 || arguments.Count > 6) return OdfFormulaError.Value;
        var valNper = arguments[0].Evaluate(context);
        var valPmt = arguments[1].Evaluate(context);
        var valPv = arguments[2].Evaluate(context);

        if (valNper is OdfFormulaError err1) return err1;
        if (valPmt is OdfFormulaError err2) return err2;
        if (valPv is OdfFormulaError err3) return err3;

        if (!TryCoerceDouble(valNper, out double nper) ||
            !TryCoerceDouble(valPmt, out double pmt) ||
            !TryCoerceDouble(valPv, out double pv))
            return OdfFormulaError.Value;

        double fv = 0;
        if (arguments.Count >= 4)
        {
            var valFv = arguments[3].Evaluate(context);
            if (valFv is OdfFormulaError err4) return err4;
            if (!TryCoerceDouble(valFv, out fv)) return OdfFormulaError.Value;
        }

        double type = 0;
        if (arguments.Count >= 5)
        {
            var valType = arguments[4].Evaluate(context);
            if (valType is OdfFormulaError err5) return err5;
            if (!TryCoerceDouble(valType, out type)) return OdfFormulaError.Value;
        }

        double guess = 0.1;
        if (arguments.Count == 6)
        {
            var valGuess = arguments[5].Evaluate(context);
            if (valGuess is OdfFormulaError err6) return err6;
            if (!TryCoerceDouble(valGuess, out guess)) return OdfFormulaError.Value;
        }

        double F(double r)
        {
            if (r == 0)
            {
                return pv + pmt * nper + fv;
            }
            else
            {
                double p = Math.Pow(1 + r, nper);
                return pv * p + pmt * (1 + r * (type != 0 ? 1 : 0)) * (p - 1) / r + fv;
            }
        }

        // Secant method solver
        double r0 = guess;
        double r1 = guess * 1.1 + 0.01;
        
        for (int i = 0; i < 100; i++)
        {
            double f0 = F(r0);
            double f1 = F(r1);

            if (Math.Abs(f1 - f0) < 1e-15) break;

            double r_next = r1 - f1 * (r1 - r0) / (f1 - f0);
            if (Math.Abs(r_next - r1) < 1e-9)
            {
                return r_next;
            }

            r0 = r1;
            r1 = r_next;
        }

        return OdfFormulaError.Num;
    }

    private static object EvaluateIpmt(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count < 4 || arguments.Count > 6) return OdfFormulaError.Value;
        var valRate = arguments[0].Evaluate(context);
        var valPer = arguments[1].Evaluate(context);
        var valNper = arguments[2].Evaluate(context);
        var valPv = arguments[3].Evaluate(context);

        if (valRate is OdfFormulaError err1) return err1;
        if (valPer is OdfFormulaError err2) return err2;
        if (valNper is OdfFormulaError err3) return err3;
        if (valPv is OdfFormulaError err4) return err4;

        if (!TryCoerceDouble(valRate, out double rate) ||
            !TryCoerceDouble(valPer, out double per) ||
            !TryCoerceDouble(valNper, out double nper) ||
            !TryCoerceDouble(valPv, out double pv))
            return OdfFormulaError.Value;

        double fv = 0;
        if (arguments.Count >= 5)
        {
            var valFv = arguments[4].Evaluate(context);
            if (valFv is OdfFormulaError err5) return err5;
            if (!TryCoerceDouble(valFv, out fv)) return OdfFormulaError.Value;
        }

        double type = 0;
        if (arguments.Count == 6)
        {
            var valType = arguments[5].Evaluate(context);
            if (valType is OdfFormulaError err6) return err6;
            if (!TryCoerceDouble(valType, out type)) return OdfFormulaError.Value;
        }

        if (per < 1 || per > nper) return OdfFormulaError.Value;

        // Calculate standard PMT
        var pmtArgs = new List<AstNode> {
            new LiteralNode(rate),
            new LiteralNode(nper),
            new LiteralNode(pv),
            new LiteralNode(fv),
            new LiteralNode(type)
        };
        var pmtResult = EvaluatePmt(pmtArgs, context);
        if (pmtResult is OdfFormulaError) return pmtResult;
        double pmt = (double)pmtResult;

        // Simple balance progression
        double balance = pv;
        double interest = 0;

        for (int t = 1; t <= per; t++)
        {
            if (type != 0)
            {
                if (t == 1)
                {
                    interest = 0;
                    balance = balance + pmt;
                }
                else
                {
                    interest = (balance) * rate;
                    balance = balance + interest + pmt;
                }
            }
            else
            {
                interest = balance * rate;
                balance = balance + interest + pmt;
            }
        }

        return -interest;
    }

    private static object EvaluatePpmt(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count < 4 || arguments.Count > 6) return OdfFormulaError.Value;

        // PPMT = PMT - IPMT
        var rate = arguments[0].Evaluate(context);
        var per = arguments[1].Evaluate(context);
        var nper = arguments[2].Evaluate(context);
        var pv = arguments[3].Evaluate(context);

        if (rate is OdfFormulaError err1) return err1;
        if (per is OdfFormulaError err2) return err2;
        if (nper is OdfFormulaError err3) return err3;
        if (pv is OdfFormulaError err4) return err4;

        double fv = 0;
        if (arguments.Count >= 5)
        {
            var valFv = arguments[4].Evaluate(context);
            if (valFv is OdfFormulaError err5) return err5;
            if (!TryCoerceDouble(valFv, out fv)) return OdfFormulaError.Value;
        }

        double type = 0;
        if (arguments.Count == 6)
        {
            var valType = arguments[5].Evaluate(context);
            if (valType is OdfFormulaError err6) return err6;
            if (!TryCoerceDouble(valType, out type)) return OdfFormulaError.Value;
        }

        var pmtArgs = new List<AstNode> {
            new LiteralNode(rate),
            new LiteralNode(nper),
            new LiteralNode(pv),
            new LiteralNode(fv),
            new LiteralNode(type)
        };
        var pmtResult = EvaluatePmt(pmtArgs, context);
        if (pmtResult is OdfFormulaError) return pmtResult;

        var ipmtResult = EvaluateIpmt(arguments, context);
        if (ipmtResult is OdfFormulaError) return ipmtResult;

        return (double)pmtResult - (double)ipmtResult;
    }

    #endregion

    #region Date and Time Functions

    private static readonly DateTime Epoch = new DateTime(1899, 12, 30, 0, 0, 0, DateTimeKind.Unspecified);

    private static bool TryCoerceDateTime(object val, out DateTime dt)
    {
        dt = DateTime.MinValue;
        if (val == null || val is OdfFormulaError) return false;

        if (val is double d)
        {
            try
            {
                dt = Epoch.AddDays(d);
                return true;
            }
            catch
            {
                return false;
            }
        }

        string s = val.ToString() ?? "";
        if (double.TryParse(s, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double num))
        {
            try
            {
                dt = Epoch.AddDays(num);
                return true;
            }
            catch
            {
                return false;
            }
        }

        if (s.StartsWith("PT", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var ts = System.Xml.XmlConvert.ToTimeSpan(s);
                dt = Epoch.Add(ts);
                return true;
            }
            catch {}
        }

        if (DateTime.TryParse(s, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out dt))
        {
            return true;
        }

        return false;
    }

    private static object EvaluateDate(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 3) return OdfFormulaError.Value;
        var valY = arguments[0].Evaluate(context);
        var valM = arguments[1].Evaluate(context);
        var valD = arguments[2].Evaluate(context);

        if (valY is OdfFormulaError err1) return err1;
        if (valM is OdfFormulaError err2) return err2;
        if (valD is OdfFormulaError err3) return err3;

        if (!TryCoerceDouble(valY, out double yD) || !TryCoerceDouble(valM, out double mD) || !TryCoerceDouble(valD, out double dD))
            return OdfFormulaError.Value;

        int y = (int)yD;
        int m = (int)mD;
        int d = (int)dD;

        if (y >= 0 && y < 1900) y += 1900;
        try
        {
            DateTime baseDate = new DateTime(y, 1, 1);
            DateTime dt = baseDate.AddMonths(m - 1).AddDays(d - 1);
            return (dt - Epoch).TotalDays;
        }
        catch
        {
            return OdfFormulaError.Value;
        }
    }

    private static object EvaluateDay(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 1) return OdfFormulaError.Value;
        var val = arguments[0].Evaluate(context);
        if (val is OdfFormulaError err) return err;
        if (!TryCoerceDateTime(val, out DateTime dt)) return OdfFormulaError.Value;
        return (double)dt.Day;
    }

    private static object EvaluateHour(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 1) return OdfFormulaError.Value;
        var val = arguments[0].Evaluate(context);
        if (val is OdfFormulaError err) return err;
        if (!TryCoerceDateTime(val, out DateTime dt)) return OdfFormulaError.Value;
        return (double)dt.Hour;
    }

    private static object EvaluateMinute(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 1) return OdfFormulaError.Value;
        var val = arguments[0].Evaluate(context);
        if (val is OdfFormulaError err) return err;
        if (!TryCoerceDateTime(val, out DateTime dt)) return OdfFormulaError.Value;
        return (double)dt.Minute;
    }

    private static object EvaluateMonth(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 1) return OdfFormulaError.Value;
        var val = arguments[0].Evaluate(context);
        if (val is OdfFormulaError err) return err;
        if (!TryCoerceDateTime(val, out DateTime dt)) return OdfFormulaError.Value;
        return (double)dt.Month;
    }

    private static object EvaluateNow(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 0) return OdfFormulaError.Value;
        return (DateTime.Now - Epoch).TotalDays;
    }

    private static object EvaluateSecond(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 1) return OdfFormulaError.Value;
        var val = arguments[0].Evaluate(context);
        if (val is OdfFormulaError err) return err;
        if (!TryCoerceDateTime(val, out DateTime dt)) return OdfFormulaError.Value;
        return (double)dt.Second;
    }

    private static object EvaluateTime(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 3) return OdfFormulaError.Value;
        var valH = arguments[0].Evaluate(context);
        var valM = arguments[1].Evaluate(context);
        var valS = arguments[2].Evaluate(context);

        if (valH is OdfFormulaError err1) return err1;
        if (valM is OdfFormulaError err2) return err2;
        if (valS is OdfFormulaError err3) return err3;

        if (!TryCoerceDouble(valH, out double h) || !TryCoerceDouble(valM, out double m) || !TryCoerceDouble(valS, out double s))
            return OdfFormulaError.Value;

        return (h * 3600.0 + m * 60.0 + s) / 86400.0;
    }

    private static object EvaluateToday(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 0) return OdfFormulaError.Value;
        return (DateTime.Today - Epoch).TotalDays;
    }

    private static object EvaluateYear(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 1) return OdfFormulaError.Value;
        var val = arguments[0].Evaluate(context);
        if (val is OdfFormulaError err) return err;
        if (!TryCoerceDateTime(val, out DateTime dt)) return OdfFormulaError.Value;
        return (double)dt.Year;
    }

    #endregion

    #region Matrix Functions

    private static object EvaluateTranspose(List<AstNode> arguments, IEvaluationContext context)
    {
        if (arguments.Count != 1) return OdfFormulaError.Value;
        var val = arguments[0].Evaluate(context);
        if (val is OdfFormulaError err) return err;

        if (val is object[,] arr)
        {
            int rows = arr.GetLength(0);
            int cols = arr.GetLength(1);
            var result = new object[cols, rows];
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    result[c, r] = arr[r, c];
                }
            }
            return result;
        }
        else
        {
            var result = new object[1, 1];
            result[0, 0] = val;
            return result;
        }
    }

    #endregion

    #region Helpers & Coercion

    private static IEnumerable<object> FlattenValues(object val)
    {
        if (val is OdfReferenceList refList)
        {
            foreach (var r in refList.References)
            {
                foreach (var item in FlattenValues(r)) yield return item;
            }
        }
        else if (val is object[,] arr)
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
                    const int MaxRepeat = 10000;
                    int rowRepeated = 1;
                    string? rowRepeatedStr = rowChild.GetAttribute("number-rows-repeated", OdfNamespaces.Table);
                    if (!string.IsNullOrEmpty(rowRepeatedStr) && int.TryParse(rowRepeatedStr, out int rRep) && rRep > 0)
                    {
                        rowRepeated = Math.Min(rRep, MaxRepeat);
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
                                        colRepeated = Math.Min(cRep, MaxRepeat);
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
        if (string.IsNullOrEmpty(sheetName)) return null;
        if (node.NodeType == OdfNodeType.Element && node.LocalName == "table" && node.NamespaceUri == OdfNamespaces.Table)
        {
            if (node.GetAttribute("name", OdfNamespaces.Table) == sheetName)
                return node;
        }
        foreach (var child in node.Children)
        {
            var match = FindSheetNode(child, sheetName);
            if (match != null) return match;
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
            if (match != null) return match;
        }

        return null;
    }
}
