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

    /// <summary>
    /// 驗證列插入與刪除結構修訂可記錄並接受或拒絕。
    /// </summary>
    [Fact]
    public void RowInsertionAndDeletionChangesCanBeRecordedAcceptedOrRejected()
    {
        using var document = SpreadsheetDocument.Create();
        document.TrackedChanges = false;
        OdfTableSheet sheet = document.AddSheet("Data");
        sheet.Cells["A1"].CellValue = "第一列";
        sheet.Cells["A2"].CellValue = "第二列";

        document.TrackedChanges = true;
        sheet.InsertRows(1, 1);
        OdfSpreadsheetTrackedChangeInfo insertion = document.GetTrackedChanges().Single(c => c.Kind == OdfSpreadsheetChangeKind.Insertion);
        Assert.Equal("row", insertion.StructuralType);
        Assert.Equal(1, insertion.StructuralPosition);
        Assert.Equal("Data", insertion.SheetName);

        document.RejectChange(insertion.ChangeId);
        Assert.Empty(document.GetTrackedChanges());
        Assert.Equal("第二列", sheet.Cells["A2"].CellValue);

        sheet.InsertRows(1, 1);
        insertion = document.GetTrackedChanges().Single(c => c.Kind == OdfSpreadsheetChangeKind.Insertion);
        document.AcceptChange(insertion.ChangeId);
        Assert.Empty(document.GetTrackedChanges());

        sheet.DeleteRows(0, 1);
        OdfSpreadsheetTrackedChangeInfo deletion = document.GetTrackedChanges().Single(c => c.Kind == OdfSpreadsheetChangeKind.Deletion);
        Assert.Equal("row", deletion.StructuralType);
        Assert.Equal(0, deletion.StructuralPosition);

        document.RejectChange(deletion.ChangeId);
        Assert.Empty(document.GetTrackedChanges());
        Assert.Equal("第一列", sheet.Cells["A1"].CellValue);
    }

    /// <summary>
    /// 驗證欄插入與刪除結構修訂可記錄並接受或拒絕。
    /// </summary>
    [Fact]
    public void ColumnInsertionAndDeletionChangesCanBeRecordedAcceptedOrRejected()
    {
        using var document = SpreadsheetDocument.Create();
        document.TrackedChanges = false;
        OdfTableSheet sheet = document.AddSheet("Data");
        sheet.Cells["A1"].CellValue = "欄 A";
        sheet.Cells["B1"].CellValue = "欄 B";

        document.TrackedChanges = true;
        sheet.InsertColumns(1, 1);
        OdfSpreadsheetTrackedChangeInfo insertion = document.GetTrackedChanges().Single(c => c.Kind == OdfSpreadsheetChangeKind.Insertion);
        Assert.Equal("column", insertion.StructuralType);
        Assert.Equal(1, insertion.StructuralPosition);
        Assert.Equal("Data", insertion.SheetName);

        document.RejectChange(insertion.ChangeId);
        Assert.Empty(document.GetTrackedChanges());
        Assert.Equal("欄 B", sheet.Cells["B1"].CellValue);

        sheet.InsertColumns(1, 1);
        insertion = document.GetTrackedChanges().Single(c => c.Kind == OdfSpreadsheetChangeKind.Insertion);
        document.AcceptChange(insertion.ChangeId);
        Assert.Empty(document.GetTrackedChanges());

        sheet.DeleteColumns(0, 1);
        OdfSpreadsheetTrackedChangeInfo deletion = document.GetTrackedChanges().Single(c => c.Kind == OdfSpreadsheetChangeKind.Deletion);
        Assert.Equal("column", deletion.StructuralType);
        Assert.Equal(0, deletion.StructuralPosition);

        document.RejectChange(deletion.ChangeId);
        Assert.Empty(document.GetTrackedChanges());
        Assert.Equal("欄 A", sheet.Cells["A1"].CellValue);
    }

    /// <summary>
    /// 驗證公式變更可記錄為 <c>table:cell-content-change</c> 並接受或拒絕。
    /// </summary>
    [Fact]
    public void FormulaChangeCanBeRecordedAcceptedOrRejected()
    {
        using var document = SpreadsheetDocument.Create();
        document.TrackedChanges = false;
        OdfTableSheet sheet = document.AddSheet("Data");
        sheet.Cells["A1"].CellValue = 10d;
        sheet.Cells["B1"].CellValue = 20d;
        sheet.Cells["C1"].Formula = "of:=[.A1]+[.B1]";

        document.TrackedChanges = true;
        sheet.Cells["C1"].Formula = "of:=[.A1]*[.B1]";
        OdfSpreadsheetTrackedChangeInfo change = document.GetTrackedChanges().Single();
        Assert.Equal(OdfSpreadsheetChangeKind.CellContentChange, change.Kind);
        Assert.Equal("of:=[.A1]+[.B1]", change.PreviousFormula);
        Assert.Equal("of:=[.A1]*[.B1]", sheet.Cells["C1"].Formula);

        document.RejectChange(change.ChangeId);
        Assert.Empty(document.GetTrackedChanges());
        Assert.Equal("of:=[.A1]+[.B1]", sheet.Cells["C1"].Formula);

        sheet.Cells["C1"].Formula = "of:=[.A1]*[.B1]";
        change = document.GetTrackedChanges().Single();
        document.AcceptChange(change.ChangeId);
        Assert.Empty(document.GetTrackedChanges());
        Assert.Equal("of:=[.A1]*[.B1]", sheet.Cells["C1"].Formula);
    }

    /// <summary>
    /// 驗證可載入含公式快照的 <c>table:cell-content-change</c> 並讀回修訂摘要。
    /// </summary>
    [Fact]
    public void LoadReadsExistingFormulaCellContentChange()
    {
        using var stream = new MemoryStream();
        WriteFormulaTrackedChangesOds(stream);
        stream.Position = 0;

        using SpreadsheetDocument document = SpreadsheetDocument.Load(stream);
        OdfSpreadsheetTrackedChangeInfo change = document.GetTrackedChanges().Single();

        Assert.Equal("chg-formula", change.ChangeId);
        Assert.Equal("of:=[.A1]+[.B1]", change.PreviousFormula);
        Assert.Equal("Sheet1", change.CellAddress?.SheetName);
        Assert.Equal(0, change.CellAddress?.Row);
        Assert.Equal(2, change.CellAddress?.Column);
    }

    /// <summary>
    /// 驗證儲存格移動可記錄為 <c>table:movement</c> 並接受或拒絕。
    /// </summary>
    [Fact]
    public void CellMovementChangeCanBeRecordedAcceptedOrRejected()
    {
        using var document = SpreadsheetDocument.Create();
        document.TrackedChanges = false;
        OdfTableSheet sheet = document.AddSheet("Data");
        sheet.Cells["A1"].CellValue = "來源";

        document.TrackedChanges = true;
        sheet.MoveCell(0, 0, 1, 1);
        OdfSpreadsheetTrackedChangeInfo movement = document.GetTrackedChanges().Single(c => c.Kind == OdfSpreadsheetChangeKind.Movement);
        Assert.Equal("Data", movement.SheetName);
        Assert.NotNull(movement.SourceAddress);
        Assert.NotNull(movement.TargetAddress);
        Assert.Equal(0, movement.SourceAddress.Value.Row);
        Assert.Equal(0, movement.SourceAddress.Value.Column);
        Assert.Equal(1, movement.TargetAddress.Value.Row);
        Assert.Equal(1, movement.TargetAddress.Value.Column);
        Assert.Equal("來源", sheet.Cells["B2"].CellValue);

        document.RejectChange(movement.ChangeId);
        Assert.Empty(document.GetTrackedChanges());
        Assert.Equal("來源", sheet.Cells["A1"].CellValue);

        sheet.MoveCell(0, 0, 1, 1);
        movement = document.GetTrackedChanges().Single(c => c.Kind == OdfSpreadsheetChangeKind.Movement);
        document.AcceptChange(movement.ChangeId);
        Assert.Empty(document.GetTrackedChanges());
        Assert.Equal("來源", sheet.Cells["B2"].CellValue);
    }

    /// <summary>
    /// 驗證 <see cref="SpreadsheetDocument.RejectAllChanges"/> 可一次拒絕多筆待處理修訂，並還原各儲存格原始內容。
    /// </summary>
    [Fact]
    public void RejectAllChangesRevertsAllPendingChanges()
    {
        using var document = SpreadsheetDocument.Create();
        document.TrackedChanges = false;
        OdfTableSheet sheet = document.AddSheet("Data");
        sheet.Cells["A1"].CellValue = "原始 A1";
        sheet.Cells["A2"].CellValue = "原始 A2";

        document.TrackedChanges = true;
        sheet.Cells["A1"].CellValue = "修改 A1";
        sheet.Cells["A2"].CellValue = "修改 A2";

        Assert.Equal(2, document.GetTrackedChanges().Count);

        document.RejectAllChanges();

        Assert.Empty(document.GetTrackedChanges());
        Assert.Equal("原始 A1", sheet.Cells["A1"].CellValue);
        Assert.Equal("原始 A2", sheet.Cells["A2"].CellValue);
    }

    /// <summary>
    /// 驗證 <see cref="SpreadsheetDocument.RejectAllChanges"/> 僅還原待處理修訂，已接受的修訂維持不變。
    /// </summary>
    [Fact]
    public void RejectAllChangesSkipsAlreadyAcceptedChanges()
    {
        using var document = SpreadsheetDocument.Create();
        document.TrackedChanges = false;
        OdfTableSheet sheet = document.AddSheet("Data");
        sheet.Cells["A1"].CellValue = "原始 A1";
        sheet.Cells["A2"].CellValue = "原始 A2";

        document.TrackedChanges = true;
        sheet.Cells["A1"].CellValue = "已接受 A1";
        sheet.Cells["A2"].CellValue = "待拒絕 A2";

        OdfSpreadsheetTrackedChangeInfo acceptedChange = document.GetTrackedChanges()
            .Single(c => c.CellAddress?.Column == 0 && c.CellAddress?.Row == 0);
        document.AcceptChange(acceptedChange.ChangeId);

        Assert.Single(document.GetTrackedChanges());

        document.RejectAllChanges();

        Assert.Empty(document.GetTrackedChanges());
        Assert.Equal("已接受 A1", sheet.Cells["A1"].CellValue);
        Assert.Equal("原始 A2", sheet.Cells["A2"].CellValue);
    }

    private static void WriteFormulaTrackedChangesOds(Stream stream)
    {
        const string contentXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
            "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" " +
            "xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\" " +
            "xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" " +
            "xmlns:dc=\"http://purl.org/dc/elements/1.1/\" office:version=\"1.4\">" +
            "<office:body><office:spreadsheet>" +
            "<table:tracked-changes table:track-changes=\"true\">" +
            "<table:cell-content-change table:id=\"chg-formula\" table:acceptance-state=\"pending\">" +
            "<table:cell-address table:column=\"2\" table:row=\"0\" table:table=\"0\"/>" +
            "<office:change-info><dc:creator>Tester</dc:creator><dc:date>2026-06-17T10:00:00Z</dc:date></office:change-info>" +
            "<table:previous><table:change-track-table-cell table:formula=\"of:=[.A1]+[.B1]\" office:value-type=\"float\" office:value=\"30\"><text:p>30</text:p></table:change-track-table-cell></table:previous>" +
            "</table:cell-content-change></table:tracked-changes>" +
            "<table:table table:name=\"Sheet1\"><table:table-row><table:table-cell office:value-type=\"float\" office:value=\"30\" table:formula=\"of:=[.A1]*[.B1]\"><text:p>30</text:p></table:table-cell></table:table-row></table:table>" +
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
