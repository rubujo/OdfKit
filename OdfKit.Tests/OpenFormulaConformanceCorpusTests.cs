using System;
using System.Globalization;
using OdfKit.Formula;
using OdfKit.Spreadsheet;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// Exercises source-authored OpenFormula conformance cases across ODF 1.2 through 1.4.
/// 執行涵蓋 ODF 1.2～1.4 的專案自撰 OpenFormula 一致性案例。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Corpus)]
[Trait(TestCategories.Kind, TestCategories.Compliance)]
public sealed class OpenFormulaConformanceCorpusTests
{
    /// <summary>
    /// Verifies scalar syntax, conversion, error, and function cases grouped by specification version.
    /// 驗證依規範版本分組的純量語法、轉換、錯誤及函式案例。
    /// </summary>
    /// <param name="version">The ODF specification version. / ODF 規範版本。</param>
    /// <param name="formula">The OpenFormula expression. / OpenFormula 運算式。</param>
    /// <param name="expectedKind">The expected value kind. / 預期值種類。</param>
    /// <param name="expectedText">The invariant expected value. / 使用不因文化特性而異格式的預期值。</param>
    [Theory]
    [InlineData("1.2", "of:=1.25E2+2.5e-1", "number", "125.25")]
    [InlineData("1.2", "of:=#N/A+#DIV/0!", "error", "#N/A")]
    [InlineData("1.2", "of:=1=TRUE()", "logical", "false")]
    [InlineData("1.2", "of:=1<>TRUE()", "logical", "true")]
    [InlineData("1.2", "of:=+TRUE()", "logical", "true")]
    [InlineData("1.2", "of:=SUM({1;2|3;4})", "number", "10")]
    [InlineData("1.2", "of:=0^0", "error", "#NUM!")]
    [InlineData("1.2", "of:=VALUE(\" 42.5 \")", "number", "42.5")]
    [InlineData("1.2", "of:==1+1", "number", "2")]
    [InlineData("1.3", "of:=IFNA(#N/A;42)", "number", "42")]
    [InlineData("1.3", "of:=XOR(TRUE();FALSE())", "logical", "true")]
    [InlineData("1.3", "of:=NUMBERVALUE(\"1,234.5\";\".\";\",\")", "number", "1234.5")]
    [InlineData("1.3", "of:=MDETERM({1;2|3;4})", "number", "-2")]
    [InlineData("1.3", "of:=ERROR.TYPE(#REF!)", "number", "4")]
    [InlineData("1.4", "of:=BASE(255;16;4)", "text", "00FF")]
    [InlineData("1.4", "of:=DECIMAL(\"FF\";16)", "number", "255")]
    [InlineData("1.4", "of:=UNICODE(\"𠮷\")", "number", "134071")]
    [InlineData("1.4", "of:=FTEST({1;1};{2;2})", "error", "#DIV/0!")]
    [InlineData("1.4", "of:=ZTEST({1;2;3};2;1)", "number", "0.5")]
    [InlineData("1.4", "of:=BESSELJ(1;0.5)", "error", "#NUM!")]
    [InlineData("1.4", "of:=DDE(\"service\";\"topic\";\"item\")", "error", "#N/A")]
    public void ScalarCorpusMatchesExpectedResult(
        string version,
        string formula,
        string expectedKind,
        string expectedText)
    {
        Assert.Contains(version, new[] { "1.2", "1.3", "1.4" });
        object result = new DefaultFormulaEvaluator().Evaluate(
            formula,
            new EmptyEvaluationContext());

        switch (expectedKind)
        {
            case "number":
                Assert.Equal(
                    double.Parse(expectedText, CultureInfo.InvariantCulture),
                    Assert.IsType<double>(result),
                    precision: 10);
                break;
            case "logical":
                Assert.Equal(
                    bool.Parse(expectedText),
                    Assert.IsType<bool>(result));
                break;
            case "text":
                Assert.Equal(expectedText, Assert.IsType<string>(result));
                break;
            case "error":
                Assert.Equal(
                    expectedText,
                    Assert.IsType<OdfFormulaError>(result).ToErrorString());
                break;
            default:
                throw new InvalidOperationException(expectedKind);
        }
    }

    /// <summary>
    /// Verifies element-wise array evaluation and row or column broadcasting.
    /// 驗證逐元素陣列求值與資料列或資料欄廣播。
    /// </summary>
    [Fact]
    public void ArrayCorpusPreservesRectangularShapes()
    {
        object[,] result = Assert.IsType<object[,]>(
            new DefaultFormulaEvaluator().Evaluate(
                "of:={1;2|3;4}+{10|20}",
                new EmptyEvaluationContext()));

        Assert.Equal(11d, result[0, 0]);
        Assert.Equal(12d, result[0, 1]);
        Assert.Equal(23d, result[1, 0]);
        Assert.Equal(24d, result[1, 1]);
    }

    private sealed class EmptyEvaluationContext : IEvaluationContext
    {
        public OdfCellAddress CurrentCell => default;

        public object GetCellValue(OdfCellAddress address) => 0d;

        public object[,] GetRangeValues(OdfCellRange range) => new object[0, 0];

        public string? GetCellFormula(OdfCellAddress address) => null;

        public object GetNamedRangeOrExpressionValue(string name) =>
            OdfFormulaError.Name;
    }
}
