using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text;

/// <summary>
/// 文字文件修訂追蹤記錄引擎（內部協作者）。
/// </summary>
internal static class TextDocumentTrackChangesRecordingEngine
{
    /// <summary>
    /// 新增一個追蹤修訂記錄。
    /// </summary>
    internal static string AddTrackedChange(
        TextDocumentMutationContext context,
        string changeType,
        string creator,
        DateTime date,
        OdfNode? extraContent = null,
        string? originalStyleName = null,
        string? targetFamily = null)
    {
        OdfNode bodyTextRoot = context.BodyTextRoot;
        OdfNode? tcNode = null;
        foreach (OdfNode child in bodyTextRoot.Children)
        {
            if (child.LocalName == "tracked-changes" && child.NamespaceUri == OdfNamespaces.Text)
            {
                tcNode = child;
                break;
            }
        }

        if (tcNode is null)
        {
            tcNode = new OdfNode(OdfNodeType.Element, "tracked-changes", OdfNamespaces.Text, "text");
            if (bodyTextRoot.Children.Count > 0)
                bodyTextRoot.InsertBefore(tcNode, bodyTextRoot.Children[0]);
            else
                bodyTextRoot.AppendChild(tcNode);
        }

        string changeId = "ct_" + Guid.NewGuid().ToString("N").Substring(0, 8);

        var changedRegion = new OdfNode(OdfNodeType.Element, "changed-region", OdfNamespaces.Text, "text");
        changedRegion.SetAttribute("id", OdfNamespaces.Text, changeId, "text");

        var typeNode = new OdfNode(OdfNodeType.Element, changeType, OdfNamespaces.Text, "text");
        if (changeType == "deletion" && extraContent is not null)
            typeNode.AppendChild(extraContent.CloneNode(true));
        else if (changeType == "format-change")
        {
            if (originalStyleName is not null)
                typeNode.SetAttribute("style-name", OdfNamespaces.Text, originalStyleName, "text");
            if (targetFamily is not null)
                typeNode.SetAttribute("target-family", OdfNamespaces.Text, targetFamily, "text");
        }

        changedRegion.AppendChild(typeNode);

        var changeInfo = new OdfNode(OdfNodeType.Element, "change-info", OdfNamespaces.Office, "office");
        typeNode.AppendChild(changeInfo);

        var creatorNode = new OdfNode(OdfNodeType.Element, "creator", OdfNamespaces.Dc, "dc");
        creatorNode.TextContent = creator;
        changeInfo.AppendChild(creatorNode);

        var dateNode = new OdfNode(OdfNodeType.Element, "date", OdfNamespaces.Dc, "dc");
        dateNode.TextContent = date == DateTime.MinValue ? "0001-01-01T00:00:00Z" :
                               date == DateTime.MaxValue ? "9999-12-31T23:59:59.9999999Z" :
                               date.ToString("yyyy-MM-ddTHH:mm:ssZ");
        changeInfo.AppendChild(dateNode);

        tcNode.AppendChild(changedRegion);
        return changeId;
    }

