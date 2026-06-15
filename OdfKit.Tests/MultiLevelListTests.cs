using System.Collections.Generic;
using System.IO;
using OdfKit.Core;
using OdfKit.Styles;
using OdfKit.Text;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 鎖定 Y-2 多層級清單樣式 API 的整合測試。
/// </summary>
public class MultiLevelListTests
{
    private static string GetContentXml(TextDocument doc)
    {
        using var ms = new MemoryStream();
        doc.SaveToStream(ms);
        ms.Position = 0;
        using var pkg = OdfPackage.Open(ms, leaveOpen: true);
        using var stream = pkg.GetEntryStream("content.xml");
        return new System.IO.StreamReader(stream).ReadToEnd();
    }

    private static string GetStylesXml(TextDocument doc)
    {
        using var ms = new MemoryStream();
        doc.SaveToStream(ms);
        ms.Position = 0;
        using var pkg = OdfPackage.Open(ms, leaveOpen: true);
        using var stream = pkg.GetEntryStream("styles.xml");
        return new System.IO.StreamReader(stream).ReadToEnd();
    }

    /// <summary>
    /// 驗證 AddListWithStyle 將 text:list-style 寫入 styles.xml 的 office:styles 節點。
    /// </summary>
    [Fact]
    public void AddListWithStyle_WritesListStyleToStylesXml()
    {
        using var doc = TextDocument.Create();
        var levels = new List<OdfListLevelStyle>
        {
            new() { Level = 1, Type = OdfListLevelType.Number, NumFormat = "1", NumSuffix = "." },
            new() { Level = 2, Type = OdfListLevelType.Number, NumFormat = "1", NumSuffix = "." },
        };

        doc.AddListWithStyle("LegalList", levels);

        string stylesXml = GetStylesXml(doc);

        Assert.Contains("text:list-style", stylesXml);
        Assert.Contains("style:name=\"LegalList\"", stylesXml);
        Assert.Contains("text:list-level-style-number", stylesXml);
    }

    /// <summary>
    /// 驗證多層級清單樣式的各層級 text:level 屬性正確。
    /// </summary>
    [Fact]
    public void AddListWithStyle_ThreeLevels_WritesAllLevelAttributes()
    {
        using var doc = TextDocument.Create();
        var levels = new List<OdfListLevelStyle>
        {
            new() { Level = 1, NumFormat = "1", NumSuffix = "." },
            new() { Level = 2, NumFormat = "a", NumSuffix = ")" },
            new() { Level = 3, NumFormat = "i", NumSuffix = "." },
        };

        doc.AddListWithStyle("MyList", levels);

        string stylesXml = GetStylesXml(doc);

        Assert.Contains("text:level=\"1\"", stylesXml);
        Assert.Contains("text:level=\"2\"", stylesXml);
        Assert.Contains("text:level=\"3\"", stylesXml);
        Assert.Contains("fo:num-format=\"1\"", stylesXml);
        Assert.Contains("fo:num-format=\"a\"", stylesXml);
        Assert.Contains("fo:num-format=\"i\"", stylesXml);
    }

    /// <summary>
    /// 驗證項目符號層級寫入 text:list-level-style-bullet 及 text:bullet-char。
    /// </summary>
    [Fact]
    public void AddListWithStyle_BulletLevel_WritesBulletElement()
    {
        using var doc = TextDocument.Create();
        var levels = new List<OdfListLevelStyle>
        {
            new() { Level = 1, Type = OdfListLevelType.Bullet, BulletChar = "•" },
            new() { Level = 2, Type = OdfListLevelType.Bullet, BulletChar = "–" },
        };

        doc.AddListWithStyle("BulletList", levels);

        string stylesXml = GetStylesXml(doc);

        Assert.Contains("text:list-level-style-bullet", stylesXml);
        Assert.Contains("text:bullet-char=\"•\"", stylesXml);
        Assert.Contains("text:bullet-char=\"–\"", stylesXml);
    }

