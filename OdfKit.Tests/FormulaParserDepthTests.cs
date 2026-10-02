using System;
using OdfKit.Formula;
using OdfKit.Spreadsheet;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 回歸測試：過深的公式巢狀（超長前置運算子鏈、括號、函式引數、內嵌陣列）必須以例外拒絕，
/// 不得造成無法攔截的堆疊溢位（會使整個處理程序崩潰）。
/// 修正前，約 10,000 字元的 <c>-</c> 鏈或約 1,000 層括號即可使處理程序崩潰。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Boundary)]
public sealed class FormulaParserDepthTests
{
    private static void Parse(string formula)
    {
        var parser = new FormulaParser(formula);
        parser.Parse();
    }

    [Theory]
    [InlineData(20_000)]
    [InlineData(1_000_000)]
    public void UnaryOperatorChainBeyondLimitIsRejected(int length)
    {
        string formula = new string('-', length) + "1";

        Assert.Throws<InvalidOperationException>(() => Parse(formula));
    }

    [Fact]
    public void MixedUnaryOperatorChainBeyondLimitIsRejected()
    {
        string formula = string.Concat(System.Linq.Enumerable.Repeat("-+", 5_000)) + "1";

        Assert.Throws<InvalidOperationException>(() => Parse(formula));
    }

    [Theory]
    [InlineData(2_000)]
    [InlineData(200_000)]
    public void NestedParenthesesBeyondLimitAreRejected(int depth)
    {
        string formula = new string('(', depth) + "1" + new string(')', depth);

        Assert.Throws<InvalidOperationException>(() => Parse(formula));
    }

    [Fact]
    public void NestedFunctionCallsBeyondLimitAreRejected()
    {
        string formula = string.Concat(System.Linq.Enumerable.Repeat("ABS(", 5_000)) + "1" + new string(')', 5_000);

        Assert.Throws<InvalidOperationException>(() => Parse(formula));
    }

    [Fact]
    public void NestedInlineArraysBeyondLimitAreRejected()
    {
        string formula = new string('{', 5_000) + "1" + new string('}', 5_000);

        Assert.Throws<InvalidOperationException>(() => Parse(formula));
    }

    /// <summary>
    /// 左結合運算子鏈（<c>1+1+…</c>、<c>1%%%…</c> 等）會產生與運算子個數等高的 AST；求值以遞迴走訪，
    /// 16,000 項的 <c>1+1+…</c> 在堆疊 ≤1 MB 的執行緒上會使整個處理程序崩潰。
    /// </summary>
    [Theory]
    [InlineData("+1", 5_000)]
    [InlineData("-1", 5_000)]
    [InlineData("*1", 5_000)]
    [InlineData("/1", 5_000)]
    [InlineData("^1", 5_000)]
    [InlineData("&1", 5_000)]
    [InlineData("=1", 5_000)]
    [InlineData("<>1", 5_000)]
    public void OperatorChainBeyondLimitIsRejected(string unit, int count)
    {
        string formula = "1" + string.Concat(System.Linq.Enumerable.Repeat(unit, count));

        Assert.Throws<InvalidOperationException>(() => Parse(formula));
    }

    [Fact]
    public void PercentChainBeyondLimitIsRejected()
    {
        Assert.Throws<InvalidOperationException>(() => Parse("1" + new string('%', 5_000)));
    }

    [Fact]
    public void MixedOperatorChainBeyondLimitIsRejected()
    {
        string formula = "1" + string.Concat(System.Linq.Enumerable.Repeat("+1*2-3", 1_500));

        Assert.Throws<InvalidOperationException>(() => Parse(formula));
    }

    [Fact]
    public void OperatorChainWithinLimitStillParses()
    {
        Parse("1" + string.Concat(System.Linq.Enumerable.Repeat("+1", 4_000)));
        Parse("1" + new string('%', 4_000));
        Parse("SUM(" + string.Join(
            ";",
            System.Linq.Enumerable.Select(
                System.Linq.Enumerable.Range(0, 3_000),
                i => i.ToString(System.Globalization.CultureInfo.InvariantCulture))) + ")");
    }

    [Fact]
    public void ReasonableNestingStillParses()
    {
        Parse(new string('(', 200) + "1" + new string(')', 200));
        Parse(string.Concat(System.Linq.Enumerable.Repeat("ABS(", 100)) + "1" + new string(')', 100));
        Parse(new string('-', 200) + "1");
        Parse("-+-1");
        Parse("-2^2");
    }

    [Fact]
    public void EvaluateFormulasWithOverlyDeepUnaryChainFailsWithoutCrashing()
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.AddSheet("Data");
        sheet.Cells["A1"].SetFormula("of:=" + new string('-', 20_000) + "1", 0d);

        Assert.ThrowsAny<Exception>(() => document.EvaluateFormulas());
    }

    [Theory]
    [InlineData(256)]
    [InlineData(128)]
    public void MaxOperatorChainOnSmallStackThreadFailsCatchablyInsteadOfOverflowing(int stackKb)
    {
        // 修正前，接近 4,096 運算子上限的連鎖公式在 256 KB 堆疊上會使處理程序崩潰（無法攔截）。
        string formula = "1" + string.Concat(System.Linq.Enumerable.Repeat("+1", 4_000));
        Exception? failure = null;
        bool completed = false;

        var thread = new System.Threading.Thread(
            () =>
            {
                try
                {
                    var parser = new FormulaParser(formula);
                    Formula.AST.AstNode ast = parser.Parse();
                    _ = ast.Serialize();
                    completed = true;
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
            },
            stackKb * 1024);
        thread.Start();
        thread.Join();

        // 能跑完或以可攔截的例外結束皆可；關鍵是測試處理程序仍存活。
        Assert.True(completed || failure is InsufficientExecutionStackException or InvalidOperationException, failure?.ToString());
    }

    [Fact]
    public void EvaluateFormulasWithReasonableUnaryChainProducesTheExpectedSign()
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.AddSheet("Data");
        sheet.Cells["A1"].SetFormula("of:=" + new string('-', 5) + "1", 0d);
        sheet.Cells["A2"].SetFormula("of:=" + new string('-', 6) + "1", 0d);

        document.EvaluateFormulas();

        Assert.Equal(-1d, sheet.Cells["A1"].CellValue);
        Assert.Equal(1d, sheet.Cells["A2"].CellValue);
    }
}
