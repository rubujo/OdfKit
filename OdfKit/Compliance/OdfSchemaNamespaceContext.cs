using System.Xml.Linq;

namespace OdfKit.Compliance;

/// <summary>
/// 保留外來元素標籤移除前的命名空間範圍，避免複製大量 namespace 宣告。
/// </summary>
internal sealed class OdfSchemaNamespaceContext(XElement element, OdfSchemaNamespaceContext? parent)
{
    private readonly XElement _element = element;
    private readonly OdfSchemaNamespaceContext? _parent = parent;

    internal XNamespace? ResolvePrefix(string prefix)
    {
        if (prefix == "xml")
            return XNamespace.Xml;

        XName declarationName = prefix.Length == 0 ? XName.Get("xmlns") : XNamespace.Xmlns + prefix;
        for (OdfSchemaNamespaceContext? scope = this; scope is not null; scope = scope._parent)
        {
            XAttribute? declaration = scope._element.Attribute(declarationName);
            if (declaration is not null)
                return XNamespace.Get(declaration.Value);
        }
        return null;
    }
}
