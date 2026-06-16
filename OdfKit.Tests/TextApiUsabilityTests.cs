using System;
using System.IO;
using System.Linq;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.Text;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 鎖定文字文件高階 API 的易用入口。
/// </summary>
public class TextApiUsabilityTests
{
    /// <summary>
    /// 驗證可用 Body facade 建立常見 ODT 內容並 round-trip。
    /// </summary>
    [Fact]
    public void CreateLoadBodyParagraphsHeadingsRunsListsTablesImagesAndMetadata()
    {
        using var document = TextDocument.Create();
        document.Metadata.Title = "狀態報告";
        document.Metadata.Creator = "OdfKit";
        document.Metadata.Subject = "G4";
        document.Metadata.Description = "文字文件高階 API 測試";

        OdfHeading heading = document.Body.Headings.Add("本週摘要", 1);
        heading.StyleName = "Heading_20_1";
        OdfParagraph paragraph = document.Body.Paragraphs.Add("開頭");
        OdfTextRun boldRun = paragraph.AddTextRun("重點");
        boldRun.IsBold = true;
        OdfTextRun italicRun = paragraph.AddTextRun("補充");
        italicRun.IsItalic = true;

        OdfList list = document.Body.Lists.Add();
        list.AddListItem("完成項目");
        list.AddListItem("待辦項目");

        OdfTable table = document.Body.Tables.Add(1, 2);
        table.GetCell(0, 0).AddParagraph("欄一");
        table.GetCell(0, 1).AddParagraph("欄二");

        OdfImage image = document.Body.Images.Add(CreatePngBytes(), "1cm", "1cm", "Logo");

        using var stream = new MemoryStream();
        document.SaveToStream(stream);
        stream.Position = 0;

        using TextDocument loaded = TextDocument.Load(stream);

        Assert.Equal("狀態報告", loaded.Metadata.Title);
        Assert.Equal("OdfKit", loaded.Metadata.Creator);
        Assert.Equal("G4", loaded.Metadata.Subject);
        Assert.Equal("文字文件高階 API 測試", loaded.Metadata.Description);
        Assert.Equal("本週摘要", loaded.Body.Headings.Items[0].TextContent);
        Assert.Equal(1, loaded.Body.Headings.Items[0].OutlineLevel);
        Assert.Contains(loaded.Body.Paragraphs.Items, item => item.TextContent.Contains("開頭", StringComparison.Ordinal));
        Assert.Equal("完成項目", loaded.Body.Lists.Items[0].Items[0].Paragraphs[0].TextContent);
        Assert.Equal(1, loaded.Body.Tables.Items[0].RowCount);
        Assert.Equal(2, loaded.Body.Tables.Items[0].ColumnCount);
        Assert.Equal("Logo", loaded.Body.Images.Items[0].Name);
        Assert.Equal("Pictures/Logo.png", loaded.Body.Images.Items[0].ImageHref);
        Assert.Equal("application/vnd.oasis.opendocument.text", loaded.Package.MimeType);
    }

    /// <summary>
    /// 驗證圖片 API 會寫入封裝並在 content.xml 建立影像參照。
    /// </summary>
    [Fact]
    public void BodyImagesAddWritesPackageEntryAndReference()
    {
        using var document = TextDocument.Create();

        document.Body.Images.Add(CreatePngBytes(), "2cm", "2cm", "TinyPng");

        using var stream = new MemoryStream();
        document.SaveToStream(stream);
        stream.Position = 0;

        using OdfPackage package = OdfPackage.Open(stream, leaveOpen: true);
        string contentXml = ReadEntry(package, "content.xml");

        Assert.Contains("draw:name=\"TinyPng\"", contentXml);
        Assert.Contains("xlink:href=\"Pictures/TinyPng.png\"", contentXml);
        Assert.True(package.HasEntry("Pictures/TinyPng.png"));
    }

    /// <summary>
    /// 驗證非 ODT 文件不會被誤載為文字文件。
    /// </summary>
    [Fact]
    public void LoadRejectsNonTextDocument()
    {
        using var stream = new MemoryStream();
        using (OdfPackage package = OdfDocumentFactory.CreatePackage(stream, OdfDocumentKind.Spreadsheet, leaveOpen: true))
        {
            package.Save();
        }

        stream.Position = 0;

        Assert.Throws<InvalidOperationException>(() => TextDocument.Load(stream, "sheet.ods"));
    }

    /// <summary>
    /// 驗證文字文件 Body 集合可直接使用 LINQ 查詢。
    /// </summary>
    [Fact]
    public void BodyCollectionsSupportLinqQueries()
    {
        using var doc = TextDocument.Create();
        doc.Body.Headings.Add("標題", 1);
        doc.Body.Paragraphs.Add("第一段");
        doc.Body.Paragraphs.Add("");
        doc.Body.Lists.Add().AddListItem("項目");
        doc.Body.Tables.Add(1, 1);

        var nonEmptyParagraphs = doc.Body.Paragraphs
            .Where(paragraph => paragraph.TextContent.Length > 0)
            .ToArray();
        var headingTexts = doc.Body.Headings.Select(heading => heading.TextContent).ToArray();

        Assert.Single(nonEmptyParagraphs);
        Assert.Equal("第一段", nonEmptyParagraphs[0].TextContent);
        Assert.Equal(["標題"], headingTexts);
        Assert.Single(doc.Body.Lists);
        Assert.Single(doc.Body.Tables);
    }

