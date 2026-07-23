using System;
using System.Collections.Generic;
using System.Linq;
using OdfKit.Formula;
using OdfKit.Spreadsheet;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// Verifies the extensible evaluator and the mandatory Small-group function baseline.
/// 驗證可擴充評估器與 Small Group 強制函式基線。
/// </summary>
public sealed class OpenFormulaExtendedEvaluatorTests
{
    private static readonly string[] SmallBaselineAdditions =
    [
        "DCOUNTA", "DGET", "DPRODUCT", "DSTDEV", "DSTDEVP", "DVAR", "DVARP",
        "ISERR", "N", "NPV", "PROPER", "SYD", "T", "VALUE"
    ];

    /// <summary>
    /// Verifies that every formerly missing mandatory Small-group function is advertised.
    /// 驗證先前缺少的 Small Group 強制函式皆已列入支援表。
    /// </summary>
    [Fact]
    public void SmallGroupFunctionGapIsClosedInSupportTable()
    {
        Assert.Empty(OdfFormulaSupport.GetMissingSmallGroupFunctions());

        foreach (string functionName in SmallBaselineAdditions)
            Assert.True(OdfFormulaSupport.IsFunctionSupported(functionName), functionName);
    }

    /// <summary>
    /// Verifies scalar information, text, and financial Small-group functions.
    /// 驗證 Small Group 的純量資訊、文字及財務函式。
    /// </summary>
    [Fact]
    public void AddedSmallGroupScalarFunctionsEvaluate()
    {
        var evaluator = new DefaultFormulaEvaluator();
        var context = new ExtendedEvaluationContext();

        Assert.Equal(true, evaluator.Evaluate("ISERR(1/0)", context));
        Assert.Equal(false, evaluator.Evaluate("ISERR(NA())", context));
        Assert.Equal(1d, evaluator.Evaluate("N(TRUE())", context));
        Assert.Equal(string.Empty, evaluator.Evaluate("T(42)", context));
        Assert.Equal(42.5d, evaluator.Evaluate("VALUE(\"42.5\")", context));
        Assert.Equal("Odf Kit-Formula", evaluator.Evaluate("PROPER(\"odf kit-formula\")", context));
        Assert.Equal(80d, Assert.IsType<double>(evaluator.Evaluate("NPV(0.25,100)", context)), 10);
        Assert.Equal(300d, Assert.IsType<double>(evaluator.Evaluate("SYD(1000,100,3,2)", context)), 10);
    }

    /// <summary>
    /// Verifies database aggregation functions required by the Small group.
    /// 驗證 Small Group 要求的資料庫彙總函式。
    /// </summary>
    [Fact]
    public void AddedSmallGroupDatabaseFunctionsEvaluate()
    {
        var evaluator = new DefaultFormulaEvaluator();
        var context = new ExtendedEvaluationContext();

        Assert.Equal(3d, evaluator.Evaluate("DCOUNTA(DB,\"Score\",CRIT)", context));
        Assert.Equal(20d, evaluator.Evaluate("DGET(DB,\"Score\",ONE)", context));
        Assert.Equal(6000d, evaluator.Evaluate("DPRODUCT(DB,\"Score\",CRIT)", context));
        Assert.Equal(100d, Assert.IsType<double>(evaluator.Evaluate("DVAR(DB,\"Score\",CRIT)", context)), 10);
        Assert.Equal(200d / 3d, Assert.IsType<double>(evaluator.Evaluate("DVARP(DB,\"Score\",CRIT)", context)), 10);
        Assert.Equal(10d, Assert.IsType<double>(evaluator.Evaluate("DSTDEV(DB,\"Score\",CRIT)", context)), 10);
        Assert.Equal(Math.Sqrt(200d / 3d), Assert.IsType<double>(evaluator.Evaluate("DSTDEVP(DB,\"Score\",CRIT)", context)), 10);
    }

    /// <summary>
    /// Verifies instance-scoped custom function evaluation and registry-aware diagnostics.
    /// 驗證執行個體範圍的自訂函式求值與註冊表感知診斷。
    /// </summary>
    [Theory]
    [InlineData("of:=DOUBLE(21)")]
    [InlineData("oooc:=DOUBLE(21)")]
    public void CustomFunctionsExtendAllSupportedFormulaDialects(string formula)
    {
        var functions = new OdfFormulaFunctionRegistry();
        functions.Register("DOUBLE", static (arguments, _) => (double)arguments[0] * 2);
        var evaluator = new DefaultFormulaEvaluator(functions);
        var context = new ExtendedEvaluationContext();

        OdfFormulaAnalysis analysis = OdfFormulaSupport.Analyze(formula, functions);
        object result = evaluator.Evaluate(formula, context);

        Assert.False(analysis.HasUnsupportedFunctions);
        Assert.Equal(42d, result);
        Assert.False(new OdfFormulaFunctionRegistry().Contains("DOUBLE"));
    }

