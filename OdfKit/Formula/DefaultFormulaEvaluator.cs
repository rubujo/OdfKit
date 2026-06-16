using System;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Formula.AST;
using OdfKit.Spreadsheet;

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
public partial class DefaultFormulaEvaluator : IOdfFormulaEvaluator
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
                "NA" => arguments.Count == 0 ? OdfFormulaError.NA : OdfFormulaError.Value,
                "NOT" => arguments.Count == 1 ? (object)!CoerceToBool(arguments[0].Evaluate(context)) : OdfFormulaError.Value,
                "XOR" => EvaluateXor(arguments, context),
                "IFERROR" => EvaluateIfError(arguments, context),
                "IFNA" => EvaluateIfNa(arguments, context),
                "IFS" => EvaluateIfs(arguments, context),
                "SWITCH" => EvaluateSwitch(arguments, context),
                "ISNUMBER" => arguments.Count == 1 ? arguments[0].Evaluate(context) is double : OdfFormulaError.Value,
                "ISTEXT" => arguments.Count == 1 ? arguments[0].Evaluate(context) is string : OdfFormulaError.Value,
                "ISBLANK" => EvaluateIsBlank(arguments, context),
                "ISERROR" => arguments.Count == 1 ? arguments[0].Evaluate(context) is OdfFormulaError : OdfFormulaError.Value,
                "ISNA" => arguments.Count == 1 ? (arguments[0].Evaluate(context) is OdfFormulaError err && err.ErrorType == OdfFormulaErrorType.NA) : OdfFormulaError.Value,
                "ISREF" => EvaluateIsRef(arguments, context),
                "ISLOGICAL" => arguments.Count == 1 ? arguments[0].Evaluate(context) is bool : OdfFormulaError.Value,
                "TYPE" => EvaluateType(arguments, context),
                "ISODD" => EvaluateIsOdd(arguments, context),
                "ISEVEN" => EvaluateIsEven(arguments, context),
                "BITAND" => EvaluateBitAnd(arguments, context),
                "BITOR" => EvaluateBitOr(arguments, context),
                "BITXOR" => EvaluateBitXor(arguments, context),
                "BITLSHIFT" => EvaluateBitLShift(arguments, context),
                "BITRSHIFT" => EvaluateBitRShift(arguments, context),

                // 2. String Functions
                "CONCAT" => EvaluateConcat(arguments, context),
                "CONCATENATE" => EvaluateConcat(arguments, context),
                "SUBSTITUTE" => EvaluateSubstitute(arguments, context),
                "FIND" => EvaluateFind(arguments, context),
                "SEARCH" => EvaluateSearch(arguments, context),
                "REPT" => EvaluateRept(arguments, context),
                "EXACT" => EvaluateExact(arguments, context),
                "CODE" => EvaluateCode(arguments, context),
                "CHAR" => EvaluateChar(arguments, context),
                "TEXT" => EvaluateText(arguments, context),
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
                "COUNTA" => EvaluateCountA(arguments, context),
                "COUNTBLANK" => EvaluateCountBlank(arguments, context),
                "AVERAGEIF" => EvaluateAverageIf(arguments, context),
                "AVERAGEIFS" => EvaluateAverageIfs(arguments, context),
                "SUMIFS" => EvaluateSumIfs(arguments, context),
                "COUNTIFS" => EvaluateCountIfs(arguments, context),
                "MEDIAN" => EvaluateMedian(arguments, context),
                "STDEV" => EvaluateStDev(arguments, context),
                "STDEVP" => EvaluateStDevP(arguments, context),
                "VAR" => EvaluateVar(arguments, context),
                "VARP" => EvaluateVarP(arguments, context),
                "LARGE" => EvaluateLarge(arguments, context),
                "SMALL" => EvaluateSmall(arguments, context),
                "RANK" => EvaluateRank(arguments, context),
                "PERCENTILE" => EvaluatePercentile(arguments, context),
                "QUARTILE" => EvaluateQuartile(arguments, context),
                "SUMIF" => EvaluateSumIf(arguments, context),
                "COUNTIF" => EvaluateCountIf(arguments, context),
                "MAX" => EvaluateMax(arguments, context),
                "MIN" => EvaluateMin(arguments, context),

                // 4. Lookup Functions
                "VLOOKUP" => EvaluateVLookup(arguments, context),
                "HLOOKUP" => EvaluateHLookup(arguments, context),
                "INDEX" => EvaluateIndex(arguments, context),
                "MATCH" => EvaluateMatch(arguments, context),
                "OFFSET" => EvaluateOffset(arguments, context),
                "INDIRECT" => EvaluateIndirect(arguments, context),
                "ROW" => EvaluateRow(arguments, context),
                "COLUMN" => EvaluateColumn(arguments, context),
                "ROWS" => EvaluateRows(arguments, context),
                "COLUMNS" => EvaluateColumns(arguments, context),
                "CHOOSE" => EvaluateChoose(arguments, context),

                // 5. Math Functions
                "INT" => EvaluateInt(arguments, context),
                "SIGN" => EvaluateSign(arguments, context),
                "ODD" => EvaluateOdd(arguments, context),
                "EVEN" => EvaluateEven(arguments, context),
                "PRODUCT" => EvaluateProduct(arguments, context),
                "FACT" => EvaluateFact(arguments, context),
                "MROUND" => EvaluateMRound(arguments, context),
                "ROUNDUP" => EvaluateRoundUp(arguments, context),
                "ROUNDDOWN" => EvaluateRoundDown(arguments, context),
                "RAND" => EvaluateRand(arguments, context),
                "RANDBETWEEN" => EvaluateRandBetween(arguments, context),
                "ASIN" => EvaluateAsin(arguments, context),
                "ACOS" => EvaluateAcos(arguments, context),
                "ATAN" => EvaluateAtan(arguments, context),
                "ATAN2" => EvaluateAtan2(arguments, context),
                "LOG10" => EvaluateLog10(arguments, context),
                "SUMPRODUCT" => EvaluateSumProduct(arguments, context),
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
                "DATEDIF" => EvaluateDateDif(arguments, context),
                "DATEVALUE" => EvaluateDateValue(arguments, context),
                "TIMEVALUE" => EvaluateTimeValue(arguments, context),
                "WEEKDAY" => EvaluateWeekday(arguments, context),
                "WEEKNUM" => EvaluateWeekNum(arguments, context),
                "WORKDAY" => EvaluateWorkday(arguments, context),
                "NETWORKDAYS" => EvaluateNetWorkDays(arguments, context),
                "EDATE" => EvaluateEDate(arguments, context),
                "EOMONTH" => EvaluateEOMonth(arguments, context),
                "ORG.OPENOFFICE.EASTERSUNDAY" => EvaluateOpenOfficeEasterSunday(arguments, context),
                "ORG.OPENOFFICE.ISOMITTED" => EvaluateOpenOfficeIsOmitted(arguments),

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
                "IRR" => EvaluateIrr(arguments, context),
                "MIRR" => EvaluateMirr(arguments, context),
                "SLN" => EvaluateSln(arguments, context),
                "DDB" => EvaluateDdb(arguments, context),

                _ => OdfFormulaError.Name
            };
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            OdfKitDiagnostics.Warn($"Formula function '{name}' threw unexpected exception: {ex.GetType().Name}");
            return OdfFormulaError.Value;
        }
    }

}
