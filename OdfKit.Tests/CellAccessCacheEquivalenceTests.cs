using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Spreadsheet;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 驗證 <c>OdfTableSheet.Internals.CellAccess.cs</c> 的列／儲存格前綴式部分快取，在文件含
/// <c>number-rows-repeated</c>／<c>number-columns-repeated</c> 壓縮節點時，快取路徑
/// （<see cref="OdfTableSheet.TryGetCellNode(int, int)"/>）與全表重掃引擎路徑
/// （<see cref="OdfTableSheetDomAccessEngine.TryGetCellNode(OdfNode, int, int)"/>）對同一文件、
/// 同一座標須回傳相同節點，且隨機（非循序）存取不得因壓縮節點而讀寫到錯誤位置。
/// </summary>
public class CellAccessCacheEquivalenceTests
{
    [Fact]
    public void RandomAccess_AfterBulkInsertRowsCreatesRepeatedBlock_ReadsAndWritesStayCorrect()
    {
        using SpreadsheetDocument doc = SpreadsheetDocument.Create();
        OdfTableSheet sheet = doc.AddSheet("Sheet1");

        // 先寫入位於任何壓縮節點之前的「真實」資料列，驗證前綴快取持續有效。
        sheet.GetCell(0, 0).CellValue = "A0";
        sheet.GetCell(1, 0).CellValue = "A1";
        sheet.GetCell(2, 0).CellValue = "A2";
        sheet.GetCell(3, 0).CellValue = "A3";

        // 常見的「巨量插入壓縮空白列」情境：InsertRows(count > 1) 會以單一
        // number-rows-repeated 節點表示新插入的列，並呼叫 InvalidateAccessCache。
        sheet.InsertRows(2, 500);

        AssertEngineEquivalence(sheet, 0, 0);
        AssertEngineEquivalence(sheet, 1, 0);
        AssertEngineEquivalence(sheet, 502, 0);
        AssertEngineEquivalence(sheet, 503, 0);

        Assert.Equal("A0", sheet.GetCell(0, 0).CellValue);
        Assert.Equal("A1", sheet.GetCell(1, 0).CellValue);
        Assert.Equal("A2", sheet.GetCell(502, 0).CellValue);
        Assert.Equal("A3", sheet.GetCell(503, 0).CellValue);

        // 對壓縮區塊之前、內部（含首尾）、之後的位置進行非循序隨機存取。
        sheet.GetCell(300, 5).CellValue = "inside-300";
        sheet.GetCell(2, 5).CellValue = "inside-2";
        sheet.GetCell(501, 5).CellValue = "inside-501";
        sheet.GetCell(1, 0).CellValue = "A1-rewritten";

        Assert.Equal("inside-300", sheet.GetCell(300, 5).CellValue);
        Assert.Equal("inside-2", sheet.GetCell(2, 5).CellValue);
        Assert.Equal("inside-501", sheet.GetCell(501, 5).CellValue);
        Assert.Equal("A1-rewritten", sheet.GetCell(1, 0).CellValue);

        // 壓縮區塊中未寫入的其他列，不得因鄰近拆分而被污染。
        Assert.Null(sheet.GetCell(100, 5).CellValue);
        Assert.Null(sheet.GetCell(250, 5).CellValue);
        Assert.Null(sheet.GetCell(499, 5).CellValue);

        // 拆分發生後，之前已快取的前綴（真實資料列）與拆分邊界周邊仍須與引擎路徑一致。
        AssertEngineEquivalence(sheet, 0, 0);
        AssertEngineEquivalence(sheet, 1, 0);
        AssertEngineEquivalence(sheet, 2, 5);
        AssertEngineEquivalence(sheet, 300, 5);
        AssertEngineEquivalence(sheet, 501, 5);
        AssertEngineEquivalence(sheet, 502, 0);
        AssertEngineEquivalence(sheet, 503, 0);
        Assert.Equal("A2", sheet.GetCell(502, 0).CellValue);
        Assert.Equal("A3", sheet.GetCell(503, 0).CellValue);
    }

