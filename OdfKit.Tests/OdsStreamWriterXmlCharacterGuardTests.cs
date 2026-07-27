using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Spreadsheet;

namespace OdfKit.Tests;

/// <summary>
/// OdsStreamWriter 關閉 XmlWriter CheckCharacters 後之輕量字元防線邊界測試：
/// 控制字元拒絕、tab/LF/CR 放行、合法代理對放行、孤立代理拒絕、空字串行為不變。
/// 為避免原始碼夾帶不可見字元，非法字元一律以 (char)0xXX 常數運算式表示。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Boundary)]
public class OdsStreamWriterXmlCharacterGuardTests
{
    [Theory]
    [InlineData((char)0x00)]
    [InlineData((char)0x01)]
    [InlineData((char)0x08)]
    [InlineData((char)0x0B)]
    [InlineData((char)0x0C)]
    [InlineData((char)0x0E)]
    [InlineData((char)0x1F)]
    [InlineData((char)0xFFFE)]
    [InlineData((char)0xFFFF)]
    public void WriteCellStringWithInvalidXmlCharacterThrowsLocalizedArgumentException(char invalid)
    {
        using var ms = new MemoryStream();
        using var writer = new OdsStreamWriter(ms);
        writer.WriteStartSheet("Sheet1");
        writer.WriteStartRow();

        var ex = Assert.Throws<ArgumentException>(() => writer.WriteCell("bad" + invalid));

        Assert.Equal("value", ex.ParamName);
        string expected = OdfLocalizer.GetMessage(
            "Err_OdfStreamWriter_InvalidXmlCharacter", $"U+{(int)invalid:X4}", 3);
        Assert.Contains(expected, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteCellStringWithLoneHighSurrogateThrows()
    {
        using var ms = new MemoryStream();
        using var writer = new OdsStreamWriter(ms);
        writer.WriteStartSheet("Sheet1");
        writer.WriteStartRow();

        // 高代理落在字串結尾（缺少後續低代理）。
        var exAtEnd = Assert.Throws<ArgumentException>(() => writer.WriteCell("x" + (char)0xD800));
        Assert.Equal("value", exAtEnd.ParamName);

        // 高代理後面接非低代理字元。
        var exInMiddle = Assert.Throws<ArgumentException>(() => writer.WriteCell("x" + (char)0xD800 + "y"));
        Assert.Equal("value", exInMiddle.ParamName);
    }

    [Fact]
    public void WriteCellStringWithLoneLowSurrogateThrows()
    {
        using var ms = new MemoryStream();
        using var writer = new OdsStreamWriter(ms);
        writer.WriteStartSheet("Sheet1");
        writer.WriteStartRow();

        var ex = Assert.Throws<ArgumentException>(() => writer.WriteCell("x" + (char)0xDC00 + "y"));
        Assert.Equal("value", ex.ParamName);
    }

    [Fact]
    public void WriteCellSpanWithInvalidXmlCharacterThrows()
    {
        using var ms = new MemoryStream();
        using var writer = new OdsStreamWriter(ms);
        writer.WriteStartSheet("Sheet1");
        writer.WriteStartRow();

        string bad = "bad" + (char)0x01;
        var ex = Assert.Throws<ArgumentException>(() => writer.WriteCell(bad.AsSpan()));
        Assert.Equal("value", ex.ParamName);
    }

    [Fact]
    public void WriteCellStringWithTabLfCrIsAllowedAndDocumentStaysWellFormed()
    {
        using var ms = new MemoryStream();
        using (var writer = new OdsStreamWriter(ms))
        {
            writer.WriteStartSheet("Sheet1");
            writer.WriteStartRow();
            writer.WriteCell("a\tb\nc\rd");
            writer.WriteEndRow();
            writer.WriteEndSheet();
        }

        ms.Position = 0;
        using var package = OdfPackage.Open(ms);
        using var s = package.GetEntryStream("content.xml");
        OdfNode root = OdfXmlReader.Parse(s);
        List<OdfNode> paragraphs = FindNodesByLocalName(root, "p");
        // tab 不受 XML 行尾規範化影響，必定原樣保留。
        Assert.Contains(paragraphs, p => p.TextContent.Contains("a\tb", StringComparison.Ordinal));
    }

    [Fact]
    public void WriteCellStringWithSurrogatePairEmojiRoundTrips()
    {
        // U+1F600（GRINNING FACE）以合法代理對組成，必須放行且完整往返。
        string emojiText = "分數 " + char.ConvertFromUtf32(0x1F600) + " 滿分";
        using var ms = new MemoryStream();
        using (var writer = new OdsStreamWriter(ms))
        {
            writer.WriteStartSheet("Sheet1");
            writer.WriteStartRow();
            writer.WriteCell(emojiText);
            writer.WriteEndRow();
            writer.WriteEndSheet();
        }

        ms.Position = 0;
        using var package = OdfPackage.Open(ms);
        using var s = package.GetEntryStream("content.xml");
        OdfNode root = OdfXmlReader.Parse(s);
        List<OdfNode> paragraphs = FindNodesByLocalName(root, "p");
        Assert.Contains(paragraphs, p => p.TextContent == emojiText);
    }

    [Fact]
    public void WriteCellStringWithReplacementCharacterUpperBoundIsAllowed()
    {
        using var ms = new MemoryStream();
        using var writer = new OdsStreamWriter(ms);
        writer.WriteStartSheet("Sheet1");
        writer.WriteStartRow();

        // U+FFFD 是 BMP 合法區間 [#xE000, #xFFFD] 的上界，必須放行。
        writer.WriteCell("x" + (char)0xFFFD);
        writer.WriteEndRow();
        writer.WriteEndSheet();
    }

    [Fact]
    public void WriteCellStringEmptyAndNullBehaviorUnchanged()
    {
        using var ms = new MemoryStream();
        using (var writer = new OdsStreamWriter(ms))
        {
            writer.WriteStartSheet("Sheet1");
            writer.WriteStartRow();
            writer.WriteCell(string.Empty);
            writer.WriteCell((string)null!);
            writer.WriteEndRow();
            writer.WriteEndSheet();
        }

        ms.Position = 0;
        using var package = OdfPackage.Open(ms);
        using var s = package.GetEntryStream("content.xml");
        OdfNode root = OdfXmlReader.Parse(s);
        List<OdfNode> cells = FindNodesByLocalName(root, "table-cell");
        Assert.Equal(2, cells.Count);
        // 空字串與 null 皆輸出空的 text:p，不擲出例外。
        Assert.All(cells, cell => Assert.Equal(string.Empty, cell.TextContent));
    }

    [Fact]
    public void WriteCellStyleNameWithInvalidXmlCharacterThrows()
    {
        using var ms = new MemoryStream();
        using var writer = new OdsStreamWriter(ms);
        writer.WriteStartSheet("Sheet1");
        writer.WriteStartRow();

        string badStyle = "style" + (char)0x01;
        var stringEx = Assert.Throws<ArgumentException>(() => writer.WriteCell("ok", badStyle));
        Assert.Equal("styleName", stringEx.ParamName);

        var doubleEx = Assert.Throws<ArgumentException>(() => writer.WriteCell(1.5, badStyle));
        Assert.Equal("styleName", doubleEx.ParamName);
    }

    [Fact]
    public void WriteStartSheetSheetNameWithInvalidXmlCharacterThrows()
    {
        using var ms = new MemoryStream();
        using var writer = new OdsStreamWriter(ms);

        var ex = Assert.Throws<ArgumentException>(() => writer.WriteStartSheet("Sheet" + (char)0x0B));
        Assert.Equal("sheetName", ex.ParamName);
    }

    [Fact]
    public void SwitchToSheetBufferedCellWriteWithInvalidXmlCharacterThrows()
    {
        using var ms = new MemoryStream();
        using var writer = new OdsStreamWriter(ms);
        writer.SwitchToSheet("Buffered");
        writer.WriteStartRow();

        // 緩衝工作表的 XmlWriter 同樣關閉 CheckCharacters，防線必須在此路徑生效。
        var ex = Assert.Throws<ArgumentException>(() => writer.WriteCell("bad" + (char)0x02));
        Assert.Equal("value", ex.ParamName);
    }

    [Fact]
    public async Task WriteSheetsAsyncSheetWriterStringCellWithInvalidXmlCharacterThrows()
    {
        using var ms = new MemoryStream();
        using var writer = new OdsStreamWriter(ms);
        var jobs = new[]
        {
            new OdsSheetWriteJob("Jobs", (sheetWriter, _) =>
            {
                sheetWriter.WriteStartRow();
                sheetWriter.WriteCell("bad" + (char)0x01);
                return Task.CompletedTask;
            })
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => writer.WriteSheetsAsync(jobs, maxConcurrency: 0, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public void WriteCellAfterGuardExceptionWriterRemainsUsable()
    {
        using var ms = new MemoryStream();
        using (var writer = new OdsStreamWriter(ms))
        {
            writer.WriteStartSheet("Sheet1");
            writer.WriteStartRow();
            Assert.Throws<ArgumentException>(() => writer.WriteCell("bad" + (char)0x01));

            // 防線在寫入任何元素前擲出，因此文件結構不會殘留半開標籤，可繼續寫入。
            writer.WriteCell("recovered");
            writer.WriteEndRow();
            writer.WriteEndSheet();
        }

        ms.Position = 0;
        using var package = OdfPackage.Open(ms);
        using var s = package.GetEntryStream("content.xml");
        OdfNode root = OdfXmlReader.Parse(s);
        List<OdfNode> paragraphs = FindNodesByLocalName(root, "p");
        Assert.Contains(paragraphs, p => p.TextContent == "recovered");
    }

    private static List<OdfNode> FindNodesByLocalName(OdfNode root, string localName)
    {
        var result = new List<OdfNode>();
        CollectNodes(root, localName, result);
        return result;
    }

    private static void CollectNodes(OdfNode node, string localName, List<OdfNode> result)
    {
        if (node.LocalName == localName)
        {
            result.Add(node);
        }

        foreach (OdfNode child in node.Children)
        {
            CollectNodes(child, localName, result);
        }
    }
}
