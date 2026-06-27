using System;
using System.Collections.Generic;
using System.Linq;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text;

/// <summary>
/// 文字文件範本欄位反向提取引擎（內部協作者）。
/// </summary>
internal static class TextDocumentFieldExtractionEngine
{
    internal static IReadOnlyDictionary<string, string> ExtractFieldValues(
        TextDocument document,
        string startDelimiter,
        string endDelimiter)
    {
        Dictionary<string, OdfExtractedFieldInfo> fields = ExtractFields(document, startDelimiter, endDelimiter);
        return fields.ToDictionary(static pair => pair.Key, static pair => pair.Value.Value, StringComparer.Ordinal);
    }

    internal static Dictionary<string, OdfExtractedFieldInfo> ExtractFields(
        TextDocument document,
        string startDelimiter,
        string endDelimiter)
    {
        if (document is null)
            throw new ArgumentNullException(nameof(document));
        if (string.IsNullOrEmpty(startDelimiter))
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_TextDocumentFieldExtraction_DelimiterCannotBeEmpty"), nameof(startDelimiter));
        if (string.IsNullOrEmpty(endDelimiter))
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_TextDocumentFieldExtraction_DelimiterCannotBeEmpty"), nameof(endDelimiter));

        Dictionary<string, OdfExtractedFieldInfo> fields = new(StringComparer.Ordinal);
        ExtractDelimitedFields(document.BodyTextRoot, startDelimiter, endDelimiter, fields);
        ExtractBookmarkFields(document.BodyTextRoot, fields);
        ExtractOdfFields(document.BodyTextRoot, fields);
        return fields;
    }

    private static void ExtractDelimitedFields(
        OdfNode root,
        string startDelimiter,
        string endDelimiter,
        Dictionary<string, OdfExtractedFieldInfo> fields)
    {
        foreach (OdfNode paragraph in EnumerateParagraphLikeNodes(root))
        {
            string text = string.Concat(CollectTextNodes(paragraph).Select(static node => node.TextContent));
            int searchIndex = 0;
            while (searchIndex < text.Length)
            {
                int openStart = text.IndexOf(startDelimiter, searchIndex, StringComparison.Ordinal);
                if (openStart < 0)
                    break;

                int nameStart = openStart + startDelimiter.Length;
                int openEnd = text.IndexOf(endDelimiter, nameStart, StringComparison.Ordinal);
                if (openEnd < 0)
                    break;

                string name = text.Substring(nameStart, openEnd - nameStart).Trim();
                if (name.Length == 0 || name[0] == '/')
                {
                    searchIndex = openEnd + endDelimiter.Length;
                    continue;
                }

                string closeMarker = startDelimiter + "/" + name + endDelimiter;
                int valueStart = openEnd + endDelimiter.Length;
                int closeStart = text.IndexOf(closeMarker, valueStart, StringComparison.Ordinal);
                if (closeStart < 0)
                {
                    searchIndex = valueStart;
                    continue;
                }

                string value = text.Substring(valueStart, closeStart - valueStart);
                AddField(fields, name, value, OdfExtractedFieldSource.DelimitedText);
                searchIndex = closeStart + closeMarker.Length;
            }
        }
    }

    private static void ExtractBookmarkFields(OdfNode root, Dictionary<string, OdfExtractedFieldInfo> fields)
    {
        List<(string Name, List<OdfNode> Nodes)> ranges = [];
        foreach (OdfNode node in EnumerateDepthFirst(root))
        {
            if (node.NodeType == OdfNodeType.Text)
            {
                foreach ((string _, List<OdfNode> nodes) in ranges)
                {
                    nodes.Add(node);
                }

                continue;
            }

            if (node.NodeType != OdfNodeType.Element || node.NamespaceUri != OdfNamespaces.Text)
                continue;

            if (node.LocalName == "bookmark-start")
            {
                string? name = node.GetAttribute("name", OdfNamespaces.Text);
                if (!string.IsNullOrEmpty(name))
                {
                    ranges.Add((name!, []));
                }

                continue;
            }

            if (node.LocalName == "bookmark-end")
            {
                string? name = node.GetAttribute("name", OdfNamespaces.Text);
                if (string.IsNullOrEmpty(name))
                    continue;

                int rangeIndex = ranges.FindLastIndex(range => range.Name == name);
                if (rangeIndex >= 0)
                {
                    string value = string.Concat(ranges[rangeIndex].Nodes.Select(GetVisibleText));
                    AddField(fields, name!, value, OdfExtractedFieldSource.Bookmark);
                    ranges.RemoveAt(rangeIndex);
                }

                continue;
            }

        }
    }

    private static void ExtractOdfFields(OdfNode root, Dictionary<string, OdfExtractedFieldInfo> fields)
    {
        foreach (OdfNode node in EnumerateDepthFirst(root))
        {
            if (node.NodeType != OdfNodeType.Element || node.NamespaceUri != OdfNamespaces.Text)
                continue;

            if (node.LocalName is not ("variable-set" or "user-field-input"))
                continue;

            string? name = node.GetAttribute("name", OdfNamespaces.Text);
            if (string.IsNullOrEmpty(name))
                continue;

            AddField(fields, name!, GetVisibleText(node), OdfExtractedFieldSource.OdfField);
        }
    }

    private static IEnumerable<OdfNode> EnumerateParagraphLikeNodes(OdfNode root)
    {
        foreach (OdfNode node in EnumerateDepthFirst(root))
        {
            if (node.NodeType == OdfNodeType.Element &&
                node.NamespaceUri == OdfNamespaces.Text &&
                node.LocalName is "p" or "h")
            {
                yield return node;
            }
        }
    }

    private static IEnumerable<OdfNode> EnumerateDepthFirst(OdfNode root)
    {
        Stack<OdfNode> stack = new();
        stack.Push(root);
        while (stack.Count > 0)
        {
            OdfNode node = stack.Pop();
            yield return node;

            for (int i = node.Children.Count - 1; i >= 0; i--)
            {
                stack.Push(node.Children[i]);
            }
        }
    }

    private static List<OdfNode> CollectTextNodes(OdfNode root)
    {
        List<OdfNode> nodes = [];
        foreach (OdfNode node in EnumerateDepthFirst(root))
        {
            if (node.NodeType == OdfNodeType.Text)
            {
                nodes.Add(node);
            }
        }

        return nodes;
    }

    private static string GetVisibleText(OdfNode node)
    {
        if (node.NodeType == OdfNodeType.Text)
            return node.TextContent;

        return string.Concat(CollectTextNodes(node).Select(static textNode => textNode.TextContent));
    }

    private static void AddField(
        Dictionary<string, OdfExtractedFieldInfo> fields,
        string name,
        string value,
        OdfExtractedFieldSource source)
    {
        if (!fields.ContainsKey(name))
        {
            fields.Add(name, new OdfExtractedFieldInfo(name, value, source));
        }
    }
}
