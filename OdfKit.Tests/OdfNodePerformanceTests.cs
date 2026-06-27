using System.Buffers;
using System.Collections.Generic;
using System.Reflection;

using OdfKit.Core;
using OdfKit.DOM;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 驗證 DOM 效能相關優化（SiblingIndex、TextContent 快速路徑）的正確性。
/// </summary>
public class OdfNodePerformanceTests
{
    /// <summary>
    /// 驗證連續 <see cref="OdfNode.InsertAfter"/> 後子節點索引快取仍正確。
    /// </summary>
    [Fact]
    public void InsertAfter_MaintainsSiblingIndexForSequentialInserts()
    {
        var table = new OdfNode(OdfNodeType.Element, "table", OdfNamespaces.Table, "table");
        var firstRow = new OdfNode(OdfNodeType.Element, "table-row", OdfNamespaces.Table, "table");
        table.AppendChild(firstRow);
        Assert.Equal(0, firstRow.SiblingIndex);

        OdfNode? previous = firstRow;
        for (int i = 1; i < 100; i++)
        {
            var row = new OdfNode(OdfNodeType.Element, "table-row", OdfNamespaces.Table, "table");
            table.InsertAfter(row, previous!);
            Assert.Equal(i, row.SiblingIndex);
            previous = row;
        }

        Assert.Equal(100, table.Children.Count);
        for (int i = 0; i < table.Children.Count; i++)
        {
            Assert.Equal(i, table.Children[i].SiblingIndex);
        }
    }

    /// <summary>
    /// 驗證單一文字子節點時 <see cref="OdfNode.TextContent"/> 讀取結果正確。
    /// </summary>
    [Fact]
    public void TextContent_SingleTextChild_ReturnsExpectedValue()
    {
        var paragraph = new OdfNode(OdfNodeType.Element, "p", OdfNamespaces.Text, "text");
        var text = new OdfNode(OdfNodeType.Text, string.Empty, string.Empty) { TextContent = "快速路徑" };
        paragraph.AppendChild(text);

        Assert.Equal("快速路徑", paragraph.TextContent);
    }

    /// <summary>
    /// 驗證 <see cref="OdfNode.TryWriteTextContent"/> 與 <see cref="OdfNode.TextContent"/> 結果一致。
    /// </summary>
    [Fact]
    public void TryWriteTextContent_MatchesTextContent_ForMixedChildren()
    {
        var paragraph = new OdfNode(OdfNodeType.Element, "p", OdfNamespaces.Text, "text");
        paragraph.AppendChild(new OdfNode(OdfNodeType.Text, string.Empty, string.Empty) { TextContent = "Hello" });
        paragraph.AppendChild(new OdfNode(OdfNodeType.Element, "line-break", OdfNamespaces.Text, "text"));
        paragraph.AppendChild(new OdfNode(OdfNodeType.Text, string.Empty, string.Empty) { TextContent = "World" });

        var bufferWriter = new ArrayBufferWriter<char>();
        Assert.True(paragraph.TryWriteTextContent(bufferWriter));

        Assert.Equal(paragraph.TextContent, new string(bufferWriter.WrittenSpan));
    }

    /// <summary>
    /// 驗證 <see cref="OdfAttributeName"/> 雜湊在常見屬性名稱下可穩定查詢字典。
    /// </summary>
    [Fact]
    public void OdfAttributeName_HashCode_AllowsStableDictionaryLookup()
    {
        var attributes = new Dictionary<OdfAttributeName, string>
        {
            [new OdfAttributeName("name", OdfNamespaces.Table)] = "Sheet1",
            [new OdfAttributeName("style-name", OdfNamespaces.Table)] = "Default",
            [new OdfAttributeName("cell-range-address", OdfNamespaces.Table)] = "A1",
        };

        Assert.Equal("Sheet1", attributes[new OdfAttributeName("name", OdfNamespaces.Table)]);
        Assert.Equal("Default", attributes[new OdfAttributeName("style-name", OdfNamespaces.Table)]);
    }

    /// <summary>
    /// 驗證 <see cref="OdfNode.PruneAndCollect"/> 會清除子節點隨機存取索引陣列，避免已剪裁子樹被快取保留。
    /// </summary>
    [Fact]
    public void PruneAndCollect_ClearsChildListIndexCache()
    {
        var root = new OdfNode(OdfNodeType.Element, "root", OdfNamespaces.Office, "office");
        var table = new OdfNode(OdfNodeType.Element, "table", OdfNamespaces.Table, "table");
        root.AppendChild(table);
        for (int i = 0; i < 4; i++)
        {
            table.AppendChild(new OdfNode(OdfNodeType.Element, "table-row", OdfNamespaces.Table, "table"));
        }

        _ = table.Children[2];

        FieldInfo cacheField = typeof(OdfNodeChildList).GetField("_indexCache", BindingFlags.NonPublic | BindingFlags.Instance)!;
        Assert.NotNull(cacheField.GetValue(table.Children));

        table.PruneAndCollect();

        Assert.Null(cacheField.GetValue(table.Children));
    }

    /// <summary>
    /// 驗證 <see cref="OdfNode.ReleaseUnusedNodes"/> 會非破壞式釋放子節點索引快取。
    /// </summary>
    [Fact]
    public void ReleaseUnusedNodes_ClearsChildListIndexCacheWithoutPruningDom()
    {
        var root = new OdfNode(OdfNodeType.Element, "root", OdfNamespaces.Office, "office");
        var table = new OdfNode(OdfNodeType.Element, "table", OdfNamespaces.Table, "table");
        root.AppendChild(table);
        for (int i = 0; i < 4; i++)
        {
            table.AppendChild(new OdfNode(OdfNodeType.Element, "table-row", OdfNamespaces.Table, "table"));
        }

        _ = table.Children[2];

        FieldInfo cacheField = typeof(OdfNodeChildList).GetField("_indexCache", BindingFlags.NonPublic | BindingFlags.Instance)!;
        Assert.NotNull(cacheField.GetValue(table.Children));

        int releasedCount = root.ReleaseUnusedNodes();

        Assert.Equal(6, releasedCount);
        Assert.Same(table, root.Children[0]);
        Assert.Equal(4, table.Children.Count);
        Assert.Null(cacheField.GetValue(table.Children));
        Assert.Equal(0, table.Children[0].SiblingIndex);
    }
}
