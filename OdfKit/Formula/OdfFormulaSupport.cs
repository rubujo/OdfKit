using System;
using System.Collections.Generic;
using OdfKit.Compliance;
using OdfKit.Formula.AST;

namespace OdfKit.Formula;

/// <summary>
/// Analyzes OpenFormula support coverage, preservation-safe serialization, and diagnostics.
/// 提供 OpenFormula 支援範圍、保真序列化與診斷工具。
/// </summary>
public static class OdfFormulaSupport
{
    internal const string UnsupportedFunctionCode = "OF0002";

    private static readonly OdfFormulaFunctionInfo[] FunctionTable =
    [
        // 邏輯函數
        new("IF", "Logical", OdfFormulaSupportLevel.Evaluated),
        new("AND", "Logical", OdfFormulaSupportLevel.Evaluated),
        new("OR", "Logical", OdfFormulaSupportLevel.Evaluated),
        new("TRUE", "Logical", OdfFormulaSupportLevel.Evaluated),
        new("FALSE", "Logical", OdfFormulaSupportLevel.Evaluated),
        new("NOT", "Logical", OdfFormulaSupportLevel.Evaluated),
        new("XOR", "Logical", OdfFormulaSupportLevel.Evaluated),
        new("IFERROR", "Logical", OdfFormulaSupportLevel.Evaluated),
        new("IFNA", "Logical", OdfFormulaSupportLevel.Evaluated),
        new("IFS", "Logical", OdfFormulaSupportLevel.Evaluated),
        new("SWITCH", "Logical", OdfFormulaSupportLevel.Evaluated),
        new("BITAND", "Logical", OdfFormulaSupportLevel.Evaluated),
        new("BITOR", "Logical", OdfFormulaSupportLevel.Evaluated),
        new("BITXOR", "Logical", OdfFormulaSupportLevel.Evaluated),
        new("BITLSHIFT", "Logical", OdfFormulaSupportLevel.Evaluated),
        new("BITRSHIFT", "Logical", OdfFormulaSupportLevel.Evaluated),

        // 資訊函數
        new("ISNUMBER", "Information", OdfFormulaSupportLevel.Evaluated),
        new("ISTEXT", "Information", OdfFormulaSupportLevel.Evaluated),
        new("ISNONTEXT", "Information", OdfFormulaSupportLevel.Evaluated),
        new("ISFORMULA", "Information", OdfFormulaSupportLevel.Evaluated),
        new("ISBLANK", "Information", OdfFormulaSupportLevel.Evaluated),
        new("ISERROR", "Information", OdfFormulaSupportLevel.Evaluated),
        new("ISERR", "Information", OdfFormulaSupportLevel.Evaluated),
        new("ISNA", "Information", OdfFormulaSupportLevel.Evaluated),
        new("ISREF", "Information", OdfFormulaSupportLevel.Evaluated),
        new("ISLOGICAL", "Information", OdfFormulaSupportLevel.Evaluated),
        new("TYPE", "Information", OdfFormulaSupportLevel.Evaluated),
        new("ISODD", "Information", OdfFormulaSupportLevel.Evaluated),
        new("ISEVEN", "Information", OdfFormulaSupportLevel.Evaluated),
        new("NA", "Information", OdfFormulaSupportLevel.Evaluated),
        new("N", "Information", OdfFormulaSupportLevel.Evaluated),
        new("T", "Information", OdfFormulaSupportLevel.Evaluated),
        new("VALUE", "Information", OdfFormulaSupportLevel.Evaluated),

        // 文字函數
        new("CONCAT", "Text", OdfFormulaSupportLevel.Evaluated),
        new("CONCATENATE", "Text", OdfFormulaSupportLevel.Evaluated),
        new("LEFT", "Text", OdfFormulaSupportLevel.Evaluated),
        new("RIGHT", "Text", OdfFormulaSupportLevel.Evaluated),
        new("MID", "Text", OdfFormulaSupportLevel.Evaluated),
        new("LEN", "Text", OdfFormulaSupportLevel.Evaluated),
        new("LOWER", "Text", OdfFormulaSupportLevel.Evaluated),
        new("UPPER", "Text", OdfFormulaSupportLevel.Evaluated),
        new("TRIM", "Text", OdfFormulaSupportLevel.Evaluated),
        new("REPLACE", "Text", OdfFormulaSupportLevel.Evaluated),
        new("CLEAN", "Text", OdfFormulaSupportLevel.Evaluated),
        new("UNICHAR", "Text", OdfFormulaSupportLevel.Evaluated),
        new("UNICODE", "Text", OdfFormulaSupportLevel.Evaluated),
        new("NUMBERVALUE", "Text", OdfFormulaSupportLevel.Evaluated),
        new("SUBSTITUTE", "Text", OdfFormulaSupportLevel.Evaluated),
        new("FIND", "Text", OdfFormulaSupportLevel.Evaluated),
        new("SEARCH", "Text", OdfFormulaSupportLevel.Evaluated),
        new("REPT", "Text", OdfFormulaSupportLevel.Evaluated),
        new("EXACT", "Text", OdfFormulaSupportLevel.Evaluated),
        new("CODE", "Text", OdfFormulaSupportLevel.Evaluated),
        new("CHAR", "Text", OdfFormulaSupportLevel.Evaluated),
        new("TEXT", "Text", OdfFormulaSupportLevel.Evaluated),
        new("PROPER", "Text", OdfFormulaSupportLevel.Evaluated),
        new("FIXED", "Text", OdfFormulaSupportLevel.Evaluated),
        new("ASC", "Text", OdfFormulaSupportLevel.Evaluated),
        new("JIS", "Text", OdfFormulaSupportLevel.Evaluated),
        new("FINDB", "Text", OdfFormulaSupportLevel.Evaluated),
        new("SEARCHB", "Text", OdfFormulaSupportLevel.Evaluated),
        new("LEFTB", "Text", OdfFormulaSupportLevel.Evaluated),
        new("RIGHTB", "Text", OdfFormulaSupportLevel.Evaluated),
        new("MIDB", "Text", OdfFormulaSupportLevel.Evaluated),
        new("LENB", "Text", OdfFormulaSupportLevel.Evaluated),
        new("REPLACEB", "Text", OdfFormulaSupportLevel.Evaluated),

        // 統計函數
        new("SUM", "Statistical", OdfFormulaSupportLevel.Evaluated),
        new("AVERAGE", "Statistical", OdfFormulaSupportLevel.Evaluated),
        new("COUNT", "Statistical", OdfFormulaSupportLevel.Evaluated),
        new("COUNTA", "Statistical", OdfFormulaSupportLevel.Evaluated),
        new("COUNTBLANK", "Statistical", OdfFormulaSupportLevel.Evaluated),
        new("AVERAGEIF", "Statistical", OdfFormulaSupportLevel.Evaluated),
        new("AVERAGEIFS", "Statistical", OdfFormulaSupportLevel.Evaluated),
        new("SUMIFS", "Statistical", OdfFormulaSupportLevel.Evaluated),
        new("COUNTIFS", "Statistical", OdfFormulaSupportLevel.Evaluated),
        new("MEDIAN", "Statistical", OdfFormulaSupportLevel.Evaluated),
        new("STDEV", "Statistical", OdfFormulaSupportLevel.Evaluated),
        new("STDEVP", "Statistical", OdfFormulaSupportLevel.Evaluated),
        new("VAR", "Statistical", OdfFormulaSupportLevel.Evaluated),
        new("VARP", "Statistical", OdfFormulaSupportLevel.Evaluated),
        new("LARGE", "Statistical", OdfFormulaSupportLevel.Evaluated),
        new("SMALL", "Statistical", OdfFormulaSupportLevel.Evaluated),
        new("RANK", "Statistical", OdfFormulaSupportLevel.Evaluated),
        new("PERCENTILE", "Statistical", OdfFormulaSupportLevel.Evaluated),
        new("QUARTILE", "Statistical", OdfFormulaSupportLevel.Evaluated),
        new("SUMIF", "Statistical", OdfFormulaSupportLevel.Evaluated),
        new("COUNTIF", "Statistical", OdfFormulaSupportLevel.Evaluated),
        new("MAX", "Statistical", OdfFormulaSupportLevel.Evaluated),
        new("MIN", "Statistical", OdfFormulaSupportLevel.Evaluated),
        new("AVEDEV", "Statistical", OdfFormulaSupportLevel.Evaluated),
        new("CORREL", "Statistical", OdfFormulaSupportLevel.Evaluated),
        new("COVAR", "Statistical", OdfFormulaSupportLevel.Evaluated),
        new("DEVSQ", "Statistical", OdfFormulaSupportLevel.Evaluated),
        new("GEOMEAN", "Statistical", OdfFormulaSupportLevel.Evaluated),
        new("HARMEAN", "Statistical", OdfFormulaSupportLevel.Evaluated),
        new("INTERCEPT", "Statistical", OdfFormulaSupportLevel.Evaluated),
        new("PEARSON", "Statistical", OdfFormulaSupportLevel.Evaluated),
        new("RSQ", "Statistical", OdfFormulaSupportLevel.Evaluated),
        new("SLOPE", "Statistical", OdfFormulaSupportLevel.Evaluated),
        new("FORECAST", "Statistical", OdfFormulaSupportLevel.Evaluated),
        new("MODE", "Statistical", OdfFormulaSupportLevel.Evaluated),
        new("STANDARDIZE", "Statistical", OdfFormulaSupportLevel.Evaluated),
        new("STDEVA", "Statistical", OdfFormulaSupportLevel.Evaluated),
        new("STDEVPA", "Statistical", OdfFormulaSupportLevel.Evaluated),
        new("SUMSQ", "Statistical", OdfFormulaSupportLevel.Evaluated),
        new("SUMX2MY2", "Statistical", OdfFormulaSupportLevel.Evaluated),
        new("SUMX2PY2", "Statistical", OdfFormulaSupportLevel.Evaluated),
        new("SUMXMY2", "Statistical", OdfFormulaSupportLevel.Evaluated),
        new("VARA", "Statistical", OdfFormulaSupportLevel.Evaluated),
        new("AVERAGEA", "Statistical", OdfFormulaSupportLevel.Evaluated),
        new("MAXA", "Statistical", OdfFormulaSupportLevel.Evaluated),
        new("MINA", "Statistical", OdfFormulaSupportLevel.Evaluated),
        new("VARPA", "Statistical", OdfFormulaSupportLevel.Evaluated),
        new("BINOM.DIST.RANGE", "Statistical", OdfFormulaSupportLevel.Evaluated),
        new("GAUSS", "Statistical", OdfFormulaSupportLevel.Evaluated),
        new("PHI", "Statistical", OdfFormulaSupportLevel.Evaluated),
        new("BINOMDIST", "Statistical", OdfFormulaSupportLevel.Evaluated),
        new("CONFIDENCE", "Statistical", OdfFormulaSupportLevel.Evaluated),
        new("EXPONDIST", "Statistical", OdfFormulaSupportLevel.Evaluated),
        new("FISHER", "Statistical", OdfFormulaSupportLevel.Evaluated),
        new("FISHERINV", "Statistical", OdfFormulaSupportLevel.Evaluated),
        new("LEGACY.NORMSDIST", "Statistical", OdfFormulaSupportLevel.Evaluated),
        new("LEGACY.NORMSINV", "Statistical", OdfFormulaSupportLevel.Evaluated),
        new("NEGBINOMDIST", "Statistical", OdfFormulaSupportLevel.Evaluated),
        new("NORMDIST", "Statistical", OdfFormulaSupportLevel.Evaluated),
        new("NORMINV", "Statistical", OdfFormulaSupportLevel.Evaluated),
        new("POISSON", "Statistical", OdfFormulaSupportLevel.Evaluated),
        new("WEIBULL", "Statistical", OdfFormulaSupportLevel.Evaluated),

        // 查閱函數
        new("VLOOKUP", "Lookup", OdfFormulaSupportLevel.Evaluated),
        new("HLOOKUP", "Lookup", OdfFormulaSupportLevel.Evaluated),
        new("INDEX", "Lookup", OdfFormulaSupportLevel.Evaluated),
        new("MATCH", "Lookup", OdfFormulaSupportLevel.Evaluated),
        new("OFFSET", "Lookup", OdfFormulaSupportLevel.Evaluated),
        new("INDIRECT", "Lookup", OdfFormulaSupportLevel.Evaluated),
        new("ROW", "Lookup", OdfFormulaSupportLevel.Evaluated),
        new("COLUMN", "Lookup", OdfFormulaSupportLevel.Evaluated),
        new("ROWS", "Lookup", OdfFormulaSupportLevel.Evaluated),
        new("COLUMNS", "Lookup", OdfFormulaSupportLevel.Evaluated),
        new("CHOOSE", "Lookup", OdfFormulaSupportLevel.Evaluated),
        new("ADDRESS", "Lookup", OdfFormulaSupportLevel.Evaluated),
        new("AREAS", "Lookup", OdfFormulaSupportLevel.Evaluated),

        // 數學函數
        new("ABS", "Math", OdfFormulaSupportLevel.Evaluated),
        new("SQRT", "Math", OdfFormulaSupportLevel.Evaluated),
        new("ROUND", "Math", OdfFormulaSupportLevel.Evaluated),
        new("MOD", "Math", OdfFormulaSupportLevel.Evaluated),
        new("POWER", "Math", OdfFormulaSupportLevel.Evaluated),
        new("LN", "Math", OdfFormulaSupportLevel.Evaluated),
        new("LOG", "Math", OdfFormulaSupportLevel.Evaluated),
        new("EXP", "Math", OdfFormulaSupportLevel.Evaluated),
        new("CEILING", "Math", OdfFormulaSupportLevel.Evaluated),
        new("FLOOR", "Math", OdfFormulaSupportLevel.Evaluated),
        new("PI", "Math", OdfFormulaSupportLevel.Evaluated),
        new("DEGREES", "Math", OdfFormulaSupportLevel.Evaluated),
        new("RADIANS", "Math", OdfFormulaSupportLevel.Evaluated),
        new("SIN", "Math", OdfFormulaSupportLevel.Evaluated),
        new("COS", "Math", OdfFormulaSupportLevel.Evaluated),
        new("TAN", "Math", OdfFormulaSupportLevel.Evaluated),
        new("TRUNC", "Math", OdfFormulaSupportLevel.Evaluated),
        new("INT", "Math", OdfFormulaSupportLevel.Evaluated),
        new("SIGN", "Math", OdfFormulaSupportLevel.Evaluated),
        new("ODD", "Math", OdfFormulaSupportLevel.Evaluated),
        new("EVEN", "Math", OdfFormulaSupportLevel.Evaluated),
        new("PRODUCT", "Math", OdfFormulaSupportLevel.Evaluated),
        new("FACT", "Math", OdfFormulaSupportLevel.Evaluated),
        new("MROUND", "Math", OdfFormulaSupportLevel.Evaluated),
        new("ROUNDUP", "Math", OdfFormulaSupportLevel.Evaluated),
        new("ROUNDDOWN", "Math", OdfFormulaSupportLevel.Evaluated),
        new("RAND", "Math", OdfFormulaSupportLevel.Evaluated),
        new("RANDBETWEEN", "Math", OdfFormulaSupportLevel.Evaluated),
        new("ASIN", "Math", OdfFormulaSupportLevel.Evaluated),
        new("ACOS", "Math", OdfFormulaSupportLevel.Evaluated),
        new("ATAN", "Math", OdfFormulaSupportLevel.Evaluated),
        new("ATAN2", "Math", OdfFormulaSupportLevel.Evaluated),
        new("LOG10", "Math", OdfFormulaSupportLevel.Evaluated),
        new("SUMPRODUCT", "Math", OdfFormulaSupportLevel.Evaluated),
        new("CONVERT", "Math", OdfFormulaSupportLevel.Evaluated),
        new("ACOSH", "Math", OdfFormulaSupportLevel.Evaluated),
        new("ACOT", "Math", OdfFormulaSupportLevel.Evaluated),
        new("ACOTH", "Math", OdfFormulaSupportLevel.Evaluated),
        new("ASINH", "Math", OdfFormulaSupportLevel.Evaluated),
        new("ATANH", "Math", OdfFormulaSupportLevel.Evaluated),
        new("COMBIN", "Math", OdfFormulaSupportLevel.Evaluated),
        new("COSH", "Math", OdfFormulaSupportLevel.Evaluated),
        new("COT", "Math", OdfFormulaSupportLevel.Evaluated),
        new("COTH", "Math", OdfFormulaSupportLevel.Evaluated),
        new("CSC", "Math", OdfFormulaSupportLevel.Evaluated),
        new("CSCH", "Math", OdfFormulaSupportLevel.Evaluated),
        new("DELTA", "Math", OdfFormulaSupportLevel.Evaluated),
        new("FACTDOUBLE", "Math", OdfFormulaSupportLevel.Evaluated),
        new("GCD", "Math", OdfFormulaSupportLevel.Evaluated),
        new("GESTEP", "Math", OdfFormulaSupportLevel.Evaluated),
        new("LCM", "Math", OdfFormulaSupportLevel.Evaluated),
        new("MULTINOMIAL", "Math", OdfFormulaSupportLevel.Evaluated),
        new("QUOTIENT", "Math", OdfFormulaSupportLevel.Evaluated),
        new("SEC", "Math", OdfFormulaSupportLevel.Evaluated),
        new("SECH", "Math", OdfFormulaSupportLevel.Evaluated),
        new("SINH", "Math", OdfFormulaSupportLevel.Evaluated),
        new("SQRTPI", "Math", OdfFormulaSupportLevel.Evaluated),
        new("TANH", "Math", OdfFormulaSupportLevel.Evaluated),
        new("BASE", "Math", OdfFormulaSupportLevel.Evaluated),
        new("DECIMAL", "Math", OdfFormulaSupportLevel.Evaluated),
        new("ARABIC", "Math", OdfFormulaSupportLevel.Evaluated),
        new("COMBINA", "Math", OdfFormulaSupportLevel.Evaluated),
        new("PERMUTATIONA", "Math", OdfFormulaSupportLevel.Evaluated),
        new("GAMMA", "Math", OdfFormulaSupportLevel.Evaluated),
        new("ERF", "Math", OdfFormulaSupportLevel.Evaluated),
        new("ERFC", "Math", OdfFormulaSupportLevel.Evaluated),
        new("GAMMALN", "Math", OdfFormulaSupportLevel.Evaluated),
        new("PERMUT", "Math", OdfFormulaSupportLevel.Evaluated),
        new("ROMAN", "Math", OdfFormulaSupportLevel.Evaluated),
        new("SERIESSUM", "Math", OdfFormulaSupportLevel.Evaluated),
        new("BIN2DEC", "Engineering", OdfFormulaSupportLevel.Evaluated),
        new("BIN2HEX", "Engineering", OdfFormulaSupportLevel.Evaluated),
        new("BIN2OCT", "Engineering", OdfFormulaSupportLevel.Evaluated),
        new("DEC2BIN", "Engineering", OdfFormulaSupportLevel.Evaluated),
        new("DEC2HEX", "Engineering", OdfFormulaSupportLevel.Evaluated),
        new("DEC2OCT", "Engineering", OdfFormulaSupportLevel.Evaluated),
        new("HEX2BIN", "Engineering", OdfFormulaSupportLevel.Evaluated),
        new("HEX2DEC", "Engineering", OdfFormulaSupportLevel.Evaluated),
        new("HEX2OCT", "Engineering", OdfFormulaSupportLevel.Evaluated),
        new("OCT2BIN", "Engineering", OdfFormulaSupportLevel.Evaluated),
        new("OCT2DEC", "Engineering", OdfFormulaSupportLevel.Evaluated),
        new("OCT2HEX", "Engineering", OdfFormulaSupportLevel.Evaluated),

        // 複數函數
        new("COMPLEX", "Complex", OdfFormulaSupportLevel.Evaluated),
        new("IMABS", "Complex", OdfFormulaSupportLevel.Evaluated),
        new("IMAGINARY", "Complex", OdfFormulaSupportLevel.Evaluated),
        new("IMARGUMENT", "Complex", OdfFormulaSupportLevel.Evaluated),
        new("IMCONJUGATE", "Complex", OdfFormulaSupportLevel.Evaluated),
        new("IMCOS", "Complex", OdfFormulaSupportLevel.Evaluated),
        new("IMCOT", "Complex", OdfFormulaSupportLevel.Evaluated),
        new("IMCSC", "Complex", OdfFormulaSupportLevel.Evaluated),
        new("IMCSCH", "Complex", OdfFormulaSupportLevel.Evaluated),
        new("IMDIV", "Complex", OdfFormulaSupportLevel.Evaluated),
        new("IMEXP", "Complex", OdfFormulaSupportLevel.Evaluated),
        new("IMLN", "Complex", OdfFormulaSupportLevel.Evaluated),
        new("IMLOG10", "Complex", OdfFormulaSupportLevel.Evaluated),
        new("IMLOG2", "Complex", OdfFormulaSupportLevel.Evaluated),
        new("IMPOWER", "Complex", OdfFormulaSupportLevel.Evaluated),
        new("IMPRODUCT", "Complex", OdfFormulaSupportLevel.Evaluated),
        new("IMREAL", "Complex", OdfFormulaSupportLevel.Evaluated),
        new("IMSEC", "Complex", OdfFormulaSupportLevel.Evaluated),
        new("IMSECH", "Complex", OdfFormulaSupportLevel.Evaluated),
        new("IMSIN", "Complex", OdfFormulaSupportLevel.Evaluated),
        new("IMSQRT", "Complex", OdfFormulaSupportLevel.Evaluated),
        new("IMSUB", "Complex", OdfFormulaSupportLevel.Evaluated),
        new("IMSUM", "Complex", OdfFormulaSupportLevel.Evaluated),
        new("IMTAN", "Complex", OdfFormulaSupportLevel.Evaluated),

        // 日期／時間函數
        new("DATE", "DateTime", OdfFormulaSupportLevel.Evaluated),
        new("DAY", "DateTime", OdfFormulaSupportLevel.Evaluated),
        new("HOUR", "DateTime", OdfFormulaSupportLevel.Evaluated),
        new("MINUTE", "DateTime", OdfFormulaSupportLevel.Evaluated),
        new("MONTH", "DateTime", OdfFormulaSupportLevel.Evaluated),
        new("NOW", "DateTime", OdfFormulaSupportLevel.Evaluated),
        new("SECOND", "DateTime", OdfFormulaSupportLevel.Evaluated),
        new("TIME", "DateTime", OdfFormulaSupportLevel.Evaluated),
        new("TODAY", "DateTime", OdfFormulaSupportLevel.Evaluated),
        new("YEAR", "DateTime", OdfFormulaSupportLevel.Evaluated),
        new("DATEDIF", "DateTime", OdfFormulaSupportLevel.Evaluated),
        new("DATEVALUE", "DateTime", OdfFormulaSupportLevel.Evaluated),
        new("TIMEVALUE", "DateTime", OdfFormulaSupportLevel.Evaluated),
        new("WEEKDAY", "DateTime", OdfFormulaSupportLevel.Evaluated),
        new("WEEKNUM", "DateTime", OdfFormulaSupportLevel.Evaluated),
        new("WORKDAY", "DateTime", OdfFormulaSupportLevel.Evaluated),
        new("NETWORKDAYS", "DateTime", OdfFormulaSupportLevel.Evaluated),
        new("EDATE", "DateTime", OdfFormulaSupportLevel.Evaluated),
        new("EOMONTH", "DateTime", OdfFormulaSupportLevel.Evaluated),
        new("DAYS", "DateTime", OdfFormulaSupportLevel.Evaluated),
        new("ISOWEEKNUM", "DateTime", OdfFormulaSupportLevel.Evaluated),
        new("EASTERSUNDAY", "DateTime", OdfFormulaSupportLevel.Evaluated),

        // LibreOffice 擴充函數（ORG.OPENOFFICE.EASTERSUNDAY 為 ODF 1.4 標準化 EASTERSUNDAY 前的舊版供應商前綴名稱，保留以維持回溯相容）
        new("ORG.OPENOFFICE.EASTERSUNDAY", "LibreOffice", OdfFormulaSupportLevel.Evaluated),
        new("ORG.OPENOFFICE.ISOMITTED", "LibreOffice", OdfFormulaSupportLevel.Evaluated),

        // 矩陣函數
        new("MDETERM", "Matrix", OdfFormulaSupportLevel.Evaluated),
        new("MINVERSE", "Matrix", OdfFormulaSupportLevel.Evaluated),
        new("MMULT", "Matrix", OdfFormulaSupportLevel.Evaluated),
        new("MUNIT", "Matrix", OdfFormulaSupportLevel.Evaluated),
        new("TRANSPOSE", "Matrix", OdfFormulaSupportLevel.Evaluated),

        // 資料庫函數
        new("DSUM", "Database", OdfFormulaSupportLevel.Evaluated),
        new("DAVERAGE", "Database", OdfFormulaSupportLevel.Evaluated),
        new("DCOUNT", "Database", OdfFormulaSupportLevel.Evaluated),
        new("DCOUNTA", "Database", OdfFormulaSupportLevel.Evaluated),
        new("DGET", "Database", OdfFormulaSupportLevel.Evaluated),
        new("DPRODUCT", "Database", OdfFormulaSupportLevel.Evaluated),
        new("DSTDEV", "Database", OdfFormulaSupportLevel.Evaluated),
        new("DSTDEVP", "Database", OdfFormulaSupportLevel.Evaluated),
        new("DVAR", "Database", OdfFormulaSupportLevel.Evaluated),
        new("DVARP", "Database", OdfFormulaSupportLevel.Evaluated),
        new("DMAX", "Database", OdfFormulaSupportLevel.Evaluated),
        new("DMIN", "Database", OdfFormulaSupportLevel.Evaluated),

        // 財務函數
        new("PMT", "Financial", OdfFormulaSupportLevel.Evaluated),
        new("FV", "Financial", OdfFormulaSupportLevel.Evaluated),
        new("PV", "Financial", OdfFormulaSupportLevel.Evaluated),
        new("NPER", "Financial", OdfFormulaSupportLevel.Evaluated),
        new("RATE", "Financial", OdfFormulaSupportLevel.Evaluated),
        new("IPMT", "Financial", OdfFormulaSupportLevel.Evaluated),
        new("PPMT", "Financial", OdfFormulaSupportLevel.Evaluated),
        new("IRR", "Financial", OdfFormulaSupportLevel.Evaluated),
        new("MIRR", "Financial", OdfFormulaSupportLevel.Evaluated),
        new("SLN", "Financial", OdfFormulaSupportLevel.Evaluated),
        new("DDB", "Financial", OdfFormulaSupportLevel.Evaluated),
        new("NPV", "Financial", OdfFormulaSupportLevel.Evaluated),
        new("SYD", "Financial", OdfFormulaSupportLevel.Evaluated),
        new("FVSCHEDULE", "Financial", OdfFormulaSupportLevel.Evaluated),
        new("ISPMT", "Financial", OdfFormulaSupportLevel.Evaluated),
        new("PDURATION", "Financial", OdfFormulaSupportLevel.Evaluated),
        new("RRI", "Financial", OdfFormulaSupportLevel.Evaluated),

        // 資訊函數（Large Group）
        new("ERROR.TYPE", "Information", OdfFormulaSupportLevel.Evaluated),

        // OpenFormula Medium／Large Group 相容函式
        new("ACCRINT", "Financial", OdfFormulaSupportLevel.Evaluated),
        new("ACCRINTM", "Financial", OdfFormulaSupportLevel.Evaluated),
        new("AMORLINC", "Financial", OdfFormulaSupportLevel.Evaluated),
        new("BESSELI", "Engineering", OdfFormulaSupportLevel.BestEffort),
        new("BESSELJ", "Engineering", OdfFormulaSupportLevel.BestEffort),
        new("BESSELK", "Engineering", OdfFormulaSupportLevel.BestEffort),
        new("BESSELY", "Engineering", OdfFormulaSupportLevel.BestEffort),
        new("BETADIST", "Statistical", OdfFormulaSupportLevel.Evaluated),
        new("BETAINV", "Statistical", OdfFormulaSupportLevel.Evaluated),
        new("CHISQDIST", "Statistical", OdfFormulaSupportLevel.Evaluated),
        new("CHISQINV", "Statistical", OdfFormulaSupportLevel.Evaluated),
        new("COUPDAYBS", "Financial", OdfFormulaSupportLevel.Evaluated),
        new("COUPDAYS", "Financial", OdfFormulaSupportLevel.Evaluated),
        new("COUPDAYSNC", "Financial", OdfFormulaSupportLevel.Evaluated),
        new("COUPNCD", "Financial", OdfFormulaSupportLevel.Evaluated),
        new("COUPNUM", "Financial", OdfFormulaSupportLevel.Evaluated),
        new("COUPPCD", "Financial", OdfFormulaSupportLevel.Evaluated),
        new("CRITBINOM", "Statistical", OdfFormulaSupportLevel.Evaluated),
        new("CUMIPMT", "Financial", OdfFormulaSupportLevel.Evaluated),
        new("CUMPRINC", "Financial", OdfFormulaSupportLevel.Evaluated),
        new("DAYS360", "DateTime", OdfFormulaSupportLevel.Evaluated),
        new("DB", "Financial", OdfFormulaSupportLevel.Evaluated),
        new("DDE", "External", OdfFormulaSupportLevel.BestEffort),
        new("DISC", "Financial", OdfFormulaSupportLevel.Evaluated),
        new("DOLLARDE", "Financial", OdfFormulaSupportLevel.Evaluated),
        new("DOLLARFR", "Financial", OdfFormulaSupportLevel.Evaluated),
        new("DURATION", "Financial", OdfFormulaSupportLevel.Evaluated),
        new("EFFECT", "Financial", OdfFormulaSupportLevel.Evaluated),
        new("EUROCONVERT", "Financial", OdfFormulaSupportLevel.Evaluated),
        new("FDIST", "Statistical", OdfFormulaSupportLevel.Evaluated),
        new("FINV", "Statistical", OdfFormulaSupportLevel.Evaluated),
        new("FORMULA", "Information", OdfFormulaSupportLevel.Evaluated),
        new("FREQUENCY", "Statistical", OdfFormulaSupportLevel.Evaluated),
        new("FTEST", "Statistical", OdfFormulaSupportLevel.BestEffort),
        new("GAMMADIST", "Statistical", OdfFormulaSupportLevel.Evaluated),
        new("GAMMAINV", "Statistical", OdfFormulaSupportLevel.Evaluated),
        new("GETPIVOTDATA", "Lookup", OdfFormulaSupportLevel.BestEffort),
        new("GROWTH", "Statistical", OdfFormulaSupportLevel.BestEffort),
        new("HYPERLINK", "Lookup", OdfFormulaSupportLevel.Evaluated),
        new("HYPGEOMDIST", "Statistical", OdfFormulaSupportLevel.Evaluated),
        new("INFO", "Information", OdfFormulaSupportLevel.BestEffort),
        new("INTRATE", "Financial", OdfFormulaSupportLevel.Evaluated),
        new("KURT", "Statistical", OdfFormulaSupportLevel.Evaluated),
        new("LEGACY.CHIDIST", "Statistical", OdfFormulaSupportLevel.Evaluated),
        new("LEGACY.CHIINV", "Statistical", OdfFormulaSupportLevel.Evaluated),
        new("LEGACY.CHITEST", "Statistical", OdfFormulaSupportLevel.Evaluated),
        new("LEGACY.FDIST", "Statistical", OdfFormulaSupportLevel.Evaluated),
        new("LEGACY.FINV", "Statistical", OdfFormulaSupportLevel.Evaluated),
        new("LEGACY.TDIST", "Statistical", OdfFormulaSupportLevel.Evaluated),
        new("LINEST", "Statistical", OdfFormulaSupportLevel.BestEffort),
        new("LOGEST", "Statistical", OdfFormulaSupportLevel.BestEffort),
        new("LOGINV", "Statistical", OdfFormulaSupportLevel.Evaluated),
        new("LOGNORMDIST", "Statistical", OdfFormulaSupportLevel.Evaluated),
        new("LOOKUP", "Lookup", OdfFormulaSupportLevel.Evaluated),
        new("MDURATION", "Financial", OdfFormulaSupportLevel.Evaluated),
        new("MULTIPLE.OPERATIONS", "Information", OdfFormulaSupportLevel.BestEffort),
        new("NOMINAL", "Financial", OdfFormulaSupportLevel.Evaluated),
        new("ODDFPRICE", "Financial", OdfFormulaSupportLevel.BestEffort),
        new("ODDFYIELD", "Financial", OdfFormulaSupportLevel.BestEffort),
        new("ODDLPRICE", "Financial", OdfFormulaSupportLevel.BestEffort),
        new("ODDLYIELD", "Financial", OdfFormulaSupportLevel.BestEffort),
        new("PERCENTRANK", "Statistical", OdfFormulaSupportLevel.Evaluated),
        new("PRICE", "Financial", OdfFormulaSupportLevel.Evaluated),
        new("PRICEDISC", "Financial", OdfFormulaSupportLevel.Evaluated),
        new("PRICEMAT", "Financial", OdfFormulaSupportLevel.Evaluated),
        new("PROB", "Statistical", OdfFormulaSupportLevel.Evaluated),
        new("RECEIVED", "Financial", OdfFormulaSupportLevel.Evaluated),
        new("SHEET", "Information", OdfFormulaSupportLevel.BestEffort),
        new("SHEETS", "Information", OdfFormulaSupportLevel.BestEffort),
        new("SKEW", "Statistical", OdfFormulaSupportLevel.Evaluated),
        new("SKEWP", "Statistical", OdfFormulaSupportLevel.Evaluated),
        new("STEYX", "Statistical", OdfFormulaSupportLevel.Evaluated),
        new("SUBTOTAL", "Statistical", OdfFormulaSupportLevel.Evaluated),
        new("TBILLEQ", "Financial", OdfFormulaSupportLevel.Evaluated),
        new("TBILLPRICE", "Financial", OdfFormulaSupportLevel.Evaluated),
        new("TBILLYIELD", "Financial", OdfFormulaSupportLevel.Evaluated),
        new("TINV", "Statistical", OdfFormulaSupportLevel.Evaluated),
        new("TREND", "Statistical", OdfFormulaSupportLevel.BestEffort),
        new("TRIMMEAN", "Statistical", OdfFormulaSupportLevel.Evaluated),
        new("TTEST", "Statistical", OdfFormulaSupportLevel.BestEffort),
        new("VDB", "Financial", OdfFormulaSupportLevel.Evaluated),
        new("XIRR", "Financial", OdfFormulaSupportLevel.Evaluated),
        new("XNPV", "Financial", OdfFormulaSupportLevel.Evaluated),
        new("YEARFRAC", "DateTime", OdfFormulaSupportLevel.Evaluated),
        new("YIELD", "Financial", OdfFormulaSupportLevel.Evaluated),
        new("YIELDDISC", "Financial", OdfFormulaSupportLevel.Evaluated),
        new("YIELDMAT", "Financial", OdfFormulaSupportLevel.Evaluated),
        new("ZTEST", "Statistical", OdfFormulaSupportLevel.BestEffort)
    ];

