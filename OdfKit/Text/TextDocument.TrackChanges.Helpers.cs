using System;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text;

public partial class TextDocument
{
    #region Tracked Changes - Helpers

    private void RestoreDeletedContent(OdfNode tcNode, string changeId)
    {
        OdfNode? deletionContent = null;
        foreach (var changedRegion in tcNode.Children)
        {
            if (changedRegion.GetAttribute("id", OdfNamespaces.Text) == changeId)
            {
                foreach (var spec in changedRegion.Children)
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

        OdfNode? startNode = FindChangeNode(BodyTextRoot, "change-start", changeId);
        if (startNode is not null && startNode.Parent is not null)
        {
            var parent = startNode.Parent;
            foreach (var child in deletionContent.Children)
            {
                if (child.LocalName != "change-info")
                {
                    var imported = OdfNode.ImportNode(child, Package, Package);
                    parent.InsertBefore(imported, startNode);
                }
            }
        }
    }

    private OdfNode? FindChangeNode(OdfNode root, string localName, string changeId)
    {
        if (root.LocalName == localName && root.NamespaceUri == OdfNamespaces.Text && root.GetAttribute("change-id", OdfNamespaces.Text) == changeId)
        {
            return root;
        }
        foreach (var child in root.Children)
        {
            var found = FindChangeNode(child, localName, changeId);
            if (found is not null)
                return found;
        }
        return null;
    }

    private void RemoveChangeMarkersForId(OdfNode node, string changeId)
    {
        for (int i = node.Children.Count - 1; i >= 0; i--)
        {
            var child = node.Children[i];
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

    private void ExtractTrackedChangesMeta(OdfNode tcNode, Dictionary<string, string> changes)
    {
        foreach (var changedRegion in tcNode.Children)
        {
            string? id = changedRegion.GetAttribute("id", OdfNamespaces.Text);
            if (string.IsNullOrEmpty(id))
                continue;

            foreach (var spec in changedRegion.Children)
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

    private void CleanupRemainingChangeMarkers(OdfNode node)
    {
        for (int i = node.Children.Count - 1; i >= 0; i--)
        {
            var child = node.Children[i];
            if ((child.LocalName == "change-start" || child.LocalName == "change-end") && child.NamespaceUri == OdfNamespaces.Text)
            {
                node.RemoveChild(child);
            }
            else
            {
                CleanupRemainingChangeMarkers(child);
            }
        }
    }

    private class ChangePurger(string targetId)
    {
        private readonly string _targetId = targetId;
        private bool _foundStart = false;
        private bool _foundEnd = false;

        public void Purge(OdfNode node)
        {
            for (int i = node.Children.Count - 1; i >= 0; i--)
            {
                var child = node.Children[i];

                bool isEnd = (child.LocalName == "change-end" && child.NamespaceUri == OdfNamespaces.Text && child.GetAttribute("change-id", OdfNamespaces.Text) == _targetId);
                bool isStart = (child.LocalName == "change-start" && child.NamespaceUri == OdfNamespaces.Text && child.GetAttribute("change-id", OdfNamespaces.Text) == _targetId);

                if (isEnd)
                {
                    _foundEnd = true;
                    node.RemoveChild(child);
                    continue;
                }

                if (isStart)
                {
                    _foundStart = true;
                    node.RemoveChild(child);
                    continue;
                }

                bool wasEndFoundBefore = _foundEnd;
                bool wasStartFoundBefore = _foundStart;

                Purge(child);

                bool containedEnd = (!wasEndFoundBefore && _foundEnd);
                bool containedStart = (!wasStartFoundBefore && _foundStart);

                if (_foundEnd && !_foundStart && !containedEnd)
                {
                    node.RemoveChild(child);
                }
            }
        }
    }



    #endregion
}
