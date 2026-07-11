using System;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text;

/// <summary>
/// 文字文件註解引擎（內部協作者）。
/// </summary>
internal static class TextDocumentCommentsEngine
{
    /// <summary>
    /// 在指定段落中新增註解。
    /// </summary>
    internal static void AddComment(OdfParagraph paragraph, OdfComment comment)
    {
        if (paragraph is null)
            throw new ArgumentNullException(nameof(paragraph));
        if (comment is null)
            throw new ArgumentNullException(nameof(comment));

        OdfNode node = comment.ToXmlNode();
        if (node.LocalName == "annotation-list")
        {
            foreach (OdfNode child in new List<OdfNode>(node.Children))
            {
                paragraph.Node.AppendChild(child);
            }
        }
        else
        {
            paragraph.Node.AppendChild(node);
        }
    }

    /// <summary>
    /// 取得文件本文中所有最上層註解。
    /// </summary>
    internal static List<OdfComment> GetComments(OdfNode bodyTextRoot)
    {
        List<OdfComment> list = [];
        FindCommentsRecursive(bodyTextRoot, list);
        return list;
    }

    internal static bool UpdateComment(OdfNode bodyTextRoot, string name, string author, string text)
    {
        OdfNode? annotation = FindAnnotation(bodyTextRoot, name);
        if (annotation is null)
            return false;

        OdfNode? creator = null;
        List<OdfNode> paragraphs = [];
        foreach (OdfNode child in annotation.Children)
        {
            if (child.LocalName == "creator" && child.NamespaceUri == OdfNamespaces.Dc)
                creator = child;
            else if (child.LocalName == "p" && child.NamespaceUri == OdfNamespaces.Text)
                paragraphs.Add(child);
        }
        if (creator is null)
        {
            creator = new OdfNode(OdfNodeType.Element, "creator", OdfNamespaces.Dc, "dc");
            if (annotation.Children.Count > 0)
                annotation.InsertBefore(creator, annotation.Children[0]);
            else
                annotation.AppendChild(creator);
        }
        creator.TextContent = author;
        foreach (OdfNode paragraph in paragraphs)
            annotation.RemoveChild(paragraph);
        foreach (string line in text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None))
        {
            annotation.AppendChild(new OdfNode(OdfNodeType.Element, "p", OdfNamespaces.Text, "text")
            {
                TextContent = line,
            });
        }
        return true;
    }

    internal static int RemoveComment(OdfNode bodyTextRoot, string name)
    {
        HashSet<string> names = new(StringComparer.Ordinal) { name };
        bool added;
        do
        {
            added = CollectReplyNames(bodyTextRoot, names);
        }
        while (added);
        return RemoveAnnotations(bodyTextRoot, names);
    }

    private static OdfNode? FindAnnotation(OdfNode root, string name)
    {
        if (root.LocalName == "annotation" && root.NamespaceUri == OdfNamespaces.Office &&
            string.Equals(root.GetAttribute("name", OdfNamespaces.Office), name, StringComparison.Ordinal))
            return root;
        foreach (OdfNode child in root.Children)
        {
            OdfNode? found = FindAnnotation(child, name);
            if (found is not null)
                return found;
        }
        return null;
    }

    private static bool CollectReplyNames(OdfNode root, HashSet<string> names)
    {
        bool added = false;
        if (root.LocalName == "annotation" && root.NamespaceUri == OdfNamespaces.Office &&
            names.Contains(root.GetAttribute("annotation-parent", OdfNamespaces.Office) ?? string.Empty))
        {
            string? name = root.GetAttribute("name", OdfNamespaces.Office);
            if (!string.IsNullOrEmpty(name))
                added |= names.Add(name!);
        }
        foreach (OdfNode child in root.Children)
            added |= CollectReplyNames(child, names);
        return added;
    }

    private static int RemoveAnnotations(OdfNode root, HashSet<string> names)
    {
        List<OdfNode> removals = [];
        int count = 0;
        foreach (OdfNode child in root.Children)
        {
            string? name = child.GetAttribute("name", OdfNamespaces.Office);
            bool annotation = child.NamespaceUri == OdfNamespaces.Office &&
                child.LocalName is "annotation" or "annotation-end" &&
                !string.IsNullOrEmpty(name) && names.Contains(name!);
            if (annotation)
                removals.Add(child);
            else
                count += RemoveAnnotations(child, names);
        }
        foreach (OdfNode removal in removals)
            root.RemoveChild(removal);
        return count + removals.Count;
    }

    private static void FindCommentsRecursive(OdfNode node, List<OdfComment> list)
    {
        if (node.LocalName == "annotation" && node.NamespaceUri == OdfNamespaces.Office)
        {
            string? parent = node.GetAttribute("annotation-parent", OdfNamespaces.Office);
            if (string.IsNullOrEmpty(parent))
            {
                try
                {
                    list.Add(OdfComment.FromXmlNode(node));
                }
                catch (Exception ex)
                {
                    OdfKitDiagnostics.Warn($"Failed to parse comment node: {ex.Message}");
                }
            }
        }

        foreach (OdfNode child in node.Children)
        {
            FindCommentsRecursive(child, list);
        }
    }
}