    /// <summary>
    /// Verifies that application functions cannot override standard built-ins.
    /// 驗證應用程式函式無法覆寫標準內建函式。
    /// </summary>
    [Fact]
    public void CustomRegistryCannotOverrideBuiltInFunction()
    {
        var functions = new OdfFormulaFunctionRegistry();
        functions.Register("SUM", static (_, _) => -1d);
        var evaluator = new DefaultFormulaEvaluator(functions);

        Assert.Equal(3d, evaluator.Evaluate("SUM(1,2)", new ExtendedEvaluationContext()));
    }

    /// <summary>
    /// Verifies that an unsupported whole formula can be delegated to an external fallback.
    /// 驗證不受支援的完整公式可委派給外部後援。
    /// </summary>
    [Fact]
    public void UnsupportedFormulaUsesConfiguredFallback()
    {
        var fallback = new RecordingFallback(123d);
        var evaluator = new DefaultFormulaEvaluator(new OdfFormulaFunctionRegistry(), fallback);

        object result = evaluator.Evaluate("XLOOKUP(1,2,3)", new ExtendedEvaluationContext());

        Assert.Equal(123d, result);
        Assert.Equal("XLOOKUP(1,2,3)", fallback.Formula);
    }

    /// <summary>
    /// Verifies that document-level parallel recalculation preserves the configured function registry.
    /// 驗證文件層級並行重算會保留已設定的函式註冊表。
    /// </summary>
    [Fact]
    public void SpreadsheetRecalculationUsesConfiguredCustomFunctions()
    {
        var functions = new OdfFormulaFunctionRegistry();
        functions.Register("DOUBLE", static (arguments, _) => (double)arguments[0] * 2);
        var evaluator = new DefaultFormulaEvaluator(functions);
        using SpreadsheetDocument document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.AddSheet("Data");
        sheet.Cells["A1"].Formula = "of:=DOUBLE(21)";

        document.EvaluateFormulas(evaluator);

        Assert.Equal(42d, sheet.Cells["A1"].CellValue);
    }

    /// <summary>
    /// Verifies OpenFormula inline-array parsing, evaluation, and serialization.
    /// 驗證 OpenFormula 內嵌陣列的剖析、求值及序列化。
    /// </summary>
    [Fact]
    public void InlineArraysEvaluateAndSerialize()
    {
        var evaluator = new DefaultFormulaEvaluator();
        var context = new ExtendedEvaluationContext();

        Assert.Equal(10d, evaluator.Evaluate("SUM({1;2|3;4})", context));

        var parser = new FormulaParser("{1;2|3;4}");
        Assert.Equal("{1;2|3;4}", parser.Parse().Serialize());
    }

    /// <summary>
    /// Verifies matrix functions required by the Medium and Large groups.
    /// 驗證 Medium 與 Large Group 要求的矩陣函式。
    /// </summary>
    [Fact]
    public void MatrixFunctionsEvaluateInlineArrays()
    {
        var evaluator = new DefaultFormulaEvaluator();
        var context = new ExtendedEvaluationContext();

        Assert.Equal(-2d, Assert.IsType<double>(evaluator.Evaluate("MDETERM({1;2|3;4})", context)), 10);

        object[,] inverse = Assert.IsType<object[,]>(
            evaluator.Evaluate("MINVERSE({1;2|3;4})", context));
        Assert.Equal(-2d, Assert.IsType<double>(inverse[0, 0]), 10);
        Assert.Equal(1d, Assert.IsType<double>(inverse[0, 1]), 10);
        Assert.Equal(1.5d, Assert.IsType<double>(inverse[1, 0]), 10);
        Assert.Equal(-0.5d, Assert.IsType<double>(inverse[1, 1]), 10);

        object[,] product = Assert.IsType<object[,]>(
            evaluator.Evaluate("MMULT({1;2|3;4};{5;6|7;8})", context));
        Assert.Equal(19d, product[0, 0]);
        Assert.Equal(22d, product[0, 1]);
        Assert.Equal(43d, product[1, 0]);
        Assert.Equal(50d, product[1, 1]);

        object[,] identity = Assert.IsType<object[,]>(evaluator.Evaluate("MUNIT(2)", context));
        Assert.Equal(1d, identity[0, 0]);
        Assert.Equal(0d, identity[0, 1]);
        Assert.Equal(0d, identity[1, 0]);
        Assert.Equal(1d, identity[1, 1]);
    }

