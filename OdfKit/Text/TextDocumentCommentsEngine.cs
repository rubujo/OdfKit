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
