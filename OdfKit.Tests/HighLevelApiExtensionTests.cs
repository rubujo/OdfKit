using System;
using System.Collections.Generic;
using System.Linq;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Text;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 針對高階 API 擴充項目（書籤、清單、行內富文本）的單元測試。
/// </summary>
public sealed class HighLevelApiExtensionTests
{
    [Fact]
    public void TestBookmarkManager_InlineBookmark_ReadWrite()
    {
        using var doc = TextDocument.Create();
        var paragraph = doc.AddParagraph("段落文字");

        // 新增 inline 書籤
        paragraph.AddBookmark("InlineBookmark");

        // 驗證書籤名稱是否被正確收集
        var names = doc.Bookmarks.Names.ToList();
        Assert.Contains("InlineBookmark", names);

        // 取得 inline 書籤值，預設為空字串
        var val = doc.Bookmarks["InlineBookmark"].Value;
        Assert.Equal(string.Empty, val);

        // 設值給 inline 書籤，會將其升級為範圍書籤，並把內容夾在中間
        doc.Bookmarks["InlineBookmark"].Value = "新填入內容";

        // 設值後可正確讀取出寫入的內容
        Assert.Equal("新填入內容", doc.Bookmarks["InlineBookmark"].Value);
        Assert.Contains("新填入內容", paragraph.TextContent);
    }

    [Fact]
    public void TestBookmarkManager_InlineBookmark_DuplicateWrite_ReplacesValue()
    {
        using var doc = TextDocument.Create();
        var paragraph = doc.AddParagraph("段落文字");

        // 新增 inline 書籤
        paragraph.AddBookmark("InlineBookmark");

        // 第一次設值
        doc.Bookmarks["InlineBookmark"].Value = "第一次填入內容";
        Assert.Equal("第一次填入內容", doc.Bookmarks["InlineBookmark"].Value);
        Assert.Contains("第一次填入內容", paragraph.TextContent);
        Assert.DoesNotContain("第二次填入內容", paragraph.TextContent);

        // 第二次設值，應取代前值，而不是累積
        doc.Bookmarks["InlineBookmark"].Value = "第二次填入內容";
        Assert.Equal("第二次填入內容", doc.Bookmarks["InlineBookmark"].Value);
        Assert.Contains("第二次填入內容", paragraph.TextContent);
    }

    [Fact]
    public void TestBookmarkManager_InlineBookmark_SaveAndReload_DuplicateWrite_ReplacesValue()
    {
        using var ms = new System.IO.MemoryStream();

        // 1. 建立文件並寫入第一次設值
        using (var doc = TextDocument.Create())
        {
            var paragraph = doc.AddParagraph("段落文字");
            paragraph.AddBookmark("SaveReloadBookmark");
            doc.Bookmarks["SaveReloadBookmark"].Value = "第一次內容";
            doc.SaveToStream(ms);
        }

        ms.Position = 0;

        // 2. 重新載入並再次設值，驗證是否為取代而非累積
        using (var doc2 = TextDocument.Load(ms))
        {
            var paragraph = doc2.Body.Paragraphs.FirstOrDefault();
            Assert.NotNull(paragraph);

            // 驗證重載後，設值已正確轉為範圍書籤，因此可讀取出來
            Assert.Equal("第一次內容", doc2.Bookmarks["SaveReloadBookmark"].Value);

            // 第二次設值
            doc2.Bookmarks["SaveReloadBookmark"].Value = "第二次內容";

            // 驗證讀取值與段落內容
            Assert.Equal("第二次內容", doc2.Bookmarks["SaveReloadBookmark"].Value);
            Assert.Contains("第二次內容", paragraph!.TextContent);
            Assert.DoesNotContain("第一次內容", paragraph.TextContent);
        }
    }

    [Fact]
    public void TestBookmarkManager_RangeBookmark_ReadWrite()
    {
        using var doc = TextDocument.Create();
        var paragraph = doc.AddParagraph();
        var pNode = paragraph.Node;

        // 手動建立成對的 range 書籤
        var startNode = new OdfNode(OdfNodeType.Element, "bookmark-start", OdfNamespaces.Text, "text");
        startNode.SetAttribute("name", OdfNamespaces.Text, "RangeBookmark", "text");

        var endNode = new OdfNode(OdfNodeType.Element, "bookmark-end", OdfNamespaces.Text, "text");
        endNode.SetAttribute("name", OdfNamespaces.Text, "RangeBookmark", "text");

        var textNode = new OdfNode(OdfNodeType.Text, string.Empty, string.Empty) { TextContent = "原始內容" };

        pNode.AppendChild(startNode);
        pNode.AppendChild(textNode);
        pNode.AppendChild(endNode);

        // 1. 驗證讀取書籤內容
        Assert.Equal("原始內容", doc.Bookmarks["RangeBookmark"].Value);

        // 2. 驗證替換書籤內容
        doc.Bookmarks["RangeBookmark"].Value = "修改後的內容";
        Assert.Equal("修改後的內容", doc.Bookmarks["RangeBookmark"].Value);

        // 3. 驗證書籤節點本身依然存在，沒有被刪除
        var descendants = pNode.Descendants().ToList();
        Assert.Contains(startNode, descendants);
        Assert.Contains(endNode, descendants);
    }

