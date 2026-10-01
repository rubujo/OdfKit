using System;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text;

/// <summary>
/// 文字文件 DOM 節點查詢與建立輔助工具（內部協作者）。
/// </summary>
internal static class TextDocumentDomHelper
{
    /// <summary>
    /// 尋找直接子元素節點。
    /// </summary>
    internal static OdfNode? FindChildElement(OdfNode parent, string localName, string ns)
    {
        foreach (OdfNode child in parent.Children)
        {
            if (child.LocalName == localName && child.NamespaceUri == ns)
                return child;
        }

        return null;
    }

    /// <summary>
    /// 尋找或建立直接子元素節點。
    /// </summary>
    internal static OdfNode FindOrCreateChild(OdfNode parent, string localName, string ns, string prefix)
    {
        foreach (OdfNode child in parent.Children)
        {
            if (child.LocalName == localName && child.NamespaceUri == ns)
                return child;
        }

        var node = new OdfNode(OdfNodeType.Element, localName, ns, prefix);
        OdfNode? successor = FindSchemaSuccessor(parent, localName, ns);
        if (successor is not null)
        {
            parent.InsertBefore(node, successor);
        }
        else
        {
            parent.AppendChild(node);
        }

        return node;
    }

    // office:document-content 與 office:document-styles 的子元素在 ODF schema 中有固定順序；
    // 例如 automatic-styles 必須在 body 之前，附加在最後會使文件不符合 schema。
    private static readonly string[] s_documentContentOrder = ["scripts", "font-face-decls", "automatic-styles", "body"];
    private static readonly string[] s_documentStylesOrder = ["font-face-decls", "styles", "automatic-styles", "master-styles"];

    private static OdfNode? FindSchemaSuccessor(OdfNode parent, string localName, string ns)
    {
        if (ns != OdfNamespaces.Office || parent.NamespaceUri != OdfNamespaces.Office)
        {
            return null;
        }

        string[]? order = parent.LocalName switch
        {
            "document-content" => s_documentContentOrder,
            "document-styles" => s_documentStylesOrder,
            _ => null,
        };
        int index = order is null ? -1 : Array.IndexOf(order, localName);
        if (order is null || index < 0)
        {
            return null;
        }

        foreach (OdfNode child in parent.Children)
        {
            if (child.NamespaceUri == OdfNamespaces.Office && Array.IndexOf(order, child.LocalName) > index)
            {
                return child;
            }
        }

        return null;
    }

    /// <summary>
    /// 解碼 HTML 實體字串（含 <c>&amp;apos;</c> 變體）。
    /// </summary>
    internal static string DecodeHtmlEntities(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        string decoded = System.Net.WebUtility.HtmlDecode(text);
        if (decoded.Contains("&apos;"))
            decoded = decoded.Replace("&apos;", "'");
        if (decoded.Contains("&APOS;"))
            decoded = decoded.Replace("&APOS;", "'");
        return decoded;
    }
}