    /// <summary>
    /// Verifies the cumulative OASIS mandatory-function catalogs and current gaps.
    /// 驗證 OASIS 累計強制函式目錄與目前缺口。
    /// </summary>
    [Fact]
    public void ConformanceReportsUseCumulativeOfficialFunctionCatalogs()
    {
        OdfFormulaConformanceReport small =
            OdfFormulaSupport.GetConformanceReport(OdfFormulaConformanceGroup.Small);
        OdfFormulaConformanceReport medium =
            OdfFormulaSupport.GetConformanceReport(OdfFormulaConformanceGroup.Medium);
        OdfFormulaConformanceReport large =
            OdfFormulaSupport.GetConformanceReport(OdfFormulaConformanceGroup.Large);

        Assert.Equal(110, small.RequiredFunctions.Count);
        Assert.True(small.HasCompleteFunctionSet);
        Assert.Equal(272, medium.RequiredFunctions.Count);
        Assert.False(medium.HasCompleteFunctionSet);
        Assert.Equal(91, medium.MissingFunctions.Count);
        Assert.DoesNotContain("MMULT", medium.MissingFunctions);
        Assert.Equal(388, large.RequiredFunctions.Count);
        Assert.False(large.HasCompleteFunctionSet);
        Assert.Equal(145, large.MissingFunctions.Count);
        Assert.DoesNotContain("COMPLEX", large.MissingFunctions);
        Assert.Contains("DDE", large.MissingFunctions);
    }

    /// <summary>
    /// Verifies added Medium and Large mathematical functions.
    /// 驗證新增的 Medium 與 Large Group 數學函式。
    /// </summary>
    [Fact]
    public void ExtendedMathematicalFunctionsEvaluate()
    {
        var evaluator = new DefaultFormulaEvaluator();
        var context = new ExtendedEvaluationContext();

        Assert.Equal(6d, Assert.IsType<double>(evaluator.Evaluate("GCD(12;18)", context)));
        Assert.Equal(36d, Assert.IsType<double>(evaluator.Evaluate("LCM(12;18)", context)));
        Assert.Equal(10d, Assert.IsType<double>(evaluator.Evaluate("COMBIN(5;2)", context)));
        Assert.Equal(3d, Assert.IsType<double>(evaluator.Evaluate("QUOTIENT(10;3)", context)));
        Assert.Equal(15d, Assert.IsType<double>(evaluator.Evaluate("FACTDOUBLE(5)", context)));
        Assert.Equal(1d, Assert.IsType<double>(evaluator.Evaluate("DELTA(4;4)", context)));
        Assert.Equal(1d, Assert.IsType<double>(evaluator.Evaluate("GESTEP(4;3)", context)));
        Assert.Equal(Math.Cosh(1), Assert.IsType<double>(evaluator.Evaluate("COSH(1)", context)), 10);
    }

    /// <summary>
    /// Verifies OpenFormula complex construction, arithmetic, and analysis functions.
    /// 驗證 OpenFormula 複數建立、運算及分析函式。
    /// </summary>
    [Fact]
    public void ComplexFunctionsEvaluate()
    {
        var evaluator = new DefaultFormulaEvaluator();
        var context = new ExtendedEvaluationContext();

        Assert.Equal("3+4i", evaluator.Evaluate("COMPLEX(3;4)", context));
        Assert.Equal(5d, Assert.IsType<double>(evaluator.Evaluate("IMABS(\"3+4i\")", context)));
        Assert.Equal(3d, evaluator.Evaluate("IMREAL(\"3+4i\")", context));
        Assert.Equal(4d, evaluator.Evaluate("IMAGINARY(\"3+4i\")", context));
        Assert.Equal("4+2i", evaluator.Evaluate("IMSUM(\"3+4i\";\"1-2i\")", context));
        Assert.Equal("11-2i", evaluator.Evaluate("IMPRODUCT(\"3+4i\";\"1-2i\")", context));
        Assert.Equal("3-4i", evaluator.Evaluate("IMCONJUGATE(\"3+4i\")", context));
    }

