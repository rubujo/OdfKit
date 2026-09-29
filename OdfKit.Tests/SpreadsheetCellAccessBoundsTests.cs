using System;
using System.Diagnostics;
using OdfKit.Spreadsheet;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 回歸測試：儲存格存取的欄索引必須在共用的節點存取入口驗證範圍。
/// 修正前：<c>row.Cells[int.MaxValue]</c> 會逐一建立數十億個儲存格節點而卡死，
/// 負數索引則使補格迴圈不執行而回傳 null（後續 NullReferenceException）。
/// 此問題由反射式 API 模糊測試發現。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Boundary)]
public sealed class SpreadsheetCellAccessBoundsTests
{
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(3);

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    [InlineData(OdfSpreadsheetLimits.MaxColumnIndex + 1)]
    [InlineData(int.MaxValue)]
    public void GetCell_WithOutOfRangeColumn_ThrowsArgumentOutOfRange(int column)
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.AddSheet("D");

        var stopwatch = Stopwatch.StartNew();
        Assert.Throws<ArgumentOutOfRangeException>(() => sheet.GetCell(0, column));
        Assert.True(stopwatch.Elapsed < Budget);
    }

    [Fact]
    public void GetCell_AtGridEdges_StillWorks()
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.AddSheet("D");

        sheet.GetCell(0, OdfSpreadsheetLimits.MaxColumnIndex).CellValue = "right-edge";
        sheet.GetCell(OdfSpreadsheetLimits.MaxRowIndex, 0).CellValue = "bottom-edge";

        Assert.Equal("right-edge", sheet.GetCell(0, OdfSpreadsheetLimits.MaxColumnIndex).CellValue);
        Assert.Equal("bottom-edge", sheet.GetCell(OdfSpreadsheetLimits.MaxRowIndex, 0).CellValue);
    }
}
