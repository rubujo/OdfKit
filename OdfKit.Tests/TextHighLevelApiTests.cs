using System;
using System.IO;
using System.Linq;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Text;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 鎖定文字文件高階 API 的整合測試。
/// </summary>
public class TextHighLevelApiTests
{
    /// <summary>
    /// 驗證追蹤修訂 API 的新增、讀取與接受拒絕。
    /// </summary>
    [Fact]
    public void TrackedChangesApiWorksCorrectly()
    {
        using var document = TextDocument.Create();
        document.TrackedChanges = true;

        var targetDate = new DateTime(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc);
        var changeId = document.AddTrackedChange("insertion", "CustomAuthor", targetDate);

        // 手動在段落中加入對應修訂識別碼的開始、結束節點與內容文字
        var p = document.AddParagraph("起始");
        var startNode = new OdfNode(OdfNodeType.Element, "change-start", OdfNamespaces.Text, "text");
        startNode.SetAttribute("change-id", OdfNamespaces.Text, changeId, "text");
        var textNode = new OdfNode(OdfNodeType.Text, null, null, null) { TextContent = "新增修訂文字內容" };
        var endNode = new OdfNode(OdfNodeType.Element, "change-end", OdfNamespaces.Text, "text");
        endNode.SetAttribute("change-id", OdfNamespaces.Text, changeId, "text");

        p.Node.AppendChild(startNode);
        p.Node.AppendChild(textNode);
        p.Node.AppendChild(endNode);

        // 1. 測試讀取追蹤修訂
        var changes = document.GetTrackedChanges().ToList();
        var change = changes.FirstOrDefault(c => c.RegionId == changeId);
        Assert.NotNull(change);
        Assert.Equal(OdfChangeType.Insertion, change.ChangeType);
        Assert.Equal("CustomAuthor", change.Author);
        Assert.Equal(targetDate, change.ChangedAt);
        Assert.Equal("新增修訂文字內容", change.Content);

        // 2. 測試安全處理 DateTime.MinValue 與 DateTime.MaxValue 邊界值
        var minChangeId = document.AddTrackedChange("insertion", "MinAuthor", DateTime.MinValue);
        var maxChangeId = document.AddTrackedChange("insertion", "MaxAuthor", DateTime.MaxValue);

        var boundaryChanges = document.GetTrackedChanges().ToList();
        var minChange = boundaryChanges.FirstOrDefault(c => c.RegionId == minChangeId);
        var maxChange = boundaryChanges.FirstOrDefault(c => c.RegionId == maxChangeId);

        Assert.NotNull(minChange);
        Assert.Equal(DateTime.MinValue, minChange.ChangedAt);
        Assert.NotNull(maxChange);
        Assert.Equal(DateTime.MaxValue, maxChange.ChangedAt);

        // 3. 測試接受所有修訂
        document.AcceptAllChanges();
        Assert.Empty(document.GetTrackedChanges());

        var contentXml = SaveAndGetContentXml(document);
        Assert.Contains("新增修訂文字內容", contentXml);
        Assert.DoesNotContain("change-start", contentXml);
        Assert.DoesNotContain("change-end", contentXml);
    }

    /// <summary>
    /// 驗證追蹤修訂中的刪除類型內容提取。
    /// </summary>
    [Fact]
    public void TrackedChangesDeletionContentExtractionWorks()
    {
        using var document = TextDocument.Create();
        document.TrackedChanges = true;

        var targetDate = new DateTime(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc);
        
        // 模擬一個被刪除的段落節點內容
        var deletedPara = new OdfNode(OdfNodeType.Element, "p", OdfNamespaces.Text, "text");
        var deletedText = new OdfNode(OdfNodeType.Text, null, null, null) { TextContent = "被刪除的文字內容" };
        deletedPara.AppendChild(deletedText);

        var changeId = document.AddTrackedChange("deletion", "DeleteAuthor", targetDate, extraContent: deletedPara);

        var changes = document.GetTrackedChanges().ToList();
        var change = changes.FirstOrDefault(c => c.RegionId == changeId);
        Assert.NotNull(change);
        Assert.Equal(OdfChangeType.Deletion, change.ChangeType);
        Assert.Equal("DeleteAuthor", change.Author);
        Assert.Equal("被刪除的文字內容", change.Content);
    }

    /// <summary>
    /// 驗證書籤參照欄位 (text:bookmark-ref) 的插入。
    /// </summary>
    [Fact]
    public void BookmarkReferenceFieldIsInsertedCorrectly()
    {
        using var document = TextDocument.Create();
        var p = document.AddParagraph("書籤測試");
        
        document.AddBookmark(p, "MyTestBookmark");
        document.AddBookmarkReferenceField(p, "MyTestBookmark", "text");

        var contentXml = SaveAndGetContentXml(document);
        Assert.Contains("text:name=\"MyTestBookmark\"", contentXml);
        Assert.Contains("text:ref-name=\"MyTestBookmark\"", contentXml);
        Assert.Contains("text:reference-format=\"text\"", contentXml);
        Assert.Contains("bookmark-ref", contentXml);
    }

    /// <summary>
    /// 驗證主文件子文件參照 (text:section-source) 的插入。
    /// </summary>
    [Fact]
    public void SubDocumentReferenceIsInsertedCorrectly()
    {
        using var document = TextDocument.Create();
        
        document.AddSubDocumentReference("SubSec1", "subdocument_1.odt");

        var contentXml = SaveAndGetContentXml(document);
        Assert.Contains("text:name=\"SubSec1\"", contentXml);
        Assert.Contains("text:section-source", contentXml);
        Assert.Contains("xlink:type=\"simple\"", contentXml);
        Assert.Contains("xlink:href=\"subdocument_1.odt\"", contentXml);
        Assert.Contains("xlink:show=\"embed\"", contentXml);
        Assert.Contains("xlink:actuate=\"onLoad\"", contentXml);
    }

    private static string SaveAndGetContentXml(TextDocument document)
    {
        using var stream = new MemoryStream();
        document.SaveToStream(stream);
        stream.Position = 0;

        using OdfPackage package = OdfPackage.Open(stream, leaveOpen: true);
        using Stream contentStream = package.GetEntryStream("content.xml");
        using var reader = new StreamReader(contentStream);
        return reader.ReadToEnd();
    }
}