    [Fact]
    public void TestBookmarkManager_RangeBookmark_CrossParagraph_ReadWrite()
    {
        using var doc = TextDocument.Create();
        var p1 = doc.AddParagraph();
        var p2 = doc.AddParagraph();

        // 跨段落建立書籤：start 在 p1，end 在 p2
        var startNode = new OdfNode(OdfNodeType.Element, "bookmark-start", OdfNamespaces.Text, "text");
        startNode.SetAttribute("name", OdfNamespaces.Text, "CrossBookmark", "text");

        var endNode = new OdfNode(OdfNodeType.Element, "bookmark-end", OdfNamespaces.Text, "text");
        endNode.SetAttribute("name", OdfNamespaces.Text, "CrossBookmark", "text");

        var text1 = new OdfNode(OdfNodeType.Text, string.Empty, string.Empty) { TextContent = "文字一" };
        var text2 = new OdfNode(OdfNodeType.Text, string.Empty, string.Empty) { TextContent = "文字二" };

        p1.Node.AppendChild(startNode);
        p1.Node.AppendChild(text1);
        p2.Node.AppendChild(text2);
        p2.Node.AppendChild(endNode);

        // 1. 驗證讀取
        Assert.Equal("文字一文字二", doc.Bookmarks["CrossBookmark"].Value);

        // 2. 驗證跨段落安全替換
        doc.Bookmarks["CrossBookmark"].Value = "替換後的文字";

        // 替換後的文字應插入在 start 之後 (即 p1)
        Assert.Equal("替換後的文字", doc.Bookmarks["CrossBookmark"].Value);
        Assert.Equal("替換後的文字", p1.TextContent);
        Assert.Equal(string.Empty, p2.TextContent);
    }

    [Fact]
    public void TestBookmarkManager_NonExistent_ThrowsException()
    {
        using var doc = TextDocument.Create();

        var exRead = Assert.Throws<KeyNotFoundException>(() => doc.Bookmarks["NoSuchBookmark"].Value);
        Assert.Contains("NoSuchBookmark", exRead.Message);
        Assert.DoesNotContain("Err_Bookmark_NotFound", exRead.Message);

        var exWrite = Assert.Throws<KeyNotFoundException>(() => doc.Bookmarks["NoSuchBookmark"].Value = "New Value");
        Assert.Contains("NoSuchBookmark", exWrite.Message);
        Assert.DoesNotContain("Err_Bookmark_NotFound", exWrite.Message);
    }

    [Fact]
    public void TestListBuilder_NestedLists_Generation()
    {
        using var doc = TextDocument.Create();

        // 鏈式建構巢狀清單
        doc.AppendList()
            .Item("第一項")
            .Item("第二項")
            .SubList()
                .Item("第二之一項")
                .Item("第二之二項")
                .Up()
            .Item("第三項");

        // 取得本文根節點
        var bodyNode = doc.BodyTextRoot;

        // 驗證層級：應有一層最外圍的 text:list，其包含三個 list-item；而第二個 list-item 底下又有一個 text:list
        var listElements = bodyNode.Descendants()
            .Where(n => n.NodeType == OdfNodeType.Element && n.NamespaceUri == OdfNamespaces.Text && n.LocalName == "list")
            .ToList();

        Assert.Equal(2, listElements.Count);

        var firstList = listElements[0];
        var firstListItems = firstList.Children
            .Where(n => n.NodeType == OdfNodeType.Element && n.NamespaceUri == OdfNamespaces.Text && n.LocalName == "list-item")
            .ToList();

        Assert.Equal(3, firstListItems.Count);

        // 第二個項目下面應有 nested list
        var secondItem = firstListItems[1];
        var nestedList = secondItem.Descendants()
            .FirstOrDefault(n => n.NodeType == OdfNodeType.Element && n.NamespaceUri == OdfNamespaces.Text && n.LocalName == "list");

        Assert.NotNull(nestedList);
        Assert.Same(listElements[1], nestedList);
    }

    [Fact]
    public void TestInlineTextBuilder_RichText_Flyweight()
    {
        using var doc = TextDocument.Create();
        var p1 = doc.AddParagraph();
        var p2 = doc.AddParagraph();

        // 在段落一寫入富文本，並在段落二寫入相同的富文本
        p1.AppendText()
            .Text("正常文字 ")
            .Bold()
            .Italic()
            .Color("#FF0000")
            .Text("紅色粗斜體")
            .Clear()
            .Text(" 又變回正常文字");

        p2.AppendText()
            .Bold()
            .Italic()
            .Color("#FF0000")
            .Text("另一個相同樣式紅色粗斜體");

        // 驗證段落內容
        Assert.Contains("紅色粗斜體", p1.TextContent);
        Assert.Contains("另一個相同樣式紅色粗斜體", p2.TextContent);

        // 尋找所有的 span 節點
        var spans1 = p1.Node.Descendants()
            .Where(n => n.NodeType == OdfNodeType.Element && n.NamespaceUri == OdfNamespaces.Text && n.LocalName == "span")
            .ToList();
        var spans2 = p2.Node.Descendants()
            .Where(n => n.NodeType == OdfNodeType.Element && n.NamespaceUri == OdfNamespaces.Text && n.LocalName == "span")
            .ToList();

        Assert.Single(spans1);
        Assert.Single(spans2);

        // 取得它們套用的樣式名稱
        string? styleName1 = spans1[0].GetAttribute("style-name", OdfNamespaces.Text);
        string? styleName2 = spans2[0].GetAttribute("style-name", OdfNamespaces.Text);

        Assert.NotNull(styleName1);
        Assert.NotNull(styleName2);

        // 由於享元樣式池的作用，相同樣式設定的 span 應指向同一個自動產生的去重樣式名稱！
        Assert.Equal(styleName1, styleName2);
    }
}
