using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Spreadsheet;

namespace OdfKit.Tests;

/// <summary>
/// OdsStreamWriter UTF-8 直寫快速路徑的輸出等價測試：
/// 轉義完整性（文字與屬性）、換行改寫行為、emoji/CJK 多位元組與緩衝邊界、
/// double 與 DateTime 邊界格式、WriteNode 混用、SwitchToSheet 緩衝模式一致性。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Boundary)]
public class OdsStreamWriterFastPathTests
{
    [Fact]
    public void WriteCell_TextEscaping_MatchesXmlWriterBehavior()
    {
        string xml = WriteContent(writer =>
        {
            writer.WriteStartSheet("S");
            writer.WriteStartRow();
            writer.WriteCell("&<>\"'");
            writer.WriteCell("a\tb\nc\rd");
            writer.WriteCell("crlf\r\nend");
            writer.WriteEndRow();
            writer.WriteEndSheet();
        });

        // 文字內容：轉義 & < >；引號與單引號保持字面；tab 保持字面。
        Assert.Contains("<text:p>&amp;&lt;&gt;\"'</text:p>", xml, StringComparison.Ordinal);
        // NewLineHandling.Replace 一致性：\n 與 \r 均改寫為 \r\n，\r\n 維持單一 \r\n。
        Assert.Contains("<text:p>a\tb\r\nc\r\nd</text:p>", xml, StringComparison.Ordinal);
        Assert.Contains("<text:p>crlf\r\nend</text:p>", xml, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteCell_AttributeEscaping_MatchesXmlWriterBehavior()
    {
        string xml = WriteContent(writer =>
        {
            writer.WriteStartSheet("Edge & <Cases> \"q\" 'a'");
            writer.WriteStartRow();
            writer.WriteCell("v1", "st&y<le\"x\ty");
            writer.WriteCell("v2", "n\nr\rt>");
            writer.WriteEndRow();
            writer.WriteEndSheet();
        });

        // 屬性值：轉義 & < > "；單引號字面；tab/LF/CR 以字元參照輸出。
        Assert.Contains("table:name=\"Edge &amp; &lt;Cases&gt; &quot;q&quot; 'a'\"", xml, StringComparison.Ordinal);
        Assert.Contains("table:style-name=\"st&amp;y&lt;le&quot;x&#x9;y\"", xml, StringComparison.Ordinal);
        Assert.Contains("table:style-name=\"n&#xA;r&#xD;t&gt;\"", xml, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteCell_EmojiAndCjk_RoundTripsThroughParser()
    {
        string emojiText = "分數 " + char.ConvertFromUtf32(0x1F600) + " 滿分：中文字串";
        string xml = WriteContent(writer =>
        {
            writer.WriteStartSheet("S");
            writer.WriteStartRow();
            writer.WriteCell(emojiText);
            writer.WriteEndRow();
            writer.WriteEndSheet();
        });

        OdfNode root = Parse(xml);
        List<OdfNode> paragraphs = FindNodesByLocalName(root, "p");
        Assert.Contains(paragraphs, p => p.TextContent == emojiText);
    }

    [Fact]
    public void WriteCell_LongTextAcrossBufferBoundary_PreservesSurrogatePairs()
    {
        // 超過快速路徑 16K 字元緩衝的長字串，且以代理對（emoji）密集分佈，
        // 驗證沖洗邊界不切開代理對、內容完整往返。
        var builder = new StringBuilder(48 * 1024);
        string emoji = char.ConvertFromUtf32(0x1F600);
        for (int i = 0; i < 6000; i++)
        {
            builder.Append("中文チャンク-").Append(i.ToString(CultureInfo.InvariantCulture)).Append(emoji);
        }

        string longText = builder.ToString();
        string xml = WriteContent(writer =>
        {
            writer.WriteStartSheet("S");
            writer.WriteStartRow();
            writer.WriteCell(longText);
            writer.WriteEndRow();
            writer.WriteEndSheet();
        });

        OdfNode root = Parse(xml);
        List<OdfNode> paragraphs = FindNodesByLocalName(root, "p");
        Assert.Contains(paragraphs, p => p.TextContent == longText);
    }

    [Fact]
    public void WriteCell_DoubleEdgeValues_MatchInvariantCultureFormatting()
    {
        double[] values = [0.0, -0.0, double.NaN, double.PositiveInfinity, double.NegativeInfinity, double.MaxValue, double.MinValue, double.Epsilon, 123456789.123456789, 1e-15];
        string xml = WriteContent(writer =>
        {
            writer.WriteStartSheet("S");
            writer.WriteStartRow();
            foreach (double value in values)
            {
                writer.WriteCell(value);
            }

            writer.WriteEndRow();
            writer.WriteEndSheet();
        });

        foreach (double value in values)
        {
            string text = value.ToString(CultureInfo.InvariantCulture);
            Assert.Contains($"office:value=\"{text}\"><text:p>{text}</text:p>", xml, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void WriteCell_DateTimeBoundaries_MatchExistingFormats()
    {
        string xml = WriteContent(writer =>
        {
            writer.WriteStartSheet("S");
            writer.WriteStartRow();
            writer.WriteCell(DateTime.MinValue);
            writer.WriteCell(DateTime.MaxValue);
            writer.WriteCell(DateTime.MinValue, timezoneNaive: true);
            writer.WriteCell(DateTime.MaxValue, timezoneNaive: true);
            writer.WriteCell(new DateTime(2026, 7, 9, 12, 34, 56, DateTimeKind.Utc));
            writer.WriteEndRow();
            writer.WriteEndSheet();
        });

        Assert.Contains("office:date-value=\"0001-01-01T00:00:00Z\"", xml, StringComparison.Ordinal);
        Assert.Contains("office:date-value=\"9999-12-31T23:59:59Z\"", xml, StringComparison.Ordinal);
        Assert.Contains("office:date-value=\"0001-01-01T00:00:00\"><text:p>0001-01-01T00:00:00</text:p>", xml, StringComparison.Ordinal);
        Assert.Contains("office:date-value=\"9999-12-31T23:59:59\"><text:p>9999-12-31T23:59:59</text:p>", xml, StringComparison.Ordinal);
        Assert.Contains("office:date-value=\"2026-07-09T12:34:56Z\"", xml, StringComparison.Ordinal);
    }

    [Fact]
    public void EmptyRowAndEmptySheet_UseSelfClosingForms()
    {
        string xml = WriteContent(writer =>
        {
            writer.WriteStartSheet("S");
            writer.WriteStartRow();
            writer.WriteEndRow();
            writer.WriteStartRow();
            writer.WriteCell(string.Empty);
            writer.WriteEndRow();
            writer.WriteEndSheet();
            writer.WriteStartSheet("Empty");
            writer.WriteEndSheet();
        });

        // 與 XmlWriter 相同的自閉合輸出（含空格斜線）。
        Assert.Contains("<table:table-row />", xml, StringComparison.Ordinal);
        Assert.Contains("<text:p />", xml, StringComparison.Ordinal);
        Assert.Contains("<table:table table:name=\"Empty\" />", xml, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteNode_MixedWithFastPath_PreservesOrderAndWellFormedness()
    {
        string xml = WriteContent(writer =>
        {
            writer.WriteStartSheet("S");
            writer.WriteStartRow();
            writer.WriteCell("before-node");
            writer.WriteNode(CreateDomCell("dom&<text> 節點"));
            writer.WriteCell("after-node");
            writer.WriteEndRow();
            // 列外（工作表層級）的 WriteNode 交接。
            writer.WriteNode(CreateDomRow("row-level"));
            writer.WriteStartRow();
            writer.WriteCell("resumed");
            writer.WriteEndRow();
            writer.WriteEndSheet();
        });

        OdfNode root = Parse(xml);
        List<OdfNode> rows = FindNodesByLocalName(root, "table-row");
        Assert.Equal(3, rows.Count);

        List<OdfNode> firstRowCells = FindNodesByLocalName(rows[0], "table-cell");
        Assert.Equal(3, firstRowCells.Count);
        Assert.Equal("before-node", firstRowCells[0].TextContent);
        Assert.Equal("dom&<text> 節點", firstRowCells[1].TextContent);
        Assert.Equal("after-node", firstRowCells[2].TextContent);

        Assert.Equal("row-level", rows[1].TextContent);
        Assert.Equal("resumed", rows[2].TextContent);
    }

    [Fact]
    public void SwitchToSheet_BufferedMode_ProducesSameRowMarkupAsDirectMode()
    {
        string xml = WriteContent(writer =>
        {
            writer.WriteStartSheet("Direct");
            WriteSampleRows(writer);
            writer.WriteEndSheet();
            writer.SwitchToSheet("Buffered");
            WriteSampleRows(writer);
        });

        string directRows = ExtractSheetInner(xml, "Direct");
        string bufferedRows = ExtractSheetInner(xml, "Buffered");
        // 兩條路徑輸出的列標記必須完全一致（緩衝模式不得產生不同格式）。
        Assert.Equal(directRows, bufferedRows);

        static void WriteSampleRows(OdsStreamWriter writer)
        {
            writer.WriteStartRow();
            writer.WriteCell("A1 &<>");
            writer.WriteCell(42.5);
            writer.WriteCell(true);
            writer.WriteCell(new DateTime(2026, 7, 9, 0, 0, 0, DateTimeKind.Utc));
            writer.WriteEndRow();
        }
    }

    [Fact]
    public async Task WriteSheetsAsync_FastPathFragments_MatchMainPathShapes()
    {
        using var ms = new MemoryStream();
        using (var writer = new OdsStreamWriter(ms))
        {
            var jobs = new[]
            {
                new OdsSheetWriteJob("Jobs", (sheetWriter, _) =>
                {
                    sheetWriter.WriteStartRow();
                    sheetWriter.WriteCell("text &<>");
                    sheetWriter.WriteCell(1.25);
                    sheetWriter.WriteCell(false);
                    return Task.CompletedTask;
                })
            };
            await writer.WriteSheetsAsync(jobs, maxConcurrency: 0, cancellationToken: TestContext.Current.CancellationToken);
        }

        ms.Position = 0;
        string xml = ReadContentXml(ms);
        Assert.Contains("<table:table-cell office:value-type=\"string\"><text:p>text &amp;&lt;&gt;</text:p></table:table-cell>", xml, StringComparison.Ordinal);
        Assert.Contains("<table:table-cell office:value-type=\"float\" office:value=\"1.25\"><text:p>1.25</text:p></table:table-cell>", xml, StringComparison.Ordinal);
        Assert.Contains("<table:table-cell office:value-type=\"boolean\" office:boolean-value=\"false\"><text:p>FALSE</text:p></table:table-cell>", xml, StringComparison.Ordinal);
    }

    private static OdfNode CreateDomCell(string text)
    {
        var cellNode = new OdfNode(OdfNodeType.Element, "table-cell", OdfNamespaces.Table, "table");
        cellNode.Attributes[new OdfAttributeName("value-type", OdfNamespaces.Office)] = "string";
        var pNode = new OdfNode(OdfNodeType.Element, "p", OdfNamespaces.Text, "text");
        pNode.Children.Add(new OdfNode(OdfNodeType.Text, string.Empty, string.Empty) { TextContent = text });
        cellNode.Children.Add(pNode);
        return cellNode;
    }

    private static OdfNode CreateDomRow(string text)
    {
        var rowNode = new OdfNode(OdfNodeType.Element, "table-row", OdfNamespaces.Table, "table");
        rowNode.Children.Add(CreateDomCell(text));
        return rowNode;
    }

    private static string WriteContent(Action<OdsStreamWriter> write)
    {
        using var ms = new MemoryStream();
        using (var writer = new OdsStreamWriter(ms))
        {
            write(writer);
        }

        ms.Position = 0;
        return ReadContentXml(ms);
    }

    private static string ReadContentXml(Stream odsStream)
    {
        using var package = OdfPackage.Open(odsStream);
        using var s = package.GetEntryStream("content.xml");
        using var reader = new StreamReader(s, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static OdfNode Parse(string xml)
    {
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(xml));
        return OdfXmlReader.Parse(ms);
    }

    private static string ExtractSheetInner(string xml, string sheetName)
    {
        string startMarker = $"<table:table table:name=\"{sheetName}\"";
        int start = xml.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"找不到工作表 {sheetName}");
        int contentStart = xml.IndexOf('>', start) + 1;
        int end = xml.IndexOf("</table:table>", contentStart, StringComparison.Ordinal);
        Assert.True(end >= 0, $"工作表 {sheetName} 缺少結束標籤");
        return xml.Substring(contentStart, end - contentStart);
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
