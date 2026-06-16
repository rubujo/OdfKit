using System.Xml.Linq;

namespace OdfKit.Core;

/// <summary>
/// ODF 封裝 XML 命名空間複製輔助工具（內部協作者）。
/// </summary>
internal static class OdfPackageXmlNamespaceHelper
{
    /// <summary>
    /// 將來源元素的命名空間宣告複製至目標元素（不覆寫既有宣告）。
    /// </summary>
    internal static void CopyNamespaces(XElement source, XElement target)
    {
        foreach (XAttribute attr in source.Attributes())
        {
            if (attr.IsNamespaceDeclaration && target.Attribute(attr.Name) is null)
                target.SetAttributeValue(attr.Name, attr.Value);
        }
    }
}
