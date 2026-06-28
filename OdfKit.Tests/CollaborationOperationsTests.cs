using System.Text.Json;
using System.IO;
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
    /// 驗證匯出端可選擇 TDF ODF Toolkit 相容的 changes 封包。
    /// </summary>
    [Fact]
    public void ExportToJson_TdfEnvelope_WrapsOperationsInChanges()
    {
        using TextDocument document = TextDocument.Create();
        document.AddParagraph("封包段落");

        string json = OdtOperationsExporter.ExportToJson(
            document,
            OdtOperationCompatibilityOptions.CreateTdfCompatibility());

        using JsonDocument parsed = JsonDocument.Parse(json);
        JsonElement changes = parsed.RootElement.GetProperty("changes");

        Assert.Equal(JsonValueKind.Array, changes.ValueKind);
        Assert.Equal("addParagraph", changes.EnumerateArray().First().GetProperty("name").GetString());
    }

    /// <summary>
    /// 驗證換行符號可匯出為 TDF 相容的 addLineBreak operation。
    /// </summary>
    [Fact]
    public void ExportToJson_EmitsAddLineBreakOperation()
    {
        using TextDocument document = TextDocument.Create();
        OdfParagraph paragraph = document.AddParagraph();
        paragraph.AddTextRun("前");
        paragraph.AddLineBreak();
        paragraph.AddTextRun("後");

        string json = OdtOperationsExporter.ExportToJson(document);
        using JsonDocument parsed = JsonDocument.Parse(json);

        Assert.Contains(parsed.RootElement.EnumerateArray(), operation =>
            operation.GetProperty("name").GetString() == "addLineBreak");
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
    /// 驗證匯入端可接受 TDF ODF Toolkit 的 changes 封包。
    /// </summary>
    [Fact]
    public void Merge_AcceptsTdfChangesEnvelope()
    {
        const string json = """
            {
                "changes": [
                    { "name": "documentLayout", "start": [0], "attrs": { "view": "text" } },
                    { "name": "addParagraph", "start": [0], "attrs": { "styleId": "Heading_20_1" } },
                    { "name": "addText", "start": [0, 0], "text": "TDF 段落" }
                ]
            }
            """;

        using TextDocument merged = OdtOperationsImporter.Merge(
            json,
            OdtOperationCompatibilityOptions.CreateTdfCompatibility(),
            out OdtOperationImportReport report);

        OdfParagraph paragraph = Assert.Single(merged.Body.Paragraphs);
        Assert.Equal("TDF 段落", paragraph.TextContent);
        Assert.Equal("Heading_20_1", paragraph.StyleName);
        Assert.Equal(2, report.ReplayedCount);
        Assert.Equal(1, report.IgnoredCount);
        Assert.Contains(report.Diagnostics, message => message.Contains("documentLayout", StringComparison.Ordinal));
    }

    /// <summary>
    /// 驗證 addLineBreak operation 可重播為段落換行節點。
    /// </summary>
    [Fact]
    public void Merge_ReplaysAddLineBreakOperation()
    {
        const string json = """
            [
                { "name": "addParagraph", "start": [0] },
                { "name": "addText", "start": [0, 0], "text": "前" },
                { "name": "addLineBreak", "start": [0, 1] },
                { "name": "addText", "start": [0, 2], "text": "後" }
            ]
            """;

        using TextDocument merged = OdtOperationsImporter.Merge(json);

        OdfParagraph paragraph = Assert.Single(merged.Body.Paragraphs);
        Assert.Contains("前", paragraph.TextContent);
        Assert.Contains("後", paragraph.TextContent);
    }

    /// <summary>
    /// 驗證 format operation 的基本字元屬性可套用至最近的文字片段。
    /// </summary>
    [Fact]
    public void Merge_ReplaysBasicFormatOperationOnLastTextRun()
    {
        const string json = """
            [
                { "name": "addParagraph", "start": [0] },
                { "name": "addText", "start": [0, 0], "text": "強調" },
                {
                    "name": "format",
                    "start": [0, 0],
                    "end": [0, 2],
                    "attrs": {
                        "bold": true,
                        "italic": true,
                        "underline": true,
                        "fontSize": "14pt",
                        "color": "#0066CC"
                    }
                }
            ]
            """;

        using TextDocument merged = OdtOperationsImporter.Merge(json);

        OdfTextRun run = Assert.Single(Assert.Single(merged.Body.Paragraphs).Runs);
        Assert.True(run.IsBold);
        Assert.True(run.IsItalic);
        Assert.True(run.IsUnderline);
        Assert.Equal("14pt", run.FontSize);
        Assert.Equal("#0066CC", run.Color);
    }

    /// <summary>
    /// 驗證 format operation 可依 start/end 範圍分裂文字片段並只套用指定區段。
    /// </summary>
    [Fact]
    public void Merge_ReplaysFormatOperationOnTextRange()
    {
        const string json = """
            [
                { "name": "addParagraph", "start": [0] },
                { "name": "addText", "start": [0, 0], "text": "AlphaBeta" },
                {
                    "name": "format",
                    "start": [0, 5],
                    "end": [0, 9],
                    "attrs": {
                        "bold": true,
                        "color": "#0066CC",
                        "backgroundColor": "#FFF2CC",
                        "textTransform": "uppercase",
                        "fontVariant": "small-caps",
                        "superscript": true
                    }
                }
            ]
            """;

        using TextDocument merged = OdtOperationsImporter.Merge(
            json,
            OdtOperationCompatibilityOptions.CreateTdfCompatibility(),
            out OdtOperationImportReport report);

        OdfParagraph paragraph = Assert.Single(merged.Body.Paragraphs);
        Assert.Equal("AlphaBeta", paragraph.TextContent);
        OdfTextRun alpha = Assert.Single(paragraph.Runs.Where(run => run.Text == "Alpha"));
        OdfTextRun beta = Assert.Single(paragraph.Runs.Where(run => run.Text == "Beta"));
        Assert.False(alpha.IsBold);
        Assert.True(beta.IsBold);
        Assert.Equal("#0066CC", beta.Color);
        Assert.Equal("#FFF2CC", beta.BackgroundColor);
        Assert.Equal("uppercase", beta.TextTransform);
        Assert.Equal("small-caps", beta.FontVariant);
        Assert.True(beta.IsSuperscript);
        Assert.Equal(3, report.ReplayedCount);
        Assert.Equal(0, report.UnsupportedCount);
    }

    /// <summary>
    /// 驗證 addStyle operation 會以 metadata-only 子集合容忍，不阻斷後續段落重播。
    /// </summary>
    [Fact]
    public void Merge_ToleratesAddStyleMetadataOperation()
    {
        const string json = """
            [
                { "name": "addStyle", "attrs": { "styleId": "Heading_20_1", "family": "paragraph" } },
                { "name": "addParagraph", "start": [0], "attrs": { "styleId": "Heading_20_1" } },
                { "name": "addText", "start": [0, 0], "text": "標題" }
            ]
            """;

        using TextDocument merged = OdtOperationsImporter.Merge(
            json,
            OdtOperationCompatibilityOptions.CreateTdfCompatibility(),
            out OdtOperationImportReport report);

        OdfParagraph paragraph = Assert.Single(merged.Body.Paragraphs);
        Assert.Equal("標題", paragraph.TextContent);
        Assert.Equal("Heading_20_1", paragraph.StyleName);
        Assert.Equal(2, report.ReplayedCount);
        Assert.Equal(1, report.IgnoredCount);
    }

    /// <summary>
    /// 驗證第二波相容子集合可重播單段落刪除、分割與合併。
    /// </summary>
    [Fact]
    public void Merge_ReplaysDeleteSplitAndMergeParagraphSubset()
    {
        const string json = """
            [
                { "name": "addParagraph", "start": [0] },
                { "name": "addText", "start": [0, 0], "text": "AlphaBeta" },
                { "name": "delete", "start": [0, 5], "end": [0, 9] },
                { "name": "splitParagraph", "start": [0, 2] },
                { "name": "mergeParagraph", "start": [0, 2] }
            ]
            """;

        using TextDocument merged = OdtOperationsImporter.Merge(
            json,
            OdtOperationCompatibilityOptions.CreateTdfCompatibility(),
            out OdtOperationImportReport report);

        OdfParagraph paragraph = Assert.Single(merged.Body.Paragraphs);
        Assert.Equal("Alpha", paragraph.TextContent);
        Assert.Equal(5, report.ReplayedCount);
        Assert.Equal(0, report.UnsupportedCount);
    }

    /// <summary>
    /// 驗證第二波相容子集合可容忍清單樣式 metadata，並建立基本清單段落。
    /// </summary>
    [Fact]
    public void Merge_ReplaysBasicListParagraphSubset()
    {
        const string json = """
            [
                { "name": "addListStyle", "attrs": { "styleId": "BulletList" } },
                { "name": "addParagraph", "start": [0], "attrs": { "listStyleName": "BulletList", "listLevel": 1 } },
                { "name": "addText", "start": [0, 0], "text": "待辦項目" }
            ]
            """;

        using TextDocument merged = OdtOperationsImporter.Merge(
            json,
            OdtOperationCompatibilityOptions.CreateTdfCompatibility(),
            out OdtOperationImportReport report);

        OdfList list = Assert.Single(merged.Body.Lists);
        Assert.Equal("BulletList", list.StyleName);
        OdfParagraph paragraph = Assert.Single(Assert.Single(list.Items).Paragraphs);
        Assert.Equal("待辦項目", paragraph.TextContent);
        Assert.Equal(2, report.ReplayedCount);
        Assert.Equal(1, report.IgnoredCount);
    }

    /// <summary>
    /// 驗證第二波相容子集合可建立簡單文字表格並依序填入儲存格。
    /// </summary>
    [Fact]
    public void Merge_ReplaysSimpleTableSubset()
    {
        const string json = """
            [
                { "name": "addTable", "attrs": { "tableName": "Metrics", "rows": 2, "columns": 2 } },
                { "name": "addCells", "attrs": { "values": [ "Name", "Value" ] } },
                { "name": "addRows", "attrs": { "row": 1 } },
                { "name": "addCells", "attrs": { "values": [ "Revenue", "42" ] } }
            ]
            """;

        using TextDocument merged = OdtOperationsImporter.Merge(
            json,
            OdtOperationCompatibilityOptions.CreateTdfCompatibility(),
            out OdtOperationImportReport report);

        Assert.Single(merged.Body.Tables);
        Assert.Contains("Name", merged.BodyTextRoot.TextContent, StringComparison.Ordinal);
        Assert.Contains("Value", merged.BodyTextRoot.TextContent, StringComparison.Ordinal);
        Assert.Contains("Revenue", merged.BodyTextRoot.TextContent, StringComparison.Ordinal);
        Assert.Contains("42", merged.BodyTextRoot.TextContent, StringComparison.Ordinal);
        Assert.Equal(4, report.ReplayedCount);
        Assert.Equal(0, report.UnsupportedCount);
    }

    /// <summary>
    /// 驗證 repo 內 clean-room TDF subset fixture 可由匯入端重播。
    /// </summary>
    [Fact]
    public void Merge_ReplaysRepositoryTdfSubsetFixture()
    {
        string path = Path.Combine(FindRepositoryRoot(), "tests", "fixtures", "collaboration", "tdf-subset-envelope.json");
        string json = File.ReadAllText(path);

        using TextDocument merged = OdtOperationsImporter.Merge(
            json,
            OdtOperationCompatibilityOptions.CreateTdfCompatibility(),
            out OdtOperationImportReport report);

        OdfParagraph firstParagraph = merged.Body.Paragraphs.First();
        Assert.Equal("協作草稿段落\n", firstParagraph.TextContent);
        Assert.Equal("Heading_20_1", firstParagraph.StyleName);
        OdfTextRun formattedRun = Assert.Single(firstParagraph.Runs.Where(run => run.Text == "段落"));
        Assert.True(formattedRun.IsBold);
        Assert.Equal("#0066CC", formattedRun.Color);
        Assert.Equal("#FFF2CC", formattedRun.BackgroundColor);
        Assert.Equal("uppercase", formattedRun.TextTransform);
        Assert.Equal("small-caps", formattedRun.FontVariant);
        Assert.Contains(firstParagraph.Runs, run => run.Text == "草稿" && !run.IsBold);

        OdfList list = Assert.Single(merged.Body.Lists);
        Assert.Equal("BulletList", list.StyleName);
        Assert.Equal("清單", Assert.Single(Assert.Single(list.Items).Paragraphs).TextContent);

        Assert.Single(merged.Body.Tables);
        Assert.Contains("Name", merged.BodyTextRoot.TextContent, StringComparison.Ordinal);
        Assert.Contains("Value", merged.BodyTextRoot.TextContent, StringComparison.Ordinal);
        Assert.Contains("Revenue", merged.BodyTextRoot.TextContent, StringComparison.Ordinal);
        Assert.Contains("42", merged.BodyTextRoot.TextContent, StringComparison.Ordinal);
        Assert.Equal(14, report.ReplayedCount);
        Assert.Equal(3, report.IgnoredCount);
        Assert.Equal(0, report.UnsupportedCount);
    }

    /// <summary>
    /// 驗證未知 operation 預設會被略過並留下可稽核診斷。
    /// </summary>
    [Fact]
    public void Merge_UnsupportedOperations_AreReportedWithoutThrowing()
    {
        using TextDocument document = TextDocument.Create();
        const string json = """
            [
                { "name": "addParagraph", "start": [0] },
                { "name": "move", "start": [0], "end": [0, 1] }
            ]
            """;

        OdtOperationImportReport report = OdtOperationsImporter.Merge(document, json, null);

        Assert.Equal(1, report.ReplayedCount);
        Assert.Equal(1, report.UnsupportedCount);
        Assert.Equal(1, report.IgnoredCount);
        Assert.Contains(report.Diagnostics, message => message.Contains("move", StringComparison.Ordinal));
    }

    /// <summary>
    /// 驗證未支援 operation 的嚴格策略會中止匯入。
    /// </summary>
    [Fact]
    public void Merge_ThrowPolicy_ThrowsForUnsupportedOperation()
    {
        using TextDocument document = TextDocument.Create();
        var options = new OdtOperationCompatibilityOptions
        {
            UnsupportedOperationPolicy = OdtUnsupportedOperationPolicy.Throw,
        };

        const string json = """[{ "name": "move", "start": [0], "end": [0, 1] }]""";

        Assert.Throws<NotSupportedException>(() => OdtOperationsImporter.Merge(document, json, options));
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

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "OdfKit.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("找不到 OdfKit repo root。");
    }
}
