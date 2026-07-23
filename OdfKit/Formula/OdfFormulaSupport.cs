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
        new("SUBSTITUTE", "Text", OdfFormulaSupportLevel.Evaluated),
        new("FIND", "Text", OdfFormulaSupportLevel.Evaluated),
        new("SEARCH", "Text", OdfFormulaSupportLevel.Evaluated),
        new("REPT", "Text", OdfFormulaSupportLevel.Evaluated),
        new("EXACT", "Text", OdfFormulaSupportLevel.Evaluated),
        new("CODE", "Text", OdfFormulaSupportLevel.Evaluated),
        new("CHAR", "Text", OdfFormulaSupportLevel.Evaluated),
        new("TEXT", "Text", OdfFormulaSupportLevel.Evaluated),
        new("PROPER", "Text", OdfFormulaSupportLevel.Evaluated),

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
        new("EASTERSUNDAY", "DateTime", OdfFormulaSupportLevel.Evaluated),

        // LibreOffice 擴充函數（ORG.OPENOFFICE.EASTERSUNDAY 為 ODF 1.4 標準化 EASTERSUNDAY 前的舊版供應商前綴名稱，保留以維持回溯相容）
        new("ORG.OPENOFFICE.EASTERSUNDAY", "LibreOffice", OdfFormulaSupportLevel.Evaluated),
        new("ORG.OPENOFFICE.ISOMITTED", "LibreOffice", OdfFormulaSupportLevel.Evaluated),

        // 矩陣函數
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
        new("SYD", "Financial", OdfFormulaSupportLevel.Evaluated)
    ];

    private static readonly HashSet<string> SupportedFunctionNames = CreateSupportedFunctionSet();

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
