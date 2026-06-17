using System.IO;
using System.Linq;
using System.Text;
using OdfKit.Core;
using OdfKit.Spreadsheet;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 鎖定 ODS <c>table:tracked-changes</c> 追蹤修訂 API。
/// </summary>
public class SpreadsheetTrackedChangesTests
{
    /// <summary>
    /// 驗證啟用追蹤修訂後修改儲存格會記錄變更，並可接受或拒絕。
    /// </summary>
    [Fact]
    public void CellContentChangeCanBeRecordedAcceptedOrRejected()
    {
        using var document = SpreadsheetDocument.Create();
        document.TrackedChanges = false;
        OdfTableSheet sheet = document.AddSheet("Data");
        sheet.Cells["A1"].CellValue = "原始值";

        document.TrackedChanges = true;
        sheet.Cells["A1"].CellValue = "新值";

        OdfSpreadsheetTrackedChangeInfo change = document.GetTrackedChanges().Single();
        Assert.Equal(OdfSpreadsheetChangeKind.CellContentChange, change.Kind);
        Assert.Equal("原始值", change.PreviousContent);
        Assert.Equal("pending", change.AcceptanceState);
        Assert.NotNull(change.CellAddress);
        Assert.Equal("Data", change.CellAddress.Value.SheetName);

        document.RejectChange(change.ChangeId);
        Assert.Empty(document.GetTrackedChanges());
        Assert.Equal("原始值", sheet.Cells["A1"].CellValue);

        document.TrackedChanges = true;
        sheet.Cells["A1"].CellValue = "再次變更";
        change = document.GetTrackedChanges().Single();
        document.AcceptChange(change.ChangeId);

        Assert.Empty(document.GetTrackedChanges());
        Assert.Equal("再次變更", sheet.Cells["A1"].CellValue);
    }

    /// <summary>
    /// 驗證可載入含 <c>table:cell-content-change</c> 的 ODS 並讀回修訂摘要。
    /// </summary>
    [Fact]
    public void LoadReadsExistingCellContentChange()
    {
        using var stream = new MemoryStream();
        WriteTrackedChangesOds(stream);
        stream.Position = 0;

        using SpreadsheetDocument document = SpreadsheetDocument.Load(stream);
        OdfSpreadsheetTrackedChangeInfo change = document.GetTrackedChanges().Single();

        Assert.Equal("chg1", change.ChangeId);
        Assert.Equal(OdfSpreadsheetChangeKind.CellContentChange, change.Kind);
        Assert.Equal("舊資料", change.PreviousContent);
        Assert.Equal("Sheet1", change.CellAddress?.SheetName);
        Assert.Equal(0, change.CellAddress?.Row);
        Assert.Equal(0, change.CellAddress?.Column);
    }

    private static void WriteTrackedChangesOds(Stream stream)
    {
        const string contentXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
            "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" " +
            "xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\" " +
            "xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" " +
            "xmlns:dc=\"http://purl.org/dc/elements/1.1/\" office:version=\"1.4\">" +
            "<office:body><office:spreadsheet>" +
            "<table:tracked-changes table:track-changes=\"true\">" +
            "<table:cell-content-change table:id=\"chg1\" table:acceptance-state=\"pending\">" +
            "<table:cell-address table:column=\"0\" table:row=\"0\" table:table=\"0\"/>" +
            "<office:change-info><dc:creator>Tester</dc:creator><dc:date>2026-06-17T10:00:00Z</dc:date></office:change-info>" +
            "<table:previous><table:change-track-table-cell office:value-type=\"string\"><text:p>舊資料</text:p></table:change-track-table-cell></table:previous>" +
            "</table:cell-content-change></table:tracked-changes>" +
            "<table:table table:name=\"Sheet1\"><table:table-row><table:table-cell office:value-type=\"string\"><text:p>新資料</text:p></table:table-cell></table:table-row></table:table>" +
            "</office:spreadsheet></office:body></office:document-content>";

        using OdfPackage package = OdfPackage.Create(stream, leaveOpen: true);
        package.SetMimeType("application/vnd.oasis.opendocument.spreadsheet");
        package.WriteEntry("content.xml", Encoding.UTF8.GetBytes(contentXml), "text/xml");
        package.WriteEntry(
            "styles.xml",
            Encoding.UTF8.GetBytes(
                "<office:document-styles xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" office:version=\"1.4\"><office:styles/></office:document-styles>"),
            "text/xml");
        package.Save();
    }
}
