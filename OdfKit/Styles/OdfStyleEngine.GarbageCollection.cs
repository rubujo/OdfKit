using System;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Styles;

public partial class OdfStyleEngine
{
    /// <summary>
    /// 移除未被文件 DOM 或樣式繼承鏈引用的 <c>style:style</c> 樣式定義。
    /// </summary>
    /// <returns>移除的樣式數量</returns>
    public int CollectGarbage()
    {
        RebuildStyleIndex();

        Dictionary<string, OdfNode> styles = CollectPrunableStyles();
        if (styles.Count == 0)
        {
            return 0;
        }

        HashSet<string> usedStyleNames = CollectReferencedStyleNames(styles);
        ExpandReferencedParentStyles(styles, usedStyleNames);

        int removed = 0;
        foreach (KeyValuePair<string, OdfNode> entry in styles)
        {
            if (usedStyleNames.Contains(entry.Key))
            {
                continue;
            }

            OdfNode? parent = entry.Value.Parent;
            if (parent is null)
            {
                continue;
            }

            parent.RemoveChild(entry.Value);
            removed++;
        }

        if (removed > 0)
        {
            RebuildStyleIndex();
        }

        return removed;
    }

    /// <summary>
    /// 移除未被文件 DOM 或樣式繼承鏈引用的 <c>style:style</c> 樣式定義。
    /// </summary>
    /// <returns>移除的樣式數量</returns>
    public int GC() => CollectGarbage();

    private Dictionary<string, OdfNode> CollectPrunableStyles()
    {
        Dictionary<string, OdfNode> styles = new(StringComparer.Ordinal);
        AddPrunableStyles(FindChildElement(_contentRoot, "automatic-styles", OdfNamespaces.Office), styles);
        AddPrunableStyles(FindChildElement(_stylesRoot, "automatic-styles", OdfNamespaces.Office), styles);
        AddPrunableStyles(FindChildElement(_stylesRoot, "styles", OdfNamespaces.Office), styles);
        return styles;
    }

    private static void AddPrunableStyles(OdfNode? container, Dictionary<string, OdfNode> styles)
    {
        if (container is null)
        {
            return;
        }

        foreach (OdfNode child in container.Children)
        {
            if (child.NodeType is not OdfNodeType.Element ||
                child.LocalName != "style" ||
                child.NamespaceUri != OdfNamespaces.Style)
            {
                continue;
            }

            string? name = child.GetAttribute("name", OdfNamespaces.Style);
            if (!string.IsNullOrEmpty(name))
            {
                styles[name!] = child;
            }
        }
    }

    private HashSet<string> CollectReferencedStyleNames(Dictionary<string, OdfNode> styles)
    {
        HashSet<string> referenced = new(StringComparer.Ordinal);
        CollectReferencedStyleNames(_contentRoot, styles, referenced);
        CollectReferencedStyleNames(_stylesRoot, styles, referenced);
        return referenced;
    }

    private static void CollectReferencedStyleNames(
        OdfNode root,
        Dictionary<string, OdfNode> styles,
        HashSet<string> referenced)
    {
        foreach (OdfNode node in EnumerateSelfAndDescendants(root))
        {
            if (node.NodeType is not OdfNodeType.Element)
            {
                continue;
            }

            foreach (KeyValuePair<OdfAttributeName, string> attribute in node.Attributes)
            {
                string localName = attribute.Key.LocalName;
                if (!IsStyleReferenceAttribute(localName) ||
                    !styles.ContainsKey(attribute.Value))
                {
                    continue;
                }

                string? declaredStyleName = node.GetAttribute("name", OdfNamespaces.Style);
                if (node.NamespaceUri == OdfNamespaces.Style &&
                    node.LocalName == "style" &&
                    string.Equals(declaredStyleName, attribute.Value, StringComparison.Ordinal))
                {
                    continue;
                }

                referenced.Add(attribute.Value);
            }
        }
    }

    private static void ExpandReferencedParentStyles(Dictionary<string, OdfNode> styles, HashSet<string> referenced)
    {
        Stack<string> pending = new(referenced);
        while (pending.Count > 0)
        {
            string styleName = pending.Pop();
            if (!styles.TryGetValue(styleName, out OdfNode? styleNode))
            {
                continue;
            }

            string? parentStyleName = styleNode.GetAttribute("parent-style-name", OdfNamespaces.Style);
            if (!string.IsNullOrEmpty(parentStyleName) &&
                styles.ContainsKey(parentStyleName!) &&
                referenced.Add(parentStyleName!))
            {
                pending.Push(parentStyleName!);
            }
        }
    }

    private static bool IsStyleReferenceAttribute(string localName)
    {
        return string.Equals(localName, "style-name", StringComparison.Ordinal) ||
            string.Equals(localName, "apply-style-name", StringComparison.Ordinal) ||
            string.Equals(localName, "next-style-name", StringComparison.Ordinal);
    }

    private static IEnumerable<OdfNode> EnumerateSelfAndDescendants(OdfNode root)
    {
        yield return root;
        foreach (OdfNode descendant in root.Descendants())
        {
            yield return descendant;
        }
    }
}