    /// <summary>
    /// 取得文件中所有的追蹤修訂。
    /// </summary>
    internal static IEnumerable<OdfTrackedChange> GetTrackedChanges(TextDocument document, TextDocumentMutationContext context)
    {
        var list = new List<OdfTrackedChange>();
        OdfNode bodyTextRoot = context.BodyTextRoot;
        OdfNode? tcNode = TextDocumentDomHelper.FindChildElement(bodyTextRoot, "tracked-changes", OdfNamespaces.Text);
        if (tcNode is null)
            return list;

        foreach (OdfNode changedRegion in tcNode.Children)
        {
            string? id = changedRegion.GetAttribute("id", OdfNamespaces.Text);
            if (string.IsNullOrEmpty(id))
                continue;

            string changeType = "";
            string creator = "";
            DateTime date = DateTime.MinValue;
            OdfNode? specNode = null;

            foreach (OdfNode child in changedRegion.Children)
            {
                if ((child.LocalName == "insertion" || child.LocalName == "deletion" || child.LocalName == "format-change") &&
                    child.NamespaceUri == OdfNamespaces.Text)
                {
                    changeType = child.LocalName;
                    specNode = child;
                    break;
                }
            }

            if (specNode is not null)
            {
                OdfNode? changeInfo = TextDocumentDomHelper.FindChildElement(specNode, "change-info", OdfNamespaces.Office);
                if (changeInfo is not null)
                {
                    OdfNode? creatorNode = TextDocumentDomHelper.FindChildElement(changeInfo, "creator", OdfNamespaces.Dc);
                    if (creatorNode is not null)
                        creator = creatorNode.TextContent ?? "";

                    OdfNode? dateNode = TextDocumentDomHelper.FindChildElement(changeInfo, "date", OdfNamespaces.Dc);
                    if (dateNode is not null)
                    {
                        string? textContent = dateNode.TextContent;
                        if (!string.IsNullOrEmpty(textContent))
                        {
                            if (textContent == "0001-01-01T00:00:00Z" || textContent.StartsWith("0001-01-01"))
                                date = DateTime.MinValue;
                            else if (textContent == "9999-12-31T23:59:59.9999999Z" || textContent.StartsWith("9999-12-31"))
                                date = DateTime.MaxValue;
                            else if (DateTime.TryParse(textContent, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out DateTime parsedDate))
                                date = parsedDate;
                        }
                    }
                }
            }

            string content = "";
            if (changeType == "deletion" && specNode is not null)
            {
                var sb = new StringBuilder();
                OdfTrackedChangeTextExtractor.ExtractTextContentIgnoringChangeInfo(specNode, sb);
                content = sb.ToString();
            }
            else if (changeType == "insertion" || changeType == "format-change")
            {
                content = OdfTrackedChangeTextExtractor.ExtractTextBetweenMarkers(bodyTextRoot, id!);
            }

            list.Add(new OdfTrackedChange
            {
                RegionId = id!,
                ChangeType = changeType switch
                {
                    "deletion" => OdfChangeType.Deletion,
                    "format-change" => OdfChangeType.FormatChange,
                    _ => OdfChangeType.Insertion,
                },
                Author = creator,
                ChangedAt = date,
                Content = content,
            });
        }

        return list;
    }

    /// <summary>
    /// 追蹤格式變更。
    /// </summary>
    internal static void TrackFormatChange(TextDocument document, OdfNode node, string family)
    {
        if (!document.TrackedChanges)
            return;

        string styleAttr = "style-name";
        string styleNs = family switch
        {
            "table-cell" or "table-row" or "table-column" => OdfNamespaces.Table,
            "graphic" => OdfNamespaces.Draw,
            _ => OdfNamespaces.Text
        };
        string? originalStyleName = node.GetAttribute(styleAttr, styleNs);

        string changeId = document.RecordTrackedChange("format-change", null, originalStyleName, family);

        var startNode = new OdfNode(OdfNodeType.Element, "change-start", OdfNamespaces.Text, "text");
        startNode.SetAttribute("change-id", OdfNamespaces.Text, changeId, "text");
        var endNode = new OdfNode(OdfNodeType.Element, "change-end", OdfNamespaces.Text, "text");
        endNode.SetAttribute("change-id", OdfNamespaces.Text, changeId, "text");

        if (node.LocalName == "p" || node.LocalName == "h")
        {
            if (node.Children.Count > 0)
                node.InsertBefore(startNode, node.Children[0]);
            else
                node.AppendChild(startNode);
            node.AppendChild(endNode);
        }
        else
        {
            OdfNode? parent = node.Parent;
            if (parent is not null)
            {
                parent.InsertBefore(startNode, node);
                parent.InsertAfter(endNode, node);
            }
        }
    }

    /// <summary>
    /// 刪除指定的節點並記錄刪除修訂（若啟用修訂追蹤）。
    /// </summary>
    internal static void DeleteNode(TextDocument document, OdfNode node)
    {
        if (node.Parent is null)
            return;

        OdfNode parent = node.Parent;

        if (document.TrackedChanges)
        {
            string changeId = document.RecordTrackedChange("deletion", node);

            var startNode = new OdfNode(OdfNodeType.Element, "change-start", OdfNamespaces.Text, "text");
            startNode.SetAttribute("change-id", OdfNamespaces.Text, changeId, "text");
            var endNode = new OdfNode(OdfNodeType.Element, "change-end", OdfNamespaces.Text, "text");
            endNode.SetAttribute("change-id", OdfNamespaces.Text, changeId, "text");

            parent.InsertBefore(startNode, node);
            parent.InsertAfter(endNode, node);
            parent.RemoveChild(node);
        }
        else
        {
            parent.RemoveChild(node);
        }
    }
}
