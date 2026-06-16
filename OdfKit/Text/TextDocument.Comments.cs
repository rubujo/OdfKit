using System;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text;

public partial class TextDocument
{
    #region Comments / Annotations


    /// <summary>
    /// 在指定的段落中新增註解。
    /// </summary>
    /// <param name="paragraph">要新增註解的段落</param>
    /// <param name="comment">註解物件執行個體</param>
    internal void AddComment(OdfParagraph paragraph, OdfComment comment)
    {
        if (paragraph is null)
            throw new ArgumentNullException(nameof(paragraph));
        if (comment is null)
            throw new ArgumentNullException(nameof(comment));

        var node = comment.ToXmlNode();
        if (node.LocalName == "annotation-list")
        {
            foreach (var child in new List<OdfNode>(node.Children))
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
    /// 取得文件中所有註解的列表。
    /// </summary>
    /// <returns>註解物件列表</returns>
    public List<OdfComment> GetComments()
    {
        List<OdfComment> list = [];
        FindCommentsRecursive(BodyTextRoot, list);
        return list;
    }

    private void FindCommentsRecursive(OdfNode node, List<OdfComment> list)
    {
        if (node.LocalName == "annotation" && node.NamespaceUri == OdfNamespaces.Office)
        {
            // 檢查是否為最上層註解（沒有 annotation-parent）
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

        foreach (var child in node.Children)
        {
            FindCommentsRecursive(child, list);
        }
    }


    #endregion
}
