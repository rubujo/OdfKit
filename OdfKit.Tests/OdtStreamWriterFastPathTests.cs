using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Xunit;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Text;

namespace OdfKit.Tests;

/// <summary>
/// OdtStreamWriter 原始 XML 快速路徑的輸出等價測試：
/// 轉義、換行改寫、emoji／長文、清單與 WriteNode 混用。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Boundary)]
public class OdtStreamWriterFastPathTests
{
    [Fact]
    public void AddParagraph_TextEscaping_MatchesXmlWriterBehavior()
    {
        string xml = WriteContent(writer =>
        {
            writer.AddParagraph("&<>\"'");
            writer.AddParagraph("a\tb\nc\rd");
            writer.AddParagraph("crlf\r\nend");
        });

        Assert.Contains("<text:p>&amp;&lt;&gt;\"'</text:p>", xml, StringComparison.Ordinal);
        Assert.Contains("<text:p>a\tb\r\nc\r\nd</text:p>", xml, StringComparison.Ordinal);
        Assert.Contains("<text:p>crlf\r\nend</text:p>", xml, StringComparison.Ordinal);
    }

    [Fact]
    public void AddParagraph_StyleNameEscaping_MatchesXmlWriterBehavior()
    {
        string xml = WriteContent(writer =>
        {
            writer.AddParagraph("body", "st&y<le\"x\ty");
            writer.AddParagraph("body2", "n\nr\rt>");
        });

        Assert.Contains("text:style-name=\"st&amp;y&lt;le&quot;x&#x9;y\"", xml, StringComparison.Ordinal);
        Assert.Contains("text:style-name=\"n&#xA;r&#xD;t&gt;\"", xml, StringComparison.Ordinal);
    }

    [Fact]
    public void AddHeadingAndList_WriteExpectedStructure()
    {
        string xml = WriteContent(writer =>
        {
            writer.AddHeading("章節一", 1);
            writer.BeginList();
            writer.AddListItem("項目 & 甲");
            writer.AddListItem("項目乙");
            writer.EndList();
            writer.AddParagraph("結尾");
        });

        Assert.Contains("<text:h text:outline-level=\"1\">章節一</text:h>", xml, StringComparison.Ordinal);
        Assert.Contains("<text:list>", xml, StringComparison.Ordinal);
        Assert.Contains("<text:list-item><text:p>項目 &amp; 甲</text:p></text:list-item>", xml, StringComparison.Ordinal);
        Assert.Contains("<text:p>結尾</text:p>", xml, StringComparison.Ordinal);
    }

    [Fact]
    public void AddParagraph_EmojiAndLongText_RoundTripsThroughParser()
    {
        string emojiText = "分數 " + char.ConvertFromUtf32(0x1F600) + " 滿分";
        var builder = new StringBuilder(24 * 1024);
        for (int i = 0; i < 2000; i++)
        {
            builder.Append("段落-").Append(i).Append(char.ConvertFromUtf32(0x1F600));
        }

        string longText = builder.ToString();
        string xml = WriteContent(writer =>
        {
            writer.AddParagraph(emojiText);
            writer.AddParagraph(longText);
        });

        OdfNode root = Parse(xml);
        List<OdfNode> paragraphs = FindNodesByLocalName(root, "p");
        Assert.Contains(paragraphs, p => p.TextContent == emojiText);
        Assert.Contains(paragraphs, p => p.TextContent == longText);
    }

    [Fact]
    public void WriteNode_MixedWithFastPath_PreservesOrder()
    {
        string xml = WriteContent(writer =>
        {
            writer.AddParagraph("before");
            var node = new OdfNode(OdfNodeType.Element, "p", OdfNamespaces.Text, "text");
            node.Children.Add(new OdfNode(OdfNodeType.Text, "#text", string.Empty) { TextContent = "dom&<x>" });
            writer.WriteNode(node);
            writer.AddParagraph("after");
        });

        OdfNode root = Parse(xml);
        List<OdfNode> paragraphs = FindNodesByLocalName(root, "p");
        Assert.Equal(3, paragraphs.Count);
        Assert.Equal("before", paragraphs[0].TextContent);
        Assert.Equal("dom&<x>", paragraphs[1].TextContent);
        Assert.Equal("after", paragraphs[2].TextContent);
    }

    [Fact]
    public void EmptyParagraph_UsesSelfClosingForm()
    {
        string xml = WriteContent(writer => writer.AddParagraph(string.Empty));
        Assert.Contains("<text:p />", xml, StringComparison.Ordinal);
    }

    private static string WriteContent(Action<OdtStreamWriter> write)
    {
        using var ms = new MemoryStream();
        using (var writer = new OdtStreamWriter(ms))
        {
            write(writer);
        }

        ms.Position = 0;
        using var package = OdfPackage.Open(ms);
        using var s = package.GetEntryStream("content.xml");
        using var reader = new StreamReader(s, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static OdfNode Parse(string xml)
    {
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(xml));
        return OdfXmlReader.Parse(ms);
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
