using System.Collections.Generic;
using System.Text;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text;

/// <summary>
/// 修訂追蹤區間的文字擷取與格式變更影響節點判定工具。
/// </summary>
internal static class OdfTrackedChangeTextExtractor
{
    /// <summary>
    /// 擷取節點文字內容，略過 <c>office:change-info</c> 子節點。
    /// </summary>
    public static void ExtractTextContentIgnoringChangeInfo(OdfNode node, StringBuilder sb)
    {
        if (node.LocalName == "change-info" && node.NamespaceUri == OdfNamespaces.Office)
            return;

        if (node.NodeType == OdfNodeType.Text)
            sb.Append(node.TextContent);

        foreach (OdfNode child in node.Children)
            ExtractTextContentIgnoringChangeInfo(child, sb);
    }

    /// <summary>
    /// 擷取變更起始與結束標記之間的文字內容。
    /// </summary>
    public static string ExtractTextBetweenMarkers(OdfNode root, string changeId)
    {
        var sb = new StringBuilder();
        bool collect = false;
        ExtractTextBetweenMarkersRecursive(root, changeId, ref collect, sb);
        return sb.ToString();
    }

    /// <summary>
    /// 取得格式變更修訂所影響的 DOM 節點。
    /// </summary>
    public static List<OdfNode> GetAffectedNodesForFormatChange(
        OdfNode bodyTextRoot,
        string changeId,
        OdfNode? trackedChangesNode)
    {
        List<OdfNode> affected = [];

        string? targetFamily = null;
        if (trackedChangesNode is not null)
        {
            foreach (OdfNode region in trackedChangesNode.Children)
            {
                if (region.GetAttribute("id", OdfNamespaces.Text) == changeId)
                {
                    foreach (OdfNode spec in region.Children)
                    {
                        if (spec.LocalName == "format-change" && spec.NamespaceUri == OdfNamespaces.Text)
                        {
                            targetFamily = spec.GetAttribute("target-family", OdfNamespaces.Text);
                            break;
                        }
                    }
                }
            }
        }

        OdfNode? startNode = OdfTrackedChangeDom.FindChangeNode(bodyTextRoot, "change-start", changeId);
        if (startNode is null || startNode.Parent is null)
            return affected;

        OdfNode parent = startNode.Parent;

        if (targetFamily == "paragraph")
        {
            affected.Add(parent);
            return affected;
        }

        if (parent.LocalName == "p" || parent.LocalName == "h")
        {
            OdfNode? endNode = OdfTrackedChangeDom.FindChangeNode(parent, "change-end", changeId);
            if (endNode is not null && endNode.Parent == parent)
            {
                List<OdfNode> siblingsBetween = [];
                bool collect = false;
                foreach (OdfNode child in parent.Children)
                {
                    if (child == startNode)
                    {
                        collect = true;
                        continue;
                    }

                    if (child == endNode)
                    {
                        collect = false;
                        break;
                    }

                    if (collect)
                        siblingsBetween.Add(child);
                }

                if (siblingsBetween.Count > 0)
                {
                    foreach (OdfNode sibling in siblingsBetween)
                    {
                        if (sibling.LocalName == "span")
                            affected.Add(sibling);
                    }
                }
                else
                {
                    affected.Add(parent);
                }
            }
        }
        else
        {
            OdfNode? endNode = OdfTrackedChangeDom.FindChangeNode(bodyTextRoot, "change-end", changeId);
            if (endNode is not null && endNode.Parent == parent)
            {
                bool collect = false;
                foreach (OdfNode child in parent.Children)
                {
                    if (child == startNode)
                    {
                        collect = true;
                        continue;
                    }

                    if (child == endNode)
                    {
                        collect = false;
                        break;
                    }

                    if (collect)
                        affected.Add(child);
                }
            }
        }

        if (affected.Count == 0)
            affected.Add(parent);

        return affected;
    }

    private static void ExtractTextBetweenMarkersRecursive(
        OdfNode node,
        string changeId,
        ref bool collect,
        StringBuilder sb)
    {
        if (node.LocalName == "change-start" &&
            node.NamespaceUri == OdfNamespaces.Text &&
            node.GetAttribute("change-id", OdfNamespaces.Text) == changeId)
        {
            collect = true;
            return;
        }

        if (node.LocalName == "change-end" &&
            node.NamespaceUri == OdfNamespaces.Text &&
            node.GetAttribute("change-id", OdfNamespaces.Text) == changeId)
        {
            collect = false;
            return;
        }

        if (collect && node.NodeType == OdfNodeType.Text)
            sb.Append(node.TextContent);

        foreach (OdfNode child in node.Children)
            ExtractTextBetweenMarkersRecursive(child, changeId, ref collect, sb);
    }
}