    private static readonly HashSet<string> SupportedFunctionNames = CreateSupportedFunctionSet();
    private static readonly HashSet<string> BestEffortFunctionNames =
        CreateFunctionSet(OdfFormulaSupportLevel.BestEffort);

    private static readonly string[] SmallGroupFunctionTable =
    [
        "ABS", "ACOS", "AND", "ASIN", "ATAN", "ATAN2", "AVERAGE", "AVERAGEIF",
        "CHOOSE", "COLUMNS", "COS", "COUNT", "COUNTA", "COUNTBLANK", "COUNTIF",
        "DATE", "DAVERAGE", "DAY", "DCOUNT", "DCOUNTA", "DDB", "DEGREES", "DGET",
        "DMAX", "DMIN", "DPRODUCT", "DSTDEV", "DSTDEVP", "DSUM", "DVAR", "DVARP",
        "EVEN", "EXACT", "EXP", "FACT", "FALSE", "FIND", "FV", "HLOOKUP", "HOUR",
        "IF", "INDEX", "INT", "IRR", "ISBLANK", "ISERR", "ISERROR", "ISLOGICAL",
        "ISNA", "ISNONTEXT", "ISNUMBER", "ISTEXT", "LEFT", "LEN", "LN", "LOG",
        "LOG10", "LOWER", "MATCH", "MAX", "MID", "MIN", "MINUTE", "MOD", "MONTH",
        "N", "NA", "NOT", "NOW", "NPER", "NPV", "ODD", "OR", "PI", "PMT", "POWER",
        "PRODUCT", "PROPER", "PV", "RADIANS", "RATE", "REPLACE", "REPT", "RIGHT",
        "ROUND", "ROWS", "SECOND", "SIN", "SLN", "SQRT", "STDEV", "STDEVP",
        "SUBSTITUTE", "SUM", "SUMIF", "SYD", "T", "TAN", "TIME", "TODAY", "TRIM",
        "TRUE", "TRUNC", "UPPER", "VALUE", "VAR", "VARP", "VLOOKUP", "WEEKDAY", "YEAR"
    ];

