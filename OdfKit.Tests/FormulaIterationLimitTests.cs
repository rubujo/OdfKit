using System;
using System.Diagnostics;
using OdfKit.Spreadsheet;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 回歸測試：以引數作為迴圈上限的公式函式不得空轉數十億次。
/// 修正前：<c>CRITBINOM(2000000000;0.5;0.5)</c> 超過 25 秒仍未完成（迴圈內每次還呼叫 O(k) 的 Combination）；
/// <c>MULTINOMIAL(1E9;1E9)</c> 約 4.2 秒；<c>FACT(1E9)</c> 在已溢位後仍迭代 10 億次。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Boundary)]
public sealed class FormulaIterationLimitTests
{
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(3);

    private static (object? Value, TimeSpan Elapsed) Evaluate(string formula)
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.AddSheet("D");
        sheet.Cells["A1"].SetFormula(formula, 0d);
        var stopwatch = Stopwatch.StartNew();
        document.EvaluateFormulas();
        stopwatch.Stop();
        return (sheet.Cells["A1"].CellValue, stopwatch.Elapsed);
    }

    [Theory]
    [InlineData("of:=CRITBINOM(2000000000;0.5;0.5)")]
    [InlineData("of:=MULTINOMIAL(1000000000;1000000000)")]
    [InlineData("of:=FACT(1000000000)")]
    [InlineData("of:=FACTDOUBLE(1000000000)")]
    [InlineData("of:=COMBIN(1000000000;500000000)")]
    [InlineData("of:=HYPGEOMDIST(1000000000;2000000000;1000000000;3000000000;1)")]
    public void LargeArguments_FinishQuicklyWithNumError(string formula)
    {
        (object? value, TimeSpan elapsed) = Evaluate(formula);

        Assert.Equal("#NUM!", value?.ToString());
        Assert.True(elapsed < Budget, $"耗時 {elapsed.TotalSeconds:F1} 秒，超過 {Budget.TotalSeconds:F0} 秒。");
    }

    /// <summary>
    /// 以整個函式表的模糊測試（394 個函式 × 多種極端引數）找出的案例：
    /// 引數（期數、事件數、試驗次數）直接作為迴圈上限，或因 int 計數器溢位而形成無窮迴圈。
    /// </summary>
    [Theory]
    [InlineData("of:=BINOMDIST(1000000000;2147483647;0.5;-1)")]
    [InlineData("of:=BINOMDIST(1000000000;2147483647;0.5;1)")]
    [InlineData("of:=POISSON(1000000000;1000000000;1)")]
    [InlineData("of:=POISSON(1E18;1E18;1E18)")]
    [InlineData("of:=COMBINA(1E18;1E18)")]
    [InlineData("of:=PERMUT(1E18;1E18)")]
    [InlineData("of:=NEGBINOMDIST(1000000000;2147483647;0.5)")]
    [InlineData("of:=DB(2147483647;2147483647;2147483647;2147483647)")]
    [InlineData("of:=DB(1E18;1E18;1E18;1E18)")]
    [InlineData("of:=DDB(2147483647;2147483647;2147483647;2147483647)")]
    [InlineData("of:=DDB(1E18;1E18;1E18;1E18;1E18)")]
    [InlineData("of:=VDB(2147483647;2147483647;2147483647;2147483647;2147483647)")]
    [InlineData("of:=VDB(1E18;1E18;1E18;1E18;1E18)")]
    [InlineData("of:=CUMIPMT(0.05;2147483647;1000;1;2147483647;0)")]
    [InlineData("of:=HYPGEOMDIST(2147483647;2147483647;2147483647;2147483647;1)")]
    public void FuzzFoundCases_FinishQuickly(string formula)
    {
        (_, TimeSpan elapsed) = Evaluate(formula);

        Assert.True(elapsed < Budget, $"耗時 {elapsed.TotalSeconds:F1} 秒，超過 {Budget.TotalSeconds:F0} 秒。");
    }

    [Theory]
    [InlineData("of:=BINOMDIST(3;10;0.5;1)", 0.171875d)]
    [InlineData("of:=POISSON(2;3;1)", 0.42319008112684353d)]
    [InlineData("of:=PERMUT(5;2)", 20d)]
    [InlineData("of:=DB(1000000;100000;6;1;7)", 186083.33333333334d)]
    [InlineData("of:=DDB(2400;300;10;1)", 480d)]
    [InlineData("of:=VDB(2400;300;10;0;1)", 480d)]
    public void ReasonableArguments_OfFuzzFoundFunctions_KeepTheirValues(string formula, double expected)
    {
        (object? value, _) = Evaluate(formula);

        Assert.Equal(expected, Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture), 6);
    }

    [Theory]
    [InlineData("of:=FACT(5)", 120d)]
    [InlineData("of:=FACT(170)", 7.257415615307994E+306)]
    [InlineData("of:=FACTDOUBLE(7)", 105d)]
    [InlineData("of:=COMBIN(5;2)", 10d)]
    [InlineData("of:=COMBIN(52;5)", 2598960d)]
    [InlineData("of:=MULTINOMIAL(2;3;4)", 1260d)]
    [InlineData("of:=CRITBINOM(10;0.5;0.5)", 5d)]
    public void ReasonableArguments_StillComputeExactly(string formula, double expected)
    {
        (object? value, _) = Evaluate(formula);

        Assert.Equal(expected, Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture), 6);
    }
}
