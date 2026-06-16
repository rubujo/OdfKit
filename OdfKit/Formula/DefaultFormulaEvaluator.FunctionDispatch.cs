using System;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.Formula.AST;

namespace OdfKit.Formula;

public partial class DefaultFormulaEvaluator
{
    #region Function Dispatch

    /// <summary>
    /// 評估所有支援之公式函式的中央分派方法。
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
                "NOT" => arguments.Count == 1 ? (object)!FormulaCoercion.CoerceToBool(arguments[0].Evaluate(context)) : OdfFormulaError.Value,
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

    #endregion
}
