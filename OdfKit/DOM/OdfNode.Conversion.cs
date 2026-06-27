namespace OdfKit.DOM;

public partial class OdfNode
{
    /// <summary>
    /// 嘗試將此節點轉型為指定的 typed DOM 元素型別，成功時回傳同一個節點實例。
    /// </summary>
    /// <typeparam name="TElement">目標 typed DOM 元素型別</typeparam>
    /// <returns>轉型成功時為同一個 typed DOM 元素實例；否則為 <see langword="null"/></returns>
    public TElement? As<TElement>()
        where TElement : OdfElement
        => this as TElement;
}
