using System;

using OdfKit.Compliance;
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
            throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_OdfNode_CannotAddChildNodes_3"));
        }

        IsModified = true;
        child.Parent?.RemoveChild(child);
        Children.Append(child);
        child.InvalidateStyle();
        InvalidateStyle();
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
            throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_OdfNode_CannotAddChildNodes_3"));
        }

        if (refChild.Parent != this)
        {
            throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_OdfNode_ReferenceNodeChildNode_2"));
        }

        IsModified = true;
        newChild.Parent?.RemoveChild(newChild);
        Children.InsertBefore(newChild, refChild);
        newChild.InvalidateStyle();
        InvalidateStyle();
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
            throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_OdfNode_CannotAddChildNodes_3"));
        }

        if (refChild.Parent != this)
        {
            throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_OdfNode_ReferenceNodeChildNode_2"));
        }

        IsModified = true;
        newChild.Parent?.RemoveChild(newChild);
        Children.InsertAfter(newChild, refChild);
        newChild.InvalidateStyle();
        InvalidateStyle();
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

        if (child.Parent != this)
        {
            return;
        }

        IsModified = true;
        Children.Remove(child);
        child.InvalidateStyle();
        InvalidateStyle();
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
}
