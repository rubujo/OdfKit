using System.IO;
using System.Linq;
using System.Text;
using OdfKit.Core;
using OdfKit.Text;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 驗證追蹤修訂的封裝往返與 LibreOffice 風格 ODT 互通。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Interop)]
public class TrackedChangesInteropTests
{
    /// <summary>
    /// 驗證追蹤修訂在 ODT 儲存與重新載入後仍可列舉並接受。
    /// </summary>
    [Fact]
    public void TrackedChangesSurvivesLoadSaveRoundTrip()
    {
        using var stream = new MemoryStream();
        byte[] acceptedBytes;
        string changeId;
        using (TextDocument document = TextDocument.Create())
        {
            document.TrackedChanges = true;
            document.AddParagraph(string.Empty).AddTextRun("往返修訂文字");
            changeId = document.GetTrackedChanges()
                .First(candidate => candidate.Content == "往返修訂文字")
                .RegionId;
            document.SaveToStream(stream);
        }

        stream.Position = 0;
        using (TextDocument loaded = TextDocument.Load(stream))
        {
            OdfTrackedChange change = loaded.GetTrackedChanges()
                .First(candidate => candidate.RegionId == changeId);
            Assert.Equal(OdfChangeType.Insertion, change.ChangeType);
            Assert.Equal("往返修訂文字", change.Content);

            loaded.AcceptAllChanges();
            Assert.Empty(loaded.GetTrackedChanges());

            using var acceptedStream = new MemoryStream();
            loaded.SaveToStream(acceptedStream);
            acceptedBytes = acceptedStream.ToArray();
        }

        using TextDocument finalDocument = TextDocument.Load(new MemoryStream(acceptedBytes));
        Assert.Empty(finalDocument.GetTrackedChanges());
        Assert.Contains("往返修訂文字", ReadContentXml(finalDocument));
        Assert.DoesNotContain("text:tracked-changes", ReadContentXml(finalDocument));
    }

    /// <summary>
    /// 驗證表格儲存格文字插入可記錄追蹤修訂並接受或拒絕。
    /// </summary>
    [Fact]
    public void TableCellInsertionCanBeRecordedAcceptedOrRejected()
    {
        using TextDocument document = TextDocument.Create();
        document.TrackedChanges = true;
        OdfTable table = document.AddTable(1, 1);
        table.GetCell(0, 0).AddParagraph("表格新增文字");

        OdfTrackedChange change = document.GetTrackedChanges().Single();
        Assert.Equal(OdfChangeType.Insertion, change.ChangeType);
        Assert.Equal("表格新增文字", change.Content);

        document.RejectChange(change.RegionId);
        Assert.Empty(document.GetTrackedChanges());
        Assert.DoesNotContain("表格新增文字", ReadContentXml(document));

        table.GetCell(0, 0).AddParagraph("再次新增");
        change = document.GetTrackedChanges().Single();
        document.AcceptChange(change.RegionId);
        Assert.Empty(document.GetTrackedChanges());
        Assert.Contains("再次新增", ReadContentXml(document));
    }

    /// <summary>
    /// 驗證表格儲存格格式變更可記錄追蹤修訂並拒絕還原。
    /// </summary>
    [Fact]
    public void TableCellFormatChangeCanBeRecordedAndRejected()
    {
        using TextDocument document = TextDocument.Create();
        document.TrackedChanges = false;
        OdfTable table = document.AddTable(1, 1);
        table.SetCellStyle(0, 0, "CellStyleA");

        document.TrackedChanges = true;
        table.SetCellStyle(0, 0, "CellStyleB");

        OdfTrackedChange change = document.GetTrackedChanges().Single(c => c.ChangeType == OdfChangeType.FormatChange);
        document.RejectChange(change.RegionId);
        Assert.Empty(document.GetTrackedChanges());
        Assert.Contains("CellStyleA", ReadContentXml(document));
    }

    /// <summary>
    /// 驗證可載入 LibreOffice 風格 ODT 封裝中的表格追蹤修訂並接受。
    /// </summary>
    [Fact]
    public void TrackedChangesLoadsFromLibreOfficeStyleOdtPackage()
    {
        using var stream = new MemoryStream();
        WriteLibreOfficeStyleTrackedChangesOdt(stream);

        stream.Position = 0;
        using TextDocument document = TextDocument.Load(stream);

        OdfTrackedChange change = document.GetTrackedChanges().Single();
        Assert.Equal("Writer", change.Author);
        Assert.Equal(OdfChangeType.Insertion, change.ChangeType);
        Assert.Equal("LO 表格修訂", change.Content);

        document.AcceptAllChanges();

        string contentXml = ReadContentXml(document);
        Assert.Contains("LO 表格修訂", contentXml);
        Assert.Contains("table:table-cell", contentXml);
        Assert.DoesNotContain("text:change-start", contentXml);
        Assert.DoesNotContain("text:tracked-changes", contentXml);
    }

    private static void WriteLibreOfficeStyleTrackedChangesOdt(Stream stream)
    {
        const string contentXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
            "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" " +
            "xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" " +
            "xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\" " +
            "xmlns:dc=\"http://purl.org/dc/elements/1.1/\" office:version=\"1.4\">" +
            "<office:body><office:text>" +
            "<text:tracked-changes>" +
            "<text:changed-region text:id=\"ch1\">" +
            "<text:insertion text:change-author=\"Writer\" text:change-date-and-time=\"2026-06-16T10:00:00Z\">" +
            "<office:change-info><dc:creator>Writer</dc:creator><dc:date>2026-06-16T10:00:00Z</dc:date></office:change-info>" +
            "</text:insertion></text:changed-region></text:tracked-changes>" +
            "<table:table><table:table-row><table:table-cell>" +
            "<text:p><text:change-start text:change-id=\"ch1\"/>LO 表格修訂<text:change-end text:change-id=\"ch1\"/></text:p>" +
            "</table:table-cell></table:table-row></table:table>" +
            "</office:text></office:body></office:document-content>";

        using OdfPackage package = OdfPackage.Create(stream, leaveOpen: true);
        package.SetMimeType("application/vnd.oasis.opendocument.text");
        package.WriteEntry("content.xml", Encoding.UTF8.GetBytes(contentXml), "text/xml");
        package.WriteEntry(
            "styles.xml",
            Encoding.UTF8.GetBytes(
                "<office:document-styles xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" office:version=\"1.4\"><office:styles/></office:document-styles>"),
            "text/xml");
        package.Save();
    }

    private static string ReadContentXml(TextDocument document)
    {
        using var stream = new MemoryStream();
        document.SaveToStream(stream);
        stream.Position = 0;

        using OdfPackage package = OdfPackage.Open(stream, leaveOpen: true);
        using Stream contentStream = package.GetEntryStream("content.xml");
        using var reader = new StreamReader(contentStream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
