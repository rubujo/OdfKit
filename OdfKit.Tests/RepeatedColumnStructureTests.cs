using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Spreadsheet;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 驗證 InsertColumns/DeleteColumns 在表格含 <c>table:table-column</c> 重複定義
/// （<c>number-columns-repeated</c>）時，能以邏輯欄索引而非 DOM 節點數正確運作。
/// </summary>
public class RepeatedColumnStructureTests
{
    [Fact]
    public void DeleteColumns_WithRepeatedColumnDefinition_RemovesCorrectLogicalColumn()
    {
        using SpreadsheetDocument doc = SpreadsheetDocument.Create();
        OdfTableSheet sheet = doc.AddSheet("Sheet1");

        // 手動建立帶 number-columns-repeated="5" 的欄定義（模擬從外部 ODS 載入的常見情況）。
        var repeatedColumn = new OdfNode(OdfNodeType.Element, "table-column", OdfNamespaces.Table, "table");
        repeatedColumn.SetAttribute("number-columns-repeated", OdfNamespaces.Table, "5", "table");
        sheet.TableNode.AppendChild(repeatedColumn);

        sheet.GetCell(0, 0).CellValue = "A0";
        sheet.GetCell(0, 1).CellValue = "A1";
        sheet.GetCell(0, 2).CellValue = "A2";

        sheet.DeleteColumns(1, 1);

        Assert.Equal(4, CountLogicalColumns(sheet.TableNode));
        Assert.Equal("A0", sheet.GetCell(0, 0).CellValue);
        Assert.Equal("A2", sheet.GetCell(0, 1).CellValue);
    }

    [Fact]
    public void DeleteColumns_WithAutoCreatedColumnsBeyondRepeatedRange_RemovesCorrectLogicalColumn()
    {
        using SpreadsheetDocument doc = SpreadsheetDocument.Create();
        OdfTableSheet sheet = doc.AddSheet("Sheet1");

        var repeatedColumn = new OdfNode(OdfNodeType.Element, "table-column", OdfNamespaces.Table, "table");
        repeatedColumn.SetAttribute("number-columns-repeated", OdfNamespaces.Table, "5", "table");
        sheet.TableNode.AppendChild(repeatedColumn);

        // 寫入超出該重複範圍的儲存格，觸發 EnsureColumnDefinitions 自動補建欄位（邏輯欄位 5、6、7）。
        sheet.GetCell(0, 7).CellValue = "Far";
        int columnNodeCountBeforeDelete = CountColumnNodes(sheet.TableNode);

        sheet.DeleteColumns(5, 1);

        Assert.True(CountColumnNodes(sheet.TableNode) < columnNodeCountBeforeDelete);
        Assert.Equal(7, CountLogicalColumns(sheet.TableNode));
    }

    [Fact]
    public void InsertColumns_WithRepeatedColumnDefinition_InsertsAtCorrectLogicalPosition()
    {
        using SpreadsheetDocument doc = SpreadsheetDocument.Create();
        OdfTableSheet sheet = doc.AddSheet("Sheet1");

        var repeatedColumn = new OdfNode(OdfNodeType.Element, "table-column", OdfNamespaces.Table, "table");
        repeatedColumn.SetAttribute("number-columns-repeated", OdfNamespaces.Table, "5", "table");
        sheet.TableNode.AppendChild(repeatedColumn);

        sheet.GetCell(0, 0).CellValue = "A0";
        sheet.GetCell(0, 1).CellValue = "A1";

        sheet.InsertColumns(1, 1);

        Assert.Equal(6, CountLogicalColumns(sheet.TableNode));
        Assert.Equal("A0", sheet.GetCell(0, 0).CellValue);
        Assert.Null(sheet.GetCell(0, 1).CellValue);
        Assert.Equal("A1", sheet.GetCell(0, 2).CellValue);
    }

    private static int CountColumnNodes(OdfNode tableNode)
    {
        int count = 0;
        foreach (OdfNode child in tableNode.Children)
        {
            if (child.LocalName == "table-column" && child.NamespaceUri == OdfNamespaces.Table)
                count++;
        }
        return count;
    }

    private static int CountLogicalColumns(OdfNode tableNode)
    {
        int total = 0;
        foreach (OdfNode child in tableNode.Children)
        {
            if (child.LocalName != "table-column" || child.NamespaceUri != OdfNamespaces.Table)
                break;

            string? repeated = child.GetAttribute("number-columns-repeated", OdfNamespaces.Table);
            total += int.TryParse(repeated, out int count) && count > 0 ? count : 1;
        }
        return total;
    }
}
