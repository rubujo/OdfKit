using System.Collections.Generic;
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
}
