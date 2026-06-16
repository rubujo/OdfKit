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
            ["IF"] = EvaluateIf,
            ["AND"] = EvaluateAnd,
            ["OR"] = EvaluateOr,
            ["TRUE"] = static (_, _) => true,
            ["FALSE"] = static (_, _) => false,
            ["NA"] = static (args, _) => args.Count == 0 ? OdfFormulaError.NA : OdfFormulaError.Value,
            ["NOT"] = static (args, ctx) => args.Count == 1
                ? (object)!FormulaCoercion.CoerceToBool(args[0].Evaluate(ctx))
                : OdfFormulaError.Value,
            ["XOR"] = EvaluateXor,
            ["IFERROR"] = EvaluateIfError,
            ["IFNA"] = EvaluateIfNa,
            ["IFS"] = EvaluateIfs,
            ["SWITCH"] = EvaluateSwitch,
            ["ISNUMBER"] = static (args, ctx) => args.Count == 1 ? args[0].Evaluate(ctx) is double : OdfFormulaError.Value,
            ["ISTEXT"] = static (args, ctx) => args.Count == 1 ? args[0].Evaluate(ctx) is string : OdfFormulaError.Value,
            ["ISBLANK"] = EvaluateIsBlank,
            ["ISERROR"] = static (args, ctx) => args.Count == 1 ? args[0].Evaluate(ctx) is OdfFormulaError : OdfFormulaError.Value,
            ["ISNA"] = static (args, ctx) => args.Count == 1
                ? args[0].Evaluate(ctx) is OdfFormulaError err && err.ErrorType == OdfFormulaErrorType.NA
                : OdfFormulaError.Value,
            ["ISREF"] = EvaluateIsRef,
            ["ISLOGICAL"] = static (args, ctx) => args.Count == 1 ? args[0].Evaluate(ctx) is bool : OdfFormulaError.Value,
            ["TYPE"] = EvaluateType,
            ["ISODD"] = EvaluateIsOdd,
            ["ISEVEN"] = EvaluateIsEven,
            ["BITAND"] = EvaluateBitAnd,
            ["BITOR"] = EvaluateBitOr,
            ["BITXOR"] = EvaluateBitXor,
            ["BITLSHIFT"] = EvaluateBitLShift,
            ["BITRSHIFT"] = EvaluateBitRShift,

            // String
            ["CONCAT"] = EvaluateConcat,
            ["CONCATENATE"] = EvaluateConcat,
            ["SUBSTITUTE"] = EvaluateSubstitute,
            ["FIND"] = EvaluateFind,
            ["SEARCH"] = EvaluateSearch,
            ["REPT"] = EvaluateRept,
            ["EXACT"] = EvaluateExact,
            ["CODE"] = EvaluateCode,
            ["CHAR"] = EvaluateChar,
            ["TEXT"] = EvaluateText,
            ["LEFT"] = EvaluateLeft,
            ["RIGHT"] = EvaluateRight,
            ["MID"] = EvaluateMid,
            ["LEN"] = EvaluateLen,
            ["LOWER"] = EvaluateLower,
            ["UPPER"] = EvaluateUpper,
            ["TRIM"] = EvaluateTrim,
            ["REPLACE"] = EvaluateReplace,

            // Statistical
            ["SUM"] = EvaluateSum,
            ["AVERAGE"] = EvaluateAverage,
            ["COUNT"] = EvaluateCount,
            ["COUNTA"] = EvaluateCountA,
            ["COUNTBLANK"] = EvaluateCountBlank,
            ["AVERAGEIF"] = EvaluateAverageIf,
            ["AVERAGEIFS"] = EvaluateAverageIfs,
            ["SUMIFS"] = EvaluateSumIfs,
            ["COUNTIFS"] = EvaluateCountIfs,
            ["MEDIAN"] = EvaluateMedian,
            ["STDEV"] = EvaluateStDev,
            ["STDEVP"] = EvaluateStDevP,
            ["VAR"] = EvaluateVar,
            ["VARP"] = EvaluateVarP,
            ["LARGE"] = EvaluateLarge,
            ["SMALL"] = EvaluateSmall,
            ["RANK"] = EvaluateRank,
            ["PERCENTILE"] = EvaluatePercentile,
            ["QUARTILE"] = EvaluateQuartile,
            ["SUMIF"] = EvaluateSumIf,
            ["COUNTIF"] = EvaluateCountIf,
            ["MAX"] = EvaluateMax,
            ["MIN"] = EvaluateMin,

            // Lookup
            ["VLOOKUP"] = EvaluateVLookup,
            ["HLOOKUP"] = EvaluateHLookup,
            ["INDEX"] = EvaluateIndex,
            ["MATCH"] = EvaluateMatch,
            ["OFFSET"] = EvaluateOffset,
            ["INDIRECT"] = EvaluateIndirect,
            ["ROW"] = EvaluateRow,
            ["COLUMN"] = EvaluateColumn,
            ["ROWS"] = EvaluateRows,
            ["COLUMNS"] = EvaluateColumns,
            ["CHOOSE"] = EvaluateChoose,

            // Math
            ["INT"] = EvaluateInt,
            ["SIGN"] = EvaluateSign,
            ["ODD"] = EvaluateOdd,
            ["EVEN"] = EvaluateEven,
            ["PRODUCT"] = EvaluateProduct,
            ["FACT"] = EvaluateFact,
            ["MROUND"] = EvaluateMRound,
            ["ROUNDUP"] = EvaluateRoundUp,
            ["ROUNDDOWN"] = EvaluateRoundDown,
            ["RAND"] = EvaluateRand,
            ["RANDBETWEEN"] = EvaluateRandBetween,
            ["ASIN"] = EvaluateAsin,
            ["ACOS"] = EvaluateAcos,
            ["ATAN"] = EvaluateAtan,
            ["ATAN2"] = EvaluateAtan2,
            ["LOG10"] = EvaluateLog10,
            ["SUMPRODUCT"] = EvaluateSumProduct,
            ["ABS"] = EvaluateAbs,
            ["SQRT"] = EvaluateSqrt,
            ["ROUND"] = EvaluateRound,
            ["MOD"] = EvaluateMod,
            ["POWER"] = EvaluatePower,
            ["LN"] = EvaluateLn,
            ["LOG"] = EvaluateLog,
            ["EXP"] = EvaluateExp,
            ["CEILING"] = EvaluateCeiling,
            ["FLOOR"] = EvaluateFloor,
            ["PI"] = EvaluatePi,
            ["DEGREES"] = EvaluateDegrees,
            ["RADIANS"] = EvaluateRadians,
            ["SIN"] = EvaluateSin,
            ["COS"] = EvaluateCos,
            ["TAN"] = EvaluateTan,
            ["TRUNC"] = EvaluateTrunc,

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
            ["TRANSPOSE"] = EvaluateTranspose,

            // Database
            ["DSUM"] = EvaluateDSum,
            ["DAVERAGE"] = EvaluateDAverage,
            ["DCOUNT"] = EvaluateDCount,
            ["DMAX"] = EvaluateDMax,
            ["DMIN"] = EvaluateDMin,

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
