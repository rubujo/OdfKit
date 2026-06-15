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

    /// <summary>
    /// 驗證欄位代碼插入 API：頁碼、總頁數、日期、序號參照、腳注、尾注。
    /// </summary>
    [Fact]
    public void FieldCodeApiWorksCorrectly()
    {
        using var document = TextDocument.Create();

        // 1. 頁碼欄位
        var p1 = document.AddParagraph("頁碼：");
        p1.AddPageNumberField();

        // 2. 總頁數欄位
        var p2 = document.AddParagraph("共 ");
        p2.AddPageCountField();

        // 3. 日期欄位
        var p3 = document.AddParagraph("日期：");
        p3.AddDateField();

        // 4. 序號欄位 + 交互參照
        var p4 = document.AddParagraph("圖 ");
        p4.AddSequenceField("Figure", "1");
        var p5 = document.AddParagraph("參見圖 ");
        p5.AddSequenceRefField("Figure");

        // 5. 腳注
        var p6 = document.AddParagraph("有腳注文字");
        p6.AddFootnote("1", "這是腳注內容");

        // 6. 尾注
        var p7 = document.AddParagraph("有尾注文字");
        p7.AddEndnote("i", "這是尾注內容");

        var contentXml = SaveAndGetContentXml(document);
        Assert.Contains("text:page-number", contentXml);
        Assert.Contains("text:page-count", contentXml);
        Assert.Contains("text:date", contentXml);
        Assert.Contains("text:sequence", contentXml);
        Assert.Contains("text:sequence-ref", contentXml);
        Assert.Contains("text:ref-name=\"Figure\"", contentXml);
        Assert.Contains("text:note-class=\"footnote\"", contentXml);
        Assert.Contains("這是腳注內容", contentXml);
        Assert.Contains("text:note-class=\"endnote\"", contentXml);
        Assert.Contains("這是尾注內容", contentXml);
    }

    /// <summary>
    /// 驗證 AddPageStyle / GetPageStyleNames / BreakPageBefore API。
    /// </summary>
    [Fact]
    public void PageStyleAndBreakApiWorksCorrectly()
    {
        using var document = TextDocument.Create();

        // 1. 新增橫向頁面樣式
        var landscape = document.AddPageStyle("Landscape", setup =>
        {
            setup.PageWidth = 29.7;
            setup.PageHeight = 21.0;
        });
        Assert.Equal("Landscape", landscape.Name);

        // 2. GetPageStyleNames 應包含 Standard 和 Landscape
        var names = document.GetPageStyleNames();
        Assert.Contains("Standard", names);
        Assert.Contains("Landscape", names);

        // 3. BreakPageBefore 切換至 Landscape
        var p1 = document.AddParagraph("直向內容");
        var p2 = document.AddParagraph("橫向內容");
        p2.BreakPageBefore("Landscape");

        var stylesXml = SaveAndGetStylesXml(document);
        Assert.Contains("style:name=\"Landscape\"", stylesXml);
        Assert.Contains("style:name=\"MPL_Landscape\"", stylesXml);

        var contentXml = SaveAndGetContentXml(document);
        Assert.Contains("style:master-page-name=\"Landscape\"", contentXml);
        Assert.Contains("fo:break-before=\"page\"", contentXml);

        // 4. BreakPageBefore 不帶 masterPageName — 純分頁
        var p3 = document.AddParagraph("另一段");
        p3.BreakPageBefore();
        var contentXml2 = SaveAndGetContentXml(document);
        Assert.Contains("fo:break-before=\"page\"", contentXml2);
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

    private static string SaveAndGetStylesXml(TextDocument document)
    {
        using var stream = new MemoryStream();
        document.SaveToStream(stream);
        stream.Position = 0;

        using OdfPackage package = OdfPackage.Open(stream, leaveOpen: true);
        using Stream stylesStream = package.GetEntryStream("styles.xml");
        using var reader = new StreamReader(stylesStream);
        return reader.ReadToEnd();
    }
}
