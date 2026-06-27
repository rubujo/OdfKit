using System;
using System.Collections.Generic;

namespace OdfKit.DOM;

/// <summary>
/// 提供 LINQ 友善的 ODF DOM 尋覽與鏈式建立擴充方法。
/// </summary>
public static class OdfNodeTraversalExtensions
{
    /// <summary>
    /// 列舉指定節點的直接子節點，並只傳回指定型別。
    /// </summary>
    /// <typeparam name="TNode">要篩選的子節點型別</typeparam>
    /// <param name="node">要尋覽的來源節點</param>
    /// <returns>符合型別的直接子節點列舉</returns>
    /// <exception cref="ArgumentNullException">當 <paramref name="node"/> 為 <see langword="null"/> 時擲出</exception>
    public static IEnumerable<TNode> Children<TNode>(this OdfNode node)
        where TNode : OdfNode
    {
        if (node is null)
        {
            throw new ArgumentNullException(nameof(node));
        }

        foreach (OdfNode child in node.Children)
        {
            if (child is TNode typedChild)
            {
                yield return typedChild;
            }
        }
    }

    /// <summary>
    /// 列舉指定節點的所有後代節點，並只傳回指定型別。
    /// </summary>
    /// <typeparam name="TNode">要篩選的後代節點型別</typeparam>
    /// <param name="node">要尋覽的來源節點</param>
    /// <returns>符合型別的後代節點列舉</returns>
    /// <exception cref="ArgumentNullException">當 <paramref name="node"/> 為 <see langword="null"/> 時擲出</exception>
    public static IEnumerable<TNode> Descendants<TNode>(this OdfNode node)
        where TNode : OdfNode
    {
        if (node is null)
        {
            throw new ArgumentNullException(nameof(node));
        }

        foreach (OdfNode descendant in node.Descendants())
        {
            if (descendant is TNode typedDescendant)
            {
                yield return typedDescendant;
            }
        }
    }

    /// <summary>
    /// 將多個子節點依序加入來源節點，並傳回同一個來源節點以便鏈式建立 DOM。
    /// </summary>
    /// <typeparam name="TNode">來源節點型別</typeparam>
    /// <param name="node">要加入子節點的來源節點</param>
    /// <param name="children">要依序加入的子節點</param>
    /// <returns>同一個來源節點</returns>
    /// <exception cref="ArgumentNullException">
    /// 當 <paramref name="node"/> 或 <paramref name="children"/> 為 <see langword="null"/> 時擲出。
    /// </exception>
    public static TNode Append<TNode>(this TNode node, params OdfNode[] children)
        where TNode : OdfNode
    {
        if (node is null)
        {
            throw new ArgumentNullException(nameof(node));
        }

        if (children is null)
        {
            throw new ArgumentNullException(nameof(children));
        }

        foreach (OdfNode child in children)
        {
            node.AppendChild(child);
        }

        return node;
    }
}
