using System.Collections.Generic;
using OdfKit.Text;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 鎖定 H-8 郵件合併泛型 API 的整合測試。
/// </summary>
public class MailMergeGenericApiTests
{
    private sealed record PersonRecord(string Name, string City, int Age);

    /// <summary>
    /// 驗證強型別泛型 MailMerge&lt;T&gt; 能正確置換文件中的預留位置。
    /// </summary>
    [Fact]
    public void MailMerge_GenericRecord_ReplacesPlaceholders()
    {
        using var doc = TextDocument.Create();
        doc.AddParagraph("親愛的 {{Name}}，您來自 {{City}}，年齡 {{Age}} 歲。");

        var data = new PersonRecord("小明", "台北", 30);
        doc.MailMerge(data);

        string text = doc.BodyTextRoot.TextContent;
        Assert.Contains("小明", text);
        Assert.Contains("台北", text);
        Assert.Contains("30", text);
        Assert.DoesNotContain("{{Name}}", text);
        Assert.DoesNotContain("{{City}}", text);
        Assert.DoesNotContain("{{Age}}", text);
    }

    /// <summary>
    /// 驗證字典型 MailMerge 能正確置換文件中的預留位置。
    /// </summary>
    [Fact]
    public void MailMerge_Dictionary_ReplacesPlaceholders()
    {
        using var doc = TextDocument.Create();
        doc.AddParagraph("產品：{{Product}}，價格：{{Price}}");

        var data = new Dictionary<string, object?>
        {
            ["Product"] = "OdfKit",
            ["Price"] = "免費"
        };
        doc.MailMerge((IReadOnlyDictionary<string, object?>)data);

        string text = doc.BodyTextRoot.TextContent;
        Assert.Contains("OdfKit", text);
        Assert.Contains("免費", text);
        Assert.DoesNotContain("{{Product}}", text);
        Assert.DoesNotContain("{{Price}}", text);
    }

    /// <summary>
    /// 驗證批次泛型 MailMerge&lt;T&gt; 回傳筆數與輸入記錄數相同。
    /// </summary>
    [Fact]
    public void MailMergeBatch_Generic_ReturnsOneDocumentPerRecord()
    {
        using var template = TextDocument.Create();
        template.AddParagraph("姓名：{{Name}}");

        var records = new[]
        {
            new PersonRecord("Alice", "台北", 25),
            new PersonRecord("Bob",   "高雄", 32),
            new PersonRecord("Carol", "台中", 28),
        };

        IReadOnlyList<TextDocument> docs = template.MailMerge<PersonRecord>(records);

        try
        {
            Assert.Equal(3, docs.Count);

            Assert.Contains("Alice", docs[0].BodyTextRoot.TextContent);
            Assert.Contains("Bob",   docs[1].BodyTextRoot.TextContent);
            Assert.Contains("Carol", docs[2].BodyTextRoot.TextContent);

            Assert.DoesNotContain("{{Name}}", docs[0].BodyTextRoot.TextContent);
            Assert.DoesNotContain("{{Name}}", docs[1].BodyTextRoot.TextContent);
            Assert.DoesNotContain("{{Name}}", docs[2].BodyTextRoot.TextContent);
        }
        finally
        {
            foreach (var d in docs) d.Dispose();
        }
    }

    /// <summary>
    /// 驗證批次字典 MailMerge 回傳正確數量的文件副本。
    /// </summary>
    [Fact]
    public void MailMergeBatch_Dictionary_ReturnsOneDocumentPerRecord()
    {
        using var template = TextDocument.Create();
        template.AddParagraph("城市：{{City}}");

        IEnumerable<IReadOnlyDictionary<string, object?>> records = new List<IReadOnlyDictionary<string, object?>>
        {
            new Dictionary<string, object?> { ["City"] = "台北" },
            new Dictionary<string, object?> { ["City"] = "高雄" },
        };

        IReadOnlyList<TextDocument> docs = template.MailMerge(records);

        try
        {
            Assert.Equal(2, docs.Count);
            Assert.Contains("台北", docs[0].BodyTextRoot.TextContent);
            Assert.Contains("高雄", docs[1].BodyTextRoot.TextContent);
        }
        finally
        {
            foreach (var d in docs) d.Dispose();
        }
    }

    /// <summary>
    /// 驗證批次合併不影響原始範本文件的內容。
    /// </summary>
    [Fact]
    public void MailMergeBatch_TemplateRemainsUnmodified()
    {
        using var template = TextDocument.Create();
        template.AddParagraph("{{Name}} 您好");

        var records = new[]
        {
            new PersonRecord("Test1", "A", 1),
            new PersonRecord("Test2", "B", 2),
        };

        IReadOnlyList<TextDocument> docs = template.MailMerge<PersonRecord>(records);
        try
        {
            Assert.Contains("{{Name}}", template.BodyTextRoot.TextContent);
        }
        finally
        {
            foreach (var d in docs) d.Dispose();
        }
    }

    /// <summary>
    /// 驗證 IReadOnlyDictionary 巢狀路徑在合併引擎中能正確解析。
    /// </summary>
    [Fact]
    public void MailMerge_ReadOnlyDictionaryPlaceholder_Resolves()
    {
        using var doc = TextDocument.Create();
        doc.AddParagraph("編號：{{Id}}，標題：{{Title}}");

        IReadOnlyDictionary<string, object?> data = new Dictionary<string, object?>
        {
            ["Id"]    = 42,
            ["Title"] = "測試文件"
        };

        doc.MailMerge(data);

        string text = doc.BodyTextRoot.TextContent;
        Assert.Contains("42", text);
        Assert.Contains("測試文件", text);
    }
}
