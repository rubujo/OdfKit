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
            if (TryEvaluate(name, arguments, context, out object result))
                return result;

            return OdfFormulaError.Name;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            OdfKitDiagnostics.Warn($"Formula function '{name}' threw unexpected exception: {ex.GetType().Name}");
            return OdfFormulaError.Value;
        }
    }

    internal static bool TryEvaluate(
        string name,
        List<AstNode> arguments,
        IEvaluationContext context,
        out object result)
    {
        if (s_registry.Value.TryGetValue(name, out FormulaBuiltinHandler? handler))
        {
            result = handler(arguments, context);
            return true;
        }

        result = OdfFormulaError.Name;
        return false;
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
            ["ISNONTEXT"] = FormulaLogicalFunctionHandlers.EvaluateIsNonText,
            ["ISFORMULA"] = FormulaLogicalFunctionHandlers.EvaluateIsFormula,
            ["ISBLANK"] = FormulaLogicalFunctionHandlers.EvaluateIsBlank,
            ["ISERROR"] = static (args, ctx) => args.Count == 1 ? args[0].Evaluate(ctx) is OdfFormulaError : OdfFormulaError.Value,
            ["ISERR"] = FormulaInformationFunctionHandlers.EvaluateIsErr,
            ["ISNA"] = static (args, ctx) => args.Count == 1
                ? args[0].Evaluate(ctx) is OdfFormulaError err && err.ErrorType == OdfFormulaErrorType.NA
                : OdfFormulaError.Value,
            ["ISREF"] = FormulaLogicalFunctionHandlers.EvaluateIsRef,
            ["ISLOGICAL"] = static (args, ctx) => args.Count == 1 ? args[0].Evaluate(ctx) is bool : OdfFormulaError.Value,
            ["TYPE"] = FormulaLogicalFunctionHandlers.EvaluateType,
            ["ISODD"] = FormulaLogicalFunctionHandlers.EvaluateIsOdd,
            ["ISEVEN"] = FormulaLogicalFunctionHandlers.EvaluateIsEven,
            ["N"] = FormulaInformationFunctionHandlers.EvaluateN,
            ["T"] = FormulaInformationFunctionHandlers.EvaluateT,
            ["VALUE"] = FormulaInformationFunctionHandlers.EvaluateValue,
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
            ["PROPER"] = FormulaStringFunctionHandlers.EvaluateProper,
            ["LEFT"] = FormulaStringFunctionHandlers.EvaluateLeft,
            ["RIGHT"] = FormulaStringFunctionHandlers.EvaluateRight,
            ["MID"] = FormulaStringFunctionHandlers.EvaluateMid,
            ["LEN"] = FormulaStringFunctionHandlers.EvaluateLen,
            ["LOWER"] = FormulaStringFunctionHandlers.EvaluateLower,
            ["UPPER"] = FormulaStringFunctionHandlers.EvaluateUpper,
            ["TRIM"] = FormulaStringFunctionHandlers.EvaluateTrim,
            ["REPLACE"] = FormulaStringFunctionHandlers.EvaluateReplace,
            ["CLEAN"] = FormulaCompatibilityFunctionHandlers.EvaluateClean,
            ["UNICHAR"] = FormulaCompatibilityFunctionHandlers.EvaluateUniChar,
            ["UNICODE"] = FormulaCompatibilityFunctionHandlers.EvaluateUnicode,
            ["NUMBERVALUE"] = FormulaLargeCompatibilityFunctionHandlers.EvaluateNumberValue,
            ["FIXED"] = FormulaRemainingCompatibilityFunctionHandlers.EvaluateFixed,
            ["ASC"] = FormulaRemainingCompatibilityFunctionHandlers.EvaluateAsc,
            ["JIS"] = FormulaRemainingCompatibilityFunctionHandlers.EvaluateJis,
            ["FINDB"] = FormulaRemainingCompatibilityFunctionHandlers.EvaluateFindB,
            ["SEARCHB"] = FormulaRemainingCompatibilityFunctionHandlers.EvaluateSearchB,
            ["LEFTB"] = FormulaRemainingCompatibilityFunctionHandlers.EvaluateLeftB,
            ["RIGHTB"] = FormulaRemainingCompatibilityFunctionHandlers.EvaluateRightB,
            ["MIDB"] = FormulaRemainingCompatibilityFunctionHandlers.EvaluateMidB,
            ["LENB"] = FormulaRemainingCompatibilityFunctionHandlers.EvaluateLenB,
            ["REPLACEB"] = FormulaRemainingCompatibilityFunctionHandlers.EvaluateReplaceB,

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
            ["AVEDEV"] = FormulaExtendedStatisticalFunctionHandlers.EvaluateAveDev,
            ["CORREL"] = FormulaExtendedStatisticalFunctionHandlers.EvaluateCorrel,
            ["COVAR"] = FormulaExtendedStatisticalFunctionHandlers.EvaluateCovar,
            ["DEVSQ"] = FormulaExtendedStatisticalFunctionHandlers.EvaluateDevSq,
            ["GEOMEAN"] = FormulaExtendedStatisticalFunctionHandlers.EvaluateGeoMean,
            ["HARMEAN"] = FormulaExtendedStatisticalFunctionHandlers.EvaluateHarMean,
            ["INTERCEPT"] = FormulaExtendedStatisticalFunctionHandlers.EvaluateIntercept,
            ["PEARSON"] = FormulaExtendedStatisticalFunctionHandlers.EvaluateCorrel,
            ["RSQ"] = FormulaExtendedStatisticalFunctionHandlers.EvaluateRsq,
            ["SLOPE"] = FormulaExtendedStatisticalFunctionHandlers.EvaluateSlope,
            ["FORECAST"] = FormulaRemainingCompatibilityFunctionHandlers.EvaluateForecast,
            ["MODE"] = FormulaRemainingCompatibilityFunctionHandlers.EvaluateMode,
            ["STANDARDIZE"] = FormulaExtendedStatisticalFunctionHandlers.EvaluateStandardize,
            ["STDEVA"] = FormulaExtendedStatisticalFunctionHandlers.EvaluateStDevA,
            ["STDEVPA"] = FormulaExtendedStatisticalFunctionHandlers.EvaluateStDevPA,
            ["SUMSQ"] = FormulaExtendedStatisticalFunctionHandlers.EvaluateSumSq,
            ["SUMX2MY2"] = FormulaExtendedStatisticalFunctionHandlers.EvaluateSumX2MY2,
            ["SUMX2PY2"] = FormulaExtendedStatisticalFunctionHandlers.EvaluateSumX2PY2,
            ["SUMXMY2"] = FormulaExtendedStatisticalFunctionHandlers.EvaluateSumXMY2,
            ["VARA"] = FormulaExtendedStatisticalFunctionHandlers.EvaluateVarA,
            ["AVERAGEA"] = FormulaExtendedStatisticalFunctionHandlers.EvaluateAverageA,
            ["MAXA"] = FormulaExtendedStatisticalFunctionHandlers.EvaluateMaxA,
            ["MINA"] = FormulaExtendedStatisticalFunctionHandlers.EvaluateMinA,
            ["VARPA"] = FormulaExtendedStatisticalFunctionHandlers.EvaluateVarPA,
            ["BINOM.DIST.RANGE"] = FormulaLargeCompatibilityFunctionHandlers.EvaluateBinomialRange,
            ["PHI"] = FormulaLargeCompatibilityFunctionHandlers.EvaluatePhi,
            ["GAUSS"] = FormulaLargeCompatibilityFunctionHandlers.EvaluateGauss,
            ["BINOMDIST"] = FormulaDistributionFunctionHandlers.EvaluateBinomDist,
            ["CONFIDENCE"] = FormulaDistributionFunctionHandlers.EvaluateConfidence,
            ["EXPONDIST"] = FormulaDistributionFunctionHandlers.EvaluateExponDist,
            ["FISHER"] = FormulaDistributionFunctionHandlers.EvaluateFisher,
            ["FISHERINV"] = FormulaDistributionFunctionHandlers.EvaluateFisherInv,
            ["LEGACY.NORMSDIST"] = FormulaDistributionFunctionHandlers.EvaluateNormSdist,
            ["LEGACY.NORMSINV"] = FormulaDistributionFunctionHandlers.EvaluateNormSInv,
            ["NEGBINOMDIST"] = FormulaDistributionFunctionHandlers.EvaluateNegBinomDist,
            ["NORMDIST"] = FormulaDistributionFunctionHandlers.EvaluateNormDist,
            ["NORMINV"] = FormulaDistributionFunctionHandlers.EvaluateNormInv,
            ["POISSON"] = FormulaDistributionFunctionHandlers.EvaluatePoisson,
            ["WEIBULL"] = FormulaDistributionFunctionHandlers.EvaluateWeibull,

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
            ["ADDRESS"] = FormulaRemainingCompatibilityFunctionHandlers.EvaluateAddress,
            ["AREAS"] = FormulaRemainingCompatibilityFunctionHandlers.EvaluateAreas,

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
            ["ACOSH"] = FormulaExtendedMathFunctionHandlers.EvaluateAcosh,
            ["ACOT"] = FormulaExtendedMathFunctionHandlers.EvaluateAcot,
            ["ACOTH"] = FormulaExtendedMathFunctionHandlers.EvaluateAcoth,
            ["ATAN"] = FormulaMathFunctionHandlers.EvaluateAtan,
            ["ATAN2"] = FormulaMathFunctionHandlers.EvaluateAtan2,
            ["ASINH"] = FormulaExtendedMathFunctionHandlers.EvaluateAsinh,
            ["ATANH"] = FormulaExtendedMathFunctionHandlers.EvaluateAtanh,
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
            ["COSH"] = FormulaExtendedMathFunctionHandlers.EvaluateCosh,
            ["COT"] = FormulaExtendedMathFunctionHandlers.EvaluateCot,
            ["COTH"] = FormulaExtendedMathFunctionHandlers.EvaluateCoth,
            ["CSC"] = FormulaExtendedMathFunctionHandlers.EvaluateCsc,
            ["CSCH"] = FormulaExtendedMathFunctionHandlers.EvaluateCsch,
            ["SEC"] = FormulaExtendedMathFunctionHandlers.EvaluateSec,
            ["SECH"] = FormulaExtendedMathFunctionHandlers.EvaluateSech,
            ["TAN"] = FormulaMathFunctionHandlers.EvaluateTan,
            ["SINH"] = FormulaExtendedMathFunctionHandlers.EvaluateSinh,
            ["TANH"] = FormulaExtendedMathFunctionHandlers.EvaluateTanh,
            ["TRUNC"] = FormulaMathFunctionHandlers.EvaluateTrunc,
            ["CONVERT"] = FormulaMathFunctionHandlers.EvaluateConvert,
            ["COMBIN"] = FormulaExtendedMathFunctionHandlers.EvaluateCombin,
            ["DELTA"] = FormulaExtendedMathFunctionHandlers.EvaluateDelta,
            ["FACTDOUBLE"] = FormulaExtendedMathFunctionHandlers.EvaluateFactDouble,
            ["GCD"] = FormulaExtendedMathFunctionHandlers.EvaluateGcd,
            ["GESTEP"] = FormulaExtendedMathFunctionHandlers.EvaluateGeStep,
            ["LCM"] = FormulaExtendedMathFunctionHandlers.EvaluateLcm,
            ["MULTINOMIAL"] = FormulaExtendedMathFunctionHandlers.EvaluateMultinomial,
            ["QUOTIENT"] = FormulaExtendedMathFunctionHandlers.EvaluateQuotient,
            ["SQRTPI"] = FormulaExtendedMathFunctionHandlers.EvaluateSqrtPi,
            ["BASE"] = FormulaCompatibilityFunctionHandlers.EvaluateBase,
            ["DECIMAL"] = FormulaCompatibilityFunctionHandlers.EvaluateDecimal,
            ["ARABIC"] = FormulaLargeCompatibilityFunctionHandlers.EvaluateArabic,
            ["COMBINA"] = FormulaLargeCompatibilityFunctionHandlers.EvaluateCombina,
            ["PERMUTATIONA"] = FormulaLargeCompatibilityFunctionHandlers.EvaluatePermutationA,
            ["GAMMA"] = FormulaLargeCompatibilityFunctionHandlers.EvaluateGamma,
            ["ERF"] = FormulaDistributionFunctionHandlers.EvaluateErf,
            ["ERFC"] = FormulaDistributionFunctionHandlers.EvaluateErfc,
            ["GAMMALN"] = FormulaDistributionFunctionHandlers.EvaluateGammaLn,
            ["PERMUT"] = FormulaRemainingCompatibilityFunctionHandlers.EvaluatePermut,
            ["ROMAN"] = FormulaRemainingCompatibilityFunctionHandlers.EvaluateRoman,
            ["SERIESSUM"] = FormulaRemainingCompatibilityFunctionHandlers.EvaluateSeriesSum,
            ["BIN2DEC"] = FormulaLargeCompatibilityFunctionHandlers.EvaluateBin2Dec,
            ["BIN2HEX"] = FormulaLargeCompatibilityFunctionHandlers.EvaluateBin2Hex,
            ["BIN2OCT"] = FormulaLargeCompatibilityFunctionHandlers.EvaluateBin2Oct,
            ["DEC2BIN"] = FormulaLargeCompatibilityFunctionHandlers.EvaluateDec2Bin,
            ["DEC2HEX"] = FormulaLargeCompatibilityFunctionHandlers.EvaluateDec2Hex,
            ["DEC2OCT"] = FormulaLargeCompatibilityFunctionHandlers.EvaluateDec2Oct,
            ["HEX2BIN"] = FormulaLargeCompatibilityFunctionHandlers.EvaluateHex2Bin,
            ["HEX2DEC"] = FormulaLargeCompatibilityFunctionHandlers.EvaluateHex2Dec,
            ["HEX2OCT"] = FormulaLargeCompatibilityFunctionHandlers.EvaluateHex2Oct,
            ["OCT2BIN"] = FormulaLargeCompatibilityFunctionHandlers.EvaluateOct2Bin,
            ["OCT2DEC"] = FormulaLargeCompatibilityFunctionHandlers.EvaluateOct2Dec,
            ["OCT2HEX"] = FormulaLargeCompatibilityFunctionHandlers.EvaluateOct2Hex,

            // 複數
            ["COMPLEX"] = FormulaComplexFunctionHandlers.EvaluateComplex,
            ["IMABS"] = FormulaComplexFunctionHandlers.EvaluateImAbs,
            ["IMAGINARY"] = FormulaComplexFunctionHandlers.EvaluateImaginary,
            ["IMARGUMENT"] = FormulaComplexFunctionHandlers.EvaluateImArgument,
            ["IMCONJUGATE"] = FormulaComplexFunctionHandlers.EvaluateImConjugate,
            ["IMCOS"] = FormulaComplexFunctionHandlers.EvaluateImCos,
            ["IMCOT"] = FormulaComplexFunctionHandlers.EvaluateImCot,
            ["IMCSC"] = FormulaComplexFunctionHandlers.EvaluateImCsc,
            ["IMCSCH"] = FormulaComplexFunctionHandlers.EvaluateImCsch,
            ["IMDIV"] = FormulaComplexFunctionHandlers.EvaluateImDiv,
            ["IMEXP"] = FormulaComplexFunctionHandlers.EvaluateImExp,
            ["IMLN"] = FormulaComplexFunctionHandlers.EvaluateImLn,
            ["IMLOG10"] = FormulaComplexFunctionHandlers.EvaluateImLog10,
            ["IMLOG2"] = FormulaComplexFunctionHandlers.EvaluateImLog2,
            ["IMPOWER"] = FormulaComplexFunctionHandlers.EvaluateImPower,
            ["IMPRODUCT"] = FormulaComplexFunctionHandlers.EvaluateImProduct,
            ["IMREAL"] = FormulaComplexFunctionHandlers.EvaluateImReal,
            ["IMSEC"] = FormulaComplexFunctionHandlers.EvaluateImSec,
            ["IMSECH"] = FormulaComplexFunctionHandlers.EvaluateImSech,
            ["IMSIN"] = FormulaComplexFunctionHandlers.EvaluateImSin,
            ["IMSQRT"] = FormulaComplexFunctionHandlers.EvaluateImSqrt,
            ["IMSUB"] = FormulaComplexFunctionHandlers.EvaluateImSub,
            ["IMSUM"] = FormulaComplexFunctionHandlers.EvaluateImSum,
            ["IMTAN"] = FormulaComplexFunctionHandlers.EvaluateImTan,

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
            ["DAYS"] = FormulaDateTimeFunctionHandlers.EvaluateDays,
            ["ISOWEEKNUM"] = FormulaDateTimeFunctionHandlers.EvaluateIsoWeekNum,
            ["EASTERSUNDAY"] = FormulaDateTimeFunctionHandlers.EvaluateOpenOfficeEasterSunday,
            ["ORG.OPENOFFICE.EASTERSUNDAY"] = FormulaDateTimeFunctionHandlers.EvaluateOpenOfficeEasterSunday,
            ["ORG.OPENOFFICE.ISOMITTED"] = static (args, _) => FormulaDateTimeFunctionHandlers.EvaluateOpenOfficeIsOmitted(args),

            // 矩陣
            ["MDETERM"] = FormulaMatrixFunctionHandlers.EvaluateMDeterm,
            ["MINVERSE"] = FormulaMatrixFunctionHandlers.EvaluateMInverse,
            ["MMULT"] = FormulaMatrixFunctionHandlers.EvaluateMMult,
            ["MUNIT"] = FormulaMatrixFunctionHandlers.EvaluateMUnit,
            ["TRANSPOSE"] = FormulaMatrixFunctionHandlers.EvaluateTranspose,

            // 資料庫
            ["DSUM"] = FormulaDatabaseFunctionHandlers.EvaluateDSum,
            ["DAVERAGE"] = FormulaDatabaseFunctionHandlers.EvaluateDAverage,
            ["DCOUNT"] = FormulaDatabaseFunctionHandlers.EvaluateDCount,
            ["DCOUNTA"] = FormulaDatabaseFunctionHandlers.EvaluateDCountA,
            ["DGET"] = FormulaDatabaseFunctionHandlers.EvaluateDGet,
            ["DPRODUCT"] = FormulaDatabaseFunctionHandlers.EvaluateDProduct,
            ["DSTDEV"] = FormulaDatabaseFunctionHandlers.EvaluateDStDev,
            ["DSTDEVP"] = FormulaDatabaseFunctionHandlers.EvaluateDStDevP,
            ["DVAR"] = FormulaDatabaseFunctionHandlers.EvaluateDVar,
            ["DVARP"] = FormulaDatabaseFunctionHandlers.EvaluateDVarP,
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
            ["NPV"] = FormulaFinancialFunctionHandlers.EvaluateNpv,
            ["SYD"] = FormulaFinancialFunctionHandlers.EvaluateSyd,
            ["FVSCHEDULE"] = FormulaLargeCompatibilityFunctionHandlers.EvaluateFvSchedule,
            ["ISPMT"] = FormulaLargeCompatibilityFunctionHandlers.EvaluateIsPmt,
            ["PDURATION"] = FormulaLargeCompatibilityFunctionHandlers.EvaluatePDuration,
            ["RRI"] = FormulaLargeCompatibilityFunctionHandlers.EvaluateRri,

            // 資訊
            ["ERROR.TYPE"] = FormulaLargeCompatibilityFunctionHandlers.EvaluateErrorType,
        };
    }
}
