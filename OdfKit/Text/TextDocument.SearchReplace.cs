using System;
using System.Text.RegularExpressions;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text;

public partial class TextDocument
{
    #region Search & Replace with Actions/Regex


    /// <summary>
    /// 搜尋指定文字並替換為新文字。
    /// </summary>
    /// <param name="search">要搜尋的關鍵字</param>
    /// <param name="replacement">要替換的新文字</param>
    /// <param name="styleAction">套用於替換後文字片段的樣式委派作業</param>
    public void ReplaceText(string search, string replacement, Action<OdfTextRun>? styleAction = null)
    {
        ReplaceTextRecursive(BodyTextRoot, search, replacement, styleAction);
    }

    /// <summary>
    /// 以規則運算式搜尋文字並替換為新文字。
    /// </summary>
    /// <param name="regex">代表搜尋條件的規則運算式物件</param>
    /// <param name="replacement">要替換的新文字</param>
    /// <param name="styleAction">套用於替換後文字片段的樣式委派作業</param>
    public void ReplaceText(Regex regex, string replacement, Action<OdfTextRun>? styleAction = null)
    {
        ReplaceTextRegexRecursive(BodyTextRoot, regex, replacement, styleAction);
    }

    private void ReplaceTextRecursive(OdfNode node, string search, string replacement, Action<OdfTextRun>? styleAction)
    {
        NormalizeParagraphTextNodes(node);

        if (node.NodeType == OdfNodeType.Text)
        {
            string text = node.TextContent;
            if (text.Contains(search))
            {
                if (styleAction is not null && node.Parent is not null)
                {
                    int index = text.IndexOf(search);
                    var left = new OdfNode(OdfNodeType.Text, string.Empty, string.Empty) { TextContent = text.Substring(0, index) };
                    var mid = new OdfNode(OdfNodeType.Element, "span", OdfNamespaces.Text, "text");
                    var midRun = new OdfTextRun(mid, this)
                    {
                        Text = replacement
                    };
                    styleAction(midRun);

                    var right = new OdfNode(OdfNodeType.Text, string.Empty, string.Empty) { TextContent = text.Substring(index + search.Length) };

                    var parent = node.Parent;
                    parent.InsertBefore(left, node);
                    parent.InsertBefore(mid, node);
                    parent.InsertBefore(right, node);
                    parent.RemoveChild(node);
                }
                else
                {
                    node.TextContent = text.Replace(search, replacement);
                }
            }
            return;
        }

        if (node.LocalName == "annotation" && node.NamespaceUri == OdfNamespaces.Office)
        {
            foreach (var child in node.Children)
            {
                if (child.LocalName == "p" && child.NamespaceUri == OdfNamespaces.Text)
                {
                    ReplaceTextRecursive(child, search, replacement, styleAction);
                }
            }
        }

        for (int i = 0; i < node.Children.Count; i++)
        {
            ReplaceTextRecursive(node.Children[i], search, replacement, styleAction);
        }
    }

    private void ReplaceTextRegexRecursive(OdfNode node, Regex regex, string replacement, Action<OdfTextRun>? styleAction)
    {
        NormalizeParagraphTextNodes(node);

        if (node.NodeType == OdfNodeType.Text)
        {
            string text = node.TextContent;
            if (regex.IsMatch(text))
            {
                if (styleAction is not null && node.Parent is not null)
                {
                    var match = regex.Match(text);
                    int index = match.Index;

                    var left = new OdfNode(OdfNodeType.Text, string.Empty, string.Empty) { TextContent = text.Substring(0, index) };
                    var mid = new OdfNode(OdfNodeType.Element, "span", OdfNamespaces.Text, "text");
                    var midRun = new OdfTextRun(mid, this);
                    midRun.Text = regex.Replace(match.Value, replacement);
                    styleAction(midRun);

                    var right = new OdfNode(OdfNodeType.Text, string.Empty, string.Empty) { TextContent = text.Substring(index + match.Length) };

                    var parent = node.Parent;
                    parent.InsertBefore(left, node);
                    parent.InsertBefore(mid, node);
                    parent.InsertBefore(right, node);
                    parent.RemoveChild(node);
                }
                else
                {
                    node.TextContent = regex.Replace(text, replacement);
                }
            }
            return;
        }

        for (int i = 0; i < node.Children.Count; i++)
        {
            ReplaceTextRegexRecursive(node.Children[i], regex, replacement, styleAction);
        }
    }

    private void NormalizeParagraphTextNodes(OdfNode parent)
    {
        if (parent.LocalName == "p" && parent.NamespaceUri == OdfNamespaces.Text)
        {
            for (int i = parent.Children.Count - 2; i >= 0; i--)
            {
                if (parent.Children[i].NodeType == OdfNodeType.Text && parent.Children[i + 1].NodeType == OdfNodeType.Text)
                {
                    parent.Children[i].TextContent += parent.Children[i + 1].TextContent;
                    parent.RemoveChild(parent.Children[i + 1]);
                }
            }
        }
    }


    #endregion
}
