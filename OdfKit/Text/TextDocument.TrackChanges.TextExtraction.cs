using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text;

public partial class TextDocument
{
    #region Tracked Changes - Text Extraction

    private void ExtractTextContentIgnoringChangeInfo(OdfNode node, System.Text.StringBuilder sb)
    {
        if (node.LocalName == "change-info" && node.NamespaceUri == OdfNamespaces.Office)
        {
            return;
        }
        if (node.NodeType == OdfNodeType.Text)
        {
            sb.Append(node.TextContent);
        }
        foreach (var child in node.Children)
        {
            ExtractTextContentIgnoringChangeInfo(child, sb);
        }
    }

    private string ExtractTextBetweenMarkers(OdfNode root, string changeId)
    {
        var sb = new System.Text.StringBuilder();
        bool collect = false;
        ExtractTextBetweenMarkersRecursive(root, changeId, ref collect, sb);
        return sb.ToString();
    }

    private void ExtractTextBetweenMarkersRecursive(OdfNode node, string changeId, ref bool collect, System.Text.StringBuilder sb)
    {
        if (node.LocalName == "change-start" && node.NamespaceUri == OdfNamespaces.Text && node.GetAttribute("change-id", OdfNamespaces.Text) == changeId)
        {
            collect = true;
            return;
        }
        if (node.LocalName == "change-end" && node.NamespaceUri == OdfNamespaces.Text && node.GetAttribute("change-id", OdfNamespaces.Text) == changeId)
        {
            collect = false;
            return;
        }

        if (collect && node.NodeType == OdfNodeType.Text)
        {
            sb.Append(node.TextContent);
        }

        foreach (var child in node.Children)
        {
            ExtractTextBetweenMarkersRecursive(child, changeId, ref collect, sb);
        }
    }
    private List<OdfNode> FindAffectedNodesForFormatChange(string changeId)
    {
        List<OdfNode> affected = [];

        // 從修訂追蹤中尋找 targetFamily
        string? targetFamily = null;
        var tcNode = FindChild(BodyTextRoot, "tracked-changes", OdfNamespaces.Text);
        if (tcNode is not null)
        {
            foreach (var region in tcNode.Children)
            {
                if (region.GetAttribute("id", OdfNamespaces.Text) == changeId)
                {
                    foreach (var spec in region.Children)
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

        var startNode = FindChangeNode(BodyTextRoot, "change-start", changeId);
        if (startNode is null || startNode.Parent is null)
            return affected;

        var parent = startNode.Parent;

        if (targetFamily == "paragraph")
        {
            affected.Add(parent);
            return affected;
        }

        if (parent.LocalName == "p" || parent.LocalName == "h")
        {
            var endNode = FindChangeNode(parent, "change-end", changeId);
            if (endNode is not null && endNode.Parent == parent)
            {
                List<OdfNode> siblingsBetween = [];
                bool collect = false;
                foreach (var child in parent.Children)
                {
                    if (child == startNode)
                    { collect = true; continue; }
                    if (child == endNode)
                    { collect = false; break; }
                    if (collect)
                        siblingsBetween.Add(child);
                }

                if (siblingsBetween.Count > 0)
                {
                    foreach (var sibling in siblingsBetween)
                    {
                        if (sibling.LocalName == "span")
                        {
                            affected.Add(sibling);
                        }
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
            var endNode = FindChangeNode(BodyTextRoot, "change-end", changeId);
            if (endNode is not null && endNode.Parent == parent)
            {
                bool collect = false;
                foreach (var child in parent.Children)
                {
                    if (child == startNode)
                    { collect = true; continue; }
                    if (child == endNode)
                    { collect = false; break; }
                    if (collect)
                        affected.Add(child);
                }
            }
        }

        if (affected.Count == 0)
        {
            affected.Add(parent);
        }

        return affected;
    }

    #endregion
}