    private static readonly IReadOnlyList<string> SmallGroupFunctions =
        Array.AsReadOnly(SmallGroupFunctionTable);

    private static readonly string[] MediumGroupAdditionalFunctionTable =
    [
        "ACCRINT", "ACCRINTM", "ACOSH", "ACOT", "ACOTH", "ADDRESS", "ASINH", "ATANH",
        "AVEDEV", "BESSELI", "BESSELJ", "BESSELK", "BESSELY", "BETADIST", "BETAINV",
        "BINOMDIST", "CEILING", "CHAR", "CLEAN", "CODE", "COLUMN", "COMBIN", "CONCATENATE",
        "CONFIDENCE", "CONVERT", "CORREL", "COSH", "COT", "COTH", "COUPDAYBS", "COUPDAYS",
        "COUPDAYSNC", "COUPNCD", "COUPNUM", "COUPPCD", "COVAR", "CRITBINOM", "CUMIPMT",
        "CUMPRINC", "DATEVALUE", "DAYS360", "DB", "DEVSQ", "DISC", "DOLLARDE", "DOLLARFR",
        "DURATION", "EFFECT", "EOMONTH", "ERF", "ERFC", "EXPONDIST", "FISHER", "FISHERINV",
        "FIXED", "FLOOR", "FORECAST", "FTEST", "GAMMADIST", "GAMMAINV", "GAMMALN", "GCD",
        "GEOMEAN", "HARMEAN", "HYPGEOMDIST", "INTERCEPT", "INTRATE", "ISEVEN", "ISODD",
        "ISOWEEKNUM", "KURT", "LARGE", "LCM", "LEGACY.CHIDIST", "LEGACY.CHIINV",
        "LEGACY.CHITEST", "LEGACY.FDIST", "LEGACY.FINV", "LEGACY.NORMSDIST",
        "LEGACY.NORMSINV", "LEGACY.TDIST", "LINEST", "LOGEST", "LOGINV", "LOGNORMDIST",
        "LOOKUP", "MDURATION", "MEDIAN", "MINVERSE", "MIRR", "MMULT", "MODE", "MROUND",
        "MULTINOMIAL", "NEGBINOMDIST", "NETWORKDAYS", "NOMINAL", "ODDFPRICE", "ODDFYIELD",
        "ODDLPRICE", "ODDLYIELD", "OFFSET", "PEARSON", "PERCENTILE", "PERCENTRANK",
        "PERMUT", "POISSON", "PRICE", "PRICEMAT", "PROB", "QUARTILE", "QUOTIENT", "RAND",
        "RANDBETWEEN", "RANK", "RECEIVED", "ROMAN", "ROUNDDOWN", "ROUNDUP", "ROW", "RSQ",
        "SERIESSUM", "SIGN", "SINH", "SKEW", "SKEWP", "SLOPE", "SMALL", "SQRTPI",
        "STANDARDIZE", "STDEVA", "STDEVPA", "STEYX", "SUBTOTAL", "SUMPRODUCT", "SUMSQ",
        "SUMX2MY2", "SUMX2PY2", "SUMXMY2", "TANH", "TBILLEQ", "TBILLPRICE", "TBILLYIELD",
        "TIMEVALUE", "TINV", "TRANSPOSE", "TREND", "TRIMMEAN", "TTEST", "TYPE", "VARA",
        "VDB", "WEEKNUM", "WEIBULL", "WORKDAY", "XIRR", "XNPV", "YEARFRAC", "YIELD",
        "YIELDDISC", "YIELDMAT", "ZTEST"
    ];

