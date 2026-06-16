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
            Assert.Contains("Bob", docs[1].BodyTextRoot.TextContent);
            Assert.Contains("Carol", docs[2].BodyTextRoot.TextContent);

            Assert.DoesNotContain("{{Name}}", docs[0].BodyTextRoot.TextContent);
            Assert.DoesNotContain("{{Name}}", docs[1].BodyTextRoot.TextContent);
            Assert.DoesNotContain("{{Name}}", docs[2].BodyTextRoot.TextContent);
        }
        finally
        {
            foreach (var d in docs)
                d.Dispose();
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
            foreach (var d in docs)
                d.Dispose();
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
            foreach (var d in docs)
                d.Dispose();
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
            ["Id"] = 42,
            ["Title"] = "測試文件"
        };

        doc.MailMerge(data);

        string text = doc.BodyTextRoot.TextContent;
        Assert.Contains("42", text);
        Assert.Contains("測試文件", text);
    }

    /// <summary>
    /// 驗證多層巢狀 TableStart / TableEnd 區段展開與資料解析。
    /// </summary>
    [Fact]
    public void MailMerge_TableStartEndNested_ExpandsAndResolves()
    {
        using var doc = TextDocument.Create();
        doc.AddParagraph("{{TableStart:Departments}}");
        doc.AddParagraph("部門：{{DeptName}}");
        doc.AddParagraph("{{TableStart:Teams}}");
        doc.AddParagraph("  團隊：{{TeamName}}");
        doc.AddParagraph("{{TableStart:Members}}");
        doc.AddParagraph("    成員：{{MemberName}} ({{Role}})");
        doc.AddParagraph("{{TableEnd:Members}}");
        doc.AddParagraph("{{TableEnd:Teams}}");
        doc.AddParagraph("{{TableEnd:Departments}}");

        var data = new Dictionary<string, object?>
        {
            ["Departments"] = new[]
            {
                new Dictionary<string, object?>
                {
                    ["DeptName"] = "研發部",
                    ["Teams"] = new[]
                    {
                        new Dictionary<string, object?>
                        {
                            ["TeamName"] = "核心組",
                            ["Members"] = new[]
                            {
                                new Dictionary<string, object?> { ["MemberName"] = "Alice", ["Role"] = "資深架構師" },
                                new Dictionary<string, object?> { ["MemberName"] = "Bob", ["Role"] = "工程師" }
                            }
                        },
                        new Dictionary<string, object?>
                        {
                            ["TeamName"] = "測試組",
                            ["Members"] = new[]
                            {
                                new Dictionary<string, object?> { ["MemberName"] = "Charlie", ["Role"] = "測試經理" }
                            }
                        }
                    }
                },
                new Dictionary<string, object?>
                {
                    ["DeptName"] = "業務部",
                    ["Teams"] = new[]
                    {
                        new Dictionary<string, object?>
                        {
                            ["TeamName"] = "海外組",
                            ["Members"] = new[]
                            {
                                new Dictionary<string, object?> { ["MemberName"] = "David", ["Role"] = "業務主管" }
                            }
                        }
                    }
                }
            }
        };

        var engine = new OdfMailMergeEngine(doc);
        var report = engine.Execute(doc.BodyTextRoot, data, new OdfMailMergeOptions());

        string text = doc.BodyTextRoot.TextContent;

        Assert.Contains("部門：研發部", text);
        Assert.Contains("部門：業務部", text);
        Assert.Contains("團隊：核心組", text);
        Assert.Contains("團隊：測試組", text);
        Assert.Contains("團隊：海外組", text);
        Assert.Contains("成員：Alice (資深架構師)", text);
        Assert.Contains("成員：Bob (工程師)", text);
        Assert.Contains("成員：Charlie (測試經理)", text);
        Assert.Contains("成員：David (業務主管)", text);

        Assert.DoesNotContain("{{TableStart", text);
        Assert.DoesNotContain("{{TableEnd", text);
        Assert.Empty(report.UnresolvedPlaceholders);
    }

    /// <summary>
    /// 驗證當範本中含有無法由資料來源解析的預留位置時，會被正確收集於報告中且原字串留空或被清除。
    /// </summary>
    [Fact]
    public void MailMerge_UnresolvedPlaceholders_ReportsUnresolved()
    {
        using var doc = TextDocument.Create();
        doc.AddParagraph("親愛的 {{Name}}，您來自 {{City}}，您喜歡 {{MissingField}}！");
        doc.AddParagraph("{{TableStart:MissingRegion}}");
        doc.AddParagraph("區域：{{SubField}}");
        doc.AddParagraph("{{TableEnd:MissingRegion}}");

        var data = new Dictionary<string, object?>
        {
            ["Name"] = "小明",
            ["City"] = "台北"
        };

        var engine = new OdfMailMergeEngine(doc);
        var report = engine.Execute(doc.BodyTextRoot, data, new OdfMailMergeOptions());

        Assert.Contains("MissingField", report.UnresolvedPlaceholders);
        Assert.Contains("MissingRegion", report.UnresolvedPlaceholders);

        string text = doc.BodyTextRoot.TextContent;
        Assert.Contains("小明", text);
        Assert.Contains("台北", text);
        Assert.DoesNotContain("{{Name}}", text);
        Assert.DoesNotContain("{{MissingField}}", text);
        Assert.DoesNotContain("區域：", text);
    }
}

