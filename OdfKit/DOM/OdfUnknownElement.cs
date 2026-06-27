namespace OdfKit.DOM;

/// <summary>
/// 表示 OdfKit 尚未提供 typed wrapper 的未知或第三方擴充元素。
/// </summary>
public sealed class OdfUnknownElement : OdfElement
{
    /// <summary>
    /// 初始化 <see cref="OdfUnknownElement"/> 類別的新執行個體。
    /// </summary>
    /// <param name="localName">元素局部名稱</param>
    /// <param name="namespaceUri">元素命名空間 URI</param>
    /// <param name="prefix">選用的命名空間前綴</param>
    public OdfUnknownElement(string localName, string namespaceUri, string? prefix = null)
        : base(localName, namespaceUri, prefix)
    {
    }
}
