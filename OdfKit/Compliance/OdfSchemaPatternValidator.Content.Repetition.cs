using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

using static OdfKit.Compliance.OdfSchemaPatternNodeLists;

namespace OdfKit.Compliance;

internal static partial class OdfSchemaPatternContentMatcher
{
    #region Content Matching - Repetition

    internal static bool ContentAllowsDirectText(
        IReadOnlyList<OdfSchemaPatternNode> nodes,
        bool hasChildElements,
        OdfSchemaPatternMatchContext context)
    {
        return AnyNode(nodes, (hasChildElements, context), static (node, s) => ContentAllowsDirectText(node, s.Item1, s.Item2));
    }

    internal static bool ContentAllowsDirectText(
        OdfSchemaPatternNode node,
        bool hasChildElements,
        OdfSchemaPatternMatchContext context)
    {
        switch (node.Kind)
        {
            case OdfSchemaPatternNodeKind.Mixed:
                return true;
            case OdfSchemaPatternNodeKind.NotAllowed:
                return false;
            case OdfSchemaPatternNodeKind.Text:
            case OdfSchemaPatternNodeKind.Data:
            case OdfSchemaPatternNodeKind.Value:
            case OdfSchemaPatternNodeKind.List:
                return !hasChildElements;
            case OdfSchemaPatternNodeKind.Ref:
                return ReferenceAllowsDirectText(node.ReferenceName, hasChildElements, context);
            case OdfSchemaPatternNodeKind.Choice:
                return AnyNode(node.Children, (hasChildElements, context), static (child, s) => ContentAllowsDirectText(child, s.Item1, s.Item2));
            case OdfSchemaPatternNodeKind.Group:
            case OdfSchemaPatternNodeKind.Interleave:
            case OdfSchemaPatternNodeKind.Optional:
            case OdfSchemaPatternNodeKind.ZeroOrMore:
            case OdfSchemaPatternNodeKind.OneOrMore:
            case OdfSchemaPatternNodeKind.Other:
                return AnyNode(node.Children, (false, context), static (child, s) => ContentAllowsDirectText(child, s.Item1, s.Item2));
            default:
                return false;
        }
    }

    private static bool ReferenceAllowsDirectText(
        string referenceName,
        bool hasChildElements,
        OdfSchemaPatternMatchContext context)
    {
        if (string.IsNullOrWhiteSpace(referenceName) || !context.EnterReference(referenceName))
        {
            return false;
        }

        try
        {
            OdfSchemaPatternDefinition? pattern = context.Schema.FindPattern(referenceName);
            return pattern != null &&
                AnyNode(pattern.Roots, (hasChildElements, context), static (root, s) => ContentAllowsDirectText(root, s.Item1, s.Item2));
        }
        finally
        {
            context.LeaveReference(referenceName);
        }
    }

    private static bool ContentNodeCanMatchEmpty(
        OdfSchemaPatternNode node,
        OdfSchemaPatternMatchContext context)
    {
        return ContentNodeCanMatchEmpty(node, context, new HashSet<string>(StringComparer.Ordinal));
    }

    private static bool ContentNodeCanMatchEmpty(
        OdfSchemaPatternNode node,
        OdfSchemaPatternMatchContext context,
        HashSet<string> visitingReferences)
    {
        switch (node.Kind)
        {
            case OdfSchemaPatternNodeKind.Empty:
            case OdfSchemaPatternNodeKind.Optional:
            case OdfSchemaPatternNodeKind.ZeroOrMore:
                return true;
            case OdfSchemaPatternNodeKind.Text:
                return true;
            case OdfSchemaPatternNodeKind.Choice:
                return AnyNode(node.Children, (context, visitingReferences), static (child, s) => ContentNodeCanMatchEmpty(child, s.Item1, s.Item2));
            case OdfSchemaPatternNodeKind.Group:
            case OdfSchemaPatternNodeKind.Interleave:
            case OdfSchemaPatternNodeKind.Mixed:
            case OdfSchemaPatternNodeKind.Other:
                return AllNode(node.Children, (context, visitingReferences), static (child, s) => ContentNodeCanMatchEmpty(child, s.Item1, s.Item2));
            case OdfSchemaPatternNodeKind.OneOrMore:
                return node.Children.Count > 0 &&
                    AllNode(node.Children, (context, visitingReferences), static (child, s) => ContentNodeCanMatchEmpty(child, s.Item1, s.Item2));
            case OdfSchemaPatternNodeKind.Ref:
                return ReferenceCanMatchEmpty(node.ReferenceName, context, visitingReferences);
            default:
                return false;
        }
    }

    private static bool ReferenceCanMatchEmpty(
        string referenceName,
        OdfSchemaPatternMatchContext context,
        HashSet<string> visitingReferences)
    {
        if (string.IsNullOrWhiteSpace(referenceName) ||
            !visitingReferences.Add(referenceName))
        {
            return false;
        }

        try
        {
            OdfSchemaPatternDefinition? pattern = context.Schema.FindPattern(referenceName);
            return pattern is not null &&
                AnyNode(pattern.Roots, (context, visitingReferences), static (root, s) => ContentNodeCanMatchEmpty(root, s.Item1, s.Item2));
        }
        finally
        {
            visitingReferences.Remove(referenceName);
        }
    }

    private static string CreateInterleaveStateKey(
        int index,
        IReadOnlyList<bool> used,
        IReadOnlyList<bool> oneOrMoreSatisfied)
    {
        return index.ToString(CultureInfo.InvariantCulture) +
            "|" +
            CreateBitString(used) +
            "|" +
            CreateBitString(oneOrMoreSatisfied);
    }

    private static string CreateBitString(IReadOnlyList<bool> values)
    {
        var chars = new char[values.Count];
        for (int i = 0; i < values.Count; i++)
        {
            chars[i] = values[i] ? '1' : '0';
        }

        return new string(chars);
    }

    private static OdfPositionSet MatchOptional(
        OdfSchemaPatternNode node,
        XElement parent,
        IReadOnlyList<XElement> childElements,
        int index,
        OdfSchemaPatternMatchContext context)
    {
        var matches = new OdfPositionSet { index };
        matches.UnionWith(MatchSequence(node.Children, parent, childElements, index, context));
        return matches;
    }

    private static OdfPositionSet MatchRepeated(
        OdfSchemaPatternNode node,
        XElement parent,
        IReadOnlyList<XElement> childElements,
        int index,
        OdfSchemaPatternMatchContext context,
        bool requireOne)
    {
        // 已記憶的 reach 只用於同一個重複節點與同一份子元素清單；防護介入過的結果不記憶（可能被截斷）。
        if (!context.TryGetReach(node, childElements, index, out OdfPositionSet? reach))
        {
            long guardHitsBefore = context.GuardHits;
            reach = OdfSchemaPatternFrontierMatcher.ComputeReach(
                index,
                current => MatchSequence(node.Children, parent, childElements, current, context),
                state => context.TryGetReach(node, childElements, state, out OdfPositionSet? known) ? known : null);
            if (context.GuardHits == guardHitsBefore)
            {
                context.StoreReach(node, childElements, index, reach);
            }
        }

        if (requireOne)
        {
            return reach;
        }

        var withStart = new OdfPositionSet { index };
        withStart.UnionWith(reach);
        return withStart;
    }


    #endregion
}
