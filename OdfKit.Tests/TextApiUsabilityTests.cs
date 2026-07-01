using System;
using System.IO;
using System.Linq;
using OdfKit.Chart;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.Spreadsheet;
using OdfKit.Styles;
using OdfKit.Text;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 鎖定文字文件高階 API 的易用入口。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Smoke)]
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
    /// 驗證高階儲存預設會清理已從 DOM 移除、但仍殘留在封裝中的圖片媒體。
    /// </summary>
    [Fact]
    public void SavePrunesUnreferencedPictureEntriesByDefault()
    {
        using var document = TextDocument.Create();
        OdfImage kept = document.Body.Images.Add(CreatePngBytes(), "2cm", "2cm", "Kept");
        const string orphanedPath = "Pictures/Orphaned.png";
        document.Package.WriteEntry(orphanedPath, CreatePngBytes(), "image/png");

        using var stream = new MemoryStream();
        document.SaveToStream(stream);
        stream.Position = 0;

        using OdfPackage package = OdfPackage.Open(stream, leaveOpen: true);
        string contentXml = ReadEntry(package, "content.xml");

        Assert.True(package.HasEntry(kept.ImageHref!));
        Assert.False(package.HasEntry(orphanedPath));
        Assert.Contains($"xlink:href=\"{kept.ImageHref}\"", contentXml);
        Assert.DoesNotContain(orphanedPath, contentXml);
    }

    /// <summary>
    /// 驗證關閉 <see cref="OdfSaveOptions.PruneUnusedMedia"/> 時，高階儲存會保留孤立圖片媒體項目。
    /// </summary>
    [Fact]
    public void SaveCanPreserveUnreferencedPictureEntriesWhenPruningIsDisabled()
    {
        using var document = TextDocument.Create();
        OdfImage kept = document.Body.Images.Add(CreatePngBytes(), "2cm", "2cm", "Kept");
        const string orphanedPath = "Pictures/Orphaned.png";
        document.Package.WriteEntry(orphanedPath, CreatePngBytes(), "image/png");

        using var stream = new MemoryStream();
        document.SaveToStream(stream, new OdfSaveOptions { PruneUnusedMedia = false });
        stream.Position = 0;

        using OdfPackage package = OdfPackage.Open(stream, leaveOpen: true);
        string contentXml = ReadEntry(package, "content.xml");

        Assert.True(package.HasEntry(kept.ImageHref!));
        Assert.True(package.HasEntry(orphanedPath));
        Assert.Contains($"xlink:href=\"{kept.ImageHref}\"", contentXml);
        Assert.DoesNotContain(orphanedPath, contentXml);
    }

    /// <summary>
    /// 驗證 <see cref="TextDocument.OptimizeMedia"/> 可替換圖片封裝項目並同步更新參照。
    /// </summary>
    [Fact]
    public void OptimizeMediaRewritesImageEntryAndReference()
    {
        using var document = TextDocument.Create();
        document.Body.Images.Add(CreatePngBytes(), "2cm", "2cm", "TinyPng");
        OdfMediaOptimizationRequest? capturedRequest = null;

        int optimized = document.OptimizeMedia(144, 82, request =>
        {
            capturedRequest = request;
            return new OdfOptimizedMedia(CreateWebpBytes(), "image/webp", ".webp");
        });

        Assert.Equal(1, optimized);
        Assert.NotNull(capturedRequest);
        Assert.Equal("Pictures/TinyPng.png", capturedRequest!.PackagePath);
        Assert.Equal("image/png", capturedRequest.MediaType);
        Assert.Equal(144, capturedRequest.MaxDpi);
        Assert.Equal(82, capturedRequest.JpegQuality);

        using var stream = new MemoryStream();
        document.SaveToStream(stream);
        stream.Position = 0;

        using OdfPackage package = OdfPackage.Open(stream, leaveOpen: true);
        string contentXml = ReadEntry(package, "content.xml");

        Assert.Contains("xlink:href=\"Pictures/TinyPng.optimized.webp\"", contentXml);
        Assert.True(package.HasEntry("Pictures/TinyPng.optimized.webp"));
        Assert.False(package.HasEntry("Pictures/TinyPng.png"));
        Assert.True(package.Manifest.TryGetValue("Pictures/TinyPng.optimized.webp", out string? mediaType));
        Assert.Equal("image/webp", mediaType);
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
    /// 驗證預綁定段落寫入器可批次追加段落並避免逐段建立 facade。
    /// </summary>
    [Fact]
    public void ParagraphPrebindingWriterAppendsBatchParagraphs()
    {
        using var doc = TextDocument.Create();
        OdfParagraphPrebindingWriter writer = doc.BeginParagraphPrebinding("BodyStyle");

        writer
            .Add("第一段")
            .Add("第二段")
            .Add("第三段");

        Assert.Equal(3, writer.Count);

        using var stream = new MemoryStream();
        doc.SaveToStream(stream);
        stream.Position = 0;

        using TextDocument loaded = TextDocument.Load(stream);
        OdfParagraph[] paragraphs = loaded.Body.Paragraphs.Items.ToArray();

        Assert.Equal(["第一段", "第二段", "第三段"], paragraphs.Select(paragraph => paragraph.TextContent).ToArray());
        Assert.All(paragraphs, paragraph => Assert.Equal("BodyStyle", paragraph.StyleName));
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

    /// <summary>
    /// 驗證文字文件 Fluent builder 可建立年度報告常見結構：目錄、表格、註腳、註解、區段、頁首頁尾與嵌入圖表。
    /// </summary>
    [Fact]
    public void TextDocumentBuilderCreatesComplexAnnualReport()
    {
        using TextDocument document = TextDocument.Builder()
            .WithMetadata(metadata => metadata
                .Title("年度報告")
                .Author("OdfKit")
                .Subject("年度營運成果"))
            .WithStyles(OdfStyleSet.BusinessReport)
            .WithPageSetup(page => page
                .Header("年度報告"))
            .AddCoverPage("年度報告", "2026 年營運成果", "OdfKit", "2026 年")
            .AddTableOfContents("目錄", 2)
            .AddHeading("營運摘要", 2)
            .AddParagraph(paragraph => paragraph
                .Append("營收年增 ")
                .Append("18%", format => format.Bold().Color("#0066CC").BackgroundColor("#FFF2CC"))
                .Append("。")
                .AddFootnote("1", "示範資料，非實際財務數字。")
                .AddComment("reviewer", "請財務團隊確認最終數字。"))
            .AddTable(3, 2, table => table
                .SetCell(1, 1, "季度")
                .SetCell(1, 2, "營收")
                .SetCell(2, 1, "Q1")
                .SetCell(2, 2, "120")
                .SetCell(3, 1, "Q2")
                .SetCell(3, 2, "148"))
            .AddSection("ExecutiveSection", 2, OdfLength.FromCentimeters(0.5), section => section
                .AddParagraph("本區段使用雙欄版面呈現重點。")
                .Protected())
            .AddParagraph(paragraph => paragraph
                .Append("圖表摘要")
                .AddChart(new OdfChartDefinition
                {
                    ChartType = OdfChartType.Bar,
                    Title = "季度營收",
                    DataRange = new OdfCellRange(0, 0, 2, 1, "Data"),
                    HasLegend = true,
                }, OdfLength.FromCentimeters(8), OdfLength.FromCentimeters(5)))
            .AddParagraph(paragraph => paragraph
                .Append("品牌視覺 ")
                .AddImage(CreatePngBytes(), OdfLength.FromCentimeters(2), OdfLength.FromCentimeters(2), "AnnualLogo"))
            .Build();

        using var stream = new MemoryStream();
        document.SaveToStream(stream);
        stream.Position = 0;

        using TextDocument loaded = TextDocument.Load(stream);

        Assert.Equal("年度報告", loaded.Metadata.Title);
        Assert.Contains(loaded.Body.Headings, heading => heading.TextContent == "營運摘要");
        Assert.Contains(loaded.Body.Tables.Items, table => table.RowCount == 3 && table.ColumnCount == 2);
        Assert.Contains(loaded.Body.Sections, section => section.IsProtected);
        Assert.Single(loaded.GetCommentInfos());
        Assert.Contains(loaded.GetPageSetups(), setup => setup.HeaderText == "年度報告");

        stream.Position = 0;
        using OdfPackage package = OdfPackage.Open(stream, leaveOpen: true);
        string contentXml = ReadEntry(package, "content.xml");

        Assert.Contains("draw:object", contentXml);
        Assert.DoesNotContain("table:summary", contentXml);
        Assert.Contains("2026 年營運成果", contentXml);
        Assert.Contains("fo:break-after=\"page\"", contentXml);
        Assert.Contains("#1F4E79", contentXml);
        Assert.Contains("#D9EAF7", contentXml);
        Assert.Contains("fo:font-weight=\"bold\"", contentXml);
        Assert.Contains("#FFF2CC", contentXml);
        Assert.Contains("text:note", contentXml);
        Assert.Contains("xlink:href=\"Pictures/AnnualLogo.png\"", contentXml);
        Assert.True(package.HasEntry("Pictures/AnnualLogo.png"));
        Assert.True(package.HasEntry("Object 1/content.xml"));

        string chartContentXml = ReadEntry(package, "Object 1/content.xml");
        Assert.Contains("季度營收", chartContentXml);
    }

    /// <summary>
    /// 驗證文字文件 builder 可直接套用設計主題並映射到標題與表格首列樣式。
    /// </summary>
    [Fact]
    public void TextDocumentBuilderWithThemeMapsThemeToStyles()
    {
        var theme = new OdfDesignTheme
        {
            StrokeColor = "#123456",
            ConnectorColor = "#654321",
        }.WithAccentFillColors("#ABCDEF");

        using TextDocument document = TextDocument.Builder()
            .WithTheme(theme)
            .AddHeading("主題標題", 1)
            .AddTable(1, 2, table => table
                .SetCell(1, 1, "欄一")
                .SetCell(1, 2, "欄二"))
            .Build();

        using var stream = new MemoryStream();
        document.SaveToStream(stream);
        stream.Position = 0;

        using OdfPackage package = OdfPackage.Open(stream, leaveOpen: true);
        string contentXml = ReadEntry(package, "content.xml");

        Assert.Contains("#123456", contentXml);
        Assert.Contains("#ABCDEF", contentXml);
        Assert.Contains("#654321", contentXml);
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

    private static byte[] CreateWebpBytes()
    {
        return
        [
            0x52, 0x49, 0x46, 0x46,
            0x04, 0x00, 0x00, 0x00,
            0x57, 0x45, 0x42, 0x50,
            0x56, 0x50, 0x38, 0x20
        ];
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

    /// <summary>
    /// 驗證可在段落中插入文獻標記並 round-trip。
    /// </summary>
    [Fact]
    public void AddBibliographyMark_PersistsAttributesInOdt()
    {
        using var doc = TextDocument.Create();
        OdfParagraph para = doc.AddParagraph("依據文獻一所述");
        OdfBibliographyMark mark = para.AddBibliographyMark("ref1", "article", "Ada Lovelace", "分析機之上的筆記", "1843");

        Assert.Equal("ref1", mark.Identifier);
        Assert.Equal("article", mark.BibliographyType);
        Assert.Equal("Ada Lovelace", mark.Author);
        Assert.Equal("分析機之上的筆記", mark.Title);
        Assert.Equal("1843", mark.Year);

        using var ms = new MemoryStream();
        doc.SaveToStream(ms);
        ms.Position = 0;

        using OdfPackage package = OdfPackage.Open(ms, leaveOpen: true);
        string contentXml = ReadEntry(package, "content.xml");
        Assert.Contains("text:bibliography-mark", contentXml);
        Assert.Contains("text:identifier=\"ref1\"", contentXml);
        Assert.Contains("text:author=\"Ada Lovelace\"", contentXml);
    }

    /// <summary>
    /// 驗證可在段落中插入定位點（Tab）字元並 round-trip。
    /// </summary>
    [Fact]
    public void AddTab_PersistsTabElementInOdt()
    {
        using var doc = TextDocument.Create();
        OdfParagraph para = doc.AddParagraph("前段");
        para.AddTextRun("左");
        para.AddTab();
        para.AddTextRun("右");

        using var ms = new MemoryStream();
        doc.SaveToStream(ms);
        ms.Position = 0;

        // package 必須在重新使用 ms 前釋放：OdfPackage.Open 對啟用延遲載入的
        // 串流會啟動背景預讀工作(PreloadTask)讀取 ms；Dispose() 會同步等待該工作
        // 完成後才回傳，若不先釋放就重設 ms.Position 供第二個 OdfPackage/TextDocument
        // 讀取，兩者會競爭同一個 MemoryStream 的游標，導致間歇性
        // "A local file header is corrupt" 例外。
        using (OdfPackage package = OdfPackage.Open(ms, leaveOpen: true))
        {
            string contentXml = ReadEntry(package, "content.xml");
            Assert.Contains("<text:tab", contentXml);
        }

        ms.Position = 0;
        using TextDocument loaded = TextDocument.Load(ms);
        OdfParagraph loadedPara = loaded.Body.Paragraphs.Single(p => p.TextContent.Contains("左", StringComparison.Ordinal));
        Assert.Contains(loadedPara.Node.Children, child => child.LocalName == "tab");
    }

    /// <summary>
    /// 驗證 <see cref="OdfTextRun.WithFontName"/> 可同時設定西文、東亞與複雜文字字型並 round-trip。
    /// </summary>
    [Fact]
    public void WithFontName_SetsWesternAsianAndComplexFontsAndPersists()
    {
        using var doc = TextDocument.Create();
        OdfParagraph para = doc.AddParagraph();
        OdfTextRun run = para.AddTextRun("多語字型文字").WithFontName("Calibri", "微軟正黑體", "Arial");

        using var ms = new MemoryStream();
        doc.SaveToStream(ms);
        ms.Position = 0;

        using OdfPackage package = OdfPackage.Open(ms, leaveOpen: true);
        string contentXml = ReadEntry(package, "content.xml");
        Assert.Contains("Calibri", contentXml);
        Assert.Contains("微軟正黑體", contentXml);
        Assert.Contains("Arial", contentXml);
    }

    /// <summary>
    /// 驗證 <see cref="TextRunFormattingBuilder.Underline"/> 經由段落 Fluent builder 套用後可正確 round-trip。
    /// </summary>
    [Fact]
    public void TextParagraphBuilderAppend_UnderlineFormattingPersists()
    {
        using TextDocument document = TextDocument.Builder()
            .AddParagraph(paragraph => paragraph
                .Append("一般文字 ")
                .Append("底線文字", format => format.Underline()))
            .Build();

        using var stream = new MemoryStream();
        document.SaveToStream(stream);
        stream.Position = 0;

        using TextDocument loaded = TextDocument.Load(stream);
        OdfParagraph loadedPara = loaded.Body.Paragraphs.Single();
        OdfTextRun underlineRun = loadedPara.Runs.Single(r => r.Text == "底線文字");
        Assert.True(underlineRun.IsUnderline);

        stream.Position = 0;
        using OdfPackage package = OdfPackage.Open(stream, leaveOpen: true);
        string contentXml = ReadEntry(package, "content.xml");
        Assert.Contains("text-underline-style", contentXml);
    }

    /// <summary>
    /// 驗證文字 Fluent builder 可建立章節並通過 ODF 1.4 Extended 驗證。
    /// </summary>
    [Fact]
    public void TextDocumentBuilderSectionPassesOdf14ExtendedValidation()
    {
        using TextDocument document = TextDocument.Builder()
            .WithMetadata(metadata => metadata.Title("年度報告"))
            .AddSection("ExecutiveSection", 2, 1.2.Cm(), section => section
                .AddHeading("營運摘要", 2)
                .AddParagraph("本章節彙整主要營運指標。"))
            .Build();

        using var stream = new MemoryStream();
        document.SaveToStream(stream);
        stream.Position = 0;

        OdfValidationReport report = OdfValidator.Validate(
            stream,
            "report.odt",
            OdfComplianceProfiles.OasisOdf14Extended);

        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Issues));
        Assert.Equal(OdfDocumentKind.Text, report.DocumentKind);
    }

    /// <summary>
    /// 驗證 <see cref="OdfSubDocumentReference"/> record 衍生方法（相等性、解構、複製）的正確性。
    /// </summary>
    [Fact]
    public void OdfSubDocumentReference_RecordSemanticsAreCorrect()
    {
        var reference = new OdfSubDocumentReference("Chapter1", "chapter1.odt");
        var sameValue = new OdfSubDocumentReference("Chapter1", "chapter1.odt");
        var differentValue = new OdfSubDocumentReference("Chapter2", "chapter2.odt");

        // Equals 與 GetHashCode：依值相等而非參考相等
        Assert.Equal(reference, sameValue);
        Assert.Equal(reference.GetHashCode(), sameValue.GetHashCode());
        Assert.NotEqual(reference, differentValue);

        // Deconstruct：可解構為個別變數
        (string sectionName, string href, string actuate) = reference;
        Assert.Equal("Chapter1", sectionName);
        Assert.Equal("chapter1.odt", href);
        Assert.Equal("onLoad", actuate);

        // <Clone>$（With 表達式）：產生具有部分新值的複本，且不影響原始執行個體
        OdfSubDocumentReference clone = reference with { Href = "chapter1-revised.odt" };
        Assert.Equal("Chapter1", clone.SectionName);
        Assert.Equal("chapter1-revised.odt", clone.Href);
        Assert.Equal("chapter1.odt", reference.Href);
        Assert.NotEqual(reference, clone);
    }
}
