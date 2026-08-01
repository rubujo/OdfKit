using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using OdfKit.Core;
using OdfKit.Formula;
using OdfKit.Spreadsheet;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// Verifies the extensible evaluator and the mandatory Small-group function baseline.
/// 驗證可擴充評估器與 Small Group 強制函式基線。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Regression)]
public sealed class OpenFormulaExtendedEvaluatorTests
{
    private static readonly string[] SmallBaselineAdditions =
    [
        "DCOUNTA", "DGET", "DPRODUCT", "DSTDEV", "DSTDEVP", "DVAR", "DVARP",
        "ISERR", "N", "NPV", "PROPER", "SYD", "T", "VALUE"
    ];

    [Theory]
    [InlineData("EXP(1000)")]
    [InlineData("POWER(1E308,2)")]
    [InlineData("SUMSQ(1E308)")]
    [InlineData("ABS(EXP(1000))")]
    public void NonFiniteNumericResultsReturnNumError(string formula)
    {
        var evaluator = new DefaultFormulaEvaluator();

        object result = evaluator.Evaluate(formula, new ExtendedEvaluationContext());

        Assert.Equal(OdfFormulaError.Num, result);
    }

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
        Assert.True(small.HasOnlyFullyEvaluatedFunctions);
        Assert.Empty(small.BestEffortFunctions);
        Assert.Equal(272, medium.RequiredFunctions.Count);
        Assert.True(medium.HasCompleteFunctionSet);
        Assert.Empty(medium.MissingFunctions);
        Assert.True(medium.HasOnlyFullyEvaluatedFunctions);
        Assert.Empty(medium.BestEffortFunctions);
        Assert.DoesNotContain("MMULT", medium.MissingFunctions);
        Assert.Equal(388, large.RequiredFunctions.Count);
        Assert.True(large.HasCompleteFunctionSet);
        Assert.Empty(large.MissingFunctions);
        Assert.False(large.HasOnlyFullyEvaluatedFunctions);
        Assert.Contains("DDE", large.SecurityExcludedFunctions);
        Assert.True(large.IsSafeProfileComplete);
        Assert.DoesNotContain("GETPIVOTDATA", large.BestEffortFunctions);
        Assert.DoesNotContain("INFO", large.BestEffortFunctions);
        Assert.DoesNotContain("LINEST", large.BestEffortFunctions);
        Assert.DoesNotContain("MULTIPLE.OPERATIONS", large.BestEffortFunctions);
        Assert.DoesNotContain("SHEET", large.BestEffortFunctions);
        Assert.DoesNotContain("TTEST", large.BestEffortFunctions);
        Assert.DoesNotContain("COMPLEX", large.MissingFunctions);
        Assert.DoesNotContain("DDE", large.MissingFunctions);
    }

    /// <summary>
    /// Verifies workbook-aware sheet, pivot, and multiple-operation evaluation.
    /// 驗證可感知活頁簿的工作表、樞紐分析表與多重運算求值。
    /// </summary>
    [Fact]
    public void WorkbookAwareFunctionsUseOptionalContextServices()
    {
        var evaluator = new DefaultFormulaEvaluator();
        var context = new WorkbookEvaluationContext();

        Assert.Equal(3d, evaluator.Evaluate("SHEETS()", context));
        Assert.Equal(2d, evaluator.Evaluate("SHEET()", context));
        Assert.Equal(3d, evaluator.Evaluate("SHEET(Third!A1)", context));
        Assert.Equal(42d, evaluator.Evaluate(
            "GETPIVOTDATA(\"Sales\";Second!A1;\"Region\";\"North\")",
            context));
        Assert.Equal(99d, evaluator.Evaluate("MULTIPLE.OPERATIONS(1;2;3)", context));
    }

    /// <summary>
    /// Verifies that document recalculation exposes the real worksheet catalog.
    /// 驗證文件重算會提供實際工作表目錄。
    /// </summary>
    [Fact]
    public void DocumentEvaluationUsesActualWorksheetCatalog()
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create();
        OdfTableSheet first = document.AddSheet("First");
        OdfTableSheet second = document.AddSheet("Second");
        first.Cells["A1"].Formula = "of:=SHEETS()";
        second.Cells["A1"].Formula = "of:=SHEET()";

        document.EvaluateFormulas();

        Assert.Equal(2d, first.Cells["A1"].CellValue);
        Assert.Equal(2d, second.Cells["A1"].CellValue);
    }

    /// <summary>
    /// Verifies sheet-range references retain workbook order for SHEET and SHEETS.
    /// 驗證跨工作表參照會為 SHEET 與 SHEETS 保留活頁簿順序。
    /// </summary>
    [Fact]
    public void SheetFunctionsEvaluateThreeDimensionalReferences()
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create();
        OdfTableSheet first = document.AddSheet("First");
        document.AddSheet("Second");
        document.AddSheet("Third");
        first.Cells["A1"].Formula =
            "of:=SHEETS([First.A2]:[Third.A2])";
        first.Cells["A2"].Formula =
            "of:=SHEET([Second.A2]:[Third.A2])";

        document.EvaluateFormulas();

        Assert.Equal(3d, first.Cells["A1"].CellValue);
        Assert.Equal(2d, first.Cells["A2"].CellValue);
    }

    /// <summary>
    /// Verifies multi-predictor linear regression and prediction.
    /// 驗證多自變數線性迴歸與預測。
    /// </summary>
    [Fact]
    public void RegressionFunctionsSupportMultiplePredictors()
    {
        var evaluator = new DefaultFormulaEvaluator();
        var context = new ExtendedEvaluationContext();

        object[,] coefficients = Assert.IsType<object[,]>(
            evaluator.Evaluate("LINEST({3|4|8|9};{1;0|0;1|2;1|1;2})", context));
        Assert.Equal(3d, Assert.IsType<double>(coefficients[0, 0]), 8);
        Assert.Equal(2d, Assert.IsType<double>(coefficients[0, 1]), 8);
        Assert.Equal(1d, Assert.IsType<double>(coefficients[0, 2]), 8);

        object[,] predictions = Assert.IsType<object[,]>(
            evaluator.Evaluate("TREND({3|4|8|9};{1;0|0;1|2;1|1;2};{3;1})", context));
        Assert.Equal(10d, Assert.IsType<double>(predictions[0, 0]), 8);
    }

    /// <summary>
    /// Verifies the complete five-row LINEST statistics result.
    /// 驗證 LINEST 完整五列統計結果。
    /// </summary>
    [Fact]
    public void LinestReturnsCompleteStatistics()
    {
        var evaluator = new DefaultFormulaEvaluator();
        var context = new ExtendedEvaluationContext();

        object[,] statistics = Assert.IsType<object[,]>(evaluator.Evaluate(
            "LINEST({3|4|8|9|12};{1;0|0;1|2;1|1;2|2;2};TRUE();TRUE())",
            context));

        Assert.Equal(5, statistics.GetLength(0));
        Assert.Equal(3, statistics.GetLength(1));
        Assert.InRange(Assert.IsType<double>(statistics[2, 0]), 0, 1);
        Assert.True(Assert.IsType<double>(statistics[2, 1]) > 0);
        Assert.True(Assert.IsType<double>(statistics[3, 0]) > 0);
        Assert.Equal(2d, Assert.IsType<double>(statistics[3, 1]));
        Assert.True(Assert.IsType<double>(statistics[4, 0]) > 0);
        Assert.True(Assert.IsType<double>(statistics[4, 1]) > 0);
        Assert.Equal(OdfFormulaError.NA, statistics[2, 2]);
        Assert.Equal(OdfFormulaError.NA, statistics[4, 2]);
    }

    /// <summary>
    /// Verifies regression rejects a model with no residual degrees of freedom.
    /// 驗證迴歸模型會拒絕沒有殘差自由度的輸入。
    /// </summary>
    [Fact]
    public void RegressionRejectsZeroResidualDegreesOfFreedom()
    {
        var evaluator = new DefaultFormulaEvaluator();
        var context = new ExtendedEvaluationContext();

        Assert.Equal(
            OdfFormulaError.Num,
            evaluator.Evaluate("LINEST({3|4|8};{1;0|0;1|2;1})", context));
    }

    /// <summary>
    /// Verifies odd first and last coupon prices can be inverted back to their yields.
    /// 驗證奇數首期與末期票息價格可反算回殖利率。
    /// </summary>
    [Fact]
    public void OddCouponBondPricesAndYieldsRoundTrip()
    {
        var evaluator = new DefaultFormulaEvaluator();
        var context = new ExtendedEvaluationContext();

        double oddFirstPrice = Assert.IsType<double>(evaluator.Evaluate(
            "ODDFPRICE(DATE(2024;2;1);DATE(2026;1;1);DATE(2023;10;1);" +
            "DATE(2024;7;1);0.05;0.06;100;2;1)",
            context));
        string oddFirstYieldFormula =
            "ODDFYIELD(DATE(2024;2;1);DATE(2026;1;1);DATE(2023;10;1);" +
            "DATE(2024;7;1);0.05;" +
            oddFirstPrice.ToString("R", CultureInfo.InvariantCulture) +
            ";100;2;1)";
        Assert.Equal(0.06d, Assert.IsType<double>(
            evaluator.Evaluate(oddFirstYieldFormula, context)), 7);

        double oddLastPrice = Assert.IsType<double>(evaluator.Evaluate(
            "ODDLPRICE(DATE(2025;8;1);DATE(2026;3;15);DATE(2025;7;1);" +
            "0.05;0.06;100;2;1)",
            context));
        string oddLastYieldFormula =
            "ODDLYIELD(DATE(2025;8;1);DATE(2026;3;15);DATE(2025;7;1);0.05;" +
            oddLastPrice.ToString("R", CultureInfo.InvariantCulture) +
            ";100;2;1)";
        Assert.Equal(0.06d, Assert.IsType<double>(
            evaluator.Evaluate(oddLastYieldFormula, context)), 7);
    }

    /// <summary>
    /// Verifies Bessel functions against independent high-precision reference values.
    /// 依獨立的高精度參考值驗證 Bessel 函式。
    /// </summary>
    [Fact]
    [Trait(TestCategories.Kind, TestCategories.Corpus)]
    public void BesselFunctionsMatchIndependentReferenceValues()
    {
        var evaluator = new DefaultFormulaEvaluator();
        var context = new ExtendedEvaluationContext();

        AssertRelative(0.765197686557967, EvaluateNumber("BESSELJ(1;0)"), 2e-13);
        AssertRelative(0.130670933554863, EvaluateNumber("BESSELJ(20;4)"), 2e-11);
        AssertRelative(-0.0215287573445057, EvaluateNumber("BESSELJ(100;2)"), 2e-11);
        AssertRelative(
            0.09636667329586156,
            EvaluateNumber("BESSELJ(100;100)"),
            2e-11);
        AssertRelative(
            1.1159273690838093e-21,
            EvaluateNumber("BESSELJ(50;100)"),
            2e-11);
        AssertRelative(1.26606587775201, EvaluateNumber("BESSELI(1;0)"), 2e-13);
        AssertRelative(28935060.3187649, EvaluateNumber("BESSELI(20;4)"), 2e-13);
        AssertRelative(
            2.2551205757604039e-14,
            EvaluateNumber("BESSELI(20;50)"),
            2e-12);
        AssertRelative(
            5.4420084027529975e18,
            EvaluateNumber("BESSELI(50;20)"),
            2e-12);
        AssertRelative(0.421024438240708333, EvaluateNumber("BESSELK(1;0)"), 2e-12);
        AssertRelative(
            8.47423361989687325e-10,
            EvaluateNumber("BESSELK(20;4)"),
            2e-12);
        AssertRelative(
            4.1171120912201772e11,
            EvaluateNumber("BESSELK(20;50)"),
            2e-11);
        AssertRelative(
            1.7061483797220351e-21,
            EvaluateNumber("BESSELK(50;20)"),
            2e-11);
        AssertRelative(0.088256964215677, EvaluateNumber("BESSELY(1;0)"), 2e-12);
        AssertRelative(0.124093737059654, EvaluateNumber("BESSELY(20;4)"), 2e-11);
        AssertRelative(0.0768368671250279, EvaluateNumber("BESSELY(100;2)"), 2e-11);
        AssertRelative(
            -0.1669214114175765,
            EvaluateNumber("BESSELY(100;100)"),
            2e-10);
        AssertRelative(
            0.016442633948115778,
            EvaluateNumber("BESSELY(50;20)"),
            2e-11);
        Assert.Equal(
            -EvaluateNumber("BESSELY(1;1)"),
            EvaluateNumber("BESSELY(-1;1)"),
            12);

        double EvaluateNumber(string formula) =>
            Assert.IsType<double>(evaluator.Evaluate(formula, context));
    }

    /// <summary>
    /// Verifies published odd-coupon examples and the inverse yield calculation.
    /// 驗證已發布的奇數票息範例與反向殖利率計算。
    /// </summary>
    [Fact]
    [Trait(TestCategories.Kind, TestCategories.Corpus)]
    public void OddCouponFunctionsMatchPublishedReferenceExamples()
    {
        var evaluator = new DefaultFormulaEvaluator();
        var context = new ExtendedEvaluationContext();

        double oddFirstPrice = Assert.IsType<double>(evaluator.Evaluate(
            "ODDFPRICE(DATE(2008;11;11);DATE(2021;3;1);DATE(2008;10;15);" +
            "DATE(2009;3;1);0.0785;0.0625;100;2;1)",
            context));
        Assert.Equal(113.597717474078, oddFirstPrice, 9);
        Assert.Equal(0.0625, Assert.IsType<double>(evaluator.Evaluate(
            "ODDFYIELD(DATE(2008;11;11);DATE(2021;3;1);DATE(2008;10;15);" +
            "DATE(2009;3;1);0.0785;113.597717474078;100;2;1)",
            context)), 9);

        Assert.Equal(99.8782860147213, Assert.IsType<double>(evaluator.Evaluate(
            "ODDLPRICE(DATE(2008;2;7);DATE(2008;6;15);DATE(2007;10;15);" +
            "0.0375;0.0405;100;2;0)",
            context)), 10);
        Assert.Equal(0.0404998874758287, Assert.IsType<double>(evaluator.Evaluate(
            "ODDLYIELD(DATE(2008;2;7);DATE(2008;6;15);DATE(2007;10;15);" +
            "0.0375;99.87829;100;2;0)",
            context)), 10);
        Assert.Same(OdfFormulaError.Num, evaluator.Evaluate(
            "ODDFPRICE(DATE(2024;1;1);DATE(2025;1;1);DATE(2024;2;1);" +
            "DATE(2024;7;1);0.05;0.06;100;2)",
            context));
    }

    /// <summary>
    /// Verifies long odd coupon periods across every OpenFormula day-count basis.
    /// 驗證長奇數票息期間在所有 OpenFormula 日期基準下的結果。
    /// </summary>
    /// <param name="basis">The OpenFormula day-count basis. / OpenFormula 日期基準。</param>
    /// <param name="expectedFirstPrice">The expected odd-first price. / 預期的奇數首期價格。</param>
    /// <param name="expectedLastPrice">The expected odd-last price. / 預期的奇數末期價格。</param>
    [Theory]
    [Trait(TestCategories.Kind, TestCategories.Corpus)]
    [InlineData(0, 98.09254160075565, 99.30219937160811)]
    [InlineData(1, 98.09454497479267, 99.30776395062905)]
    [InlineData(2, 98.0057924949512, 99.30776395062905)]
    [InlineData(3, 98.08256440779863, 99.30776395062905)]
    [InlineData(4, 98.09254160075565, 99.30219937160811)]
    public void OddCouponFunctionsMatchEveryDateBasis(
        int basis,
        double expectedFirstPrice,
        double expectedLastPrice)
    {
        var evaluator = new DefaultFormulaEvaluator();
        var context = new ExtendedEvaluationContext();
        string basisText = basis.ToString(CultureInfo.InvariantCulture);

        double firstPrice = Assert.IsType<double>(evaluator.Evaluate(
            "ODDFPRICE(DATE(2024;2;1);DATE(2026;1;1);DATE(2023;1;15);" +
            "DATE(2024;7;1);0.05;0.06;100;2;" + basisText + ")",
            context));
        double lastPrice = Assert.IsType<double>(evaluator.Evaluate(
            "ODDLPRICE(DATE(2025;8;1);DATE(2026;3;15);DATE(2025;1;15);" +
            "0.05;0.06;100;2;" + basisText + ")",
            context));

        AssertRelative(expectedFirstPrice, firstPrice, 2e-11);
        AssertRelative(expectedLastPrice, lastPrice, 2e-11);
        Assert.Equal(0.06, Assert.IsType<double>(evaluator.Evaluate(
            "ODDFYIELD(DATE(2024;2;1);DATE(2026;1;1);DATE(2023;1;15);" +
            "DATE(2024;7;1);0.05;" +
            firstPrice.ToString("R", CultureInfo.InvariantCulture) +
            ";100;2;" + basisText + ")",
            context)), 8);
        Assert.Equal(0.06, Assert.IsType<double>(evaluator.Evaluate(
            "ODDLYIELD(DATE(2025;8;1);DATE(2026;3;15);DATE(2025;1;15);" +
            "0.05;" + lastPrice.ToString("R", CultureInfo.InvariantCulture) +
            ";100;2;" + basisText + ")",
            context)), 8);
    }

    /// <summary>
    /// Verifies representative functions from the completed Medium and Large mandatory catalogs.
    /// 驗證已補齊之 Medium 與 Large 強制目錄的代表性函式。
    /// </summary>
    [Fact]
    public void RemainingMediumAndLargeFunctionsEvaluate()
    {
        var evaluator = new DefaultFormulaEvaluator();
        var context = new ExtendedEvaluationContext();

        Assert.Equal(0.5d, Assert.IsType<double>(
            evaluator.Evaluate("BETADIST(0.5;2;2)", context)), 8);
        Assert.Equal(1.125d, Assert.IsType<double>(
            evaluator.Evaluate("DOLLARDE(1.02;16)", context)), 8);
        Assert.Equal(0.12682503013197d, Assert.IsType<double>(
            evaluator.Evaluate("EFFECT(0.12;12)", context)), 8);
        Assert.Equal(0.5d, Assert.IsType<double>(
            evaluator.Evaluate("PERCENTRANK({1;2;3};2)", context)), 8);
        Assert.Equal(1d, Assert.IsType<double>(
            evaluator.Evaluate("SUBTOTAL(9;{0.25;0.75})", context)), 8);
        Assert.Equal(1d, Assert.IsType<double>(
            evaluator.Evaluate("YEARFRAC(DATE(2024;1;1);DATE(2025;1;1);1)", context)), 8);
        Assert.Equal("OdfKit", evaluator.Evaluate(
            "HYPERLINK(\"https://example.invalid\";\"OdfKit\")", context));
        Assert.Equal(1.95583d, Assert.IsType<double>(
            evaluator.Evaluate("EUROCONVERT(1;\"EUR\";\"DEM\")", context)), 8);
        Assert.Equal(0.0882569642d, Assert.IsType<double>(
            evaluator.Evaluate("BESSELY(1;0)", context)), 7);
        Assert.Equal(0.4210244382d, Assert.IsType<double>(
            evaluator.Evaluate("BESSELK(1;0)", context)), 7);
        Assert.InRange(Assert.IsType<double>(
            evaluator.Evaluate("TTEST({1;2;3};{1;2;4};2;1)", context)), 0, 1);
        Assert.IsType<OdfFormulaError>(evaluator.Evaluate(
            "DDE(\"service\";\"topic\";\"item\")", context));
    }

    /// <summary>
    /// Verifies every required INFO category and host-controlled overrides.
    /// 驗證所有必要 INFO 類別及由主機控制的覆寫值。
    /// </summary>
    [Fact]
    public void InfoSupportsRequiredCategoriesAndHostOverrides()
    {
        var evaluator = new DefaultFormulaEvaluator();
        var context = new ExtendedEvaluationContext();

        Assert.Equal("virtual:/", evaluator.Evaluate("INFO(\"directory\")", context));
        Assert.IsType<double>(evaluator.Evaluate("INFO(\"memavail\")", context));
        Assert.IsType<double>(evaluator.Evaluate("INFO(\"memused\")", context));
        Assert.IsType<double>(evaluator.Evaluate("INFO(\"numfile\")", context));
        Assert.IsType<string>(evaluator.Evaluate("INFO(\"osversion\")", context));
        Assert.IsType<string>(evaluator.Evaluate("INFO(\"origin\")", context));
        Assert.IsType<string>(evaluator.Evaluate("INFO(\"recalc\")", context));
        Assert.IsType<string>(evaluator.Evaluate("INFO(\"release\")", context));
        Assert.IsType<string>(evaluator.Evaluate("INFO(\"system\")", context));
        Assert.IsType<double>(evaluator.Evaluate("INFO(\"totmem\")", context));
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
    /// Verifies additional Large-group engineering, statistical, and financial functions.
    /// 驗證額外的 Large Group 工程、統計及財務函式。
    /// </summary>
    [Fact]
    public void LargeCompatibilityFunctionsEvaluate()
    {
        var evaluator = new DefaultFormulaEvaluator();
        var context = new ExtendedEvaluationContext();

        Assert.Equal(-1d, evaluator.Evaluate("BIN2DEC(\"1111111111\")", context));
        Assert.Equal("FFFFFFFFFF", evaluator.Evaluate("DEC2HEX(-1)", context));
        Assert.Equal(-1d, evaluator.Evaluate("HEX2DEC(\"FFFFFFFFFF\")", context));
        Assert.Equal("000000FF", evaluator.Evaluate("DEC2HEX(255;8)", context));
        Assert.Equal(1999d, evaluator.Evaluate("ARABIC(\"MCMXCIX\")", context));
        Assert.Equal(15d, evaluator.Evaluate("COMBINA(3;4)", context));
        Assert.Equal(24d, Assert.IsType<double>(evaluator.Evaluate("GAMMA(5)", context)), 10);
        Assert.Equal(0d, Assert.IsType<double>(evaluator.Evaluate("GAUSS(0)", context)), 10);
        Assert.Equal(132d, Assert.IsType<double>(
            evaluator.Evaluate("FVSCHEDULE(100;{0.1;0.2})", context)), 10);
        Assert.Equal(0.1d, Assert.IsType<double>(
            evaluator.Evaluate("RRI(2;100;121)", context)), 10);
        Assert.Equal(1234.5d, evaluator.Evaluate(
            "NUMBERVALUE(\"1.234,5\";\",\";\".\")",
            context));
        Assert.Equal(2d, evaluator.Evaluate("ERROR.TYPE(1/0)", context));
    }

    /// <summary>
    /// Verifies probability distributions and special functions required by Medium and Large groups.
    /// 驗證 Medium 與 Large Group 要求的機率分佈及特殊函式。
    /// </summary>
    [Fact]
    public void DistributionFunctionsEvaluate()
    {
        var evaluator = new DefaultFormulaEvaluator();
        var context = new ExtendedEvaluationContext();

        Assert.Equal(0d, Assert.IsType<double>(evaluator.Evaluate("ERF(0)", context)), 10);
        Assert.Equal(Math.Log(24), Assert.IsType<double>(
            evaluator.Evaluate("GAMMALN(5)", context)), 10);
        Assert.Equal(0.5d, Assert.IsType<double>(
            evaluator.Evaluate("FISHERINV(FISHER(0.5))", context)), 10);
        Assert.Equal(0.375d, Assert.IsType<double>(
            evaluator.Evaluate("BINOMDIST(2;4;0.5;FALSE())", context)), 10);
        Assert.Equal(Math.Exp(-1), Assert.IsType<double>(
            evaluator.Evaluate("POISSON(0;1;FALSE())", context)), 10);
        Assert.Equal(0.5d, Assert.IsType<double>(
            evaluator.Evaluate("NORMDIST(0;0;1;TRUE())", context)), 6);
        Assert.Equal(10d, Assert.IsType<double>(
            evaluator.Evaluate("NORMINV(0.5;10;2)", context)), 10);
        Assert.InRange(Assert.IsType<double>(
            evaluator.Evaluate("NORMINV(0.975;0;1)", context)), 1.95996398d, 1.959964d);
    }

    /// <summary>
    /// Verifies remaining compatibility functions for text, references, sequences, and regression.
    /// 驗證文字、參照、序列及迴歸的剩餘相容函式。
    /// </summary>
    [Fact]
    public void RemainingCompatibilityFunctionsEvaluate()
    {
        var evaluator = new DefaultFormulaEvaluator();
        var context = new ExtendedEvaluationContext();

        Assert.Equal("$A$1", evaluator.Evaluate("ADDRESS(1;1)", context));
        Assert.Equal("1,234.50", evaluator.Evaluate("FIXED(1234.5;2;FALSE())", context));
        Assert.Equal(8d, evaluator.Evaluate("FORECAST(4;{2;4;6};{1;2;3})", context));
        Assert.Equal(2d, evaluator.Evaluate("MODE({1;2;2;3})", context));
        Assert.Equal(60d, evaluator.Evaluate("PERMUT(5;3)", context));
        Assert.Equal("MCMXCIX", evaluator.Evaluate("ROMAN(1999)", context));
        Assert.Equal(17d, evaluator.Evaluate("SERIESSUM(2;0;1;{1;2;3})", context));
        Assert.Equal("ＡＢＣ１２３", evaluator.Evaluate("JIS(\"ABC123\")", context));
        Assert.Equal("ABC123", evaluator.Evaluate("ASC(\"ＡＢＣ１２３\")", context));
        Assert.Equal(4d, evaluator.Evaluate("LENB(\"A中B\")", context));
        Assert.Equal("A中", evaluator.Evaluate("LEFTB(\"A中B\";3)", context));
        Assert.Equal(4d, evaluator.Evaluate("FINDB(\"B\";\"A中B\")", context));
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

    /// <summary>
    /// Verifies volatile functions use caller-controlled calculation-session state.
    /// 驗證 volatile 函式會使用呼叫端控制的計算工作階段狀態。
    /// </summary>
    [Fact]
    public void VolatileFunctionsUseCalculationSessionContext()
    {
        var evaluator = new DefaultFormulaEvaluator();
        var context = new VolatileEvaluationContext(
            new DateTime(2024, 2, 3, 12, 0, 0, DateTimeKind.Local),
            [0.25, 0.75, 0.75]);

        Assert.Equal(
            (context.EvaluationTimestamp - new DateTime(1899, 12, 30)).TotalDays,
            evaluator.Evaluate("NOW()", context));
        Assert.Equal(
            (context.EvaluationTimestamp.Date - new DateTime(1899, 12, 30)).TotalDays,
            evaluator.Evaluate("TODAY()", context));
        Assert.Equal(1d, evaluator.Evaluate("RAND()+RAND()", context));
        Assert.Equal(4d, evaluator.Evaluate("RANDBETWEEN(1;4)", context));
    }

    /// <summary>
    /// Verifies array operators evaluate element-wise and support scalar broadcasting.
    /// 驗證陣列運算子會逐元素求值並支援純量廣播。
    /// </summary>
    [Fact]
    public void ArrayOperatorsEvaluateElementWise()
    {
        var evaluator = new DefaultFormulaEvaluator();
        var context = new ExtendedEvaluationContext();

        object[,] result = Assert.IsType<object[,]>(
            evaluator.Evaluate("-({1;2|3;4}+1)*2", context));

        Assert.Equal(-4d, result[0, 0]);
        Assert.Equal(-6d, result[0, 1]);
        Assert.Equal(-8d, result[1, 0]);
        Assert.Equal(-10d, result[1, 1]);
    }

    /// <summary>
    /// Verifies matrix formulas write rectangular results to their declared range.
    /// 驗證矩陣公式會將矩形結果寫回宣告的範圍。
    /// </summary>
    [Fact]
    public void DocumentRecalculationWritesArrayFormulaResults()
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.AddSheet("Data");
        sheet.Ranges["A1:B2"].SetArrayFormula("of:={1;2|3;4}+10");

        document.EvaluateFormulas();

        Assert.Equal(11d, sheet.Cells["A1"].CellValue);
        Assert.Equal(12d, sheet.Cells["B1"].CellValue);
        Assert.Equal(13d, sheet.Cells["A2"].CellValue);
        Assert.Equal(14d, sheet.Cells["B2"].CellValue);
        Assert.Equal(
            "2",
            sheet.Cells["A1"].Node.GetAttribute(
                "number-matrix-columns-spanned",
                OdfNamespaces.Table));

        using var stream = new MemoryStream();
        document.Save();
        document.Package.Save(stream);
        stream.Position = 0;
        using SpreadsheetDocument reloaded = SpreadsheetDocument.Load(
            stream,
            "array-formula.ods");
        Assert.Equal(14d, reloaded.Worksheets[0].Cells["B2"].CellValue);
        Assert.Equal(
            "2",
            reloaded.Worksheets[0].Cells["A1"].Node.GetAttribute(
                "number-matrix-rows-spanned",
                OdfNamespaces.Table));

        sheet.Ranges["A1:B2"].ClearArrayFormula();

        Assert.Equal(string.Empty, sheet.Cells["A1"].Formula);
        Assert.Null(sheet.Cells["A1"].Node.GetAttribute(
            "number-matrix-columns-spanned",
            OdfNamespaces.Table));
    }

    /// <summary>
    /// Verifies constant errors, scientific notation, type-sensitive comparisons, and numeric limits.
    /// 驗證常數錯誤、科學記號、型別感知比較與數值限制。
    /// </summary>
    [Fact]
    public void CoreExpressionSemanticsFollowOpenFormulaScalarRules()
    {
        var evaluator = new DefaultFormulaEvaluator();
        var context = new ExtendedEvaluationContext();

        Assert.Equal(125.25d, evaluator.Evaluate("1.25E2+2.5e-1", context));
        Assert.Same(OdfFormulaError.NA, evaluator.Evaluate("#N/A", context));
        Assert.False(Assert.IsType<bool>(evaluator.Evaluate("1=TRUE", context)));
        Assert.True(Assert.IsType<bool>(evaluator.Evaluate("1<\"2\"", context)));
        Assert.True(Assert.IsType<bool>(evaluator.Evaluate("+TRUE", context)));
        Assert.Same(OdfFormulaError.Num, evaluator.Evaluate("0^0", context));
    }

    /// <summary>
    /// Verifies implied intersection selects one cell from a row or column reference.
    /// 驗證隱含交集會從單列或單欄參照選取一個儲存格。
    /// </summary>
    [Fact]
    public void ScalarOperatorsApplyImpliedIntersection()
    {
        var evaluator = new DefaultFormulaEvaluator();
        var context = new IntersectionEvaluationContext();

        Assert.Equal(21d, evaluator.Evaluate("A2:C2+1", context));
        Assert.Equal(21d, evaluator.Evaluate("B1:B3+1", context));
        Assert.Same(OdfFormulaError.Value, evaluator.Evaluate("A1:C3+1", context));
    }

    /// <summary>
    /// Verifies the ODF DOM evaluates pivot aggregation and multiple operations without an external engine.
    /// 驗證 ODF DOM 不需外部引擎即可評估樞紐彙總與多重運算。
    /// </summary>
    [Fact]
    [Trait(TestCategories.Kind, TestCategories.Corpus)]
    public void DocumentContextEvaluatesPivotAndMultipleOperations()
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.AddSheet("Data");
        sheet.Cells["A1"].CellValue = "Region";
        sheet.Cells["B1"].CellValue = "Category";
        sheet.Cells["C1"].CellValue = "Sales";
        sheet.Cells["A2"].CellValue = "North";
        sheet.Cells["B2"].CellValue = "North";
        sheet.Cells["C2"].CellValue = 10d;
        sheet.Cells["A3"].CellValue = "South";
        sheet.Cells["B3"].CellValue = "Retail";
        sheet.Cells["C3"].CellValue = 20d;
        sheet.Cells["A4"].CellValue = "North West";
        sheet.Cells["B4"].CellValue = "Wholesale";
        sheet.Cells["C4"].CellValue = 30d;
        sheet.CreatePivotTable(
            new OdfCellRange(0, 0, 3, 2, "Data"),
            new OdfCellAddress(0, 4, "Data"),
            pivot => pivot
                .AddRowField("Region")
                .AddRowField("Category")
                .AddDataField("Sales", OdfPivotFunction.Sum));
        sheet.Cells["F1"].Formula =
            "of:=GETPIVOTDATA(\"Sales\";[Data.E1];\"Region\";\"North\")";
        sheet.Cells["F2"].Formula =
            "of:=GETPIVOTDATA([Data.E1];\"Sales Region[North]\")";
        sheet.Cells["F3"].Formula =
            "of:=GETPIVOTDATA([Data.E1];\"Region[North]\")";
        sheet.Cells["F4"].Formula =
            "of:=GETPIVOTDATA([Data.E1];\"Sales 'North West'\")";
        sheet.Cells["F5"].Formula =
            "of:=GETPIVOTDATA([Data.E1];\"Sales Region[North;sum]\")";
        sheet.Cells["F6"].Formula =
            "of:=GETPIVOTDATA([Data.E1];\"Sales North\")";
        sheet.Cells["H1"].CellValue = 2d;
        sheet.Cells["I1"].Formula = "of:=[.H1]*10";
        sheet.Cells["J1"].Formula =
            "of:=MULTIPLE.OPERATIONS([.I1];[.H1];7)";

        document.EvaluateFormulas();

        Assert.Equal(10d, sheet.Cells["F1"].CellValue);
        Assert.Equal(10d, sheet.Cells["F2"].CellValue);
        Assert.Equal(10d, sheet.Cells["F3"].CellValue);
        Assert.Equal(30d, sheet.Cells["F4"].CellValue);
        Assert.Equal(10d, sheet.Cells["F5"].CellValue);
        Assert.Equal("#N/A", sheet.Cells["F6"].CellValue);
        Assert.Equal(70d, sheet.Cells["J1"].CellValue);
        Assert.Equal(2d, sheet.Cells["H1"].CellValue);
    }

    /// <summary>
    /// Verifies the last pivot in document order wins when target ranges overlap.
    /// 驗證樞紐分析表目標範圍重疊時，會採用文件順序中的最後一個樞紐分析表。
    /// </summary>
    [Fact]
    [Trait(TestCategories.Kind, TestCategories.Corpus)]
    public void GetPivotDataUsesLastOverlappingPivotInDocumentOrder()
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.AddSheet("Data");
        sheet.Cells["A1"].CellValue = "Region";
        sheet.Cells["B1"].CellValue = "Sales";
        sheet.Cells["A2"].CellValue = "North";
        sheet.Cells["B2"].CellValue = 10d;
        sheet.Cells["D1"].CellValue = "Region";
        sheet.Cells["E1"].CellValue = "Sales";
        sheet.Cells["D2"].CellValue = "North";
        sheet.Cells["E2"].CellValue = 99d;

        OdfCellAddress target = new(0, 6, "Data");
        sheet.CreatePivotTable(
            new OdfCellRange(0, 0, 1, 1, "Data"),
            target,
            pivot => pivot
                .AddRowField("Region")
                .AddDataField("Sales", OdfPivotFunction.Sum));
        sheet.CreatePivotTable(
            new OdfCellRange(0, 3, 1, 4, "Data"),
            target,
            pivot => pivot
                .AddRowField("Region")
                .AddDataField("Sales", OdfPivotFunction.Sum));
        sheet.Cells["I1"].Formula =
            "of:=GETPIVOTDATA(\"Sales\";[Data.G1];\"Region\";\"North\")";

        document.EvaluateFormulas();

        Assert.Equal(99d, sheet.Cells["I1"].CellValue);
    }

    /// <summary>
    /// Verifies sheet-local and external named expressions resolve case-insensitively.
    /// 驗證工作表區域與外部命名運算式會以不區分大小寫方式解析。
    /// </summary>
    [Fact]
    public void DocumentContextResolvesQualifiedAndExternalNamedExpressions()
    {
        using SpreadsheetDocument external = SpreadsheetDocument.Create();
        OdfTableSheet externalSheet = external.AddSheet("External");
        externalSheet.AddNamedExpression(
            "Answer",
            "of:=40+2",
            new OdfCellAddress(0, 0, "External"));

        using SpreadsheetDocument document = SpreadsheetDocument.Create();
        OdfTableSheet first = document.AddSheet("First");
        OdfTableSheet second = document.AddSheet("Second");
        second.AddNamedExpression(
            "Rate",
            "of:=6*7",
            new OdfCellAddress(0, 0, "Second"));
        first.Cells["A1"].Formula = "of:='Second'.rate";
        first.Cells["A2"].Formula =
            "of:='memory:book'#$'External'.answer";
        document.ExternalLinks.DocumentResolver = id =>
            id == "memory:book" ? external : null;

        document.EvaluateFormulas(new OdfFormulaEvaluationOptions
        {
            ExternalReferencePolicy =
                OdfFormulaExternalReferencePolicy.AllowConfiguredResolver
        }, TestContext.Current.CancellationToken);

        Assert.Equal(42d, first.Cells["A1"].CellValue);
        Assert.Equal(42d, first.Cells["A2"].CellValue);
    }

    /// <summary>
    /// Verifies reference ranges, named endpoints, quoted labels, and automatic intersection.
    /// 驗證參照範圍、具名端點、引號標籤及自動交集。
    /// </summary>
    [Fact]
    public void DocumentContextEvaluatesReferenceAndLabelOperators()
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.AddSheet("Data");
        sheet.Cells["B1"].CellValue = "Q1";
        sheet.Cells["C1"].CellValue = "Q2";
        sheet.Cells["A2"].CellValue = "North";
        sheet.Cells["B2"].CellValue = 10d;
        sheet.Cells["C2"].CellValue = 20d;
        sheet.Cells["A3"].CellValue = "South";
        sheet.Cells["B3"].CellValue = 30d;
        sheet.Cells["C3"].CellValue = 40d;
        sheet.AddNamedRange(
            "First",
            new OdfCellRange(1, 1, 1, 1, "Data"));
        sheet.AddNamedRange(
            "Last",
            new OdfCellRange(2, 2, 2, 2, "Data"));
        sheet.Cells["E1"].Formula = "of:=SUM([.B2]:[.C3])";
        sheet.Cells["E2"].Formula = "of:=SUM(First:Last)";
        sheet.Cells["E3"].Formula = "of:=SUM('Q1')";
        sheet.Cells["E4"].Formula = "of:='Q1'!!'North'";

        document.EvaluateFormulas();

        Assert.Equal(100d, sheet.Cells["E1"].CellValue);
        Assert.Equal(100d, sheet.Cells["E2"].CellValue);
        Assert.Equal(40d, sheet.Cells["E3"].CellValue);
        Assert.Equal(10d, sheet.Cells["E4"].CellValue);
    }

    /// <summary>
    /// Verifies regression handles rank-deficient predictor matrices without unstable normal equations.
    /// 驗證迴歸可穩定處理秩不足的預測矩陣，而不依賴不穩定的一般方程式。
    /// </summary>
    [Fact]
    public void RegressionHandlesRankDeficientPredictors()
    {
        var evaluator = new DefaultFormulaEvaluator();
        var context = new ExtendedEvaluationContext();

        object[,] coefficients = Assert.IsType<object[,]>(evaluator.Evaluate(
            "LINEST({3|5|7|9|11};{1;2|2;4|3;6|4;8|5;10})",
            context));
        object[,] predictions = Assert.IsType<object[,]>(evaluator.Evaluate(
            "TREND({3|5|7|9|11};{1;2|2;4|3;6|4;8|5;10};{6;12})",
            context));

        Assert.Contains(
            new[] { Assert.IsType<double>(coefficients[0, 0]), Assert.IsType<double>(coefficients[0, 1]) },
            coefficient => coefficient == 0);
        Assert.Equal(13d, Assert.IsType<double>(predictions[0, 0]), 8);
    }

    /// <summary>
    /// Verifies document recalculation captures one timestamp for all volatile formulas.
    /// 驗證文件重算會為所有 volatile 公式擷取同一個時間戳記。
    /// </summary>
    [Fact]
    public void DocumentRecalculationSharesVolatileTimestamp()
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.AddSheet("Sheet1");
        sheet.Cells["A1"].Formula = "of:=NOW()";
        sheet.Cells["B1"].Formula = "of:=NOW()";

        document.EvaluateFormulas();

        Assert.Equal(sheet.Cells["A1"].CellValue, sheet.Cells["B1"].CellValue);
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

    private sealed class ExtendedEvaluationContext :
        IEvaluationContext,
        IOdfFormulaEnvironmentContext
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

        public bool TryGetFormulaEnvironmentInfo(
            string category,
            out object result)
        {
            if (category.Equals("directory", StringComparison.OrdinalIgnoreCase))
            {
                result = "virtual:/";
                return true;
            }

            result = OdfFormulaError.NA;
            return false;
        }
    }

    private sealed class IntersectionEvaluationContext : IEvaluationContext
    {
        public OdfCellAddress CurrentCell => new(1, 1, null);

        public object GetCellValue(OdfCellAddress address) =>
            (address.Column + 1) * 10d;

        public object[,] GetRangeValues(OdfCellRange range)
        {
            int rows = Math.Abs(range.EndAddress.Row - range.StartAddress.Row) + 1;
            int columns = Math.Abs(
                range.EndAddress.Column - range.StartAddress.Column) + 1;
            var values = new object[rows, columns];
            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    values[row, column] = GetCellValue(new OdfCellAddress(
                        Math.Min(range.StartAddress.Row, range.EndAddress.Row) + row,
                        Math.Min(
                            range.StartAddress.Column,
                            range.EndAddress.Column) + column));
                }
            }

            return values;
        }

        public string? GetCellFormula(OdfCellAddress address) => null;

        public object GetNamedRangeOrExpressionValue(string name) =>
            OdfFormulaError.Name;
    }

    private sealed class VolatileEvaluationContext(
        DateTime evaluationTimestamp,
        IReadOnlyList<double> randomValues) :
        IEvaluationContext,
        IOdfFormulaVolatileContext
    {
        private int _randomIndex;

        public OdfCellAddress CurrentCell => default;

        public DateTime EvaluationTimestamp { get; } = evaluationTimestamp;

        public object GetCellValue(OdfCellAddress address) => 0d;

        public object[,] GetRangeValues(OdfCellRange range) => new object[0, 0];

        public string? GetCellFormula(OdfCellAddress address) => null;

        public object GetNamedRangeOrExpressionValue(string name) => OdfFormulaError.Name;

        public double NextRandomDouble() => randomValues[_randomIndex++];
    }

    private static void AssertRelative(
        double expected,
        double actual,
        double relativeTolerance)
    {
        double scale = Math.Max(1e-300, Math.Abs(expected));
        double relativeError = Math.Abs(actual - expected) / scale;
        Assert.True(
            relativeError <= relativeTolerance,
            $"Expected {expected:R}, actual {actual:R}, relative error " +
            $"{relativeError:R}, tolerance {relativeTolerance:R}.");
    }

    private sealed class WorkbookEvaluationContext : IOdfFormulaWorkbookContext
    {
        public OdfCellAddress CurrentCell => new(0, 0, "Second");

        public IReadOnlyList<string> SheetNames { get; } = ["First", "Second", "Third"];

        public object GetCellValue(OdfCellAddress address) => 0d;

        public object[,] GetRangeValues(OdfCellRange range) => new object[1, 1];

        public string? GetCellFormula(OdfCellAddress address) => null;

        public object GetNamedRangeOrExpressionValue(string name) => OdfFormulaError.Name;

        public bool TryGetPivotData(
            string dataField,
            OdfCellAddress pivotAnchor,
            IReadOnlyDictionary<string, object> filters,
            out object result)
        {
            bool matches = dataField == "Sales" &&
                pivotAnchor.SheetName == "Second" &&
                filters.TryGetValue("Region", out object? region) &&
                Equals(region, "North");
            result = matches ? 42d : OdfFormulaError.NA;
            return matches;
        }

        public bool TryEvaluateMultipleOperations(
            IReadOnlyList<object> arguments,
            out object result)
        {
            result = 99d;
            return arguments.Count == 3;
        }
    }
}