    private static readonly string[] LargeGroupAdditionalFunctionTable =
    [
        "AMORLINC", "ARABIC", "AREAS", "ASC", "AVERAGEA", "AVERAGEIFS", "BASE", "BIN2DEC",
        "BIN2HEX", "BIN2OCT", "BINOM.DIST.RANGE", "BITAND", "BITLSHIFT", "BITOR",
        "BITRSHIFT", "BITXOR", "CHISQDIST", "CHISQINV", "COMBINA", "COMPLEX", "COUNTIFS",
        "CSC", "CSCH", "DATEDIF", "DAYS", "DDE", "DEC2BIN", "DEC2HEX", "DEC2OCT", "DECIMAL",
        "DELTA", "EDATE", "ERROR.TYPE", "EUROCONVERT", "FACTDOUBLE", "FDIST", "FINDB", "FINV",
        "FORMULA", "FREQUENCY", "FVSCHEDULE", "GAMMA", "GAUSS", "GESTEP", "GETPIVOTDATA",
        "GROWTH", "HEX2BIN", "HEX2DEC", "HEX2OCT", "HYPERLINK", "IFERROR", "IFNA", "IMABS",
        "IMAGINARY", "IMARGUMENT", "IMCONJUGATE", "IMCOS", "IMCOT", "IMCSC", "IMCSCH",
        "IMDIV", "IMEXP", "IMLN", "IMLOG10", "IMLOG2", "IMPOWER", "IMPRODUCT", "IMREAL",
        "IMSEC", "IMSECH", "IMSIN", "IMSQRT", "IMSUB", "IMSUM", "IMTAN", "INDIRECT",
        "INFO", "IPMT", "ISFORMULA", "ISPMT", "ISREF", "JIS", "LEFTB", "LENB", "MAXA",
        "MDETERM", "MULTIPLE.OPERATIONS", "MUNIT", "MIDB", "MINA", "NORMDIST", "NORMINV",
        "NUMBERVALUE", "OCT2BIN", "OCT2DEC", "OCT2HEX", "PDURATION", "PERMUTATIONA", "PHI",
        "PPMT", "PRICEDISC", "REPLACEB", "RIGHTB", "RRI", "SEARCH", "SEARCHB", "SEC", "SECH",
        "SHEET", "SHEETS", "SUMIFS", "TEXT", "UNICHAR", "UNICODE", "VARPA", "XOR"
    ];

