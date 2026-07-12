using System;
using System.Collections.Generic;
using System.Linq;
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
        OdfParagraph paragraph,
        string search,
        string replacement)
    {
        if (paragraph is null)
            throw new ArgumentNullException(nameof(paragraph));
        if (string.IsNullOrEmpty(search))
            return;

        ReplaceTextInParagraph(paragraph.Node, search, replacement);
    }

    internal static void ReplaceText(
        TextDocument document,
        string search,
        string replacement,
        Action<OdfTextRun>? styleAction)
    {
        if (string.IsNullOrEmpty(search))
            return;

        if (styleAction is null)
        {
            foreach (var paragraphNode in EnumerateParagraphNodes(document.BodyTextRoot))
            {
                ReplaceTextInParagraph(paragraphNode, search, replacement);
            }
            return;
        }

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

    private static IEnumerable<OdfNode> EnumerateParagraphNodes(OdfNode root)
    {
        var stack = new Stack<OdfNode>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            OdfNode current = stack.Pop();

            // "h"（標題）與 "p" 同為段落層級節點，比照 TextDocumentFieldExtractionEngine 的
            // EnumerateParagraphLikeNodes 慣例一併納入，避免標題內文字被 FindText 找到卻無法被
            // ReplaceText 實際取代的既有不一致。
            if (current.NodeType == OdfNodeType.Element &&
                current.NamespaceUri == OdfNamespaces.Text &&
                current.LocalName is "p" or "h")
            {
                yield return current;
            }

            for (int i = current.Children.Count - 1; i >= 0; i--)
            {
                stack.Push(current.Children[i]);
            }
        }
    }

    /// <summary>
    /// 對文件所有段落／標題做單次走訪，找出符合的文字位置（可選擇同時就地取代）。
    /// FindText 與 ReplaceText(string,string) 共用此邏輯，避免各自重複走訪整份文件。
    /// </summary>
    private static List<OdfTextMatch> ScanParagraphs(
        TextDocument document,
        string search,
        OdfTextQueryOptions options,
        bool replaceWith,
        string? replacement)
    {
        var matches = new List<OdfTextMatch>();
        StringComparison comparison = options.MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        int documentOffset = 0;

        // 走訪 ContentDom（而非僅 BodyTextRoot）以涵蓋所有段落／標題節點，與既有
        // OdfDocument.ReplaceText（base 版本）的走訪範圍一致，避免縮小既有取代／尋找涵蓋範圍。
        foreach (OdfNode paragraphNode in EnumerateParagraphNodes(document.ContentDom))
        {
            if (!replaceWith && matches.Count >= options.MaxResults)
            {
                break;
            }

            List<TextNodeSlice> slices = CollectTextNodeSlices(paragraphNode);
            if (slices.Count == 0)
            {
                continue;
            }

            string paragraphText = string.Concat(slices.Select(slice => slice.Text));
            bool paragraphHasMatch = false;
            int startIndex = 0;
            OdfParagraph? paragraph = null;
            while (replaceWith || matches.Count < options.MaxResults)
            {
                int index = paragraphText.IndexOf(search, startIndex, comparison);
                if (index < 0)
                {
                    break;
                }

                if (!options.WholeWord || IsWholeWord(paragraphText, index, search.Length))
                {
                    paragraph ??= new OdfParagraph(paragraphNode, document);
                    matches.Add(new OdfTextMatch(
                        documentOffset + index,
                        search.Length,
                        paragraphText.Substring(index, search.Length),
                        paragraph,
                        index));
                    paragraphHasMatch = true;
                }

                startIndex = index + search.Length;
            }

            if (replaceWith && paragraphHasMatch)
            {
                ReplaceTextInParagraph(paragraphNode, search, replacement!);
            }

            documentOffset += paragraphText.Length;
        }

        return matches;
    }

    /// <summary>
    /// 使用具型別查詢選項尋找文字，單次走訪整份文件（不組完整字串）。
    /// </summary>
    internal static List<OdfTextMatch> FindText(TextDocument document, string search, OdfTextQueryOptions options) =>
        ScanParagraphs(document, search, options, replaceWith: false, replacement: null);

    /// <summary>
    /// 單次走訪整份文件同時尋找並取代文字，回傳所有符合項目（取代與尋找共用同一次走訪，
    /// 不再各自重複掃描）。
    /// </summary>
    internal static List<OdfTextMatch> FindAndReplaceText(TextDocument document, string search, string replacement) =>
        ScanParagraphs(document, search, OdfTextQueryOptions.Default, replaceWith: true, replacement: replacement);

    private static bool IsWholeWord(string text, int index, int length)
    {
        bool leftBoundary = index == 0 || !IsWordCharacter(text[index - 1]);
        int endIndex = index + length;
        bool rightBoundary = endIndex == text.Length || !IsWordCharacter(text[endIndex]);
        return leftBoundary && rightBoundary;
    }

    private static bool IsWordCharacter(char value) => char.IsLetterOrDigit(value) || value == '_';

    internal static void ReplaceTextInParagraph(
        OdfNode paragraphNode,
        string search,
        string replacement)
    {
        List<TextNodeSlice> slices = CollectTextNodeSlices(paragraphNode);
        if (slices.Count == 0)
        {
            return;
        }

        string paragraphText = string.Concat(slices.Select(slice => slice.Text));
        if (paragraphText.Length == 0)
        {
            return;
        }

        List<int> matches = [];
        int nextIndex = 0;
        while (nextIndex <= paragraphText.Length - search.Length)
        {
            int found = paragraphText.IndexOf(search, nextIndex, StringComparison.Ordinal);
            if (found < 0)
            {
                break;
            }

            matches.Add(found);
            nextIndex = found + search.Length;
        }

        if (matches.Count == 0)
        {
            return;
        }

        for (int i = matches.Count - 1; i >= 0; i--)
        {
            int matchStart = matches[i];
            int matchEndExclusive = matchStart + search.Length;

            int startSliceIndex = FindSliceIndex(slices, matchStart);
            int endSliceIndex = FindSliceIndex(slices, matchEndExclusive - 1);
            if (startSliceIndex < 0 || endSliceIndex < 0)
            {
                continue;
            }

            TextNodeSlice startSlice = slices[startSliceIndex];
            TextNodeSlice endSlice = slices[endSliceIndex];
            int startOffset = matchStart - startSlice.Start;
            int endOffsetExclusive = matchEndExclusive - endSlice.Start;

            string startText = startSlice.Node.TextContent;
            string endText = endSlice.Node.TextContent;
            string prefix = startText.Substring(0, startOffset);
            string suffix = endText.Substring(endOffsetExclusive);

            if (startSlice.Node == endSlice.Node)
            {
                startSlice.Node.TextContent = prefix + replacement + suffix;
                continue;
            }

            startSlice.Node.TextContent = prefix + replacement;

            for (int removeIndex = startSliceIndex + 1; removeIndex <= endSliceIndex; removeIndex++)
            {
                OdfNode removeNode = slices[removeIndex].Node;
                if (removeNode == endSlice.Node && suffix.Length > 0)
                {
                    removeNode.TextContent = suffix;
                    continue;
                }

                removeNode.Parent?.RemoveChild(removeNode);
            }
        }
    }

    /// <summary>
    /// 依原始串接文字中的位置，由後向前套用多個互不重疊的替換，並保留未命中區段的節點結構。
    /// </summary>
    /// <param name="elementNode">包含文字子節點的元素。</param>
    /// <param name="replacements">依起始位置遞增排列且互不重疊的替換。</param>
    internal static void ReplaceTextRanges(
        OdfNode elementNode,
        IReadOnlyList<TextReplacement> replacements)
    {
        if (replacements.Count == 0)
        {
            return;
        }

        List<TextNodeSlice> slices = CollectTextNodeSlices(elementNode);
        for (int replacementIndex = replacements.Count - 1; replacementIndex >= 0; replacementIndex--)
        {
            TextReplacement replacement = replacements[replacementIndex];
            if (replacement.Length <= 0)
            {
                continue;
            }

            int endExclusive = replacement.Start + replacement.Length;
            int startSliceIndex = FindSliceIndex(slices, replacement.Start);
            int endSliceIndex = FindSliceIndex(slices, endExclusive - 1);
            if (startSliceIndex < 0 || endSliceIndex < 0)
            {
                continue;
            }

            TextNodeSlice startSlice = slices[startSliceIndex];
            TextNodeSlice endSlice = slices[endSliceIndex];
            int startOffset = replacement.Start - startSlice.Start;
            int endOffsetExclusive = endExclusive - endSlice.Start;
            string startText = startSlice.Node.TextContent;
            string endText = endSlice.Node.TextContent;
            string prefix = startText.Substring(0, startOffset);
            string suffix = endText.Substring(endOffsetExclusive);

            if (startSlice.Node == endSlice.Node)
            {
                startSlice.Node.TextContent = prefix + replacement.Value + suffix;
                continue;
            }

            startSlice.Node.TextContent = prefix + replacement.Value;
            for (int removeIndex = startSliceIndex + 1; removeIndex <= endSliceIndex; removeIndex++)
            {
                OdfNode removeNode = slices[removeIndex].Node;
                if (removeNode == endSlice.Node && suffix.Length > 0)
                {
                    removeNode.TextContent = suffix;
                    continue;
                }

                removeNode.Parent?.RemoveChild(removeNode);
            }
        }
    }

    private static int FindSliceIndex(IReadOnlyList<TextNodeSlice> slices, int position)
    {
        for (int i = 0; i < slices.Count; i++)
        {
            TextNodeSlice slice = slices[i];
            if (position >= slice.Start && position < slice.Start + slice.Text.Length)
            {
                return i;
            }
        }

        return -1;
    }

    private static List<TextNodeSlice> CollectTextNodeSlices(OdfNode paragraphNode)
    {
        List<TextNodeSlice> slices = [];
        int start = 0;
        var stack = new Stack<OdfNode>();
        for (int i = paragraphNode.Children.Count - 1; i >= 0; i--)
        {
            stack.Push(paragraphNode.Children[i]);
        }

        while (stack.Count > 0)
        {
            OdfNode current = stack.Pop();
            if (current.NodeType == OdfNodeType.Text && current.TextContent.Length > 0)
            {
                slices.Add(new TextNodeSlice(current, current.TextContent, start));
                start += current.TextContent.Length;
                continue;
            }

            for (int i = current.Children.Count - 1; i >= 0; i--)
            {
                stack.Push(current.Children[i]);
            }
        }

        return slices;
    }

    private readonly struct TextNodeSlice(OdfNode node, string text, int start)
    {
        public OdfNode Node { get; } = node;

        public string Text { get; } = text;

        public int Start { get; } = start;
    }

    /// <summary>
    /// 描述串接文字中的單一位置替換。
    /// </summary>
    /// <param name="start">替換起始位置。</param>
    /// <param name="length">要移除的原始文字長度。</param>
    /// <param name="value">替換文字。</param>
    internal readonly struct TextReplacement(int start, int length, string value)
    {
        internal int Start { get; } = start;

        internal int Length { get; } = length;

        internal string Value { get; } = value;
    }
}