    /// <summary>
    /// 驗證文字文件可用非同步 API 儲存到路徑並載入。
    /// </summary>
    [Fact]
    public async Task TextAsyncSaveAndLoadByPathRoundTrips()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".odt");
        try
        {
            using (var doc = TextDocument.Create())
            {
                doc.Body.Paragraphs.Add("非同步段落");
                await doc.SaveAsync(path, cancellationToken: TestContext.Current.CancellationToken);
            }

            using TextDocument loaded = await TextDocument.LoadAsync(path, TestContext.Current.CancellationToken);

            Assert.Equal("非同步段落", loaded.Body.Paragraphs.Single().TextContent);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    /// <summary>
    /// 驗證文字圖片可標記為 ODF 1.4 裝飾性物件。
    /// </summary>
    [Fact]
    public void ImageMarkAsDecorative_WritesDrawDecorativeAttribute()
    {
        using var doc = TextDocument.Create();
        doc.Body.Images.Add(CreatePngBytes(), "1cm", "1cm", "Decorative")
            .MarkAsDecorative();

        using var stream = new MemoryStream();
        doc.SaveToStream(stream);
        stream.Position = 0;

        using OdfPackage package = OdfPackage.Open(stream, leaveOpen: true);
        string contentXml = ReadEntry(package, "content.xml");

        Assert.Contains("draw:decorative=\"true\"", contentXml);
    }

    /// <summary>
    /// 驗證文字文件 Fluent builder 可建立中繼資料、標題、段落片段與清單。
    /// </summary>
    [Fact]
    public void TextDocumentBuilderCreatesMetadataParagraphRunsAndList()
    {
        using TextDocument document = TextDocument.Builder()
            .WithMetadata(metadata => metadata
                .Title("季報")
                .Author("OdfKit")
                .Subject("營運摘要")
                .Description("2026 年第一季財務狀況。"))
            .AddHeading("第一季財務摘要", level: 1)
            .AddParagraph("本報告涵蓋 2026 年第一季的財務狀況。", format => format.FontSize(11))
            .AddParagraph(paragraph => paragraph
                .Append("總收入：")
                .Append("NT$ 1,200,000", format => format.Bold().Color("#C00000"))
                .Append("，較去年同期成長 12.3%。"))
            .AddList(list => list
                .Item("硬體銷售：NT$ 800,000")
                .Item("軟體授權：NT$ 400,000"))
            .Build();

        using var stream = new MemoryStream();
        document.SaveToStream(stream);
        stream.Position = 0;

        using TextDocument loaded = TextDocument.Load(stream);

        Assert.Equal("季報", loaded.Metadata.Title);
        Assert.Equal("OdfKit", loaded.Metadata.Creator);
        Assert.Equal("營運摘要", loaded.Metadata.Subject);
        Assert.Equal("第一季財務摘要", loaded.Body.Headings.Single().TextContent);
        Assert.Contains(loaded.Body.Paragraphs, paragraph => paragraph.TextContent.Contains("NT$ 1,200,000", StringComparison.Ordinal));
        Assert.Equal("硬體銷售：NT$ 800,000", loaded.Body.Lists.Single().Items[0].Paragraphs[0].TextContent);

        stream.Position = 0;
        using OdfPackage package = OdfPackage.Open(stream, leaveOpen: true);
        string contentXml = ReadEntry(package, "content.xml");

        Assert.Contains("#C00000", contentXml);
        Assert.Contains("11pt", contentXml);
    }

    private static string ReadEntry(OdfPackage package, string path)
    {
        using Stream stream = package.GetEntryStream(path);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static byte[] CreatePngBytes()
    {
        return Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=");
    }

    /// <summary>
    /// 驗證可在段落中插入腳注並 round-trip。
    /// </summary>
    [Fact]
    public void AddFootnote_PersistsNoteElementInOdt()
    {
        using var doc = TextDocument.Create();
        var para = doc.AddParagraph("本文");
        para.AddFootnote("1", "這是腳注內容。");

        using var ms = new MemoryStream();
        doc.SaveToStream(ms);
        ms.Position = 0;

        using OdfPackage package = OdfPackage.Open(ms, leaveOpen: true);
        string contentXml = ReadEntry(package, "content.xml");
        Assert.Contains("note-class", contentXml);
        Assert.Contains("footnote", contentXml);
        Assert.Contains("這是腳注內容。", contentXml);
    }

    /// <summary>
    /// 驗證可在段落中插入尾注並 round-trip。
    /// </summary>
    [Fact]
    public void AddEndnote_PersistsNoteElementInOdt()
    {
        using var doc = TextDocument.Create();
        var para = doc.AddParagraph("本文");
        para.AddEndnote("i", "這是尾注內容。");

        using var ms = new MemoryStream();
        doc.SaveToStream(ms);
        ms.Position = 0;

        using OdfPackage package = OdfPackage.Open(ms, leaveOpen: true);
        string contentXml = ReadEntry(package, "content.xml");
        Assert.Contains("note-class", contentXml);
        Assert.Contains("endnote", contentXml);
        Assert.Contains("這是尾注內容。", contentXml);
    }
}
