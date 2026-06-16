using System;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text;

public partial class TextDocument
{
    #region Tracked Changes - Accept/Reject

    /// <summary>
    /// 接受文件中所有的追蹤修訂。
    /// </summary>
    public void AcceptAllTrackedChanges()
    {
        var tcNode = FindChild(BodyTextRoot, "tracked-changes", OdfNamespaces.Text);
        if (tcNode is null)
            return;

        var changes = new Dictionary<string, string>(StringComparer.Ordinal);
        ExtractTrackedChangesMeta(tcNode, changes);

        foreach (var kvp in changes)
        {
            if (kvp.Value == "deletion")
            {
                var purger = new ChangePurger(kvp.Key);
                purger.Purge(BodyTextRoot);
            }
        }

        CleanupRemainingChangeMarkers(BodyTextRoot);

        BodyTextRoot.RemoveChild(tcNode);
    }

    /// <summary>
    /// 拒絕文件中所有的追蹤修訂。
    /// </summary>
    public void RejectAllTrackedChanges()
    {
        var tcNode = FindChild(BodyTextRoot, "tracked-changes", OdfNamespaces.Text);
        if (tcNode is null)
            return;

        var changes = new Dictionary<string, string>(StringComparer.Ordinal);
        ExtractTrackedChangesMeta(tcNode, changes);

        foreach (var kvp in changes)
        {
            if (kvp.Value == "insertion")
            {
                var purger = new ChangePurger(kvp.Key);
                purger.Purge(BodyTextRoot);
            }
            else if (kvp.Value == "deletion")
            {
                RestoreDeletedContent(tcNode, kvp.Key);
            }
            else if (kvp.Value == "format-change")
            {
                string? originalStyleName = null;
                foreach (var changedRegion in tcNode.Children)
                {
                    if (changedRegion.GetAttribute("id", OdfNamespaces.Text) == kvp.Key)
                    {
                        foreach (var spec in changedRegion.Children)
                        {
                            if (spec.LocalName == "format-change" && spec.NamespaceUri == OdfNamespaces.Text)
                            {
                                originalStyleName = spec.GetAttribute("style-name", OdfNamespaces.Text);
                                break;
                            }
                        }
                    }
                }

                var affected = FindAffectedNodesForFormatChange(kvp.Key);
                foreach (var node in affected)
                {
                    string styleAttr = "style-name";
                    string styleNs = OdfNamespaces.Text;
                    if (node.LocalName == "table-cell" || node.LocalName == "table-row" || node.LocalName == "table-column")
                    {
                        styleNs = OdfNamespaces.Table;
                    }
                    else if (node.LocalName == "object" || node.LocalName == "frame")
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
        }

        CleanupRemainingChangeMarkers(BodyTextRoot);
        BodyTextRoot.RemoveChild(tcNode);
    }

    /// <summary>
    /// 接受指定的追蹤修訂。
    /// </summary>
    /// <param name="changeId">修訂識別碼</param>
    public void AcceptChange(string changeId)
    {
        var tcNode = FindChild(BodyTextRoot, "tracked-changes", OdfNamespaces.Text);
        if (tcNode is null)
            return;

        var changes = new Dictionary<string, string>(StringComparer.Ordinal);
        ExtractTrackedChangesMeta(tcNode, changes);

        if (!changes.TryGetValue(changeId, out var type))
            return;

        if (type == "deletion")
        {
            var purger = new ChangePurger(changeId);
            purger.Purge(BodyTextRoot);
        }

        RemoveChangeMarkersForId(BodyTextRoot, changeId);

        OdfNode? regionToRemove = null;
        foreach (var region in tcNode.Children)
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
            BodyTextRoot.RemoveChild(tcNode);
    }

    /// <summary>
    /// 拒絕指定的追蹤修訂。
    /// </summary>
    /// <param name="changeId">修訂識別碼</param>
    public void RejectChange(string changeId)
    {
        var tcNode = FindChild(BodyTextRoot, "tracked-changes", OdfNamespaces.Text);
        if (tcNode is null)
            return;

        var changes = new Dictionary<string, string>(StringComparer.Ordinal);
        ExtractTrackedChangesMeta(tcNode, changes);

        if (!changes.TryGetValue(changeId, out var type))
            return;

        if (type == "insertion")
        {
            var purger = new ChangePurger(changeId);
            purger.Purge(BodyTextRoot);
        }
        else if (type == "deletion")
        {
            RestoreDeletedContent(tcNode, changeId);
        }
        else if (type == "format-change")
        {
            string? originalStyleName = null;
            foreach (var changedRegion in tcNode.Children)
            {
                if (changedRegion.GetAttribute("id", OdfNamespaces.Text) == changeId)
                {
                    foreach (var spec in changedRegion.Children)
                    {
                        if (spec.LocalName == "format-change" && spec.NamespaceUri == OdfNamespaces.Text)
                        {
                            originalStyleName = spec.GetAttribute("style-name", OdfNamespaces.Text);
                            break;
                        }
                    }
                }
            }

            var affected = FindAffectedNodesForFormatChange(changeId);
            foreach (var node in affected)
            {
                string styleAttr = "style-name";
                string styleNs = OdfNamespaces.Text;
                if (node.LocalName == "table-cell" || node.LocalName == "table-row" || node.LocalName == "table-column")
                {
                    styleNs = OdfNamespaces.Table;
                }
                else if (node.LocalName == "object" || node.LocalName == "frame")
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

        RemoveChangeMarkersForId(BodyTextRoot, changeId);

        OdfNode? regionToRemove = null;
        foreach (var region in tcNode.Children)
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
            BodyTextRoot.RemoveChild(tcNode);
    }

    #endregion
}
