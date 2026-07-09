namespace OdfKit.DOM;

/// <summary>
/// Provides the OdfUnknownElement API.
/// 表示 OdfKit 尚未提供 typed wrapper 的未知或第三方擴充元素。
/// </summary>
public sealed class OdfUnknownElement : OdfElement
{
    /// <summary>
    /// Initializes a new unknown element with a local name and namespace URI.
    /// 以局部名稱與命名空間 URI 初始化未知元素。
    /// </summary>
    /// <param name="localName">The element local name. / 元素局部名稱。</param>
    /// <param name="namespaceUri">The element namespace URI. / 元素命名空間 URI。</param>
    public OdfUnknownElement(string localName, string namespaceUri)
        : this(localName, namespaceUri, null)
    {
    }

    /// <summary>
    /// Initializes a new unknown element with a local name, namespace URI, and optional prefix.
    /// 以局部名稱、命名空間 URI 與選用前綴初始化未知元素。
    /// </summary>
    /// <param name="localName">The element local name. / 元素局部名稱。</param>
    /// <param name="namespaceUri">The element namespace URI. / 元素命名空間 URI。</param>
    /// <param name="prefix">The optional namespace prefix. / 選用的命名空間前綴。</param>
    public OdfUnknownElement(string localName, string namespaceUri, string? prefix)
        : base(localName, namespaceUri, prefix)
    {
    }
}