    private static readonly IReadOnlyList<string> MediumGroupRequiredFunctionNames =
        Array.AsReadOnly(CreateCumulativeFunctionTable(
            SmallGroupFunctionTable,
            MediumGroupAdditionalFunctionTable));

    private static readonly IReadOnlyList<string> LargeGroupRequiredFunctionNames =
        Array.AsReadOnly(CreateCumulativeFunctionTable(
            SmallGroupFunctionTable,
            MediumGroupAdditionalFunctionTable,
            LargeGroupAdditionalFunctionTable));

    /// <summary>
    /// Gets the table of functions supported by the default formula evaluator.
    /// 取得預設公式評估器支援的函式表。
    /// </summary>
    public static IReadOnlyList<OdfFormulaFunctionInfo> SupportedFunctions => FunctionTable;

    /// <summary>
    /// Gets the mandatory function names for the OASIS OpenFormula Small Group evaluator.
    /// 取得 OASIS OpenFormula Small Group 評估器的強制函式名稱。
    /// </summary>
    /// <remarks>
    /// Function-name coverage alone does not prove evaluator conformance; syntax, limits, conversions, and semantics require separate verification.
    /// 僅有函式名稱涵蓋並不代表評估器合規；語法、限制、型別轉換及語意仍須分別驗證。
    /// </remarks>
    public static IReadOnlyList<string> SmallGroupRequiredFunctions => SmallGroupFunctions;

