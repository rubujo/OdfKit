using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Export;
using OdfKit.Text;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 以 LibreOffice 等真實來源產生的 RTF、Markdown 結構鎖定匯入器與寫出層的行為（這些情況合成測試沒有涵蓋）。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Regression)]
public sealed class RealWorldImportTests
{
    // 摘自 LibreOffice 的 RTF 輸出結構：標頭有 \stylesheet 與含 \'01 控制字元的 \listtable，段落前有 \listtext 標籤。
    private const string LibreOfficeStyleRtf =
        @"{\rtf1\ansi\deff0{\fonttbl{\f0 Liberation Serif;}}" +
        @"{\stylesheet{\s0 Normal;}{\s19 List Paragraph;}}" +
        @"{\*\listtable{\list\listtemplateid1{\listlevel\levelnfc255\leveljc0\levelstartat1\levelfollow2{\leveltext \'01\'00;}{\levelnumbers\'01;}\fi0\li0}\listid1}}" +
        @"{\*\listoverridetable{\listoverride\listid1\listoverridecount0\ls1}}" +
        @"{\*\generator LibreOffice/26.2$Windows_X86_64}" +
        @"\pard\plain Before the list\par" +
        @"\pard\plain\s19{\listtext\pard\plain 舦\'95\tab}\ilvl0\ls1 First item\par" +
        @"\pard\plain\s19{\listtext\pard\plain 舦\'95\tab}\ilvl0\ls1 Second item\par" +
        @"}";

    /// <summary>
    /// 驗證 RTF 匯入略過標頭區的資訊群組（樣式表、清單定義、清單標籤、產生器）：
    /// 修正前清單定義裡的 <c>\'01</c> 被當成文字寫進文件，儲存時因非法 XML 字元（U+0001）失敗。
    /// </summary>
    [Fact]
    public void RtfImportSkipsHeaderGroupsAndSavesCleanly()
    {
        using TextDocument document = OdfRtfImporter.Import(LibreOfficeStyleRtf);
        string[] texts = document.Body.Paragraphs.Select(paragraph => paragraph.TextContent.Trim()).Where(text => text.Length > 0).ToArray();

        Assert.Contains("Before the list", texts);
        Assert.Contains(texts, text => text.EndsWith("First item", StringComparison.Ordinal));
        Assert.DoesNotContain(texts, text => text.Any(character => character < ' ' && character != '\t'));
        Assert.DoesNotContain(texts, text => text.Contains("Normal", StringComparison.Ordinal) || text.Contains("LibreOffice", StringComparison.Ordinal));

        using var stream = new MemoryStream();
        document.SaveToStream(stream);
        Assert.True(stream.Length > 0);
    }

    /// <summary>
    /// 驗證寫出層略過 XML 1.0 不允許的字元：控制字元放進文字或屬性值時，儲存不會擲出例外，
    /// 重新載入後只少了那個字元，其餘文字完整。
    /// </summary>
    [Fact]
    public void SavingTextWithInvalidXmlCharactersDropsOnlyThoseCharacters()
    {
        using var document = TextDocument.Create();
        OdfParagraph paragraph = document.AddParagraph("前\u0001中\u0008後😀尾");
        paragraph.Node.SetAttribute("style-name", OdfNamespaces.Text, "名\u0002稱", "text");

        using var stream = new MemoryStream();
        document.SaveToStream(stream);
        stream.Position = 0;

        using var reloaded = (TextDocument)OdfDocument.Load(stream, "invalid-characters.odt");
        OdfParagraph first = reloaded.Body.Paragraphs.First();
        Assert.Equal("前中後😀尾", first.TextContent);
        Assert.Equal("名稱", first.Node.GetAttribute("style-name", OdfNamespaces.Text));
    }

    /// <summary>
    /// 驗證 Markdown 匯入保留行內程式碼與程式碼區塊（含圍欄、縮排與空行）並使用等寬字型。
    /// 修正前兩者的文字整個消失。
    /// </summary>
    [Fact]
    public void MarkdownImportKeepsInlineCodeAndCodeBlocks()
    {
        const string markdown = "文字 `行內程式碼Alpha` 結尾\n\n```csharp\nvar x = 1;\n\n    縮排Bravo\n```\n\n- 清單\n\n      清單內程式碼Charlie\n";
        using TextDocument document = OdfMarkdownImporter.Import(markdown);
        XElement content = XElement.Parse(DocumentContentXml(document));
        XNamespace text = OdfNamespaces.Text;
        string[] paragraphs = content.Descendants(text + "p").Select(paragraph => paragraph.Value).ToArray();

        Assert.Contains("文字行內程式碼Alpha結尾", paragraphs);
        Assert.Contains("var x = 1;", paragraphs);
        Assert.Contains(paragraphs, line => line.Contains("縮排Bravo", StringComparison.Ordinal));
        Assert.Contains(paragraphs, line => line.Contains("清單內程式碼Charlie", StringComparison.Ordinal));

        // 程式碼使用等寬字型：行內程式碼與程式碼區塊的文字都帶有該字型的樣式。
        string xml = DocumentContentXml(document);
        Assert.Contains("Liberation Mono", xml, StringComparison.Ordinal);
    }

    private static string DocumentContentXml(OdfDocument document)
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
