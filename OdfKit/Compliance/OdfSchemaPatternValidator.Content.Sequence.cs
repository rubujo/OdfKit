using System;
using System.Collections.Generic;
using System.Xml.Linq;

namespace OdfKit.Compliance;

internal static partial class OdfSchemaPatternContentMatcher
{
    #region Content Matching - Sequence

    internal static OdfPositionSet MatchSequence(
        IReadOnlyList<OdfSchemaPatternNode> nodes,
        XElement parent,
        IReadOnlyList<XElement> childElements,
        int startIndex,
        OdfSchemaPatternMatchContext context)
    {
        // 單一節點的序列直接回傳該節點的結果集，避免為每次重複展開複製整個集合（結果集唯讀）。
        if (nodes.Count == 1)
        {
            return MatchContentNode(nodes[0], parent, childElements, startIndex, context);
        }

        var indices = new OdfPositionSet { startIndex };
        foreach (OdfSchemaPatternNode node in nodes)
        {
            var next = new OdfPositionSet();
            foreach (int index in indices)
            {
                foreach (int matched in MatchContentNode(node, parent, childElements, index, context))
                {
                    next.Add(matched);
                }
            }

            if (next.Count == 0)
            {
                return next;
            }

            indices = next;
        }

        return indices;
    }

    internal static OdfPositionSet MatchContentNode(
        OdfSchemaPatternNode node,
        XElement parent,
        IReadOnlyList<XElement> childElements,
        int index,
        OdfSchemaPatternMatchContext context)
    {
        // 結構性節點的結果只取決於（節點、子元素清單、起點）。巢狀重複會從大量起點重複詢問同一組結果，
        // 沒有記憶時每個起點都重新走過整棵語法樹。循環參照防護介入過的結果可能被截斷，不記憶。
        if (node.Kind is not (OdfSchemaPatternNodeKind.Ref
            or OdfSchemaPatternNodeKind.Group
            or OdfSchemaPatternNodeKind.Choice
            or OdfSchemaPatternNodeKind.Optional
            or OdfSchemaPatternNodeKind.ZeroOrMore
            or OdfSchemaPatternNodeKind.OneOrMore))
        {
            return MatchContentNodeUncached(node, parent, childElements, index, context);
        }

        if (context.TryGetContentMatch(node, childElements, index, out OdfPositionSet? cached))
        {
            return cached;
        }

        long guardHitsBefore = context.GuardHits;
        OdfPositionSet result = MatchContentNodeUncached(node, parent, childElements, index, context);
        if (context.GuardHits == guardHitsBefore)
        {
            context.StoreContentMatch(node, childElements, index, result);
        }

        return result;
    }

    private static OdfPositionSet MatchContentNodeUncached(
        OdfSchemaPatternNode node,
        XElement parent,
        IReadOnlyList<XElement> childElements,
        int index,
        OdfSchemaPatternMatchContext context)
    {
        switch (node.Kind)
        {
            case OdfSchemaPatternNodeKind.Ref:
                return MatchContentReference(node.ReferenceName, parent, childElements, index, context);
            case OdfSchemaPatternNodeKind.NotAllowed:
                return new OdfPositionSet();
            case OdfSchemaPatternNodeKind.Element:
            case OdfSchemaPatternNodeKind.AnyName:
            case OdfSchemaPatternNodeKind.NamespaceName:
            case OdfSchemaPatternNodeKind.Name:
                return MatchSingleElement(node, childElements, index, context);
            case OdfSchemaPatternNodeKind.Group:
            case OdfSchemaPatternNodeKind.Other:
                return MatchSequence(node.Children, parent, childElements, index, context);
            case OdfSchemaPatternNodeKind.Interleave:
                return MatchInterleave(node, parent, childElements, index, context);
            case OdfSchemaPatternNodeKind.Mixed:
                return MatchSequence(node.Children, parent, childElements, index, context);
            case OdfSchemaPatternNodeKind.Choice:
                return MatchChoice(node, parent, childElements, index, context);
            case OdfSchemaPatternNodeKind.Optional:
                return MatchOptional(node, parent, childElements, index, context);
            case OdfSchemaPatternNodeKind.ZeroOrMore:
                return MatchRepeated(node, parent, childElements, index, context, requireOne: false);
            case OdfSchemaPatternNodeKind.OneOrMore:
                return MatchRepeated(node, parent, childElements, index, context, requireOne: true);
            case OdfSchemaPatternNodeKind.Empty:
                return new OdfPositionSet { index };
            case OdfSchemaPatternNodeKind.Text:
                return new OdfPositionSet { index };
            case OdfSchemaPatternNodeKind.Data:
                return OdfSchemaPatternValidator.IsSimpleTextNode(parent) && OdfSchemaPatternValidator.MatchesDataValue(node, parent.Value, context)
                    ? new OdfPositionSet { index }
                    : new OdfPositionSet();
            case OdfSchemaPatternNodeKind.Value:
                return OdfSchemaPatternValidator.IsSimpleTextNode(parent) && OdfSchemaPatternValidator.MatchesLiteralValue(node, parent.Value)
                    ? new OdfPositionSet { index }
                    : new OdfPositionSet();
            case OdfSchemaPatternNodeKind.List:
                return OdfSchemaPatternValidator.IsSimpleTextNode(parent) && OdfSchemaPatternValidator.MatchesListValue(node.Children, parent.Value, context)
                    ? new OdfPositionSet { index }
                    : new OdfPositionSet();
            case OdfSchemaPatternNodeKind.Attribute:
                return OdfSchemaPatternAttributeMatcher.MatchesAttributeNode(node, parent, context)
                    ? new OdfPositionSet { index }
                    : new OdfPositionSet();
            default:
                return new OdfPositionSet();
        }
    }

