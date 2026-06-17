using System;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.Formula.AST;

namespace OdfKit.Formula;

public partial class DefaultFormulaEvaluator
{
    /// <summary>
    /// 建立內建公式函式註冊表。
    /// </summary>
    internal static Dictionary<string, FormulaBuiltinHandler> CreateBuiltinRegistry()
    {
        return new Dictionary<string, FormulaBuiltinHandler>(StringComparer.OrdinalIgnoreCase)
        {
            // Logical
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

            // String
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

            // Statistical
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

            // Lookup
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

            // Math
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

            // Date/Time
            ["DATE"] = EvaluateDate,
            ["DAY"] = EvaluateDay,
            ["HOUR"] = EvaluateHour,
            ["MINUTE"] = EvaluateMinute,
            ["MONTH"] = EvaluateMonth,
            ["NOW"] = EvaluateNow,
            ["SECOND"] = EvaluateSecond,
            ["TIME"] = EvaluateTime,
            ["TODAY"] = EvaluateToday,
            ["YEAR"] = EvaluateYear,
            ["DATEDIF"] = EvaluateDateDif,
            ["DATEVALUE"] = EvaluateDateValue,
            ["TIMEVALUE"] = EvaluateTimeValue,
            ["WEEKDAY"] = EvaluateWeekday,
            ["WEEKNUM"] = EvaluateWeekNum,
            ["WORKDAY"] = EvaluateWorkday,
            ["NETWORKDAYS"] = EvaluateNetWorkDays,
            ["EDATE"] = EvaluateEDate,
            ["EOMONTH"] = EvaluateEOMonth,
            ["ORG.OPENOFFICE.EASTERSUNDAY"] = EvaluateOpenOfficeEasterSunday,
            ["ORG.OPENOFFICE.ISOMITTED"] = static (args, _) => EvaluateOpenOfficeIsOmitted(args),

            // Matrix
            ["TRANSPOSE"] = FormulaMatrixFunctionHandlers.EvaluateTranspose,

            // Database
            ["DSUM"] = FormulaDatabaseFunctionHandlers.EvaluateDSum,
            ["DAVERAGE"] = FormulaDatabaseFunctionHandlers.EvaluateDAverage,
            ["DCOUNT"] = FormulaDatabaseFunctionHandlers.EvaluateDCount,
            ["DMAX"] = FormulaDatabaseFunctionHandlers.EvaluateDMax,
            ["DMIN"] = FormulaDatabaseFunctionHandlers.EvaluateDMin,

            // Financial
            ["PMT"] = EvaluatePmt,
            ["FV"] = EvaluateFv,
            ["PV"] = EvaluatePv,
            ["NPER"] = EvaluateNper,
            ["RATE"] = EvaluateRate,
            ["IPMT"] = EvaluateIpmt,
            ["PPMT"] = EvaluatePpmt,
            ["IRR"] = EvaluateIrr,
            ["MIRR"] = EvaluateMirr,
            ["SLN"] = EvaluateSln,
            ["DDB"] = EvaluateDdb,
        };
    }
}