    /// <summary>
    /// Verifies text cleanup, Unicode scalar, and radix conversion functions.
    /// 驗證文字清理、Unicode 純量及基數轉換函式。
    /// </summary>
    [Fact]
    public void CompatibilityConversionFunctionsEvaluate()
    {
        var evaluator = new DefaultFormulaEvaluator();
        var context = new ExtendedEvaluationContext();

        Assert.Equal("AB", evaluator.Evaluate("CLEAN(\"A\"&CHAR(10)&\"B\")", context));
        Assert.Equal(134071d, evaluator.Evaluate("UNICODE(\"𠮷\")", context));
        Assert.Equal("𠮷", evaluator.Evaluate("UNICHAR(134071)", context));
        Assert.Equal("00FF", evaluator.Evaluate("BASE(255;16;4)", context));
        Assert.Equal(255d, evaluator.Evaluate("DECIMAL(\"FF\";16)", context));
    }

    /// <summary>
    /// Verifies extended statistical functions over inline arrays.
    /// 驗證內嵌陣列的擴充統計函式。
    /// </summary>
    [Fact]
    public void ExtendedStatisticalFunctionsEvaluateInlineArrays()
    {
        var evaluator = new DefaultFormulaEvaluator();
        var context = new ExtendedEvaluationContext();

        Assert.Equal(2d / 3d, Assert.IsType<double>(
            evaluator.Evaluate("AVEDEV({1;2;3})", context)), 10);
        Assert.Equal(1d, evaluator.Evaluate("CORREL({1;2;3};{2;4;6})", context));
        Assert.Equal(2d / 3d, Assert.IsType<double>(
            evaluator.Evaluate("COVAR({1;2;3};{2;3;4})", context)), 10);
        Assert.Equal(2d, evaluator.Evaluate("DEVSQ({1;2;3})", context));
        Assert.Equal(2d, Assert.IsType<double>(
            evaluator.Evaluate("GEOMEAN({1;2;4})", context)), 10);
        Assert.Equal(12d / 7d, Assert.IsType<double>(
            evaluator.Evaluate("HARMEAN({1;2;4})", context)), 10);
        Assert.Equal(2d, evaluator.Evaluate("SLOPE({2;4;6};{1;2;3})", context));
        Assert.Equal(0d, evaluator.Evaluate("INTERCEPT({2;4;6};{1;2;3})", context));
        Assert.Equal(1d, evaluator.Evaluate("RSQ({2;4;6};{1;2;3})", context));
        Assert.Equal(14d, evaluator.Evaluate("SUMSQ({1;2;3})", context));
        Assert.Equal(1d, evaluator.Evaluate("AVERAGEA({TRUE;2;\"x\"})", context));
    }

    /// <summary>
    /// Verifies additional date functions required by Medium and Large groups.
    /// 驗證 Medium 與 Large Group 要求的額外日期函式。
    /// </summary>
    [Fact]
    public void ExtendedDateFunctionsEvaluate()
    {
        var evaluator = new DefaultFormulaEvaluator();
        var context = new ExtendedEvaluationContext();

        Assert.Equal(9d, evaluator.Evaluate(
            "DAYS(DATE(2024;1;10);DATE(2024;1;1))",
            context));
        Assert.Equal(1d, evaluator.Evaluate(
            "ISOWEEKNUM(DATE(2024;1;4))",
            context));
    }

    private sealed class RecordingFallback(object result) : IOdfFormulaEvaluationFallback
    {
        public string? Formula { get; private set; }

        public bool TryEvaluate(string formula, IEvaluationContext context, out object fallbackResult)
        {
            Formula = formula;
            fallbackResult = result;
            return true;
        }
    }

    private sealed class ExtendedEvaluationContext : IEvaluationContext
    {
        private static readonly object[,] Database =
        {
            { "Name", "Score" },
            { "A", 10d },
            { "B", 20d },
            { "C", 30d }
        };

        private static readonly object[,] AllCriteria =
        {
            { "Score" },
            { ">=10" }
        };

        private static readonly object[,] OneCriteria =
        {
            { "Score" },
            { "=20" }
        };

        public OdfCellAddress CurrentCell => default;

        public object GetCellValue(OdfCellAddress address) => 0d;

        public object[,] GetRangeValues(OdfCellRange range) => new object[0, 0];

        public string? GetCellFormula(OdfCellAddress address) => null;

        public object GetNamedRangeOrExpressionValue(string name) => name switch
        {
            "DB" => Database,
            "CRIT" => AllCriteria,
            "ONE" => OneCriteria,
            _ => OdfFormulaError.Name
        };
    }
}
