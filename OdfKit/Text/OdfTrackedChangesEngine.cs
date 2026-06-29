using System;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text;

/// <summary>
/// 修訂追蹤的接受／拒絕引擎（內部協作者）。
/// </summary>
internal static class OdfTrackedChangesEngine
{
    /// <summary>
    /// Accepts accept all.
    /// 接受文件中所有的追蹤修訂。
    /// </summary>
    public static void AcceptAll(OdfNode bodyTextRoot)
    {
        OdfNode? tcNode = OdfTrackedChangeDom.FindDirectChild(bodyTextRoot, "tracked-changes", OdfNamespaces.Text);
        if (tcNode is null)
            return;

        var changes = new Dictionary<string, string>(StringComparer.Ordinal);
        OdfTrackedChangeDom.ExtractTrackedChangesMeta(tcNode, changes);

        foreach (KeyValuePair<string, string> kvp in changes)
        {
            if (kvp.Value == "deletion")
            {
                var purger = new OdfTrackedChangePurger(kvp.Key);
                purger.Purge(bodyTextRoot);
            }
        }

        OdfTrackedChangeDom.CleanupRemainingChangeMarkers(bodyTextRoot);
        bodyTextRoot.RemoveChild(tcNode);
    }

    /// <summary>
    /// Rejects reject all.
    /// 拒絕文件中所有的追蹤修訂。
    /// </summary>
    public static void RejectAll(OdfNode bodyTextRoot, OdfPackage package)
    {
        OdfNode? tcNode = OdfTrackedChangeDom.FindDirectChild(bodyTextRoot, "tracked-changes", OdfNamespaces.Text);
        if (tcNode is null)
            return;

        var changes = new Dictionary<string, string>(StringComparer.Ordinal);
        OdfTrackedChangeDom.ExtractTrackedChangesMeta(tcNode, changes);

        foreach (KeyValuePair<string, string> kvp in changes)
        {
            RejectChangeByType(bodyTextRoot, tcNode, kvp.Key, kvp.Value, package);
        }

        OdfTrackedChangeDom.CleanupRemainingChangeMarkers(bodyTextRoot);
        bodyTextRoot.RemoveChild(tcNode);
    }

    /// <summary>
    /// Accepts accept change.
    /// 接受指定的追蹤修訂。
    /// </summary>
    public static void AcceptChange(OdfNode bodyTextRoot, string changeId)
    {
        OdfNode? tcNode = OdfTrackedChangeDom.FindDirectChild(bodyTextRoot, "tracked-changes", OdfNamespaces.Text);
        if (tcNode is null)
            return;

        var changes = new Dictionary<string, string>(StringComparer.Ordinal);
        OdfTrackedChangeDom.ExtractTrackedChangesMeta(tcNode, changes);

        if (!changes.TryGetValue(changeId, out string? type))
            return;

        if (type == "deletion")
        {
            var purger = new OdfTrackedChangePurger(changeId);
            purger.Purge(bodyTextRoot);
        }

        OdfTrackedChangeDom.RemoveChangeMarkersForId(bodyTextRoot, changeId);
        RemoveChangedRegionIfPresent(bodyTextRoot, tcNode, changeId);
    }

    /// <summary>
    /// Rejects reject change.
    /// 拒絕指定的追蹤修訂。
    /// </summary>
    public static void RejectChange(OdfNode bodyTextRoot, string changeId, OdfPackage package)
    {
        OdfNode? tcNode = OdfTrackedChangeDom.FindDirectChild(bodyTextRoot, "tracked-changes", OdfNamespaces.Text);
        if (tcNode is null)
            return;

        var changes = new Dictionary<string, string>(StringComparer.Ordinal);
        OdfTrackedChangeDom.ExtractTrackedChangesMeta(tcNode, changes);

        if (!changes.TryGetValue(changeId, out string? type))
            return;

        RejectChangeByType(bodyTextRoot, tcNode, changeId, type, package);
        OdfTrackedChangeDom.RemoveChangeMarkersForId(bodyTextRoot, changeId);
        RemoveChangedRegionIfPresent(bodyTextRoot, tcNode, changeId);
    }

    private static void RejectChangeByType(
        OdfNode bodyTextRoot,
        OdfNode tcNode,
        string changeId,
        string type,
        OdfPackage package)
    {
        if (type == "insertion")
        {
            var purger = new OdfTrackedChangePurger(changeId);
            purger.Purge(bodyTextRoot);
        }
        else if (type == "deletion")
        {
            OdfTrackedChangeDom.RestoreDeletedContent(bodyTextRoot, tcNode, changeId, package);
        }
        else if (type == "format-change")
        {
            RevertFormatChange(bodyTextRoot, tcNode, changeId);
        }
    }

    private static void RevertFormatChange(OdfNode bodyTextRoot, OdfNode tcNode, string changeId)
    {
        string? originalStyleName = null;
        foreach (OdfNode changedRegion in tcNode.Children)
        {
            if (changedRegion.GetAttribute("id", OdfNamespaces.Text) != changeId)
                continue;

            foreach (OdfNode spec in changedRegion.Children)
            {
                if (spec.LocalName == "format-change" && spec.NamespaceUri == OdfNamespaces.Text)
                {
                    originalStyleName = spec.GetAttribute("style-name", OdfNamespaces.Text);
                    break;
                }
            }
        }

        List<OdfNode> affected = OdfTrackedChangeTextExtractor.FindAffectedNodesForFormatChange(bodyTextRoot, changeId, tcNode);
        foreach (OdfNode node in affected)
        {
            string styleAttr = "style-name";
            string styleNs = OdfNamespaces.Text;
            if (node.LocalName is "table-cell" or "table-row" or "table-column")
            {
                styleNs = OdfNamespaces.Table;
            }
            else if (node.LocalName is "object" or "frame")
            {
                styleNs = OdfNamespaces.Draw;
            }

            if (originalStyleName is not null)
            {
                node.SetAttribute(styleAttr, styleNs, originalStyleName);
            }
            else
            {
                node.RemoveAttribute(styleAttr, styleNs);
            }
        }
    }

    private static void RemoveChangedRegionIfPresent(OdfNode bodyTextRoot, OdfNode tcNode, string changeId)
    {
        OdfNode? regionToRemove = null;
        foreach (OdfNode region in tcNode.Children)
        {
            if (region.GetAttribute("id", OdfNamespaces.Text) == changeId)
            {
                regionToRemove = region;
                break;
            }
        }

        if (regionToRemove is not null)
            tcNode.RemoveChild(regionToRemove);
        if (tcNode.Children.Count == 0)
            bodyTextRoot.RemoveChild(tcNode);
    }
}
