using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Text;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 鎖定 ODM 主控文字文件子文件參照管理 API 的整合測試。
/// </summary>
public class MasterDocumentTests
{
    /// <summary>
    /// 驗證 <see cref="TextMasterDocument.RemoveSubDocumentReference"/> 可移除指定名稱的子文件參照。
    /// </summary>
    [Fact]
    public void RemoveSubDocumentReference_RemovesMatchingSection()
    {
        using var master = TextMasterDocument.Create();
        master.AddSubDocumentReference("Chapter1", "chapter1.odt");
        master.AddSubDocumentReference("Chapter2", "chapter2.odt");

        Assert.True(master.RemoveSubDocumentReference("Chapter1"));
        Assert.False(master.RemoveSubDocumentReference("Chapter1"));

        var remaining = master.GetSubDocumentReferences();
        Assert.Single(remaining);
        Assert.Equal("Chapter2", remaining[0].SectionName);
    }

    /// <summary>
    /// 驗證 <see cref="TextMasterDocument.ReorderSubDocumentReferences"/> 可依指定順序重新排列子文件參照，並於儲存／載入後保留。
    /// </summary>
    [Fact]
    public void ReorderSubDocumentReferences_ReordersAndPersists()
    {
        using var master = TextMasterDocument.Create();
        master.AddSubDocumentReference("Chapter1", "chapter1.odt");
        master.AddSubDocumentReference("Chapter2", "chapter2.odt");
        master.AddSubDocumentReference("Chapter3", "chapter3.odt");

        master.ReorderSubDocumentReferences(new List<string> { "Chapter3", "Chapter1", "Chapter2" });

        var reordered = master.GetSubDocumentReferences();
        Assert.Equal(new[] { "Chapter3", "Chapter1", "Chapter2" }, reordered.Select(r => r.SectionName));

        using var stream = new MemoryStream();
        master.SaveToStream(stream);
        stream.Position = 0;

        using var loaded = TextMasterDocument.Load(stream);
        var persisted = loaded.GetSubDocumentReferences();
        Assert.Equal(new[] { "Chapter3", "Chapter1", "Chapter2" }, persisted.Select(r => r.SectionName));
    }

    /// <summary>
    /// 驗證子文件參照的載入時機（<c>xlink:actuate</c>）可透過 <c>loadOnRequest</c> 參數指定，
    /// 並透過 <see cref="TextMasterDocument.SetSubDocumentLoadOnRequest"/> 變更，於儲存／載入後保留。
    /// </summary>
    [Fact]
    public void SubDocumentActuate_SetAndPersistAfterSaveAndLoad()
    {
        using var master = TextMasterDocument.Create();
        master.AddSubDocumentReference("Chapter1", "chapter1.odt");
        master.AddSubDocumentReference("Chapter2", "chapter2.odt", loadOnRequest: true);

        var references = master.GetSubDocumentReferences();
        Assert.Equal("onLoad", references.Single(r => r.SectionName == "Chapter1").Actuate);
        Assert.Equal("onRequest", references.Single(r => r.SectionName == "Chapter2").Actuate);

        Assert.True(master.SetSubDocumentLoadOnRequest("Chapter1", true));
        Assert.False(master.SetSubDocumentLoadOnRequest("NotExist", true));

        using var stream = new MemoryStream();
        master.SaveToStream(stream);
        stream.Position = 0;

        using var loaded = TextMasterDocument.Load(stream);
        var persisted = loaded.GetSubDocumentReferences();
        Assert.Equal("onRequest", persisted.Single(r => r.SectionName == "Chapter1").Actuate);
        Assert.Equal("onRequest", persisted.Single(r => r.SectionName == "Chapter2").Actuate);
    }

    /// <summary>
    /// 驗證 <see cref="TextMasterDocument.MergeSubDocuments"/> 可依文件順序，
    /// 將主控文件本身內容與外部子文件內容合併為單一文字文件。
    /// </summary>
    [Fact]
    public void MergeSubDocuments_CombinesOwnContentAndSubDocumentsInOrder()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"odfkit-master-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string chapter1Path = Path.Combine(tempDir, "chapter1.odt");
        string chapter2Path = Path.Combine(tempDir, "chapter2.odt");

