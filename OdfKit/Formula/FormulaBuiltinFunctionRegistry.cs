using System;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.Formula.AST;

namespace OdfKit.Formula;

/// <summary>
/// 內建公式函式名稱至評估處理常式的註冊表。
/// </summary>
internal delegate object FormulaBuiltinHandler(List<AstNode> arguments, IEvaluationContext context);

/// <summary>
/// 內建公式函式分派註冊表（取代巨型 switch）。
/// </summary>
internal static class FormulaBuiltinFunctionRegistry
{
    private static readonly Lazy<IReadOnlyDictionary<string, FormulaBuiltinHandler>> s_registry =
        new(CreateBuiltinRegistry);

    /// <summary>
    /// 依函式名稱分派至已註冊的內建處理常式。
    /// </summary>
    internal static object Evaluate(string name, List<AstNode> arguments, IEvaluationContext context)
    {
        try
        {
            if (s_registry.Value.TryGetValue(name, out FormulaBuiltinHandler? handler))
                return handler(arguments, context);

            return OdfFormulaError.Name;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            OdfKitDiagnostics.Warn($"Formula function '{name}' threw unexpected exception: {ex.GetType().Name}");
            return OdfFormulaError.Value;
        }
    }

    private static Dictionary<string, FormulaBuiltinHandler> CreateBuiltinRegistry()
    {
        return new Dictionary<string, FormulaBuiltinHandler>(StringComparer.OrdinalIgnoreCase)
        {
            // 邏輯
            ["IF"] = FormulaLogicalFunctionHandlers.EvaluateIf,
            ["AND"] = FormulaLogicalFunctionHandlers.EvaluateAnd,
            ["OR"] = FormulaLogicalFunctionHandlers.EvaluateOr,
            ["TRUE"] = static (_, _) => true,
            ["FALSE"] = static (_, _) => false,
            ["NA"] = static (args, _) => args.Count == 0 ? OdfFormulaError.NA : OdfFormulaError.Value,
            ["NOT"] = static (args, ctx) => args.Count == 1
                ? (object)!FormulaCoercion.CoerceToBool(args[0].Evaluate(ctx))
                : OdfFormulaError.Value,
            ["XOR"] = FormulaLogicalFunctionHandlers.EvaluateXor,
            ["IFERROR"] = FormulaLogicalFunctionHandlers.EvaluateIfError,
            ["IFNA"] = FormulaLogicalFunctionHandlers.EvaluateIfNa,
            ["IFS"] = FormulaLogicalFunctionHandlers.EvaluateIfs,
            ["SWITCH"] = FormulaLogicalFunctionHandlers.EvaluateSwitch,
            ["ISNUMBER"] = static (args, ctx) => args.Count == 1 ? args[0].Evaluate(ctx) is double : OdfFormulaError.Value,
            ["ISTEXT"] = static (args, ctx) => args.Count == 1 ? args[0].Evaluate(ctx) is string : OdfFormulaError.Value,
            ["ISBLANK"] = FormulaLogicalFunctionHandlers.EvaluateIsBlank,
            ["ISERROR"] = static (args, ctx) => args.Count == 1 ? args[0].Evaluate(ctx) is OdfFormulaError : OdfFormulaError.Value,
            ["ISNA"] = static (args, ctx) => args.Count == 1
                ? args[0].Evaluate(ctx) is OdfFormulaError err && err.ErrorType == OdfFormulaErrorType.NA
                : OdfFormulaError.Value,
            ["ISREF"] = FormulaLogicalFunctionHandlers.EvaluateIsRef,
            ["ISLOGICAL"] = static (args, ctx) => args.Count == 1 ? args[0].Evaluate(ctx) is bool : OdfFormulaError.Value,
            ["TYPE"] = FormulaLogicalFunctionHandlers.EvaluateType,
            ["ISODD"] = FormulaLogicalFunctionHandlers.EvaluateIsOdd,
            ["ISEVEN"] = FormulaLogicalFunctionHandlers.EvaluateIsEven,
            ["BITAND"] = FormulaLogicalFunctionHandlers.EvaluateBitAnd,
            ["BITOR"] = FormulaLogicalFunctionHandlers.EvaluateBitOr,
            ["BITXOR"] = FormulaLogicalFunctionHandlers.EvaluateBitXor,
            ["BITLSHIFT"] = FormulaLogicalFunctionHandlers.EvaluateBitLShift,
            ["BITRSHIFT"] = FormulaLogicalFunctionHandlers.EvaluateBitRShift,

            // 字串
            ["CONCAT"] = FormulaStringFunctionHandlers.EvaluateConcat,
            ["CONCATENATE"] = FormulaStringFunctionHandlers.EvaluateConcat,
            ["SUBSTITUTE"] = FormulaStringFunctionHandlers.EvaluateSubstitute,
            ["FIND"] = FormulaStringFunctionHandlers.EvaluateFind,
            ["SEARCH"] = FormulaStringFunctionHandlers.EvaluateSearch,
            ["REPT"] = FormulaStringFunctionHandlers.EvaluateRept,
            ["EXACT"] = FormulaStringFunctionHandlers.EvaluateExact,
            ["CODE"] = FormulaStringFunctionHandlers.EvaluateCode,
            ["CHAR"] = FormulaStringFunctionHandlers.EvaluateChar,
            ["TEXT"] = FormulaStringFunctionHandlers.EvaluateText,
            ["LEFT"] = FormulaStringFunctionHandlers.EvaluateLeft,
            ["RIGHT"] = FormulaStringFunctionHandlers.EvaluateRight,
            ["MID"] = FormulaStringFunctionHandlers.EvaluateMid,
            ["LEN"] = FormulaStringFunctionHandlers.EvaluateLen,
            ["LOWER"] = FormulaStringFunctionHandlers.EvaluateLower,
            ["UPPER"] = FormulaStringFunctionHandlers.EvaluateUpper,
            ["TRIM"] = FormulaStringFunctionHandlers.EvaluateTrim,
            ["REPLACE"] = FormulaStringFunctionHandlers.EvaluateReplace,

            // 統計
            ["SUM"] = FormulaStatisticalFunctionHandlers.EvaluateSum,
            ["AVERAGE"] = FormulaStatisticalFunctionHandlers.EvaluateAverage,
            ["COUNT"] = FormulaStatisticalFunctionHandlers.EvaluateCount,
            ["COUNTA"] = FormulaStatisticalFunctionHandlers.EvaluateCountA,
            ["COUNTBLANK"] = FormulaStatisticalFunctionHandlers.EvaluateCountBlank,
            ["AVERAGEIF"] = FormulaStatisticalFunctionHandlers.EvaluateAverageIf,
            ["AVERAGEIFS"] = FormulaStatisticalFunctionHandlers.EvaluateAverageIfs,
            ["SUMIFS"] = FormulaStatisticalFunctionHandlers.EvaluateSumIfs,
            ["COUNTIFS"] = FormulaStatisticalFunctionHandlers.EvaluateCountIfs,
            ["MEDIAN"] = FormulaStatisticalFunctionHandlers.EvaluateMedian,
            ["STDEV"] = FormulaStatisticalFunctionHandlers.EvaluateStDev,
            ["STDEVP"] = FormulaStatisticalFunctionHandlers.EvaluateStDevP,
            ["VAR"] = FormulaStatisticalFunctionHandlers.EvaluateVar,
            ["VARP"] = FormulaStatisticalFunctionHandlers.EvaluateVarP,
            ["LARGE"] = FormulaStatisticalFunctionHandlers.EvaluateLarge,
            ["SMALL"] = FormulaStatisticalFunctionHandlers.EvaluateSmall,
            ["RANK"] = FormulaStatisticalFunctionHandlers.EvaluateRank,
            ["PERCENTILE"] = FormulaStatisticalFunctionHandlers.EvaluatePercentile,
            ["QUARTILE"] = FormulaStatisticalFunctionHandlers.EvaluateQuartile,
            ["SUMIF"] = FormulaStatisticalFunctionHandlers.EvaluateSumIf,
            ["COUNTIF"] = FormulaStatisticalFunctionHandlers.EvaluateCountIf,
            ["MAX"] = FormulaStatisticalFunctionHandlers.EvaluateMax,
            ["MIN"] = FormulaStatisticalFunctionHandlers.EvaluateMin,

            // 查閱
            ["VLOOKUP"] = FormulaLookupFunctionHandlers.EvaluateVLookup,
            ["HLOOKUP"] = FormulaLookupFunctionHandlers.EvaluateHLookup,
            ["INDEX"] = FormulaLookupFunctionHandlers.EvaluateIndex,
            ["MATCH"] = FormulaLookupFunctionHandlers.EvaluateMatch,
            ["OFFSET"] = FormulaLookupFunctionHandlers.EvaluateOffset,
            ["INDIRECT"] = FormulaLookupFunctionHandlers.EvaluateIndirect,
            ["ROW"] = FormulaLookupFunctionHandlers.EvaluateRow,
            ["COLUMN"] = FormulaLookupFunctionHandlers.EvaluateColumn,
            ["ROWS"] = FormulaLookupFunctionHandlers.EvaluateRows,
            ["COLUMNS"] = FormulaLookupFunctionHandlers.EvaluateColumns,
            ["CHOOSE"] = FormulaLookupFunctionHandlers.EvaluateChoose,

            // 數學
            ["INT"] = FormulaMathFunctionHandlers.EvaluateInt,
            ["SIGN"] = FormulaMathFunctionHandlers.EvaluateSign,
            ["ODD"] = FormulaMathFunctionHandlers.EvaluateOdd,
            ["EVEN"] = FormulaMathFunctionHandlers.EvaluateEven,
            ["PRODUCT"] = FormulaMathFunctionHandlers.EvaluateProduct,
            ["FACT"] = FormulaMathFunctionHandlers.EvaluateFact,
            ["MROUND"] = FormulaMathFunctionHandlers.EvaluateMRound,
            ["ROUNDUP"] = FormulaMathFunctionHandlers.EvaluateRoundUp,
            ["ROUNDDOWN"] = FormulaMathFunctionHandlers.EvaluateRoundDown,
            ["RAND"] = FormulaMathFunctionHandlers.EvaluateRand,
            ["RANDBETWEEN"] = FormulaMathFunctionHandlers.EvaluateRandBetween,
            ["ASIN"] = FormulaMathFunctionHandlers.EvaluateAsin,
            ["ACOS"] = FormulaMathFunctionHandlers.EvaluateAcos,
            ["ATAN"] = FormulaMathFunctionHandlers.EvaluateAtan,
            ["ATAN2"] = FormulaMathFunctionHandlers.EvaluateAtan2,
            ["LOG10"] = FormulaMathFunctionHandlers.EvaluateLog10,
            ["SUMPRODUCT"] = FormulaMathFunctionHandlers.EvaluateSumProduct,
            ["ABS"] = FormulaMathFunctionHandlers.EvaluateAbs,
            ["SQRT"] = FormulaMathFunctionHandlers.EvaluateSqrt,
            ["ROUND"] = FormulaMathFunctionHandlers.EvaluateRound,
            ["MOD"] = FormulaMathFunctionHandlers.EvaluateMod,
            ["POWER"] = FormulaMathFunctionHandlers.EvaluatePower,
            ["LN"] = FormulaMathFunctionHandlers.EvaluateLn,
            ["LOG"] = FormulaMathFunctionHandlers.EvaluateLog,
            ["EXP"] = FormulaMathFunctionHandlers.EvaluateExp,
            ["CEILING"] = FormulaMathFunctionHandlers.EvaluateCeiling,
            ["FLOOR"] = FormulaMathFunctionHandlers.EvaluateFloor,
            ["PI"] = FormulaMathFunctionHandlers.EvaluatePi,
            ["DEGREES"] = FormulaMathFunctionHandlers.EvaluateDegrees,
            ["RADIANS"] = FormulaMathFunctionHandlers.EvaluateRadians,
            ["SIN"] = FormulaMathFunctionHandlers.EvaluateSin,
            ["COS"] = FormulaMathFunctionHandlers.EvaluateCos,
            ["TAN"] = FormulaMathFunctionHandlers.EvaluateTan,
            ["TRUNC"] = FormulaMathFunctionHandlers.EvaluateTrunc,

            // 日期／時間
            ["DATE"] = FormulaDateTimeFunctionHandlers.EvaluateDate,
            ["DAY"] = FormulaDateTimeFunctionHandlers.EvaluateDay,
            ["HOUR"] = FormulaDateTimeFunctionHandlers.EvaluateHour,
            ["MINUTE"] = FormulaDateTimeFunctionHandlers.EvaluateMinute,
            ["MONTH"] = FormulaDateTimeFunctionHandlers.EvaluateMonth,
            ["NOW"] = FormulaDateTimeFunctionHandlers.EvaluateNow,
            ["SECOND"] = FormulaDateTimeFunctionHandlers.EvaluateSecond,
            ["TIME"] = FormulaDateTimeFunctionHandlers.EvaluateTime,
            ["TODAY"] = FormulaDateTimeFunctionHandlers.EvaluateToday,
            ["YEAR"] = FormulaDateTimeFunctionHandlers.EvaluateYear,
            ["DATEDIF"] = FormulaDateTimeFunctionHandlers.EvaluateDateDif,
            ["DATEVALUE"] = FormulaDateTimeFunctionHandlers.EvaluateDateValue,
            ["TIMEVALUE"] = FormulaDateTimeFunctionHandlers.EvaluateTimeValue,
            ["WEEKDAY"] = FormulaDateTimeFunctionHandlers.EvaluateWeekday,
            ["WEEKNUM"] = FormulaDateTimeFunctionHandlers.EvaluateWeekNum,
            ["WORKDAY"] = FormulaDateTimeFunctionHandlers.EvaluateWorkday,
            ["NETWORKDAYS"] = FormulaDateTimeFunctionHandlers.EvaluateNetWorkDays,
            ["EDATE"] = FormulaDateTimeFunctionHandlers.EvaluateEDate,
            ["EOMONTH"] = FormulaDateTimeFunctionHandlers.EvaluateEOMonth,
            ["ORG.OPENOFFICE.EASTERSUNDAY"] = FormulaDateTimeFunctionHandlers.EvaluateOpenOfficeEasterSunday,
            ["ORG.OPENOFFICE.ISOMITTED"] = static (args, _) => FormulaDateTimeFunctionHandlers.EvaluateOpenOfficeIsOmitted(args),

            // 矩陣
            ["TRANSPOSE"] = FormulaMatrixFunctionHandlers.EvaluateTranspose,

            // 資料庫
            ["DSUM"] = FormulaDatabaseFunctionHandlers.EvaluateDSum,
            ["DAVERAGE"] = FormulaDatabaseFunctionHandlers.EvaluateDAverage,
            ["DCOUNT"] = FormulaDatabaseFunctionHandlers.EvaluateDCount,
            ["DMAX"] = FormulaDatabaseFunctionHandlers.EvaluateDMax,
            ["DMIN"] = FormulaDatabaseFunctionHandlers.EvaluateDMin,

            // 財務
            ["PMT"] = FormulaFinancialFunctionHandlers.EvaluatePmt,
            ["FV"] = FormulaFinancialFunctionHandlers.EvaluateFv,
            ["PV"] = FormulaFinancialFunctionHandlers.EvaluatePv,
            ["NPER"] = FormulaFinancialFunctionHandlers.EvaluateNper,
            ["RATE"] = FormulaFinancialFunctionHandlers.EvaluateRate,
            ["IPMT"] = FormulaFinancialFunctionHandlers.EvaluateIpmt,
            ["PPMT"] = FormulaFinancialFunctionHandlers.EvaluatePpmt,
            ["IRR"] = FormulaFinancialFunctionHandlers.EvaluateIrr,
            ["MIRR"] = FormulaFinancialFunctionHandlers.EvaluateMirr,
            ["SLN"] = FormulaFinancialFunctionHandlers.EvaluateSln,
            ["DDB"] = FormulaFinancialFunctionHandlers.EvaluateDdb,
        };
    }
}
