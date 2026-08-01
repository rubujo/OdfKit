using System;

using OdfKit.Compliance;
namespace OdfKit.DOM;
/// <summary>
/// Provides the OdfNode API.
/// 提供 OdfNode API。
/// </summary>

public partial class OdfNode
{
    #region DOM Tree Manipulation


    /// <summary>
    /// Appends child.
    /// 將指定的節點新增至此節點的子節點清單末尾。
    /// </summary>
    /// <param name="child">要新增的子節點</param>
    /// <exception cref="ArgumentNullException">當 <paramref name="child"/> 為 <see langword="null"/> 時擲出</exception>
    /// <exception cref="InvalidOperationException">當嘗試向文字或註解節點新增子節點時擲出</exception>
    public void AppendChild(OdfNode child)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(child, nameof(child));
        if (NodeType == OdfNodeType.Text || NodeType == OdfNodeType.Comment || NodeType == OdfNodeType.ProcessingInstruction)
        {
            throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_OdfNode_CannotAddChildNodes_3"));
        }

        EnsureCanAdoptChild(child);

        IsModified = true;
        child.Parent?.RemoveChild(child);
        Children.Append(child);
        child.InvalidateStyle();
        InvalidateStyle();
    }

    /// <summary>
    /// Inserts before.
    /// 在現有的子節點之前插入新的子節點。
    /// </summary>
    /// <param name="newChild">要插入的新子節點</param>
    /// <param name="refChild">參考的子節點，新子節點將插入在此節點之前</param>
    /// <exception cref="ArgumentNullException">當 <paramref name="newChild"/> 或 <paramref name="refChild"/> 為 <see langword="null"/> 時擲出</exception>
    /// <exception cref="InvalidOperationException">當嘗試向文字或註解節點新增子節點，或參考節點不是此節點的子節點時擲出</exception>
    public void InsertBefore(OdfNode newChild, OdfNode refChild)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(newChild, nameof(newChild));
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(refChild, nameof(refChild));
        if (NodeType == OdfNodeType.Text || NodeType == OdfNodeType.Comment || NodeType == OdfNodeType.ProcessingInstruction)
        {
            throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_OdfNode_CannotAddChildNodes_3"));
        }

        if (refChild.Parent != this)
        {
            throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_OdfNode_ReferenceNodeChildNode_2"));
        }

        if (ReferenceEquals(newChild, refChild))
        {
            return;
        }

        EnsureCanAdoptChild(newChild);

        IsModified = true;
        newChild.Parent?.RemoveChild(newChild);
        Children.InsertBefore(newChild, refChild);
        newChild.InvalidateStyle();
        InvalidateStyle();
    }

    /// <summary>
    /// Inserts after.
    /// 在現有的子節點之後插入新的子節點。
    /// </summary>
    /// <param name="newChild">要插入的新子節點</param>
    /// <param name="refChild">參考的子節點，新子節點將插入在此節點之後</param>
    /// <exception cref="ArgumentNullException">當 <paramref name="newChild"/> 或 <paramref name="refChild"/> 為 <see langword="null"/> 時擲出</exception>
    /// <exception cref="InvalidOperationException">當嘗試向文字或註解節點新增子節點，或參考節點不是此節點的子節點時擲出</exception>
    public void InsertAfter(OdfNode newChild, OdfNode refChild)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(newChild, nameof(newChild));
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(refChild, nameof(refChild));
        if (NodeType == OdfNodeType.Text || NodeType == OdfNodeType.Comment || NodeType == OdfNodeType.ProcessingInstruction)
        {
            throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_OdfNode_CannotAddChildNodes_3"));
        }

        if (refChild.Parent != this)
        {
            throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_OdfNode_ReferenceNodeChildNode_2"));
        }

        if (ReferenceEquals(newChild, refChild))
        {
            return;
        }

        EnsureCanAdoptChild(newChild);

        IsModified = true;
        newChild.Parent?.RemoveChild(newChild);
        Children.InsertAfter(newChild, refChild);
        newChild.InvalidateStyle();
        InvalidateStyle();
    }

    /// <summary>
    /// Removes the specified child node from this node.
    /// 從此節點的子節點清單中移除指定的子節點。
    /// </summary>
    /// <param name="child">The child node to remove. / 要移除的子節點。</param>
    /// <returns><see langword="true"/> if the child node was removed; otherwise, <see langword="false"/>. / 若已移除子節點則為 <see langword="true"/>；否則為 <see langword="false"/>。</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="child"/> is <see langword="null"/>. / 當 <paramref name="child"/> 為 <see langword="null"/> 時擲出。</exception>
    public bool RemoveChild(OdfNode child)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(child, nameof(child));

        if (child.Parent != this)
        {
            return false;
        }

        IsModified = true;
        Children.Unlink(child);
        child.InvalidateStyle();
        InvalidateStyle();
        return true;
    }

    internal void EnsureCanAdoptChild(OdfNode child)
    {
        for (OdfNode? ancestor = this; ancestor is not null; ancestor = ancestor.Parent)
        {
            if (ReferenceEquals(ancestor, child))
            {
                throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_OdfNode_CyclicTreeInsertion"));
            }
        }
    }

    /// <summary>
    /// Performs descendants.
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
}
