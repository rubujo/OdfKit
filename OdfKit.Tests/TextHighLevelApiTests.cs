using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;
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
        var textNode = new OdfNode(OdfNodeType.Text, null!, null!, null!) { TextContent = "新增修訂文字內容" };
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
    /// 驗證表格儲存格內的追蹤修訂可讀取並接受。
    /// </summary>
    [Fact]
    public void TrackedChangesWorkInsideTableCells()
    {
        using var document = TextDocument.Create();
        document.TrackedChanges = true;

        OdfTable table = document.AddTable(1, 1);
        OdfParagraph cellParagraph = table.GetCell(0, 0).AddParagraph(string.Empty);
        cellParagraph.AddTextRun("儲存格修訂文字");

        var changes = document.GetTrackedChanges().ToList();
        Assert.Single(changes);
        Assert.Equal(OdfChangeType.Insertion, changes[0].ChangeType);
        Assert.Equal("儲存格修訂文字", changes[0].Content);

        document.AcceptAllChanges();

        Assert.Empty(document.GetTrackedChanges());
        string contentXml = SaveAndGetContentXml(document);
        Assert.Contains("儲存格修訂文字", contentXml);
        Assert.Contains("table:table-cell", contentXml);
        Assert.DoesNotContain("change-start", contentXml);
        Assert.DoesNotContain("change-end", contentXml);
    }

    /// <summary>
    /// 驗證表格儲存格內的刪除修訂可拒絕並還原內容。
    /// </summary>
    [Fact]
    public void TrackedChangesTableCellDeletionCanBeRejected()
    {
        using var document = TextDocument.Create();
        document.TrackedChanges = false;

        OdfTable table = document.AddTable(1, 1);
        OdfParagraph cellParagraph = table.GetCell(0, 0).AddParagraph("儲存格刪除測試");

        document.TrackedChanges = true;
        document.DeleteNode(cellParagraph.Node);

        OdfTrackedChange deletion = document.GetTrackedChanges().Single(c => c.ChangeType == OdfChangeType.Deletion);
        Assert.Equal("儲存格刪除測試", deletion.Content);

        document.RejectChange(deletion.RegionId);

        Assert.Empty(document.GetTrackedChanges());
        string contentXml = SaveAndGetContentXml(document);
        Assert.Contains("儲存格刪除測試", contentXml);
        Assert.DoesNotContain("change-start", contentXml);
        Assert.DoesNotContain("change-end", contentXml);
    }

    /// <summary>
    /// 驗證表格儲存格內的刪除修訂可接受並清除標記。
    /// </summary>
    [Fact]
    public void TrackedChangesTableCellDeletionCanBeAccepted()
    {
        using var document = TextDocument.Create();
        document.TrackedChanges = false;

        OdfTable table = document.AddTable(1, 1);
        OdfParagraph cellParagraph = table.GetCell(0, 0).AddParagraph("接受刪除");

        document.TrackedChanges = true;
        document.DeleteNode(cellParagraph.Node);

        string changeId = document.GetTrackedChanges().Single(c => c.ChangeType == OdfChangeType.Deletion).RegionId;
        document.AcceptChange(changeId);

        Assert.Empty(document.GetTrackedChanges());
        string contentXml = SaveAndGetContentXml(document);
        Assert.DoesNotContain("接受刪除", contentXml);
        Assert.DoesNotContain("change-start", contentXml);
    }

    /// <summary>
    /// 驗證格式變更修訂可讀取，並可接受或拒絕以還原樣式。
    /// </summary>
    [Fact]
    public void FormatChangeTrackedRevisionCanBeAcceptedOrRejected()
    {
        using var document = TextDocument.Create();
        document.TrackedChanges = false;

        OdfParagraph paragraph = document.AddParagraph("格式修訂測試");
        paragraph.StyleName = "OriginalStyle";

        document.TrackedChanges = true;
        paragraph.StyleName = "NewStyle";

        OdfTrackedChange formatChange = document.GetTrackedChanges().Single(c => c.ChangeType == OdfChangeType.FormatChange);
        Assert.Equal("格式修訂測試", formatChange.Content);

        document.RejectChange(formatChange.RegionId);
        Assert.Equal("OriginalStyle", paragraph.StyleName);
        Assert.Empty(document.GetTrackedChanges());

        document.TrackedChanges = true;
        paragraph.StyleName = "AcceptedStyle";

        formatChange = document.GetTrackedChanges().Single(c => c.ChangeType == OdfChangeType.FormatChange);
        document.AcceptChange(formatChange.RegionId);

        Assert.Equal("AcceptedStyle", paragraph.StyleName);
        Assert.Empty(document.GetTrackedChanges());

        string contentXml = SaveAndGetContentXml(document);
        Assert.DoesNotContain("format-change", contentXml);
        Assert.DoesNotContain("change-start", contentXml);
    }

    /// <summary>
    /// 驗證可讀取 LibreOffice 慣用之內嵌修訂中繼資料屬性。
    /// </summary>
    [Fact]
    public void TrackedChangesReadsLibreOfficeStyleInlineMetadata()
    {
        using var document = TextDocument.Create();
        var targetDate = new DateTime(2026, 6, 16, 8, 30, 0, DateTimeKind.Utc);

        var tcNode = new OdfNode(OdfNodeType.Element, "tracked-changes", OdfNamespaces.Text, "text");
        if (document.BodyTextRoot.Children.Count > 0)
        {
            document.BodyTextRoot.InsertBefore(tcNode, document.BodyTextRoot.Children[0]);
        }
        else
        {
            document.BodyTextRoot.AppendChild(tcNode);
        }

        var changedRegion = new OdfNode(OdfNodeType.Element, "changed-region", OdfNamespaces.Text, "text");
        changedRegion.SetAttribute("id", OdfNamespaces.Text, "lo_ct_1", "text");
        var insertion = new OdfNode(OdfNodeType.Element, "insertion", OdfNamespaces.Text, "text");
        insertion.SetAttribute("change-author", OdfNamespaces.Text, "LibreWriter", "text");
        insertion.SetAttribute("change-date-and-time", OdfNamespaces.Text, "2026-06-16T08:30:00Z", "text");
        changedRegion.AppendChild(insertion);
        tcNode.AppendChild(changedRegion);

        var paragraph = document.AddParagraph(string.Empty);
        var startNode = new OdfNode(OdfNodeType.Element, "change-start", OdfNamespaces.Text, "text");
        startNode.SetAttribute("change-id", OdfNamespaces.Text, "lo_ct_1", "text");
        var textNode = new OdfNode(OdfNodeType.Text, null!, null!, null!) { TextContent = "LO 修訂文字" };
        var endNode = new OdfNode(OdfNodeType.Element, "change-end", OdfNamespaces.Text, "text");
        endNode.SetAttribute("change-id", OdfNamespaces.Text, "lo_ct_1", "text");
        paragraph.Node.AppendChild(startNode);
        paragraph.Node.AppendChild(textNode);
        paragraph.Node.AppendChild(endNode);

        OdfTrackedChange change = document.GetTrackedChanges().Single();
        Assert.Equal("LibreWriter", change.Author);
        Assert.Equal(targetDate, change.ChangedAt);
        Assert.Equal("LO 修訂文字", change.Content);
    }

    /// <summary>
    /// 驗證寫入的修訂中繼資料同時包含內嵌屬性與 office:change-info。
    /// </summary>
    [Fact]
    public void TrackedChangesWriteDualMetadataForLibreOfficeInterop()
    {
        using var document = TextDocument.Create();
        document.TrackedChanges = true;

        OdfParagraph paragraph = document.AddParagraph(string.Empty);
        paragraph.AddTextRun("雙重中繼資料");

        string contentXml = SaveAndGetContentXml(document);
        Assert.Contains("text:change-author=\"Author\"", contentXml);
        Assert.Contains("text:change-date-and-time=", contentXml);
        Assert.Contains("office:change-info", contentXml);
        Assert.Contains("dc:creator", contentXml);
        Assert.Contains("dc:date", contentXml);
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
        var deletedText = new OdfNode(OdfNodeType.Text, null!, null!, null!) { TextContent = "被刪除的文字內容" };
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

        IReadOnlyList<OdfBookmarkInfo> bookmarks = document.GetBookmarks();
        OdfBookmarkInfo bookmark = Assert.Single(bookmarks);
        Assert.Equal("MyTestBookmark", bookmark.Name);
        Assert.Equal(OdfBookmarkKind.Inline, bookmark.Kind);
    }

    /// <summary>
    /// 驗證 <see cref="TextDocument.GetHyperlinks"/> 與 <see cref="TextDocument.GetReferenceMarks"/> 可讀回內嵌標記。
    /// </summary>
    [Fact]
    public void GetHyperlinksAndReferenceMarks_RoundTripsAfterAdd()
    {
        using var document = TextDocument.Create();
        OdfParagraph paragraph = document.AddParagraph("連結測試");
        paragraph.AddReferenceMark("RefA");
        paragraph.AddHyperlink("https://example.com", "範例連結");

        OdfReferenceMarkInfo referenceMark = Assert.Single(document.GetReferenceMarks());
        Assert.Equal("RefA", referenceMark.Name);

        OdfHyperlinkInfo hyperlink = Assert.Single(document.GetHyperlinks());
        Assert.Equal("https://example.com", hyperlink.Url);
        Assert.Equal("範例連結", hyperlink.DisplayText);
    }

    /// <summary>
    /// 驗證 <see cref="TextDocument.GetCommentInfos"/> 可讀回註解摘要。
    /// </summary>
    [Fact]
    public void GetCommentInfos_RoundTripsAfterAdd()
    {
        using var document = TextDocument.Create();
        OdfParagraph paragraph = document.AddParagraph("註解段落");
        document.AddComment(
            paragraph,
            new OdfComment("審閱者", "請修訂措辭", new DateTime(2026, 6, 17, 8, 0, 0, DateTimeKind.Utc), "comment-1"));

        OdfCommentInfo info = Assert.Single(document.GetCommentInfos());
        Assert.Equal("comment-1", info.Name);
        Assert.Equal("審閱者", info.Author);
        Assert.Equal("請修訂措辭", info.Text);
        Assert.Equal(0, info.ReplyCount);
    }

    /// <summary>
    /// 驗證 <see cref="OdfTable.InsertRows"/> 與 <see cref="OdfTable.DeleteColumns"/> 在啟用追蹤修訂時會記錄結構變更。
    /// </summary>
    [Fact]
    public void TableStructureChanges_RecordedWhenTrackedChangesEnabled()
    {
        using var document = TextDocument.Create();
        document.TrackedChanges = true;
        OdfTable table = document.AddTable(2, 2);

        table.InsertRows(1, 2);
        table.DeleteColumns(0, 1);

        IReadOnlyList<OdfTableStructuralChangeInfo> changes = document.GetTableStructuralChanges();
        Assert.Equal(2, changes.Count);

        OdfTableStructuralChangeInfo rowInsertion = changes.First(c => c.Kind == OdfTableStructuralChangeKind.Insertion);
        Assert.Equal("row", rowInsertion.StructuralType);
        Assert.Equal(1, rowInsertion.Position);
        Assert.Equal(2, rowInsertion.Count);
        Assert.Equal("Author", rowInsertion.Author);

        OdfTableStructuralChangeInfo columnDeletion = changes.First(c => c.Kind == OdfTableStructuralChangeKind.Deletion);
        Assert.Equal("column", columnDeletion.StructuralType);
        Assert.Equal(0, columnDeletion.Position);
    }

    /// <summary>
    /// 驗證 <see cref="TextDocument.GetTableStructuralChanges"/> 可讀回表格結構修訂。
    /// </summary>
    [Fact]
    public void GetTableStructuralChanges_ReadsInsertionAndDeletion()
    {
        using var document = TextDocument.Create();
        OdfTable table = document.AddTable(2, 2);

        var insertion = new OdfNode(OdfNodeType.Element, "insertion", OdfNamespaces.Table, "table");
        insertion.SetAttribute("id", OdfNamespaces.Table, "tc_row_ins", "table");
        insertion.SetAttribute("type", OdfNamespaces.Table, "row", "table");
        insertion.SetAttribute("position", OdfNamespaces.Table, "1", "table");
        insertion.SetAttribute("count", OdfNamespaces.Table, "2", "table");
        OdfTrackedChangeMetadataReader.Write(insertion, "Editor", new DateTime(2026, 6, 17, 9, 0, 0, DateTimeKind.Utc));
        table.Node.AppendChild(insertion);

        var deletion = new OdfNode(OdfNodeType.Element, "deletion", OdfNamespaces.Table, "table");
        deletion.SetAttribute("id", OdfNamespaces.Table, "tc_col_del", "table");
        deletion.SetAttribute("type", OdfNamespaces.Table, "column", "table");
        deletion.SetAttribute("position", OdfNamespaces.Table, "0", "table");
        deletion.SetAttribute("acceptance-state", OdfNamespaces.Table, "pending", "table");
        OdfTrackedChangeMetadataReader.Write(deletion, "Reviewer", new DateTime(2026, 6, 17, 10, 0, 0, DateTimeKind.Utc));
        table.Node.AppendChild(deletion);

        Assert.Equal(2, document.GetTableStructuralChanges().Count);
        OdfTableStructuralChangeInfo rowInsertion = document.GetTableStructuralChanges()
            .First(c => c.ChangeId == "tc_row_ins");
        Assert.Equal(OdfTableStructuralChangeKind.Insertion, rowInsertion.Kind);
        Assert.Equal("row", rowInsertion.StructuralType);
        Assert.Equal(1, rowInsertion.Position);
        Assert.Equal(2, rowInsertion.Count);
        Assert.Equal("Editor", rowInsertion.Author);

        OdfTableStructuralChangeInfo columnDeletion = document.GetTableStructuralChanges()
            .First(c => c.ChangeId == "tc_col_del");
        Assert.Equal(OdfTableStructuralChangeKind.Deletion, columnDeletion.Kind);
        Assert.Equal("column", columnDeletion.StructuralType);
    }

    /// <summary>
    /// 驗證 <see cref="TextDocument.UpdateIndexes"/> 可重新產生字母索引內容。
    /// </summary>
    [Fact]
    public void UpdateIndexes_RegeneratesAlphabeticalIndexBody()
    {
        using var document = TextDocument.Create();
        OdfParagraph paragraph = document.AddParagraph("索引條目");
        document.AddAlphabeticalIndexMark(paragraph, "關鍵字", "K", "1");
        OdfAlphabeticalIndex index = document.AddAlphabeticalIndex("術語索引");

        document.UpdateIndexes();

        OdfNode? body = index.BodyNode;
        Assert.NotNull(body);
        Assert.Contains(body.Children, child => child.TextContent?.Contains("關鍵字") == true);
    }

    /// <summary>
    /// 驗證 <see cref="TextDocument.GetIndexInfos"/> 與 <see cref="TextDocument.GetIndexMarks"/> 可讀回索引與標記。
    /// </summary>
    [Fact]
    public void GetIndexInfosAndMarks_RoundTripsAfterAdd()
    {
        using var document = TextDocument.Create();
        OdfParagraph paragraph = document.AddParagraph("索引條目");
        document.AddAlphabeticalIndexMark(paragraph, "關鍵字", "K", "1");
        document.AddAlphabeticalIndex("術語索引");
        document.AddTableOfContents("文件目錄", 2);

        Assert.Equal(2, document.GetIndexInfos().Count);
        Assert.Contains(document.GetIndexInfos(), i => i.Kind == OdfIndexKind.AlphabeticalIndex && i.Name == "術語索引");
        Assert.Contains(document.GetIndexInfos(), i => i.Kind == OdfIndexKind.TableOfContents);

        OdfDocumentIndexMarkInfo mark = Assert.Single(document.GetIndexMarks());
        Assert.Equal(OdfIndexMarkKind.Alphabetical, mark.Kind);
        Assert.Equal("關鍵字", mark.Term);
        Assert.Equal("K", mark.Key1);
        Assert.Equal("1", mark.Key2);
    }

    /// <summary>
    /// 驗證 <see cref="TextDocument.GetPageSetups"/> 可讀回頁首頁尾設定。
    /// </summary>
    [Fact]
    public void GetPageSetups_RoundTripsAfterConfigure()
    {
        using var document = TextDocument.Create();
        OdfPageSetup setup = document.GetDefaultPageSetup();
        setup.HeaderText = "文件頁首";
        setup.FooterText = "文件頁尾";

        OdfPageSetupInfo pageSetup = Assert.Single(document.GetPageSetups());
        Assert.Equal("Standard", pageSetup.Name);
        Assert.Equal("文件頁首", pageSetup.HeaderText);
        Assert.Equal("文件頁尾", pageSetup.FooterText);
    }

    /// <summary>
    /// 驗證 <see cref="TextDocument.GetPageSetup"/> 可依名稱取得主頁面樣式設定。
    /// </summary>
    [Fact]
    public void GetPageSetup_ReturnsNamedMasterPage()
    {
        using var document = TextDocument.Create();
        document.AddPageStyle("Landscape");

        OdfPageSetup landscape = document.GetPageSetup("Landscape");
        Assert.Equal("Landscape", landscape.MasterPageName);
        landscape.HeaderText = "橫向頁首";

        OdfPageSetupInfo info = Assert.Single(document.GetPageSetups(), s => s.Name == "Landscape");
        Assert.Equal("橫向頁首", info.HeaderText);
    }

    /// <summary>
    /// 驗證頁首頁尾欄位混排與首頁專用區域寫入。
    /// </summary>
    [Fact]
    public void HeaderFooterAdvancedEditing_WritesFieldsAndFirstPageRegions()
    {
        using var document = TextDocument.Create();
        OdfPageSetup setup = document.GetDefaultPageSetup();

        OdfParagraph footerParagraph = setup.Footer.GetOrCreateParagraph();
        footerParagraph.TextContent = "第 ";
        setup.Footer.AddPageNumberField();
        footerParagraph.AddTextRun(" 頁，共 ");
        setup.Footer.AddPageCountField();
        footerParagraph.AddTextRun(" 頁");

        setup.HeaderFirst.Text = "首頁專用頁首";
        setup.FooterFirst.Text = "首頁專用頁尾";
        setup.HeaderMinHeight = "1.2cm";
        setup.FooterMinHeight = "0.8cm";
        setup.HeaderDynamicSpacing = true;

        string stylesXml = SaveAndGetStylesXml(document);
        Assert.Contains("style:header-first", stylesXml);
        Assert.Contains("style:footer-first", stylesXml);
        Assert.Contains("首頁專用頁首", stylesXml);
        Assert.Contains("首頁專用頁尾", stylesXml);
        Assert.Contains("text:page-number", stylesXml);
        Assert.Contains("text:page-count", stylesXml);
        Assert.Contains("style:header-style", stylesXml);
        Assert.Contains("style:footer-style", stylesXml);
        Assert.Contains("fo:min-height=\"1.2cm\"", stylesXml);
        Assert.Contains("fo:min-height=\"0.8cm\"", stylesXml);
        Assert.Contains("style:dynamic-spacing=\"true\"", stylesXml);

        OdfPageSetupInfo pageSetup = Assert.Single(document.GetPageSetups());
        Assert.Equal("首頁專用頁首", pageSetup.HeaderFirstText);
        Assert.Equal("首頁專用頁尾", pageSetup.FooterFirstText);
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

        OdfFootnoteInfo footnote = Assert.Single(document.GetFootnotes());
        Assert.Equal("1", footnote.Citation);
        Assert.Equal("這是腳注內容", footnote.BodyText);

        OdfFootnoteInfo endnote = Assert.Single(document.GetEndnotes());
        Assert.Equal("i", endnote.Citation);
        Assert.Equal("這是尾注內容", endnote.BodyText);
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

    /// <summary>
    /// 驗證段落可插入浮動文字框並輸出 draw:text-box 結構。
    /// </summary>
    [Fact]
    public void FloatingTextBoxApiWritesDrawTextBox()
    {
        using var document = TextDocument.Create();
        OdfParagraph paragraph = document.AddParagraph("本文");
        paragraph.AddFloatingTextBox(
                OdfLength.Parse("3cm"),
                OdfLength.Parse("0cm"),
                OdfLength.Parse("6cm"),
                OdfLength.Parse("4cm"),
                OdfAnchorType.Paragraph,
                OdfTextWrap.Parallel)
            .AddParagraph("文字框內容");

        string contentXml = SaveAndGetContentXml(document);

        Assert.Contains("draw:text-box", contentXml);
        Assert.Contains("text:anchor-type=\"paragraph\"", contentXml);
        Assert.Contains("svg:x=\"3cm\"", contentXml);
        Assert.Contains("svg:width=\"6cm\"", contentXml);
        Assert.Contains("style:wrap=\"parallel\"", contentXml);
        Assert.Contains("文字框內容", contentXml);
    }

    /// <summary>
    /// 驗證 ODT 流式寫入器會輸出基本文字文件結構。
    /// </summary>
    [Fact]
    public void OdtStreamWriterWritesTextStructure()
    {
        using var stream = new MemoryStream();
        using (var writer = new OdtStreamWriter(stream))
        {
            writer.AddHeading("章節標題", 2);
            writer.AddParagraph("一般段落", "BodyStyle");
            writer.BeginList("ListStyle");
            writer.AddListItem("第一項");
            writer.AddListItem("第二項");
            writer.EndList();
            writer.AddPageBreak();
        }

        stream.Position = 0;
        using OdfPackage package = OdfPackage.Open(stream, leaveOpen: true);
        using Stream contentStream = package.GetEntryStream("content.xml");
        using var reader = new StreamReader(contentStream);
        string contentXml = reader.ReadToEnd();

        Assert.Contains("office:version=\"1.4\"", contentXml);
        Assert.Contains("text:h", contentXml);
        Assert.Contains("text:outline-level=\"2\"", contentXml);
        Assert.Contains("text:style-name=\"BodyStyle\"", contentXml);
        Assert.Contains("text:list", contentXml);
        Assert.Contains("text:list-item", contentXml);
        Assert.Contains("style:name=\"OdtStreamPageBreak\"", contentXml);
        Assert.Contains("fo:break-before=\"page\"", contentXml);
    }

    /// <summary>
    /// 驗證 ODT 流式讀取器可逐一讀出大型文字文件元素。
    /// </summary>
    [Fact]
    public void OdtStreamReaderReadsTextElements()
    {
        using var stream = new MemoryStream();
        using (var writer = new OdtStreamWriter(stream))
        {
            writer.AddHeading("章節標題", 3);
            writer.AddParagraph("一般段落", "BodyStyle");
            writer.BeginList();
            writer.AddListItem("清單項目");
            writer.EndList();
        }

        stream.Position = 0;
        using var odtReader = new OdtStreamReader(stream);

        Assert.True(odtReader.Read());
        Assert.Equal(OdtNodeType.Heading, odtReader.NodeType);
        Assert.Equal(3, odtReader.HeadingLevel);
        Assert.Equal("章節標題", odtReader.Text);

        Assert.True(odtReader.Read());
        Assert.Equal(OdtNodeType.Paragraph, odtReader.NodeType);
        Assert.Equal("BodyStyle", odtReader.StyleName);
        Assert.Equal("一般段落", odtReader.Text);

        Assert.True(odtReader.Read());
        Assert.Equal(OdtNodeType.ListItem, odtReader.NodeType);
        Assert.Equal("清單項目", odtReader.Text);

        Assert.False(odtReader.Read());
    }

    /// <summary>
    /// 驗證 ODT 流式寫入大量段落時不會累積完整 DOM。
    /// </summary>
    [Fact]
    public void OdtStreamWriterKeepsLargeParagraphGenerationLowMemory()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        long before = GC.GetTotalMemory(forceFullCollection: true);

        using (var writer = new OdtStreamWriter(Stream.Null))
        {
            for (int i = 0; i < 100_000; i++)
            {
                writer.AddParagraph("大型段落測試");
            }
        }

        long after = GC.GetTotalMemory(forceFullCollection: true);
        long retainedBytes = Math.Max(0, after - before);
        Assert.True(retainedBytes < 20 * 1024 * 1024, $"保留記憶體過高：{retainedBytes:N0} bytes。");
    }

    /// <summary>
    /// 驗證 <see cref="TextDocument.AddFontFace"/> 會同時於 content.xml 與 styles.xml 寫入字型宣告。
    /// </summary>
    [Fact]
    public void AddFontFace_WritesFontFaceDeclInContentAndStylesXml()
    {
        using var document = TextDocument.Create();
        document.AddFontFace("自訂字型", "Noto Sans TC", "swiss", "variable");

        string contentXml = SaveAndGetContentXml(document);
        string stylesXml = SaveAndGetStylesXml(document);

        Assert.Contains("style:name=\"自訂字型\"", contentXml);
        Assert.Contains("svg:font-family=\"Noto Sans TC\"", contentXml);
        Assert.Contains("style:font-family-generic=\"swiss\"", contentXml);
        Assert.Contains("style:font-pitch=\"variable\"", contentXml);
        Assert.Contains("style:name=\"自訂字型\"", stylesXml);
    }

    /// <summary>
    /// 驗證重複呼叫 <see cref="TextDocument.AddFontFace"/> 相同名稱時會更新既有宣告而非重複新增。
    /// </summary>
    [Fact]
    public void AddFontFace_SameNameUpdatesExistingDeclaration()
    {
        using var document = TextDocument.Create();
        document.AddFontFace("自訂字型", "Noto Sans TC");
        document.AddFontFace("自訂字型", "Microsoft JhengHei");

        string contentXml = SaveAndGetContentXml(document);
        Assert.Contains("svg:font-family=\"Microsoft JhengHei\"", contentXml);
        Assert.DoesNotContain("Noto Sans TC", contentXml);
    }

    /// <summary>
    /// 驗證 <see cref="TextDocument.GetComments"/> 可讀回文件中所有完整註解物件（含回覆）。
    /// </summary>
    [Fact]
    public void GetComments_ReturnsFullCommentObjectsIncludingReplies()
    {
        using var document = TextDocument.Create();
        OdfParagraph paragraph = document.AddParagraph("註解段落");
        var comment = new OdfComment("審閱者", "請修訂措辭", new DateTime(2026, 6, 17, 8, 0, 0, DateTimeKind.Utc), "comment-1");
        comment.AddReply("作者", "已修訂");
        document.AddComment(paragraph, comment);

        List<OdfComment> comments = document.GetComments();

        OdfComment readBack = Assert.Single(comments);
        Assert.Equal("審閱者", readBack.Author);
        Assert.Equal("請修訂措辭", readBack.Text);
        OdfComment reply = Assert.Single(readBack.Replies);
        Assert.Equal("作者", reply.Author);
        Assert.Equal("已修訂", reply.Text);
    }

    /// <summary>
    /// 驗證 <see cref="TextDocument.GetIndexes"/> 可讀回文件中所有索引物件（字母索引與目錄）。
    /// </summary>
    [Fact]
    public void GetIndexes_ReturnsAllIndexObjectsInDocument()
    {
        using var document = TextDocument.Create();
        document.AddAlphabeticalIndex("術語索引");
        document.AddTableOfContents("文件目錄", 2);

        List<OdfIndex> indexes = document.GetIndexes();

        Assert.Equal(2, indexes.Count);
        Assert.Contains(indexes, i => i is OdfAlphabeticalIndex && i.Name == "術語索引");
        Assert.Contains(indexes, i => i is OdfTableOfContents);
    }

    /// <summary>
    /// 驗證 <see cref="TextDocument.RecordTrackedChange"/> 可記錄附帶額外內容與原始樣式名稱的修訂。
    /// </summary>
    [Fact]
    public void RecordTrackedChange_RecordsChangeWithExtraContentAndOriginalStyle()
    {
        using var document = TextDocument.Create();
        OdfParagraph deletedPara = document.AddParagraph("待刪除內容");

        string changeId = document.RecordTrackedChange("deletion", deletedPara.Node, "OriginalStyle", "paragraph");

        OdfTrackedChange change = document.GetTrackedChanges().Single(c => c.RegionId == changeId);
        Assert.Equal(OdfChangeType.Deletion, change.ChangeType);
        Assert.Equal("待刪除內容", change.Content);
    }

    /// <summary>
    /// 驗證 <see cref="TextDocument.RejectAllChanges"/> 為 <see cref="TextDocument.RejectAllTrackedChanges"/> 之別名，可正確拒絕所有修訂。
    /// </summary>
    [Fact]
    public void RejectAllChanges_RejectsAllTrackedChangesInDocument()
    {
        using var document = TextDocument.Create();
        document.TrackedChanges = true;
        document.AddParagraph("插入文字");

        Assert.NotEmpty(document.GetTrackedChanges());

        document.RejectAllChanges();

        Assert.Empty(document.GetTrackedChanges());
        string contentXml = SaveAndGetContentXml(document);
        Assert.DoesNotContain("插入文字", contentXml);
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