        try
        {
            using (var chapter1 = TextDocument.Create())
            {
                chapter1.AddParagraph("第一章內容");
                chapter1.Save(chapter1Path);
            }

            using (var chapter2 = TextDocument.Create())
            {
                chapter2.AddParagraph("第二章內容");
                chapter2.Save(chapter2Path);
            }

            using var master = TextMasterDocument.Create();
            master.AddParagraph("封面標題");
            master.AddSubDocumentReference("Chapter1", "chapter1.odt");
            master.AddSubDocumentReference("Chapter2", "chapter2.odt");

            using TextDocument merged = master.MergeSubDocuments(tempDir);
            var paragraphTexts = merged.Body.Paragraphs.Select(p => p.TextContent).ToList();

            Assert.Contains("封面標題", paragraphTexts);
            Assert.Contains("第一章內容", paragraphTexts);
            Assert.Contains("第二章內容", paragraphTexts);
            Assert.True(paragraphTexts.IndexOf("封面標題") < paragraphTexts.IndexOf("第一章內容"));
            Assert.True(paragraphTexts.IndexOf("第一章內容") < paragraphTexts.IndexOf("第二章內容"));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    /// <summary>
    /// 驗證 <see cref="TextDocument.ShiftHeadingOutlineLevels"/> 可調整文件中所有標題的大綱階層，
    /// 並將結果限制在最小值 1。
    /// </summary>
    [Fact]
    public void ShiftHeadingOutlineLevels_AdjustsHeadingsAndClampsToMinimumOne()
    {
        using var doc = TextDocument.Create();
        var heading = doc.AddHeading("頂層標題", 1);

        doc.ShiftHeadingOutlineLevels(1);
        Assert.Equal(2, heading.OutlineLevel);

        doc.ShiftHeadingOutlineLevels(-10);
        Assert.Equal(1, heading.OutlineLevel);
    }

    /// <summary>
    /// 驗證 <see cref="TextMasterDocument.MergeSubDocuments"/> 的 <c>subDocumentOutlineOffset</c>
    /// 參數可位移子文件標題大綱階層，使其正確巢狀於主控文件本身的標題之下。
    /// </summary>
    [Fact]
    public void MergeSubDocuments_WithOutlineOffset_ShiftsSubDocumentHeadingLevels()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"odfkit-master-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string chapter1Path = Path.Combine(tempDir, "chapter1.odt");

        try
        {
            using (var chapter1 = TextDocument.Create())
            {
                chapter1.AddHeading("第一章", 1);
                chapter1.Save(chapter1Path);
            }

            using var master = TextMasterDocument.Create();
            master.AddHeading("封面標題", 1);
            master.AddSubDocumentReference("Chapter1", "chapter1.odt");

            using TextDocument merged = master.MergeSubDocuments(tempDir, subDocumentOutlineOffset: 1);
            var headings = merged.Body.Headings.Items;

            var coverHeading = headings.Single(h => h.TextContent == "封面標題");
            var chapterHeading = headings.Single(h => h.TextContent == "第一章");
            Assert.Equal(1, coverHeading.OutlineLevel);
            Assert.Equal(2, chapterHeading.OutlineLevel);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    /// <summary>
    /// 驗證 <see cref="TextMasterDocument.MergeSubDocuments"/> 會合併所有子文件參照，
    /// 即使其 <c>xlink:actuate</c> 設定為 <c>onRequest</c>（延遲載入）；合併屬於一次性完整展開，
    /// 不受該僅供即時檢視應用程式使用的載入時機語意影響。
    /// </summary>
    [Fact]
    public void MergeSubDocuments_IncludesOnRequestSubDocuments()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"odfkit-master-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string chapter1Path = Path.Combine(tempDir, "chapter1.odt");

        try
        {
            using (var chapter1 = TextDocument.Create())
            {
                chapter1.AddParagraph("延遲載入章節內容");
                chapter1.Save(chapter1Path);
            }

            using var master = TextMasterDocument.Create();
            master.AddSubDocumentReference("Chapter1", "chapter1.odt", loadOnRequest: true);

            var references = master.GetSubDocumentReferences();
            Assert.Equal("onRequest", references.Single(r => r.SectionName == "Chapter1").Actuate);

            using TextDocument merged = master.MergeSubDocuments(tempDir);
            var paragraphTexts = merged.Body.Paragraphs.Select(p => p.TextContent).ToList();

            Assert.Contains("延遲載入章節內容", paragraphTexts);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    /// <summary>
    /// 驗證 <see cref="TextMasterDocument.MergeSubDocuments"/> 在主控文件與子文件定義同名但
    /// 內容不同的樣式時，依 <see cref="OdfMergeOptions.StyleConflictResolution"/> 設定（預設
    /// <see cref="ConflictResolution.KeepSourceFormatting"/>）將子文件衝突的樣式重新命名，
    /// 保留兩者各自的格式設定，而非互相覆蓋。
    /// </summary>
    [Fact]
    public void MergeSubDocuments_RenamesConflictingStyleNames()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"odfkit-master-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string chapter1Path = Path.Combine(tempDir, "chapter1.odt");

        try
        {
            using (var chapter1 = TextDocument.Create())
            {
                AddNamedParagraphStyle(chapter1, "Standard", "20pt");
                chapter1.Save(chapter1Path);
            }

            using var master = TextMasterDocument.Create();
            AddNamedParagraphStyle(master, "Standard", "10pt");
            master.AddSubDocumentReference("Chapter1", "chapter1.odt");

            using TextDocument merged = master.MergeSubDocuments(tempDir);

            OdfNode mergedStyles = FindOrCreateChild(merged.StylesDom, "styles", OdfNamespaces.Office, "office");
            var paragraphStyleNames = mergedStyles.Children
                .Where(c => c.NodeType is OdfNodeType.Element && c.LocalName == "style" && c.GetAttribute("family", OdfNamespaces.Style) == "paragraph")
                .Select(c => c.GetAttribute("name", OdfNamespaces.Style))
                .ToList();

            Assert.Equal(2, paragraphStyleNames.Count);
            Assert.Contains("Standard", paragraphStyleNames);
            Assert.Contains(paragraphStyleNames, name => name != "Standard" && name!.StartsWith("Standard", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    /// <summary>
    /// 驗證 <see cref="TextMasterDocument.MergeSubDocuments"/> 在子文件參照的目標檔案不存在時，
    /// 會擲出檔案系統層級的例外，而非靜默忽略或產生不完整的合併結果。
    /// </summary>
    [Fact]
    public void MergeSubDocuments_MissingSubDocumentFile_ThrowsFileNotFoundException()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"odfkit-master-missing-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            using var master = TextMasterDocument.Create();
            master.AddSubDocumentReference("MissingChapter", "does-not-exist.odt");

            Assert.Throws<FileNotFoundException>(() => master.MergeSubDocuments(tempDir));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    /// <summary>
    /// 驗證 <see cref="TextMasterDocument.MergeSubDocuments"/> 在 <c>baseDirectory</c> 為空白時，
    /// 擲出 <see cref="ArgumentException"/>（既有邊界檢查的回歸測試）。
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void MergeSubDocuments_BlankBaseDirectory_ThrowsArgumentException(string baseDirectory)
    {
        using var master = TextMasterDocument.Create();
        master.AddSubDocumentReference("Chapter1", "chapter1.odt");

        Assert.Throws<ArgumentException>(() => master.MergeSubDocuments(baseDirectory));
    }

    private static void AddNamedParagraphStyle(TextDocument doc, string name, string fontSize)
    {
        OdfNode styles = FindOrCreateChild(doc.StylesDom, "styles", OdfNamespaces.Office, "office");
        var styleNode = new OdfNode(OdfNodeType.Element, "style", OdfNamespaces.Style, "style");
        styleNode.SetAttribute("name", OdfNamespaces.Style, name, "style");
        styleNode.SetAttribute("family", OdfNamespaces.Style, "paragraph", "style");

        var textProperties = new OdfNode(OdfNodeType.Element, "text-properties", OdfNamespaces.Style, "style");
        textProperties.SetAttribute("font-size", OdfNamespaces.Fo, fontSize, "fo");
        styleNode.AppendChild(textProperties);

        styles.AppendChild(styleNode);
    }

    private static OdfNode FindOrCreateChild(OdfNode parent, string localName, string ns, string prefix)
    {
        foreach (OdfNode child in parent.Children)
        {
            if (child.NodeType is OdfNodeType.Element && child.LocalName == localName && child.NamespaceUri == ns)
            {
                return child;
            }
        }

        var created = new OdfNode(OdfNodeType.Element, localName, ns, prefix);
        parent.AppendChild(created);
        return created;
    }
}
