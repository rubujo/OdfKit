using System;
using System.Collections.Generic;
using System.Globalization;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text;

public partial class OdfParagraph
{
    /// <summary>
    /// 將此段落設定為指定的大綱階層。
    /// </summary>
    /// <param name="outlineLevel">大綱階層，最小值為 1</param>
    /// <returns>目前段落，供鏈式呼叫使用</returns>
    public OdfParagraph SetOutlineLevel(int outlineLevel)
    {
        int normalizedLevel = Math.Max(1, outlineLevel);
        EnsureHeadingNode();
        Node.SetAttribute("outline-level", OdfNamespaces.Text, normalizedLevel.ToString(CultureInfo.InvariantCulture), "text");
        return this;
    }

    /// <summary>
    /// 啟用此大綱段落的自動編號樣式。
    /// </summary>
    /// <param name="styleName">要使用或建立的清單樣式名稱</param>
    /// <returns>目前段落，供鏈式呼叫使用</returns>
    public OdfParagraph EnableAutoNumbering(string styleName)
    {
        if (string.IsNullOrWhiteSpace(styleName))
        {
            styleName = "OutlineNumbering";
        }

        EnsureHeadingNode();
        EnsureOutlineNumberingStyle(styleName);
        Node.SetAttribute("list-style-name", OdfNamespaces.Style, styleName, "style");
        return this;
    }

    private void EnsureHeadingNode()
    {
        if (Node.LocalName == "h" && Node.NamespaceUri == OdfNamespaces.Text)
        {
            return;
        }

        var headingNode = new OdfNode(OdfNodeType.Element, "h", OdfNamespaces.Text, "text");
        CopyAttributes(Node, headingNode);

        foreach (OdfNode child in new List<OdfNode>(Node.Children))
        {
            headingNode.AppendChild(child);
        }

        OdfNode? parent = Node.Parent;
        if (parent is not null)
        {
            parent.InsertBefore(headingNode, Node);
            parent.RemoveChild(Node);
        }

        Node = headingNode;
    }

    private void EnsureOutlineNumberingStyle(string styleName)
    {
        OdfNode officeStyles = TextDocumentDomHelper.FindOrCreateChild(Doc.StylesDom, "styles", OdfNamespaces.Office, "office");
        OdfNode outlineStyle = TextDocumentDomHelper.FindOrCreateChild(officeStyles, "outline-style", OdfNamespaces.Text, "text");
        outlineStyle.SetAttribute("name", OdfNamespaces.Style, styleName, "style");

        for (int level = 1; level <= 10; level++)
        {
            OdfNode levelNode = FindOutlineLevelStyle(outlineStyle, level) ?? CreateOutlineLevelStyle(outlineStyle, level);
            levelNode.SetAttribute("level", OdfNamespaces.Text, level.ToString(CultureInfo.InvariantCulture), "text");
            levelNode.SetAttribute("num-format", OdfNamespaces.Style, "1", "style");
            levelNode.SetAttribute("display-levels", OdfNamespaces.Text, level.ToString(CultureInfo.InvariantCulture), "text");
            levelNode.SetAttribute("num-suffix", OdfNamespaces.Text, ". ", "text");
        }
    }

    private static OdfNode? FindOutlineLevelStyle(OdfNode outlineStyle, int level)
    {
        string levelText = level.ToString(CultureInfo.InvariantCulture);
        foreach (OdfNode child in outlineStyle.Children)
        {
            if (child.LocalName == "outline-level-style" &&
                child.NamespaceUri == OdfNamespaces.Text &&
                child.GetAttribute("level", OdfNamespaces.Text) == levelText)
            {
                return child;
            }
        }

        return null;
    }

    private static OdfNode CreateOutlineLevelStyle(OdfNode outlineStyle, int level)
    {
        var levelNode = new OdfNode(OdfNodeType.Element, "outline-level-style", OdfNamespaces.Text, "text");
        outlineStyle.AppendChild(levelNode);
        return levelNode;
    }

    private static void CopyAttributes(OdfNode source, OdfNode destination)
    {
        foreach (KeyValuePair<OdfAttributeName, string> pair in source.Attributes)
        {
            string? prefix = source.GetAttributePrefix(pair.Key);
            destination.SetAttribute(pair.Key.LocalName, pair.Key.NamespaceUri, pair.Value, prefix);
        }
    }
}