    // 比對失敗的共用空結果集；所有呼叫端只讀取結果集，不會修改。
    private static readonly OdfPositionSet s_emptyMatch = new();

    private static OdfPositionSet MatchSingleElement(
        OdfSchemaPatternNode node,
        IReadOnlyList<XElement> childElements,
        int index,
        OdfSchemaPatternMatchContext context)
    {
        if (index >= childElements.Count)
        {
            return new OdfPositionSet();
        }

        OdfSchemaPatternMatchContext childContext = context.CreateChildContext();
        return OdfSchemaPatternValidator.MatchesElementNode(node, childElements[index], childContext)
            ? context.GetSingletonSet(index + 1)
            : s_emptyMatch;
    }

    private static OdfPositionSet MatchContentReference(
        string referenceName,
        XElement parent,
        IReadOnlyList<XElement> childElements,
        int index,
        OdfSchemaPatternMatchContext context)
    {
        if (string.IsNullOrWhiteSpace(referenceName))
        {
            return new OdfPositionSet();
        }

        bool entered = context.EnterReference(referenceName);
        if (!entered)
        {
            OdfSchemaPatternMatchContext? recursiveContext = context.CreateRecursiveContext();
            return recursiveContext is null
                ? new OdfPositionSet()
                : MatchContentReferenceWithoutActiveGuard(referenceName, parent, childElements, index, recursiveContext);
        }

        try
        {
            return MatchContentReferenceWithoutActiveGuard(referenceName, parent, childElements, index, context);
        }
        finally
        {
            context.LeaveReference(referenceName);
        }
    }

    private static OdfPositionSet MatchContentReferenceWithoutActiveGuard(
        string referenceName,
        XElement parent,
        IReadOnlyList<XElement> childElements,
        int index,
        OdfSchemaPatternMatchContext context)
    {
        OdfSchemaPatternDefinition? pattern = context.Schema.FindPattern(referenceName);
        if (pattern == null)
        {
            return new OdfPositionSet();
        }

        var matches = new OdfPositionSet();
        foreach (OdfSchemaPatternNode root in pattern.Roots)
        {
            OdfSchemaPatternNode? contentRoot = context.GetStrippedRoot(
                root,
                OdfSchemaPatternAttributeMatcher.StripAttributePatterns);
            if (contentRoot is null)
            {
                matches.Add(index);
                continue;
            }

            matches.UnionWith(MatchContentNode(contentRoot, parent, childElements, index, context));
        }
        return matches;
    }

    private static OdfPositionSet MatchChoice(
        OdfSchemaPatternNode node,
        XElement parent,
        IReadOnlyList<XElement> childElements,
        int index,
        OdfSchemaPatternMatchContext context)
    {
        var matches = new OdfPositionSet();
        foreach (OdfSchemaPatternNode child in node.Children)
        {
            matches.UnionWith(MatchContentNode(child, parent, childElements, index, context));
        }

        return matches;
    }

    private static OdfPositionSet MatchInterleave(
        OdfSchemaPatternNode node,
        XElement parent,
        IReadOnlyList<XElement> childElements,
        int index,
        OdfSchemaPatternMatchContext context)
    {
        List<OdfSchemaPatternNode> interleavedNodes =
            ExpandInterleaveReferences(node.Children, context);
        var matches = new OdfPositionSet();
        var used = new bool[interleavedNodes.Count];
        var oneOrMoreSatisfied = new bool[interleavedNodes.Count];
        var visited = new HashSet<string>();
        MatchInterleaveRecursive(
            interleavedNodes,
            parent,
            childElements,
            index,
            context,
            used,
            oneOrMoreSatisfied,
            visited,
            matches);
        return matches;
    }

    private static List<OdfSchemaPatternNode> ExpandInterleaveReferences(
        IReadOnlyList<OdfSchemaPatternNode> nodes,
        OdfSchemaPatternMatchContext context)
    {
        var expanded = new List<OdfSchemaPatternNode>(nodes.Count);
        foreach (OdfSchemaPatternNode node in nodes)
        {
            if (node.Kind != OdfSchemaPatternNodeKind.Ref ||
                string.IsNullOrWhiteSpace(node.ReferenceName) ||
                !context.EnterReference(node.ReferenceName))
            {
                expanded.Add(node);
                continue;
            }

            try
            {
                OdfSchemaPatternDefinition? pattern = context.Schema.FindPattern(node.ReferenceName);
                if (pattern?.Roots.Count == 1 &&
                    pattern.Roots[0].Kind == OdfSchemaPatternNodeKind.Interleave)
                {
                    expanded.AddRange(ExpandInterleaveReferences(pattern.Roots[0].Children, context));
                }
                else
                {
                    expanded.Add(node);
                }
            }
            finally
            {
                context.LeaveReference(node.ReferenceName);
            }
        }

        return expanded;
    }

    #endregion
}
