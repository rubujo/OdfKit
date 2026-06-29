using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text;

/// <summary>
/// 修訂追蹤相關的 DOM 遍歷與標記操作工具。
/// </summary>
internal static class OdfTrackedChangeDom
{
    /// <summary>
    /// 在父節點的直接子節點中，依本地名稱與命名空間 URI 尋找第一個符合的節點。
    /// </summary>
    public static OdfNode? FindDirectChild(OdfNode parent, string localName, string ns)
    {
        foreach (OdfNode child in parent.Children)
        {
            if (child.LocalName == localName && child.NamespaceUri == ns)
                return child;
        }

        return null;
    }

    /// <summary>
    /// 還原已刪除內容至變更起始標記之前。
    /// </summary>
    public static void RestoreDeletedContent(
        OdfNode bodyTextRoot,
        OdfNode tcNode,
        string changeId,
        OdfPackage package)
    {
        OdfNode? deletionContent = null;
        foreach (OdfNode changedRegion in tcNode.Children)
        {
            if (changedRegion.GetAttribute("id", OdfNamespaces.Text) == changeId)
            {
                foreach (OdfNode spec in changedRegion.Children)
                {
                    if (spec.LocalName == "deletion" && spec.NamespaceUri == OdfNamespaces.Text)
                    {
                        deletionContent = spec;
                        break;
                    }
                }
            }
        }

        if (deletionContent is null)
            return;

        OdfNode? startNode = FindChangeNode(bodyTextRoot, "change-start", changeId);
        if (startNode is not null && startNode.Parent is not null)
        {
            OdfNode parent = startNode.Parent;
            foreach (OdfNode child in deletionContent.Children)
            {
                if (child.LocalName != "change-info")
                {
                    OdfNode imported = OdfNode.ImportNode(child, package, package);
                    parent.InsertBefore(imported, startNode);
                }
            }
        }
    }

    /// <summary>
    /// 在子樹中尋找指定變更 ID 的變更標記節點。
    /// </summary>
    public static OdfNode? FindChangeNode(OdfNode root, string localName, string changeId)
    {
        if (root.LocalName == localName &&
            root.NamespaceUri == OdfNamespaces.Text &&
            root.GetAttribute("change-id", OdfNamespaces.Text) == changeId)
        {
            return root;
        }

        foreach (OdfNode child in root.Children)
        {
            OdfNode? found = FindChangeNode(child, localName, changeId);
            if (found is not null)
                return found;
        }

        return null;
    }

    /// <summary>
    /// Removes remove change markers for id.
    /// 移除指定變更 ID 的所有變更起始／結束標記。
    /// </summary>
    public static void RemoveChangeMarkersForId(OdfNode node, string changeId)
    {
        for (int i = node.Children.Count - 1; i >= 0; i--)
        {
            OdfNode child = node.Children[i];
            if ((child.LocalName == "change-start" || child.LocalName == "change-end") &&
                child.NamespaceUri == OdfNamespaces.Text &&
                child.GetAttribute("change-id", OdfNamespaces.Text) == changeId)
            {
                node.RemoveChild(child);
            }
            else
            {
                RemoveChangeMarkersForId(child, changeId);
            }
        }
    }

    /// <summary>
    /// 從 <c>text:tracked-changes</c> 節點擷取修訂中繼資料。
    /// </summary>
    public static void ExtractTrackedChangesMeta(OdfNode tcNode, Dictionary<string, string> changes)
    {
        foreach (OdfNode changedRegion in tcNode.Children)
        {
            string? id = changedRegion.GetAttribute("id", OdfNamespaces.Text);
            if (string.IsNullOrEmpty(id))
                continue;

            foreach (OdfNode spec in changedRegion.Children)
            {
                if (spec.LocalName == "insertion" && spec.NamespaceUri == OdfNamespaces.Text)
                {
                    changes[id!] = "insertion";
                }
                else if (spec.LocalName == "deletion" && spec.NamespaceUri == OdfNamespaces.Text)
                {
                    changes[id!] = "deletion";
                }
                else if (spec.LocalName == "format-change" && spec.NamespaceUri == OdfNamespaces.Text)
                {
                    changes[id!] = "format-change";
                }
            }
        }
    }

    /// <summary>
    /// Removes cleanup remaining change markers.
    /// 清除子樹中所有殘留的變更標記節點。
    /// </summary>
    public static void CleanupRemainingChangeMarkers(OdfNode node)
    {
        for (int i = node.Children.Count - 1; i >= 0; i--)
        {
            OdfNode child = node.Children[i];
            if ((child.LocalName == "change-start" || child.LocalName == "change-end") &&
                child.NamespaceUri == OdfNamespaces.Text)
            {
                node.RemoveChild(child);
            }
            else
            {
                CleanupRemainingChangeMarkers(child);
            }
        }
    }
}
