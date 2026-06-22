using System.Collections.Generic;
using OdfKit.DOM;

namespace OdfKit.Core;

/// <summary>
/// ODF 文件合併樣式參照重寫引擎（內部協作者）。
/// </summary>
internal static class OdfDocumentStyleRemapEngine
{
    /// <summary>
    /// 依樣式重新命名對照表重寫節點樹中的樣式參照。
    /// </summary>
    /// <param name="node">要處理的根節點</param>
    /// <param name="renameMap">樣式重新命名對照表</param>
    internal static void RemapStylesInNodes(OdfNode node, Dictionary<string, string> renameMap)
    {
        var styleNameAttr = new OdfAttributeName("style-name", OdfNamespaces.Text);
        if (node.Attributes.TryGetValue(styleNameAttr, out string? currentStyleName))
        {
            if (currentStyleName != null && renameMap.TryGetValue(currentStyleName, out string? newName))
            {
                node.Attributes[styleNameAttr] = newName;
            }
        }

        var drawStyleAttr = new OdfAttributeName("style-name", OdfNamespaces.Draw);
        if (node.Attributes.TryGetValue(drawStyleAttr, out string? dsName))
        {
            if (dsName != null && renameMap.TryGetValue(dsName, out string? newName))
            {
                node.Attributes[drawStyleAttr] = newName;
            }
        }

        var tableStyleAttr = new OdfAttributeName("style-name", OdfNamespaces.Table);
        if (node.Attributes.TryGetValue(tableStyleAttr, out string? tsName))
        {
            if (tsName != null && renameMap.TryGetValue(tsName, out string? newName))
            {
                node.Attributes[tableStyleAttr] = newName;
            }
        }

        foreach (OdfNode child in node.Children)
        {
            RemapStylesInNodes(child, renameMap);
        }
    }
}
