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
}
