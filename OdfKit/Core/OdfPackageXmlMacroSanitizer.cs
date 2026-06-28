using System;
using System.Collections.Generic;
using OdfKit.DOM;

namespace OdfKit.Core;

/// <summary>
/// ODF XML 節點巨集與指令碼參考淨化引擎（內部協作者）。
/// </summary>
internal static class OdfPackageXmlMacroSanitizer
{
    /// <summary>
    /// 遞迴淨化指定的 XML 節點，移除事件監聽器與巨集或指令碼屬性。
    /// </summary>
    /// <param name="node">要淨化的 ODF 節點</param>
    /// <returns>若節點被修改則為 <see langword="true"/>；否則為 <see langword="false"/></returns>
    internal static bool SanitizeNode(OdfNode node)
    {
        if (node == null)
            return false;

        bool modified = false;

        for (int i = node.Children.Count - 1; i >= 0; i--)
        {
            OdfNode child = node.Children[i];
            if (child.NodeType == OdfNodeType.Element)
            {
                bool shouldRemove =
                    (child.LocalName == "scripts" && child.NamespaceUri == OdfNamespaces.Office) ||
                    (child.LocalName == "event-listeners" && (child.NamespaceUri == OdfNamespaces.Office || child.NamespaceUri == OdfNamespaces.Presentation)) ||
                    (child.LocalName == "event-listener" && (child.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:script:1.0" || child.NamespaceUri == OdfNamespaces.Presentation)) ||
                    (child.LocalName == "script" && child.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:script:1.0") ||
                    child.NamespaceUri == "urn:oasis:names:tc:opendocument:xmlns:script:1.0";

                if (shouldRemove)
                {
                    node.RemoveChild(child);
                    modified = true;
                }
                else if (SanitizeNode(child))
                {
                    modified = true;
                }
            }
        }

        var attributesToRemove = new List<OdfAttributeName>();
        foreach (var attr in node.Attributes)
        {
            string val = attr.Value;
            bool isHref = attr.Key.LocalName == "href" && attr.Key.NamespaceUri == OdfNamespaces.XLink;
            if (val != null)
            {
                if (val.IndexOf("ooo:StarBasic", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    val.IndexOf("vnd.sun.star.script", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    val.IndexOf("vnd.sun.star.VBA", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (isHref && (val.IndexOf("basic/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                val.IndexOf("basic\\", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                val.IndexOf("Scripts/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                val.IndexOf("Scripts\\", StringComparison.OrdinalIgnoreCase) >= 0)))
                {
                    attributesToRemove.Add(attr.Key);
                }
            }
        }

        foreach (OdfAttributeName attrKey in attributesToRemove)
        {
            node.RemoveAttribute(attrKey.LocalName, attrKey.NamespaceUri);
            modified = true;
        }

        return modified;
    }
}
