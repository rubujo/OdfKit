using System;
using System.Collections.Generic;
using System.Xml.Linq;

namespace OdfKit.Compliance;

public static partial class OdfSchemaPatternValidator
{
    #region Content Matching - Sequence

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

    #endregion
}
