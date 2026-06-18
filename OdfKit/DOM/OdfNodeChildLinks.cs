namespace OdfKit.DOM;

public partial class OdfNode
{
    /// <summary>
    /// 取得第一個子節點（雙向鏈結串列頭指標）。
    /// </summary>
    public OdfNode? FirstChild { get; internal set; }

    /// <summary>
    /// 取得最後一個子節點（雙向鏈結串列尾指標）。
    /// </summary>
    public OdfNode? LastChild { get; internal set; }

    /// <summary>
    /// 取得下一個兄弟節點。
    /// </summary>
    public OdfNode? NextSibling { get; internal set; }

    /// <summary>
    /// 取得上一個兄弟節點。
    /// </summary>
    public OdfNode? PreviousSibling { get; internal set; }

    internal int TryGetSiblingIndexForParent(OdfNode parent)
    {
        if (Parent != parent || SiblingIndex < 0)
        {
            return -1;
        }

        if (SiblingIndex == 0)
        {
            return ReferenceEquals(parent.FirstChild, this) ? 0 : -1;
        }

        if (PreviousSibling is not null && PreviousSibling.SiblingIndex == SiblingIndex - 1)
        {
            return SiblingIndex;
        }

        return -1;
    }

    internal void DetachFromParent()
    {
        Parent?.Children.Remove(this);
    }
}
