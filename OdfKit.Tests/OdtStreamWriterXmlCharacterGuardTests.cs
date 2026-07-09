using System;
using System.Collections.Generic;
using System.IO;
using Xunit;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Text;

namespace OdfKit.Tests;

/// <summary>
/// OdtStreamWriter 關閉 CheckCharacters 後之輕量字元防線邊界測試。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Boundary)]
public class OdtStreamWriterXmlCharacterGuardTests
{
    [Theory]
    [InlineData((char)0x00)]
    [InlineData((char)0x01)]
    [InlineData((char)0x0B)]
    [InlineData((char)0xFFFE)]
    public void AddParagraph_WithInvalidXmlCharacter_ThrowsLocalizedArgumentException(char invalid)
    {
        using var ms = new MemoryStream();
        using var writer = new OdtStreamWriter(ms);

        var ex = Assert.Throws<ArgumentException>(() => writer.AddParagraph("bad" + invalid));

        Assert.Equal("text", ex.ParamName);
        string expected = OdfLocalizer.GetMessage(
            "Err_OdfStreamWriter_InvalidXmlCharacter", $"U+{(int)invalid:X4}", 3);
        Assert.Contains(expected, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddParagraph_StyleName_WithInvalidXmlCharacter_Throws()
    {
        using var ms = new MemoryStream();
        using var writer = new OdtStreamWriter(ms);

        var ex = Assert.Throws<ArgumentException>(() => writer.AddParagraph("ok", "st" + (char)0x01));
        Assert.Equal("styleName", ex.ParamName);
    }

    [Fact]
    public void AddParagraph_WithSurrogatePairEmoji_RoundTrips()
    {
        string emojiText = "測試 " + char.ConvertFromUtf32(0x1F600);
        using var ms = new MemoryStream();
        using (var writer = new OdtStreamWriter(ms))
        {
            writer.AddParagraph(emojiText);
        }

        ms.Position = 0;
        using var package = OdfPackage.Open(ms);
        using var s = package.GetEntryStream("content.xml");
        OdfNode root = OdfXmlReader.Parse(s);
        Assert.Contains(FindNodesByLocalName(root, "p"), p => p.TextContent == emojiText);
    }

    [Fact]
    public void AddParagraph_AfterGuardException_WriterRemainsUsable()
    {
        using var ms = new MemoryStream();
        using (var writer = new OdtStreamWriter(ms))
        {
            Assert.Throws<ArgumentException>(() => writer.AddParagraph("bad" + (char)0x01));
            writer.AddParagraph("recovered");
        }

        ms.Position = 0;
        using var package = OdfPackage.Open(ms);
        using var s = package.GetEntryStream("content.xml");
        OdfNode root = OdfXmlReader.Parse(s);
        Assert.Contains(FindNodesByLocalName(root, "p"), p => p.TextContent == "recovered");
    }

    private static List<OdfNode> FindNodesByLocalName(OdfNode root, string localName)
    {
        var result = new List<OdfNode>();
        Collect(root, localName, result);
        return result;
    }

    private static void Collect(OdfNode node, string localName, List<OdfNode> result)
    {
        if (node.LocalName == localName)
            result.Add(node);
        foreach (OdfNode child in node.Children)
            Collect(child, localName, result);
    }
}
