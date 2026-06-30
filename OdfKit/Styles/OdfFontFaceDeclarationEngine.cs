using System;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Styles;

/// <summary>
/// Provides shared font-face declaration helpers for ODF documents.
/// 提供 ODF 文件共用的字型宣告輔助功能。
/// </summary>
internal static class OdfFontFaceDeclarationEngine
{
    /// <summary>
    /// Adds or updates a font-face declaration in both content and styles DOM roots.
    /// 在 content 與 styles DOM 根節點新增或更新字型宣告。
    /// </summary>
    /// <param name="document">The target ODF document. / 目標 ODF 文件。</param>
    /// <param name="name">The font-face declaration name. / 字型宣告名稱。</param>
    /// <param name="fontFamily">The concrete font family. / 實際字型家族。</param>
    /// <param name="genericFamily">The optional generic family. / 選用的泛用字型家族。</param>
    /// <param name="pitch">The optional font pitch. / 選用的字距類型。</param>
    internal static void AddFontFace(
        OdfDocument document,
        string name,
        string fontFamily,
        string? genericFamily = null,
        string? pitch = null)
    {
        if (document is null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        AddToDom(document.ContentDom, name, fontFamily, genericFamily, pitch);
        AddToDom(document.StylesDom, name, fontFamily, genericFamily, pitch);
    }

    /// <summary>
    /// Adds or updates a font-face declaration in the specified DOM root.
    /// 在指定 DOM 根節點新增或更新字型宣告。
    /// </summary>
    /// <param name="domRoot">The DOM root. / DOM 根節點。</param>
    /// <param name="name">The font-face declaration name. / 字型宣告名稱。</param>
    /// <param name="fontFamily">The concrete font family. / 實際字型家族。</param>
    /// <param name="genericFamily">The optional generic family. / 選用的泛用字型家族。</param>
    /// <param name="pitch">The optional font pitch. / 選用的字距類型。</param>
    internal static void AddToDom(
        OdfNode domRoot,
        string name,
        string fontFamily,
        string? genericFamily,
        string? pitch)
    {
        OdfNode fontDecls = FindOrCreateChild(domRoot, "font-face-decls", OdfNamespaces.Office, "office");
        foreach (OdfNode child in fontDecls.Children)
        {
            if (child.LocalName == "font-face" &&
                child.NamespaceUri == OdfNamespaces.Style &&
                child.GetAttribute("name", OdfNamespaces.Style) == name)
            {
                child.SetAttribute("font-family", OdfNamespaces.Svg, fontFamily, "svg");
                if (genericFamily is not null)
                {
                    child.SetAttribute("font-family-generic", OdfNamespaces.Style, genericFamily, "style");
                }

                if (pitch is not null)
                {
                    child.SetAttribute("font-pitch", OdfNamespaces.Style, pitch, "style");
                }

                return;
            }
        }

        var fontFace = new OdfNode(OdfNodeType.Element, "font-face", OdfNamespaces.Style, "style");
        fontFace.SetAttribute("name", OdfNamespaces.Style, name, "style");
        fontFace.SetAttribute("font-family", OdfNamespaces.Svg, fontFamily, "svg");
        if (genericFamily is not null)
        {
            fontFace.SetAttribute("font-family-generic", OdfNamespaces.Style, genericFamily, "style");
        }

        if (pitch is not null)
        {
            fontFace.SetAttribute("font-pitch", OdfNamespaces.Style, pitch, "style");
        }

        fontDecls.AppendChild(fontFace);
    }

    private static OdfNode FindOrCreateChild(OdfNode parent, string localName, string ns, string prefix)
    {
        foreach (OdfNode child in parent.Children)
        {
            if (child.LocalName == localName && child.NamespaceUri == ns)
            {
                return child;
            }
        }

        var node = new OdfNode(OdfNodeType.Element, localName, ns, prefix);
        parent.AppendChild(node);
        return node;
    }
}