    [Fact]
    public void RandomAccess_WithManuallyInjectedTrailingRepeatedRow_ReadsMatchEngineFallback()
    {
        using SpreadsheetDocument doc = SpreadsheetDocument.Create();
        OdfTableSheet sheet = doc.AddSheet("Sheet1");

        // 模擬從外部工具（例如 LibreOffice）載入既有文件的常見結構：真實資料列之後接著一個
        // 代表結尾空白列填補的巨大 number-rows-repeated 節點。
        sheet.GetCell(0, 0).CellValue = "R0";
        sheet.GetCell(1, 0).CellValue = "R1";
        sheet.GetCell(2, 0).CellValue = "R2";

        var paddingRow = new OdfNode(OdfNodeType.Element, "table-row", OdfNamespaces.Table, "table");
        paddingRow.SetAttribute("number-rows-repeated", OdfNamespaces.Table, "1048573", "table");
        sheet.TableNode.AppendChild(paddingRow);
        sheet.InvalidateAccessCache();

        // 真實資料列應持續享有前綴快取加速，且與引擎路徑一致。
        AssertEngineEquivalence(sheet, 0, 0);
        AssertEngineEquivalence(sheet, 1, 0);
        AssertEngineEquivalence(sheet, 2, 0);
        Assert.Equal("R0", sheet.GetCell(0, 0).CellValue);
        Assert.Equal("R1", sheet.GetCell(1, 0).CellValue);
        Assert.Equal("R2", sheet.GetCell(2, 0).CellValue);

        // 讀取填補區塊中間的列，不應建立任何資料（唯讀查詢不得展開巨型壓縮節點）。
        Assert.Null(sheet.GetCell(500_000, 0).CellValue);
        AssertEngineEquivalence(sheet, 500_000, 0);

        // 對填補區塊內的單一列寫入，僅應影響該邏輯列。
        sheet.GetCell(10, 0).CellValue = "Inserted10";
        Assert.Equal("Inserted10", sheet.GetCell(10, 0).CellValue);
        Assert.Null(sheet.GetCell(9, 0).CellValue);
        Assert.Null(sheet.GetCell(11, 0).CellValue);
        Assert.Equal("R0", sheet.GetCell(0, 0).CellValue);
        Assert.Equal("R2", sheet.GetCell(2, 0).CellValue);
    }

    [Fact]
    public void RandomAccess_WithRepeatedColumnsWithinRow_ReadsMatchEngineFallback()
    {
        using SpreadsheetDocument doc = SpreadsheetDocument.Create();
        OdfTableSheet sheet = doc.AddSheet("Sheet1");

        // 單一列中，前幾格為真實資料，其後由一個 number-columns-repeated 節點壓縮表示大量空白欄。
        sheet.GetCell(0, 0).CellValue = "C0";
        sheet.GetCell(0, 1).CellValue = "C1";
        sheet.GetCell(0, 2).CellValue = "C2";

        var paddingCell = new OdfNode(OdfNodeType.Element, "table-cell", OdfNamespaces.Table, "table");
        paddingCell.SetAttribute("number-columns-repeated", OdfNamespaces.Table, "16381", "table");
        OdfNode? rowNode = sheet.GetCell(0, 0).Node.Parent;
        Assert.NotNull(rowNode);
        rowNode.AppendChild(paddingCell);
        sheet.InvalidateAccessCache();

        AssertEngineEquivalence(sheet, 0, 0);
        AssertEngineEquivalence(sheet, 0, 1);
        AssertEngineEquivalence(sheet, 0, 2);
        Assert.Equal("C0", sheet.GetCell(0, 0).CellValue);
        Assert.Equal("C1", sheet.GetCell(0, 1).CellValue);
        Assert.Equal("C2", sheet.GetCell(0, 2).CellValue);

        Assert.Null(sheet.GetCell(0, 5000).CellValue);
        AssertEngineEquivalence(sheet, 0, 5000);

        sheet.GetCell(0, 10).CellValue = "Mid10";
        Assert.Equal("Mid10", sheet.GetCell(0, 10).CellValue);
        Assert.Null(sheet.GetCell(0, 9).CellValue);
        Assert.Null(sheet.GetCell(0, 11).CellValue);
        Assert.Equal("C0", sheet.GetCell(0, 0).CellValue);
    }

    /// <summary>
    /// 斷言快取路徑（<see cref="OdfTableSheet.TryGetCellNode(int, int)"/>）與全表重掃引擎路徑
    /// （<see cref="OdfTableSheetDomAccessEngine.TryGetCellNode(OdfNode, int, int)"/>）對同一座標
    /// 回傳完全相同的節點參照（或同為 <see langword="null"/>）。
    /// </summary>
    private static void AssertEngineEquivalence(OdfTableSheet sheet, int row, int col)
    {
        OdfNode? cachedResult = sheet.TryGetCellNode(row, col);
        OdfNode? engineResult = OdfTableSheetDomAccessEngine.TryGetCellNode(sheet.TableNode, row, col);
        Assert.Same(engineResult, cachedResult);
    }
}
