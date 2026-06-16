using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml.Linq;

namespace OdfKit.Compliance;

public static partial class OdfSchemaPatternValidator
{
    #region Content Matching

    private static HashSet<int> MatchSequence(
        IReadOnlyList<OdfSchemaPatternNode> nodes,
        XElement parent,
        IReadOnlyList<XElement> childElements,
        int startIndex,
        MatchContext context)
    {
        var indices = new HashSet<int> { startIndex };
        foreach (OdfSchemaPatternNode node in nodes)
        {
            var next = new HashSet<int>();
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

    private static HashSet<int> MatchContentNode(
        OdfSchemaPatternNode node,
        XElement parent,
        IReadOnlyList<XElement> childElements,
        int index,
        MatchContext context)
    {
        switch (node.Kind)
        {
            case OdfSchemaPatternNodeKind.Ref:
                return MatchContentReference(node.ReferenceName, parent, childElements, index, context);
            case OdfSchemaPatternNodeKind.NotAllowed:
                return new HashSet<int>();
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
                return new HashSet<int> { index };
            case OdfSchemaPatternNodeKind.Text:
                return new HashSet<int> { index };
            case OdfSchemaPatternNodeKind.Data:
                return IsSimpleTextNode(parent) && MatchesDataValue(node, parent.Value, context)
                    ? new HashSet<int> { index }
                    : new HashSet<int>();
            case OdfSchemaPatternNodeKind.Value:
                return IsSimpleTextNode(parent) && MatchesLiteralValue(node, parent.Value)
                    ? new HashSet<int> { index }
                    : new HashSet<int>();
            case OdfSchemaPatternNodeKind.List:
                return IsSimpleTextNode(parent) && MatchesListValue(node.Children, parent.Value, context)
                    ? new HashSet<int> { index }
                    : new HashSet<int>();
            case OdfSchemaPatternNodeKind.Attribute:
                return MatchesAttributeNode(node, parent, context)
                    ? new HashSet<int> { index }
                    : new HashSet<int>();
            default:
                return new HashSet<int>();
        }
    }

    private static HashSet<int> MatchSingleElement(
        OdfSchemaPatternNode node,
        IReadOnlyList<XElement> childElements,
        int index,
        MatchContext context)
    {
        if (index >= childElements.Count)
        {
            return new HashSet<int>();
        }

        var childContext = new MatchContext(context.Schema);
        return MatchesElementNode(node, childElements[index], childContext)
            ? new HashSet<int> { index + 1 }
            : new HashSet<int>();
    }

    private static HashSet<int> MatchContentReference(
        string referenceName,
        XElement parent,
        IReadOnlyList<XElement> childElements,
        int index,
        MatchContext context)
    {
        if (string.IsNullOrWhiteSpace(referenceName) || !context.EnterReference(referenceName))
        {
            return new HashSet<int>();
        }

        try
        {
            OdfSchemaPatternDefinition? pattern = context.Schema.FindPattern(referenceName);
            if (pattern == null)
            {
                return new HashSet<int>();
            }

            var matches = new HashSet<int>();
            foreach (OdfSchemaPatternNode root in pattern.Roots)
            {
                foreach (int matched in MatchContentNode(root, parent, childElements, index, context))
                {
                    matches.Add(matched);
                }
            }
            return matches;
        }
        finally
        {
            context.LeaveReference(referenceName);
        }
    }

    private static HashSet<int> MatchChoice(
        OdfSchemaPatternNode node,
        XElement parent,
        IReadOnlyList<XElement> childElements,
        int index,
        MatchContext context)
    {
        var matches = new HashSet<int>();
        foreach (OdfSchemaPatternNode child in node.Children)
        {
            foreach (int matched in MatchContentNode(child, parent, childElements, index, context))
            {
                matches.Add(matched);
            }
        }

        return matches;
    }

    private static HashSet<int> MatchInterleave(
        OdfSchemaPatternNode node,
        XElement parent,
        IReadOnlyList<XElement> childElements,
        int index,
        MatchContext context)
    {
        var matches = new HashSet<int>();
        var used = new bool[node.Children.Count];
        var oneOrMoreSatisfied = new bool[node.Children.Count];
        var visited = new HashSet<string>();
        MatchInterleaveRecursive(
            node.Children,
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

    private static void MatchInterleaveRecursive(
        IReadOnlyList<OdfSchemaPatternNode> nodes,
        XElement parent,
        IReadOnlyList<XElement> childElements,
        int index,
        MatchContext context,
        bool[] used,
        bool[] oneOrMoreSatisfied,
        HashSet<string> visited,
        HashSet<int> matches)
    {
        string stateKey = CreateInterleaveStateKey(index, used, oneOrMoreSatisfied);
        if (!visited.Add(stateKey))
        {
            return;
        }

        if (InterleaveRequirementsSatisfied(nodes, used, oneOrMoreSatisfied, context))
        {
            matches.Add(index);
        }

        for (int i = 0; i < used.Length; i++)
        {
            OdfSchemaPatternNode interleavedNode = nodes[i];
            if (interleavedNode.Kind == OdfSchemaPatternNodeKind.NotAllowed)
            {
                continue;
            }

            if (interleavedNode.Kind == OdfSchemaPatternNodeKind.ZeroOrMore)
            {
                foreach (int matched in MatchSequence(interleavedNode.Children, parent, childElements, index, context))
                {
                    if (matched > index)
                    {
                        MatchInterleaveRecursive(
                            nodes,
                            parent,
                            childElements,
                            matched,
                            context,
                            used,
                            oneOrMoreSatisfied,
                            visited,
                            matches);
                    }
                }

                continue;
            }

            if (interleavedNode.Kind == OdfSchemaPatternNodeKind.OneOrMore)
            {
                foreach (int matched in MatchSequence(interleavedNode.Children, parent, childElements, index, context))
                {
                    if (matched <= index)
                    {
                        continue;
                    }

                    bool previous = oneOrMoreSatisfied[i];
                    oneOrMoreSatisfied[i] = true;
                    MatchInterleaveRecursive(
                        nodes,
                        parent,
                        childElements,
                        matched,
                        context,
                        used,
                        oneOrMoreSatisfied,
                        visited,
                        matches);
                    oneOrMoreSatisfied[i] = previous;
                }

                continue;
            }

            if (used[i])
            {
                continue;
            }

            if (interleavedNode.Kind == OdfSchemaPatternNodeKind.Optional)
            {
                foreach (int matched in MatchSequence(interleavedNode.Children, parent, childElements, index, context))
                {
                    if (matched <= index)
                    {
                        continue;
                    }

                    used[i] = true;
                    MatchInterleaveRecursive(
                        nodes,
                        parent,
                        childElements,
                        matched,
                        context,
                        used,
                        oneOrMoreSatisfied,
                        visited,
                        matches);
                    used[i] = false;
                }

                continue;
            }

            if (interleavedNode.Kind == OdfSchemaPatternNodeKind.Empty)
            {
                used[i] = true;
                MatchInterleaveRecursive(
                    nodes,
                    parent,
                    childElements,
                    index,
                    context,
                    used,
                    oneOrMoreSatisfied,
                    visited,
                    matches);
                used[i] = false;
                continue;
            }

            foreach (int matched in MatchContentNode(interleavedNode, parent, childElements, index, context))
            {
                if (matched <= index)
                {
                    continue;
                }

                used[i] = true;
                MatchInterleaveRecursive(
                    nodes,
                    parent,
                    childElements,
                    matched,
                    context,
                    used,
                    oneOrMoreSatisfied,
                    visited,
                    matches);
                used[i] = false;
            }
        }
    }

    private static bool InterleaveRequirementsSatisfied(
        IReadOnlyList<OdfSchemaPatternNode> nodes,
        IReadOnlyList<bool> used,
        IReadOnlyList<bool> oneOrMoreSatisfied,
        MatchContext context)
    {
        for (int i = 0; i < nodes.Count; i++)
        {
            OdfSchemaPatternNode node = nodes[i];
            if (node.Occurrence == "optional" ||
                node.Occurrence == "zeroOrMore" ||
                ContentNodeCanMatchEmpty(node, context))
            {
                continue;
            }

            switch (node.Kind)
            {
                case OdfSchemaPatternNodeKind.ZeroOrMore:
                case OdfSchemaPatternNodeKind.Optional:
                case OdfSchemaPatternNodeKind.Empty:
                    continue;
                case OdfSchemaPatternNodeKind.OneOrMore:
                    if (!oneOrMoreSatisfied[i])
                    {
                        return false;
                    }

                    continue;
                default:
                    if (node.Occurrence == "oneOrMore")
                    {
                        if (!oneOrMoreSatisfied[i])
                        {
                            return false;
                        }
                    }
                    else if (!used[i])
                    {
                        return false;
                    }

                    continue;
            }
        }

        return true;
    }

    private static bool ContentNodeCanMatchEmpty(
        OdfSchemaPatternNode node,
        MatchContext context)
    {
        return ContentNodeCanMatchEmpty(node, context, new HashSet<string>(StringComparer.Ordinal));
    }

    private static bool ContentNodeCanMatchEmpty(
        OdfSchemaPatternNode node,
        MatchContext context,
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
                return node.Children.Any(child => ContentNodeCanMatchEmpty(child, context, visitingReferences));
            case OdfSchemaPatternNodeKind.Group:
            case OdfSchemaPatternNodeKind.Interleave:
            case OdfSchemaPatternNodeKind.Mixed:
            case OdfSchemaPatternNodeKind.Other:
                return node.Children.All(child => ContentNodeCanMatchEmpty(child, context, visitingReferences));
            case OdfSchemaPatternNodeKind.OneOrMore:
                return node.Children.Count > 0 &&
                    node.Children.All(child => ContentNodeCanMatchEmpty(child, context, visitingReferences));
            case OdfSchemaPatternNodeKind.Ref:
                return ReferenceCanMatchEmpty(node.ReferenceName, context, visitingReferences);
            default:
                return false;
        }
    }

    private static bool ReferenceCanMatchEmpty(
        string referenceName,
        MatchContext context,
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
                pattern.Roots.Any(root => ContentNodeCanMatchEmpty(root, context, visitingReferences));
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

    private static HashSet<int> MatchOptional(
        OdfSchemaPatternNode node,
        XElement parent,
        IReadOnlyList<XElement> childElements,
        int index,
        MatchContext context)
    {
        var matches = new HashSet<int> { index };
        foreach (int matched in MatchSequence(node.Children, parent, childElements, index, context))
        {
            matches.Add(matched);
        }

        return matches;
    }

    private static HashSet<int> MatchRepeated(
        OdfSchemaPatternNode node,
        XElement parent,
        IReadOnlyList<XElement> childElements,
        int index,
        MatchContext context,
        bool requireOne)
    {
        var matches = new HashSet<int>();
        var frontier = new HashSet<int> { index };
        bool consumedAny = false;

        if (!requireOne)
        {
            matches.Add(index);
        }

        while (frontier.Count > 0)
        {
            var nextFrontier = new HashSet<int>();
            foreach (int current in frontier)
            {
                foreach (int matched in MatchSequence(node.Children, parent, childElements, current, context))
                {
                    if (matched == current)
                    {
                        continue;
                    }

                    consumedAny = true;
                    if (matches.Add(matched))
                    {
                        nextFrontier.Add(matched);
                    }
                }
            }

            frontier = nextFrontier;
        }

        return requireOne && !consumedAny ? new HashSet<int>() : matches;
    }


    #endregion
}