    /// <summary>
    /// Gets cumulative mandatory function names for an OpenFormula evaluator group.
    /// 取得 OpenFormula 評估器群組的累計強制函式名稱。
    /// </summary>
    /// <param name="group">The conformance group. / 一致性群組。</param>
    /// <returns>The immutable mandatory-function list. / 不可變的強制函式清單。</returns>
    public static IReadOnlyList<string> GetRequiredFunctions(OdfFormulaConformanceGroup group)
        => GetRequiredFunctionsCore(group);

    /// <summary>
    /// Reports cumulative mandatory-function coverage for the built-in evaluator.
    /// 報告內建評估器的累計強制函式覆蓋情形。
    /// </summary>
    /// <param name="group">The conformance group. / 一致性群組。</param>
    /// <returns>The function coverage report. / 函式覆蓋報告。</returns>
    public static OdfFormulaConformanceReport GetConformanceReport(
        OdfFormulaConformanceGroup group)
        => GetConformanceReportCore(group, null);

    /// <summary>
    /// Reports cumulative mandatory-function coverage for the built-in evaluator and an application registry.
    /// 報告內建評估器與應用程式註冊表的累計強制函式覆蓋情形。
    /// </summary>
    /// <param name="group">The conformance group. / 一致性群組。</param>
    /// <param name="functions">The application-defined function registry. / 應用程式自訂函式註冊表。</param>
    /// <returns>The function coverage report. / 函式覆蓋報告。</returns>
    public static OdfFormulaConformanceReport GetConformanceReport(
        OdfFormulaConformanceGroup group,
        OdfFormulaFunctionRegistry functions)
    {
        if (functions is null)
        {
            throw new ArgumentNullException(
                nameof(functions),
                OdfLocalizer.GetMessage("Err_OdfFormulaSupport_FunctionRegistryNull"));
        }

        return GetConformanceReportCore(group, functions);
    }

