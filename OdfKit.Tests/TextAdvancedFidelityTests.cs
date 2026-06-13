using System;
using System.IO;
using OdfKit.Core;
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

        document.GetDefaultPageSetup().WritingMode = "tb-rl";
        OdfHeading heading = document.Body.Headings.Add("進階文字", 1);
        OdfParagraph paragraph = document.Body.Paragraphs.Add("漢字本文");
        paragraph.WritingMode = "tb-rl";
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
}
