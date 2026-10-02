using System;
using OdfKit.Spreadsheet;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 回歸測試：公式字串結果必須有長度上限，避免以微小公式放大成數 GB 記憶體。
/// 修正前：<c>REPT("ab";2147483647)</c> 因 int 乘法溢位而通過長度檢查，耗用約 8 GB；
/// 16 個 <c>A(n)=A(n-1)&amp;A(n-1)</c> 加倍鏈產生 5.24 億字元（約 2 GB）；
/// 60 字元的 <c>SUBSTITUTE</c> 產生 2.56 億字元。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Boundary)]
public sealed class FormulaStringResultLimitTests
{
    private const int MaxStringResultLength = 1_048_576;

    private static object? Evaluate(string formula)
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.AddSheet("D");
        sheet.Cells["A1"].SetFormula(formula, 0d);
        document.EvaluateFormulas();
        return sheet.Cells["A1"].CellValue;
    }

    [Theory]
    [InlineData("of:=REPT(\"ab\";2147483647)")]
    [InlineData("of:=REPT(\"abc\";1431655766)")]
    [InlineData("of:=REPT(\"ab\";20000)")]
    public void ReptBeyondStringLimitReturnsValueError(string formula)
    {
        Assert.Equal("#VALUE!", Evaluate(formula)?.ToString());
    }

    [Fact]
    public void ReptWithEmptyTextReturnsEmptyWithoutSpinning()
    {
        Assert.Equal(string.Empty, Evaluate("of:=REPT(\"\";2147483647)")?.ToString() ?? string.Empty);
    }

    [Fact]
    public void ReptWithinLimitStillWorks()
    {
        Assert.Equal("ababab", Evaluate("of:=REPT(\"ab\";3)"));
    }

    [Fact]
    public void SubstituteExpandingBeyondLimitReturnsValueError()
    {
        object? value = Evaluate("of:=SUBSTITUTE(REPT(\"a\";16000);\"a\";REPT(\"b\";16000))");

        Assert.Equal("#VALUE!", value?.ToString());
    }

    [Fact]
    public void SubstituteWithinLimitStillWorks()
    {
        Assert.Equal("xbcx", Evaluate("of:=SUBSTITUTE(\"abca\";\"a\";\"x\")"));
        Assert.Equal("aaaa", Evaluate("of:=SUBSTITUTE(\"aa\";\"a\";\"aa\")"));
    }

    [Fact]
    public void ConcatenationDoublingChainIsCappedInsteadOfExhaustingMemory()
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.AddSheet("D");
        sheet.Cells["A1"].SetFormula("of:=REPT(\"a\";16000)", 0d);
        const int cells = 24;
        for (int index = 2; index <= cells; index++)
        {
            sheet.Cells["A" + index].SetFormula($"of:=[.A{index - 1}]&[.A{index - 1}]", 0d);
        }

        document.EvaluateFormulas();

        object? last = sheet.Cells["A" + cells].CellValue;
        Assert.True(last is not string text || text.Length <= MaxStringResultLength, "字串結果超過上限。");
        Assert.Equal("#VALUE!", last?.ToString());
    }

    [Fact]
    public void ConcatenationWithinLimitStillWorks()
    {
        Assert.Equal("ab", Evaluate("of:=\"a\"&\"b\""));
    }
}
