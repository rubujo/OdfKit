using System;
using System.IO;
using OdfKit.Core;
using OdfKit.Styles;
using OdfKit.Text;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 鎖定 ODT 進階內容的保真使用者故事。
/// </summary>
public class TextAdvancedFidelityTests
{
    /// <summary>
    /// 驗證註解、欄位、索引、ruby、直排與追蹤修訂可一起保存。
    /// </summary>
    [Fact]
    public void AdvancedTextFeaturesRoundTripTogether()
    {
        using var document = TextDocument.Create();
        document.TrackedChanges = true;

        document.GetDefaultPageSetup().WritingMode = OdfWritingMode.TbRl;
        OdfHeading heading = document.Body.Headings.Add("進階文字", 1);
        OdfParagraph paragraph = document.Body.Paragraphs.Add("漢字本文");
        paragraph.WritingMode = OdfWritingMode.TbRl;
        document.AddDateField(paragraph);
        document.AddPageNumberField(paragraph);
        document.AddComment(
            paragraph,
            new OdfComment("作者", "保留註解", new DateTime(2026, 6, 13, 0, 0, 0, DateTimeKind.Utc), "comment-1"));
        OdfRuby ruby = document.AddRuby(paragraph, "漢字", "かんじ");
        ruby.RubyPosition = "above";
        ruby.RubyAlign = "distribute-letter";
        document.AddAlphabeticalIndexMark(paragraph, "漢字");
        document.AddTableOfContents("目錄", 2);
        document.AddAlphabeticalIndex("索引");
        heading.StyleName = "Heading_20_1";

        using var stream = new MemoryStream();
        document.SaveToStream(stream);
        stream.Position = 0;

        using TextDocument loaded = TextDocument.Load(stream);
        using var saved = new MemoryStream();
        loaded.SaveToStream(saved);
        saved.Position = 0;
        using OdfPackage package = OdfPackage.Open(saved, leaveOpen: true);

        string contentXml = ReadEntry(package, "content.xml");
        string stylesXml = ReadEntry(package, "styles.xml");
        string settingsXml = ReadEntry(package, "settings.xml");

        Assert.Contains("office:annotation", contentXml);
        Assert.Contains("保留註解", contentXml);
        Assert.Contains("text:date", contentXml);
        Assert.Contains("text:page-number", contentXml);
        Assert.Contains("text:table-of-content", contentXml);
        Assert.Contains("text:table-of-content-entry-template", contentXml);
        Assert.Contains("text:index-entry-page-number", contentXml);
        Assert.Contains("text:index-entry-tab-stop", contentXml);
        Assert.Contains("style:type=\"right\"", contentXml);
        Assert.Contains("style:leader-char=\".\"", contentXml);
        Assert.Contains("text:alphabetical-index", contentXml);
        Assert.Contains("text:ruby", contentXml);
        Assert.Contains("text:change-start", contentXml);
        Assert.Contains("style:writing-mode=\"tb-rl\"", stylesXml);
        Assert.Contains("UpdateFieldsWhenOpening", settingsXml);
    }

