using System.Text.Json;
using OdfKit.Collaboration;
using OdfKit.Text;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 驗證 ODT → JSON operations 匯出（COLLAB-1）。
/// </summary>
public sealed class CollaborationOperationsTests
{
    /// <summary>
    /// 驗證可將段落與文字匯出為 addParagraph／addText operations。
    /// </summary>
    [Fact]
    public void ExportToJson_EmitsParagraphAndTextOperations()
    {
        using TextDocument document = TextDocument.Create();
        document.AddParagraph("第一段");
        document.AddParagraph("第二段");

        string json = OdtOperationsExporter.ExportToJson(document);
        using JsonDocument parsed = JsonDocument.Parse(json);
        JsonElement.ArrayEnumerator operations = parsed.RootElement.EnumerateArray().GetEnumerator();

        Assert.True(operations.MoveNext());
        Assert.Equal("addParagraph", operations.Current.GetProperty("name").GetString());
        Assert.Equal(0, operations.Current.GetProperty("start")[0].GetInt32());

        Assert.True(operations.MoveNext());
        Assert.Equal("addText", operations.Current.GetProperty("name").GetString());
        Assert.Equal("第一段", operations.Current.GetProperty("text").GetString());
        Assert.Equal(0, operations.Current.GetProperty("start")[0].GetInt32());
        Assert.Equal(0, operations.Current.GetProperty("start")[1].GetInt32());

        Assert.True(operations.MoveNext());
        Assert.Equal("addParagraph", operations.Current.GetProperty("name").GetString());
        Assert.Equal(1, operations.Current.GetProperty("start")[0].GetInt32());

        Assert.True(operations.MoveNext());
        Assert.Equal("addText", operations.Current.GetProperty("name").GetString());
        Assert.Equal("第二段", operations.Current.GetProperty("text").GetString());
        Assert.False(operations.MoveNext());
    }

    /// <summary>
    /// 驗證可匯出段落樣式名稱至 addParagraph attrs。
    /// </summary>
    [Fact]
    public void ExportToJson_EmitsParagraphStyleName()
    {
        using TextDocument document = TextDocument.Create();
        OdfParagraph paragraph = document.AddParagraph("自訂樣式");
        paragraph.StyleName = "CustomBlock";

        string json = OdtOperationsExporter.ExportToJson(document);
        using JsonDocument parsed = JsonDocument.Parse(json);
        JsonElement paragraphOperation = parsed.RootElement.EnumerateArray().First();

        Assert.Equal("addParagraph", paragraphOperation.GetProperty("name").GetString());
        Assert.Equal("CustomBlock", paragraphOperation.GetProperty("attrs").GetProperty("styleName").GetString());
    }

    /// <summary>
    /// 驗證 JSON operations 可單向 merge 重建段落與文字內容（COLLAB-2）。
    /// </summary>
    [Fact]
    public void Merge_ReplaysAddParagraphAndAddTextOperations()
    {
        using TextDocument source = TextDocument.Create();
        source.AddParagraph("第一段");
        OdfParagraph second = source.AddParagraph("第二段");
        second.StyleName = "CustomBlock";

        string json = OdtOperationsExporter.ExportToJson(source);

        using TextDocument merged = OdtOperationsImporter.Merge(json);

        List<OdfParagraph> paragraphs = merged.Body.Paragraphs.ToList();
        Assert.Equal(2, paragraphs.Count);
        Assert.Equal("第一段", paragraphs[0].TextContent);
        Assert.Equal("第二段", paragraphs[1].TextContent);
        Assert.Equal("CustomBlock", paragraphs[1].StyleName);
    }

    /// <summary>
    /// 驗證 addTab operation 可正確重播為定位字元。
    /// </summary>
    [Fact]
    public void Merge_ReplaysAddTabOperation()
    {
        const string json = """
            [
                { "name": "addParagraph", "start": [0] },
                { "name": "addText", "start": [0, 0], "text": "前" },
                { "name": "addTab", "start": [0, 1] },
                { "name": "addText", "start": [0, 2], "text": "後" }
            ]
            """;

        using TextDocument merged = OdtOperationsImporter.Merge(json);

        OdfParagraph paragraph = Assert.Single(merged.Body.Paragraphs);
        Assert.Contains("前", paragraph.TextContent);
        Assert.Contains("後", paragraph.TextContent);
    }

    /// <summary>
    /// 驗證合併至既有文件時，新段落附加於文件結尾。
    /// </summary>
    [Fact]
    public void Merge_AppendsOntoExistingDocument()
    {
        using TextDocument document = TextDocument.Create();
        document.AddParagraph("既有段落");

        const string json = """[{ "name": "addParagraph", "start": [0] }, { "name": "addText", "start": [0, 0], "text": "新增段落" }]""";
        OdtOperationsImporter.Merge(document, json);

        List<OdfParagraph> paragraphs = document.Body.Paragraphs.ToList();
        Assert.Equal(2, paragraphs.Count);
        Assert.Equal("既有段落", paragraphs[0].TextContent);
        Assert.Equal("新增段落", paragraphs[1].TextContent);
    }
}