    /// <summary>
    /// Returns mandatory Small-group functions that are unavailable from the built-in evaluator.
    /// 傳回預設評估器尚未提供的 Small Group 強制函式。
    /// </summary>
    /// <returns>The missing function names. / 缺少的函式名稱。</returns>
    public static IReadOnlyList<string> GetMissingSmallGroupFunctions()
        => GetMissingSmallGroupFunctionsCore(null);

    /// <summary>
    /// Returns mandatory Small-group functions unavailable from both the built-in evaluator and an application registry.
    /// 傳回預設評估器與應用程式註冊表皆未提供的 Small Group 強制函式。
    /// </summary>
    /// <param name="functions">The application-defined function registry. / 應用程式自訂函式註冊表。</param>
    /// <returns>The missing function names. / 缺少的函式名稱。</returns>
    public static IReadOnlyList<string> GetMissingSmallGroupFunctions(OdfFormulaFunctionRegistry functions)
    {
        if (functions is null)
        {
            throw new ArgumentNullException(
                nameof(functions),
                OdfLocalizer.GetMessage("Err_OdfFormulaSupport_FunctionRegistryNull"));
        }

        return GetMissingSmallGroupFunctionsCore(functions);
    }

    /// <summary>
    /// Determines whether the default evaluator supports the specified function.
    /// 判斷預設評估器是否支援指定函式。
    /// </summary>
    /// <param name="name">The function name. / 函式名稱。</param>
    /// <returns>True when the function is supported; otherwise, false. / 若支援則為 true，否則為 false。</returns>
    public static bool IsFunctionSupported(string name)
        => IsFunctionSupportedCore(name, null);

    /// <summary>
    /// Determines whether the default evaluator or an application registry supports the specified function.
    /// 判斷預設評估器或應用程式註冊表是否支援指定函式。
    /// </summary>
    /// <param name="name">The function name. / 函式名稱。</param>
    /// <param name="functions">The application-defined function registry. / 應用程式自訂函式註冊表。</param>
    /// <returns>True when the function is supported; otherwise, false. / 若支援則為 true，否則為 false。</returns>
    public static bool IsFunctionSupported(string name, OdfFormulaFunctionRegistry functions)
    {
        if (functions is null)
            throw new ArgumentNullException(
                nameof(functions),
                OdfLocalizer.GetMessage("Err_OdfFormulaSupport_FunctionRegistryNull"));

        return IsFunctionSupportedCore(name, functions);
    }

