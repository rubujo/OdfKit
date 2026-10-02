using System;
using System.Diagnostics;
using OdfKit.Spreadsheet;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 回歸測試：試算表的列／欄索引必須在共用的節點存取入口驗證範圍。
/// 修正前：<c>SetColumnVisible(-1, true)</c> 因取得的欄節點為 null 而擲出 NullReferenceException；
/// 過大的索引（如 Int32.MaxValue）會逐一建立數十億個列節點而耗盡記憶體。
/// 此問題由反射式 API 模糊測試發現。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Boundary)]
public sealed class SpreadsheetIndexBoundsTests
{
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(3);

    private static OdfTableSheet CreateSheet(out SpreadsheetDocument document)
    {
        document = SpreadsheetDocument.Create();
        return document.AddSheet("D");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    [InlineData(OdfSpreadsheetLimits.MaxColumnIndex + 1)]
    [InlineData(int.MaxValue)]
    public void SetColumnVisibleWithOutOfRangeIndexThrowsArgumentOutOfRange(int column)
    {
        OdfTableSheet sheet = CreateSheet(out SpreadsheetDocument document);
        using (document)
        {
            var stopwatch = Stopwatch.StartNew();
            Assert.Throws<ArgumentOutOfRangeException>(() => sheet.SetColumnVisible(column, true));
            Assert.True(stopwatch.Elapsed < Budget);
        }
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    [InlineData(OdfSpreadsheetLimits.MaxRowIndex + 1)]
    [InlineData(int.MaxValue)]
    public void SetRowVisibleWithOutOfRangeIndexThrowsArgumentOutOfRange(int row)
    {
        OdfTableSheet sheet = CreateSheet(out SpreadsheetDocument document);
        using (document)
        {
            var stopwatch = Stopwatch.StartNew();
            Assert.Throws<ArgumentOutOfRangeException>(() => sheet.SetRowVisible(row, false));
            Assert.True(stopwatch.Elapsed < Budget);
        }
    }

    [Fact]
    public void VisibilityWithinGridStillWorks()
    {
        OdfTableSheet sheet = CreateSheet(out SpreadsheetDocument document);
        using (document)
        {
            sheet.SetColumnVisible(3, false);
            sheet.SetRowVisible(5, false);

            Assert.False(sheet.IsColumnVisible(3));
            Assert.True(sheet.IsColumnVisible(2));
            Assert.False(sheet.IsRowVisible(5));
            Assert.True(sheet.IsRowVisible(4));
        }
    }
}
