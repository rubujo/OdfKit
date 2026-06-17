using System;
using System.Collections.Generic;

namespace OdfKit.DOM;

public partial class OdfNode
{
    #region DOM Tree Manipulation


    /// <summary>
    /// 將指定的節點新增至此節點的子節點清單末尾。
    /// </summary>
    /// <param name="child">要新增的子節點</param>
    /// <exception cref="ArgumentNullException">當 <paramref name="child"/> 為 <see langword="null"/> 時擲出</exception>
    /// <exception cref="InvalidOperationException">當嘗試向文字或註解節點新增子節點時擲出</exception>
    public void AppendChild(OdfNode child)
    {
        if (child is null)
            throw new ArgumentNullException(nameof(child));
        if (NodeType == OdfNodeType.Text || NodeType == OdfNodeType.Comment || NodeType == OdfNodeType.ProcessingInstruction)
        {
            throw new InvalidOperationException("Cannot add child nodes to a text or comment node.");
        }

        IsModified = true;
        child.Parent?.RemoveChild(child);
        child.Parent = this;
        Children.Add(child);
        child.SiblingIndex = Children.Count - 1;
    }

    /// <summary>
    /// 在現有的子節點之前插入新的子節點。
    /// </summary>
    /// <param name="newChild">要插入的新子節點</param>
    /// <param name="refChild">參考的子節點，新子節點將插入在此節點之前</param>
    /// <exception cref="ArgumentNullException">當 <paramref name="newChild"/> 或 <paramref name="refChild"/> 為 <see langword="null"/> 時擲出</exception>
    /// <exception cref="InvalidOperationException">當嘗試向文字或註解節點新增子節點，或參考節點不是此節點的子節點時擲出</exception>
    public void InsertBefore(OdfNode newChild, OdfNode refChild)
    {
        if (newChild is null)
            throw new ArgumentNullException(nameof(newChild));
        if (refChild is null)
            throw new ArgumentNullException(nameof(refChild));
        if (NodeType == OdfNodeType.Text || NodeType == OdfNodeType.Comment || NodeType == OdfNodeType.ProcessingInstruction)
        {
            throw new InvalidOperationException("Cannot add child nodes to a text or comment node.");
        }

        int index = ResolveChildIndex(refChild);
        if (index == -1)
        {
            throw new InvalidOperationException("Reference node is not a child of this node.");
        }

        IsModified = true;
        newChild.Parent?.RemoveChild(newChild);
        newChild.Parent = this;
        Children.Insert(index, newChild);
        ReindexChildrenFrom(index);
    }

    /// <summary>
    /// 在現有的子節點之後插入新的子節點。
    /// </summary>
    /// <param name="newChild">要插入的新子節點</param>
    /// <param name="refChild">參考的子節點，新子節點將插入在此節點之後</param>
    /// <exception cref="ArgumentNullException">當 <paramref name="newChild"/> 或 <paramref name="refChild"/> 為 <see langword="null"/> 時擲出</exception>
    /// <exception cref="InvalidOperationException">當嘗試向文字或註解節點新增子節點，或參考節點不是此節點的子節點時擲出</exception>
    public void InsertAfter(OdfNode newChild, OdfNode refChild)
    {
        if (newChild is null)
            throw new ArgumentNullException(nameof(newChild));
        if (refChild is null)
            throw new ArgumentNullException(nameof(refChild));
        if (NodeType == OdfNodeType.Text || NodeType == OdfNodeType.Comment || NodeType == OdfNodeType.ProcessingInstruction)
        {
            throw new InvalidOperationException("Cannot add child nodes to a text or comment node.");
        }

        int index = ResolveChildIndex(refChild);
        if (index == -1)
        {
            throw new InvalidOperationException("Reference node is not a child of this node.");
        }

        IsModified = true;
        newChild.Parent?.RemoveChild(newChild);
        newChild.Parent = this;
        int insertIndex = index + 1;
        Children.Insert(insertIndex, newChild);
        ReindexChildrenFrom(insertIndex);
    }

    /// <summary>
    /// 從此節點的子節點清單中移除指定的子節點。
    /// </summary>
    /// <param name="child">要移除的子節點</param>
    /// <exception cref="ArgumentNullException">當 <paramref name="child"/> 為 <see langword="null"/> 時擲出</exception>
    public void RemoveChild(OdfNode child)
    {
        if (child is null)
            throw new ArgumentNullException(nameof(child));

        int index = TryGetCachedChildIndex(child);
        if (index < 0)
        {
            index = Children.IndexOf(child);
        }

        if (index < 0)
        {
            return;
        }

        Children.RemoveAt(index);
        IsModified = true;
        child.Parent = null;
        child.SiblingIndex = -1;
        ReindexChildrenFrom(index);
    }

    /// <summary>
    /// 取得此節點的所有後代節點。
    /// </summary>
    /// <returns>後代節點的列舉</returns>
    public IEnumerable<OdfNode> Descendants()
    {
        foreach (var child in Children)
        {
            yield return child;
            foreach (var descendant in child.Descendants())
            {
                yield return descendant;
            }
        }
    }


    #endregion

    private int ResolveChildIndex(OdfNode refChild)
    {
        int cached = TryGetCachedChildIndex(refChild);
        if (cached >= 0)
        {
            return cached;
        }

        int index = Children.IndexOf(refChild);
        if (index >= 0)
        {
            ReindexChildrenFrom(0);
        }

        return index;
    }

    private int TryGetCachedChildIndex(OdfNode child)
    {
        if (child.Parent != this || child.SiblingIndex < 0 || child.SiblingIndex >= Children.Count)
        {
            return -1;
        }

        return ReferenceEquals(Children[child.SiblingIndex], child) ? child.SiblingIndex : -1;
    }

    private void ReindexChildrenFrom(int startIndex)
    {
        for (int i = startIndex; i < Children.Count; i++)
        {
            Children[i].SiblingIndex = i;
        }
    }
}
