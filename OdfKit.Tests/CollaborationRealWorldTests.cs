using System.Linq;
using OdfKit.Collaboration;
using OdfKit.Text;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 以 LibreOffice 轉文字為裁判的往返驗證（真實 ODT → operations → 新文件）找到：匯出只輸出最上層的段落，
/// 清單項目與表格內文整個遺失，標題層級也沒有保留。
/// </summary>
public sealed class CollaborationRealWorldTests
{
    private static TextDocument CreateSource()
    {
        TextDocument document = TextDocument.Create();
        document.AddHeading("第一章 概觀", 2);
        document.AddParagraph("內文 Alpha");
        OdfList list = document.AddList();
        list.AddItem("項目 Bravo", 1);
        list.AddItem("子項 Charlie", 2);
        OdfTable table = document.AddTable(2, 2);
        table.GetCell(0, 0).AddParagraph("甲");
        table.GetCell(0, 1).AddParagraph("乙");
        table.GetCell(1, 0).AddParagraph("丙");
        table.GetCell(1, 1).AddParagraph("丁");
        document.AddParagraph("末段 Delta");
        return document;
    }

    /// <summary>
    /// 清單項目與表格儲存格都成為操作，標題帶 outlineLevel。
    /// </summary>
    [Fact]
    public void ExportIncludesListItemsTablesAndHeadingLevels()
    {
        using TextDocument document = CreateSource();
        OdtOperationLog log = OdtOperationsExporter.Export(document);

        Assert.Contains(log.Operations, operation => operation.Text == "項目 Bravo");
        Assert.Contains(log.Operations, operation => operation.Text == "子項 Charlie");
        Assert.Contains(log.Operations, operation => operation.Name == "addParagraph" && operation.TryGetInt32Attribute("outlineLevel", out int level) && level == 2);
        Assert.Contains(log.Operations, operation => operation.Name == "addTable");
        Assert.Contains(
            log.Operations,
            operation => operation.Name == "addCells" && operation.Attrs is { } attrs && attrs.GetProperty("values").EnumerateArray().Any(value => value.GetString() == "丁"));
    }

    /// <summary>
    /// 匯出再合併後全部文字都在，表格與標題還原。
    /// </summary>
    [Fact]
    public void ExportThenMergeKeepsAllTextTablesAndHeadings()
    {
        using TextDocument source = CreateSource();
        string json = OdtOperationsExporter.ExportToJson(source);
        using TextDocument merged = OdtOperationsImporter.Merge(json);

        string text = merged.BodyTextRoot.TextContent ?? string.Empty;
        foreach (string expected in new[] { "第一章 概觀", "內文 Alpha", "項目 Bravo", "子項 Charlie", "甲", "乙", "丙", "丁", "末段 Delta" })
        {
            Assert.Contains(expected, text, System.StringComparison.Ordinal);
        }

        Assert.Single(merged.Body.Tables);
        Assert.Contains(merged.Body.Headings, heading => heading.TextContent == "第一章 概觀" && heading.OutlineLevel == 2);
    }
}