    /// <summary>
    /// 驗證回傳的 OdfList 已套用正確樣式名稱，並可新增第 1 層項目。
    /// </summary>
    [Fact]
    public void AddListWithStyle_ReturnedList_HasCorrectStyleNameAndCanAddItems()
    {
        using var doc = TextDocument.Create();
        var levels = new List<OdfListLevelStyle>
        {
            new() { Level = 1, NumFormat = "1", NumSuffix = "." },
        };

        var list = doc.AddListWithStyle("NumList", levels);

        Assert.Equal("NumList", list.StyleName);

        list.AddItem("第一項");
        list.AddItem("第二項");

        string xml = GetContentXml(doc);
        Assert.Contains("第一項", xml);
        Assert.Contains("第二項", xml);
    }

    /// <summary>
    /// 驗證 AddItem(text, level=2) 在清單中建立正確的巢狀結構。
    /// </summary>
    [Fact]
    public void AddItem_Level2_CreatesNestedListStructure()
    {
        using var doc = TextDocument.Create();
        var levels = new List<OdfListLevelStyle>
        {
            new() { Level = 1, NumFormat = "1", NumSuffix = "." },
            new() { Level = 2, NumFormat = "1", NumSuffix = "." },
        };

        var list = doc.AddListWithStyle("LegalList", levels);
        list.AddItem("1. 第一層項目");
        list.AddItem("1.1. 第二層項目", level: 2);
        list.AddItem("2. 第一層另一項");

        string xml = GetContentXml(doc);

        Assert.Contains("text:list", xml);
        Assert.Contains("1. 第一層項目", xml);
        Assert.Contains("1.1. 第二層項目", xml);
        Assert.Contains("2. 第一層另一項", xml);
    }

    /// <summary>
    /// 驗證三層巢狀結構能建立三層嵌套的 text:list 節點。
    /// </summary>
    [Fact]
    public void AddItem_ThreeLevels_CreatesThreeNestedLists()
    {
        using var doc = TextDocument.Create();
        var levels = new List<OdfListLevelStyle>
        {
            new() { Level = 1, NumFormat = "1", NumSuffix = "." },
            new() { Level = 2, NumFormat = "1", NumSuffix = "." },
            new() { Level = 3, NumFormat = "1", NumSuffix = "." },
        };

        var list = doc.AddListWithStyle("LegalList3", levels);
        var item1 = list.AddItem("第一層", level: 1);
        list.AddItem("第二層", level: 2);
        list.AddItem("第三層", level: 3);

        string xml = GetContentXml(doc);

        Assert.Contains("第一層", xml);
        Assert.Contains("第二層", xml);
        Assert.Contains("第三層", xml);

        int listCount = 0;
        int start = 0;
        while ((start = xml.IndexOf("<text:list", start, System.StringComparison.Ordinal)) >= 0)
        {
            listCount++;
            start++;
        }
        Assert.True(listCount >= 3, $"Expected at least 3 text:list nodes, but found {listCount}");
    }

    /// <summary>
    /// 驗證 NumPrefix 寫入正確的 text:num-prefix 屬性。
    /// </summary>
    [Fact]
    public void AddListWithStyle_WithNumPrefix_WritesNumPrefix()
    {
        using var doc = TextDocument.Create();
        var levels = new List<OdfListLevelStyle>
        {
            new() { Level = 1, NumFormat = "1", NumPrefix = "§", NumSuffix = "." },
        };

        doc.AddListWithStyle("SectionList", levels);

        string stylesXml = GetStylesXml(doc);
        Assert.Contains("text:num-prefix=\"§\"", stylesXml);
    }

    /// <summary>
    /// 驗證 list-level-label-alignment 節點含 text:label-followed-by="listtab"。
    /// </summary>
    [Fact]
    public void AddListWithStyle_WithIndent_WritesAlignmentNode()
    {
        using var doc = TextDocument.Create();
        var levels = new List<OdfListLevelStyle>
        {
            new()
            {
                Level = 1,
                NumFormat = "1",
                IndentLeft = OdfLength.FromCentimeters(0.5),
                FirstLineIndent = OdfLength.FromCentimeters(-0.5),
            },
        };

        doc.AddListWithStyle("IndentList", levels);

        string stylesXml = GetStylesXml(doc);
        Assert.Contains("style:list-level-label-alignment", stylesXml);
        Assert.Contains("text:label-followed-by=\"listtab\"", stylesXml);
    }
}
