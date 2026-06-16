using System;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text;

public partial class TextDocument
{
    #region XML Helper


    private OdfNode? FindChild(OdfNode parent, string localName, string ns)
    {
        foreach (var child in parent.Children)
        {
            if (child.LocalName == localName && child.NamespaceUri == ns)
                return child;
        }
        return null;
    }

    private static string DecodeHtmlEntities(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;
        string decoded = System.Net.WebUtility.HtmlDecode(text);
        if (decoded.Contains("&apos;"))
        {
            decoded = decoded.Replace("&apos;", "'");
        }
        if (decoded.Contains("&APOS;"))
        {
            decoded = decoded.Replace("&APOS;", "'");
        }
        return decoded;
    }

    /// <summary>
    /// 在文件中新增字型宣告項目。
    /// </summary>
    /// <param name="name">字型代碼或別名</param>
    /// <param name="fontFamily">實際的字型名稱</param>
    /// <param name="genericFamily">泛用字型系列</param>
    /// <param name="pitch">字距模式</param>
    public void AddFontFace(string name, string fontFamily, string? genericFamily = null, string? pitch = null)
    {
        void AddToDom(OdfNode domRoot)
        {
            var fontDecls = FindOrCreateChild(domRoot, "font-face-decls", OdfNamespaces.Office, "office");
            foreach (var child in fontDecls.Children)
            {
                if (child.LocalName == "font-face" && child.NamespaceUri == OdfNamespaces.Style && child.GetAttribute("name", OdfNamespaces.Style) == name)
                {
                    child.SetAttribute("font-family", OdfNamespaces.Svg, fontFamily, "svg");
                    if (genericFamily is not null)
                        child.SetAttribute("font-family-generic", OdfNamespaces.Style, genericFamily, "style");
                    if (pitch is not null)
                        child.SetAttribute("font-pitch", OdfNamespaces.Style, pitch, "style");
                    return;
                }
            }

            var fontFace = new OdfNode(OdfNodeType.Element, "font-face", OdfNamespaces.Style, "style");
            fontFace.SetAttribute("name", OdfNamespaces.Style, name, "style");
            fontFace.SetAttribute("font-family", OdfNamespaces.Svg, fontFamily, "svg");
            if (genericFamily is not null)
                fontFace.SetAttribute("font-family-generic", OdfNamespaces.Style, genericFamily, "style");
            if (pitch is not null)
                fontFace.SetAttribute("font-pitch", OdfNamespaces.Style, pitch, "style");
            fontDecls.AppendChild(fontFace);
        }

        AddToDom(ContentDom);
        if (StylesDom is not null)
            AddToDom(StylesDom);
    }


    #endregion
}