    private static bool IsFunctionSupportedCore(string name, OdfFormulaFunctionRegistry? functions)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        string normalizedName = name.Trim();
        return SupportedFunctionNames.Contains(normalizedName) || functions?.Contains(normalizedName) == true;
    }

    /// <summary>
    /// Analyzes whether a formula can be parsed and whether it contains functions unsupported by the default evaluator.
    /// 分析公式是否可剖析，以及是否包含預設評估器不支援的函式。
    /// </summary>
    /// <param name="formula">The formula to analyze. / 要分析的公式。</param>
    /// <returns>The formula analysis result. / 公式分析結果。</returns>
    public static OdfFormulaAnalysis Analyze(string formula)
        => AnalyzeCore(formula, null);

    /// <summary>
    /// Analyzes formula support against the default evaluator and an application-defined function registry.
    /// 依預設評估器與應用程式自訂函式註冊表分析公式支援範圍。
    /// </summary>
    /// <param name="formula">The formula to analyze. / 要分析的公式。</param>
    /// <param name="functions">The application-defined function registry. / 應用程式自訂函式註冊表。</param>
    /// <returns>The formula analysis result. / 公式分析結果。</returns>
    public static OdfFormulaAnalysis Analyze(string formula, OdfFormulaFunctionRegistry functions)
    {
        if (functions is null)
            throw new ArgumentNullException(
                nameof(functions),
                OdfLocalizer.GetMessage("Err_OdfFormulaSupport_FunctionRegistryNull"));

        return AnalyzeCore(formula, functions);
    }

    private static OdfFormulaAnalysis AnalyzeCore(string formula, OdfFormulaFunctionRegistry? functions)
    {
        if (formula is null)
            throw new ArgumentNullException(nameof(formula));

        string normalized = NormalizeForParsing(formula);
        var diagnostics = new List<OdfFormulaDiagnostic>();
        List<string> extractedFunctions = ExtractFunctionNames(normalized, diagnostics);
        string? serialized = null;

        try
        {
            var parser = new FormulaParser(FormulaPrefixNormalizer.RemovePrefix(normalized));
            AstNode ast = parser.Parse();
            serialized = ast.Serialize();
        }
        catch (Exception ex)
        {
            diagnostics.Add(new OdfFormulaDiagnostic(
                "OF0001",
                OdfLocalizer.GetMessage("Diag_OdfFormulaSupport_ParseFailed", ex.Message),
                OdfFormulaDiagnosticSeverity.Error));
        }

        foreach (string functionName in extractedFunctions)
        {
            if (!IsFunctionSupportedCore(functionName, functions))
            {
                diagnostics.Add(new OdfFormulaDiagnostic(
                    UnsupportedFunctionCode,
                    OdfLocalizer.GetMessage("Diag_OdfFormulaSupport_UnsupportedFunction", functionName),
                    OdfFormulaDiagnosticSeverity.Warning));
            }
        }

        return new OdfFormulaAnalysis(formula, normalized, serialized, extractedFunctions, diagnostics);
    }

    /// <summary>
    /// Returns the reserialized form for supported formulas, while preserving unsupported or unparsable formulas.
    /// 支援的公式會回傳重新序列化結果；不支援或無法剖析時保留原公式。
    /// </summary>
    /// <param name="formula">The formula to serialize. / 要序列化的公式。</param>
    /// <returns>A preservation-safe formula string. / 安全的公式字串。</returns>
    public static string SerializePreservingUnsupported(string formula)
        => SerializePreservingUnsupportedCore(formula, null);

    /// <summary>
    /// Returns a preservation-safe serialized formula while recognizing application-defined functions.
    /// 在辨識應用程式自訂函式的情況下，傳回可安全保真的序列化公式。
    /// </summary>
    /// <param name="formula">The formula to serialize. / 要序列化的公式。</param>
    /// <param name="functions">The application-defined function registry. / 應用程式自訂函式註冊表。</param>
    /// <returns>A preservation-safe formula string. / 安全的公式字串。</returns>
    public static string SerializePreservingUnsupported(string formula, OdfFormulaFunctionRegistry functions)
    {
        if (functions is null)
            throw new ArgumentNullException(
                nameof(functions),
                OdfLocalizer.GetMessage("Err_OdfFormulaSupport_FunctionRegistryNull"));

        return SerializePreservingUnsupportedCore(formula, functions);
    }

    private static string SerializePreservingUnsupportedCore(
        string formula,
        OdfFormulaFunctionRegistry? functions)
    {
        OdfFormulaAnalysis analysis = AnalyzeCore(formula, functions);
        if (!analysis.CanParse || analysis.HasUnsupportedFunctions || analysis.SerializedFormula is null)
        {
            return formula;
        }

        if (formula.StartsWith("of:=", StringComparison.OrdinalIgnoreCase))
        {
            return "of:=" + analysis.SerializedFormula;
        }

        if (formula.StartsWith("oooc:=", StringComparison.OrdinalIgnoreCase))
        {
            return "oooc:=" + analysis.SerializedFormula;
        }

        if (formula.StartsWith("=", StringComparison.Ordinal))
        {
            return "=" + analysis.SerializedFormula;
        }

        return analysis.SerializedFormula;
    }

    private static HashSet<string> CreateSupportedFunctionSet()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var function in FunctionTable)
        {
            set.Add(function.Name);
        }

        return set;
    }

    private static HashSet<string> CreateFunctionSet(OdfFormulaSupportLevel supportLevel)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (OdfFormulaFunctionInfo function in FunctionTable)
        {
            if (function.SupportLevel == supportLevel)
                set.Add(function.Name);
        }
        return set;
    }

    private static string[] CreateCumulativeFunctionTable(params string[][] tables)
    {
        var functions = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string[] table in tables)
        {
            foreach (string functionName in table)
            {
                if (seen.Add(functionName))
                {
                    functions.Add(functionName);
                }
            }
        }

        return functions.ToArray();
    }

    private static OdfFormulaConformanceReport GetConformanceReportCore(
        OdfFormulaConformanceGroup group,
        OdfFormulaFunctionRegistry? functions)
    {
        IReadOnlyList<string> requiredFunctions = GetRequiredFunctionsCore(group);
        var missingFunctions = new List<string>();
        var bestEffortFunctions = new List<string>();
        foreach (string functionName in requiredFunctions)
        {
            if (!IsFunctionSupportedCore(functionName, functions))
            {
                missingFunctions.Add(functionName);
            }
            else if (BestEffortFunctionNames.Contains(functionName))
            {
                bestEffortFunctions.Add(functionName);
            }
        }

        return new OdfFormulaConformanceReport(
            group,
            requiredFunctions,
            Array.AsReadOnly(missingFunctions.ToArray()),
            Array.AsReadOnly(bestEffortFunctions.ToArray()));
    }

    private static IReadOnlyList<string> GetRequiredFunctionsCore(
        OdfFormulaConformanceGroup group)
        => group switch
        {
            OdfFormulaConformanceGroup.Small => SmallGroupFunctions,
            OdfFormulaConformanceGroup.Medium => MediumGroupRequiredFunctionNames,
            OdfFormulaConformanceGroup.Large => LargeGroupRequiredFunctionNames,
            _ => throw new ArgumentOutOfRangeException(nameof(group))
        };

    private static IReadOnlyList<string> GetMissingSmallGroupFunctionsCore(
        OdfFormulaFunctionRegistry? functions)
    {
        var missingFunctions = new List<string>();
        foreach (string functionName in SmallGroupFunctionTable)
        {
            if (!IsFunctionSupportedCore(functionName, functions))
            {
                missingFunctions.Add(functionName);
            }
        }

        return missingFunctions;
    }

    private static string NormalizeForParsing(string formula)
    {
        if (formula.StartsWith("of:=", StringComparison.OrdinalIgnoreCase) ||
            formula.StartsWith("oooc:=", StringComparison.OrdinalIgnoreCase))
        {
            return OdfFormulaTranslator.OdfToExcelFormula(formula);
        }

        return formula;
    }

    private static List<string> ExtractFunctionNames(string normalizedFormula, List<OdfFormulaDiagnostic> diagnostics)
    {
        string text = FormulaPrefixNormalizer.RemovePrefix(normalizedFormula);
        List<FormulaToken> tokens = OdfFormulaTranslator.Tokenize(text);
        var functions = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < tokens.Count; i++)
        {
            FormulaToken token = tokens[i];
            if (token.Type == TokenType.Unknown)
            {
                diagnostics.Add(new OdfFormulaDiagnostic(
                    "OF0003",
                    OdfLocalizer.GetMessage("Diag_OdfFormulaSupport_UnknownCharacter", token.Value),
                    OdfFormulaDiagnosticSeverity.Error,
                    token.StartIndex));
            }

            if (token.Type != TokenType.Identifier)
            {
                continue;
            }

            int nextIndex = FindNextNonWhitespace(tokens, i + 1);
            if (nextIndex >= 0 && tokens[nextIndex].Type == TokenType.OpenParenthesis)
            {
                string name = token.Value.ToUpperInvariant();
                if (seen.Add(name))
                {
                    functions.Add(name);
                }
            }
        }

        return functions;
    }

    private static int FindNextNonWhitespace(IReadOnlyList<FormulaToken> tokens, int startIndex)
    {
        for (int i = startIndex; i < tokens.Count; i++)
        {
            if (tokens[i].Type != TokenType.Whitespace)
            {
                return i;
            }
        }

        return -1;
    }
}
