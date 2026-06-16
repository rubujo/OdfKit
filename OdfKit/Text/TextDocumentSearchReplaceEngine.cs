using System;
using System.Text.RegularExpressions;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text;

/// <summary>
/// 文字文件搜尋替換引擎（內部協作者）。
/// </summary>
internal static class TextDocumentSearchReplaceEngine
{
    internal static void ReplaceText(
        TextDocument document,
        string search,
        string replacement,
        Action<OdfTextRun>? styleAction)
    {
        ReplaceTextRecursive(document, document.BodyTextRoot, search, replacement, styleAction);
    }

    internal static void ReplaceText(
        TextDocument document,
        Regex regex,
        string replacement,
        Action<OdfTextRun>? styleAction)
    {
        ReplaceTextRegexRecursive(document, document.BodyTextRoot, regex, replacement, styleAction);
    }

    private static void ReplaceTextRecursive(
        TextDocument document,
        OdfNode node,
        string search,
        string replacement,
        Action<OdfTextRun>? styleAction)
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
                    var midRun = new OdfTextRun(mid, document) { Text = replacement };
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
                    ReplaceTextRecursive(document, child, search, replacement, styleAction);
            }
        }

        for (int i = 0; i < node.Children.Count; i++)
            ReplaceTextRecursive(document, node.Children[i], search, replacement, styleAction);
    }

    private static void ReplaceTextRegexRecursive(
        TextDocument document,
        OdfNode node,
        Regex regex,
        string replacement,
        Action<OdfTextRun>? styleAction)
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
                    var midRun = new OdfTextRun(mid, document);
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
            ReplaceTextRegexRecursive(document, node.Children[i], regex, replacement, styleAction);
    }

    private static void NormalizeParagraphTextNodes(OdfNode parent)
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
}