    private static string ReadEntry(OdfPackage package, string path)
    {
        using Stream stream = package.GetEntryStream(path);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
    /// <summary>
    /// 驗證段落 Runs 與 TextContent 之間的裂變同步與互斥邏輯。
    /// </summary>
    [Fact]
    public void OdfParagraphRunsFissionSyncTest()
    {
        using var document = TextDocument.Create();
        OdfParagraph paragraph = document.Body.Paragraphs.Add("起初只有直屬文字");

        // 尚未存取 Runs 前，確認 DOM 結構中只有 text 節點，沒有 span
        Assert.Contains("起初只有直屬文字", paragraph.Node.TextContent);
        Assert.DoesNotContain("span", paragraph.Node.Children.Select(c => c.LocalName));

        // 存取 Runs 屬性，觸發裂變同步（Fission）
        var runsList = paragraph.Runs.ToList();
        Assert.Single(runsList);
        Assert.Equal("起初只有直屬文字", runsList[0].Text);
        Assert.Contains("span", paragraph.Node.Children.Select(c => c.LocalName));

        // 呼叫 Fluent API 進行 Runs 樣式設定
        runsList[0].WithBold(true).WithItalic(true).WithFontSize("14pt").WithColor("#FF0000");
        Assert.True(runsList[0].IsBold);
        Assert.True(runsList[0].IsItalic);
        Assert.Equal("14pt", runsList[0].FontSize);
        Assert.Equal("#FF0000", runsList[0].Color);

        // 新增另一個 Run
        var run2 = paragraph.AddTextRun("第二段文字").WithBold(false);
        Assert.Equal(2, paragraph.Runs.Count());
        Assert.False(run2.IsBold);

        // 設定 TextContent 會清除 span 回復為單一文字節點
        paragraph.TextContent = "覆寫成純文字";
        Assert.Equal("覆寫成純文字", paragraph.TextContent);
        Assert.DoesNotContain("span", paragraph.Node.Children.Select(c => c.LocalName));

        // 再次存取 Runs 重新觸發裂變
        Assert.Single(paragraph.Runs);
        Assert.Equal("覆寫成純文字", paragraph.Runs.First().Text);

        // 測試清除所有 Runs
        paragraph.ClearRuns();
        Assert.Empty(paragraph.Runs);
    }

    /// <summary>
    /// 驗證段落樣式代理 OdfParagraphStyleProxy 屬性讀寫保真度。
    /// </summary>
    [Fact]
    public void OdfParagraphStyleProxyTest()
    {
        using var document = TextDocument.Create();
        OdfParagraph paragraph = document.Body.Paragraphs.Add("樣式測試段落");

        paragraph.Style.Alignment = "center";
        paragraph.Style.LineSpacing = "150%";
        paragraph.Style.MarginLeft = "1.5cm";
        paragraph.Style.MarginRight = "1.0cm";

        Assert.Equal("center", paragraph.Style.Alignment);
        Assert.Equal("150%", paragraph.Style.LineSpacing);
        Assert.Equal("1.5cm", paragraph.Style.MarginLeft);
        Assert.Equal("1.0cm", paragraph.Style.MarginRight);
    }

    /// <summary>
    /// 驗證 ODT Section 唯讀/密碼保護與雜湊驗證 round-trip 正確性。
    /// </summary>
    [Fact]
    public void OdfSectionProtectionTest()
    {
        using var document = TextDocument.Create();
        OdfSection section = document.AddSection("ProtectedSection", 1, OdfLength.FromCentimeters(0));

        Assert.False(section.IsProtected);

        // 保護區段
        section.Protect("SecretPass123");
        Assert.True(section.IsProtected);

        // 驗證密碼
        Assert.True(section.VerifyPassword("SecretPass123"));
        Assert.False(section.VerifyPassword("WrongPass"));

        // 存檔與載入驗證
        using var stream = new MemoryStream();
        document.SaveToStream(stream);
        stream.Position = 0;

        using var loaded = TextDocument.Load(stream);
        var loadedSection = loaded.Body.Sections.First(s => s.Node.GetAttribute("name", OdfNamespaces.Text) == "ProtectedSection");

        Assert.True(loadedSection.IsProtected);
        Assert.True(loadedSection.VerifyPassword("SecretPass123"));
        Assert.False(loadedSection.VerifyPassword("WrongPass"));

        // 解除保護
        Assert.False(loadedSection.TryUnprotect("WrongPass"));
        Assert.True(loadedSection.IsProtected);

        Assert.True(loadedSection.TryUnprotect("SecretPass123"));
        Assert.False(loadedSection.IsProtected);
    }

    /// <summary>
    /// 驗證自動更新設定項目（UpdateFieldsWhenOpening、LinkUpdateMode、AutoCalculate）在 settings.xml 中正確生成。
    /// </summary>
    [Fact]
    public void DocumentSettingsAutoUpdateFeaturesTest()
    {
        using var document = TextDocument.Create();

        // 設定
        document.LinkUpdateMode = 1; // Always
        document.AutoCalculate = false;
        document.MutationContext.SetUpdateFieldsWhenOpening(true);


        using var stream = new MemoryStream();
        document.SaveToStream(stream);
        stream.Position = 0;

        using var loaded = TextDocument.Load(stream);

        // 驗證讀回值
        Assert.Equal(1, loaded.LinkUpdateMode);
        Assert.False(loaded.AutoCalculate);

        using var saved = new MemoryStream();
        loaded.SaveToStream(saved);
        saved.Position = 0;
        using OdfPackage package = OdfPackage.Open(saved, leaveOpen: true);
        string settingsXml = ReadEntry(package, "settings.xml");

        // 驗證正確的 XML 命名空間與結構
        Assert.Contains("config:name=\"ooo:configuration-settings\"", settingsXml);
        Assert.Contains("config:name=\"UpdateFieldsWhenOpening\"", settingsXml);
        Assert.Contains("config:name=\"LinkUpdateMode\" config:type=\"short\">1</config:", settingsXml);
        Assert.Contains("config:name=\"ooo:document-settings\"", settingsXml);
        Assert.Contains("config:name=\"AutoCalculate\" config:type=\"boolean\">false</config:", settingsXml);
    }
}

