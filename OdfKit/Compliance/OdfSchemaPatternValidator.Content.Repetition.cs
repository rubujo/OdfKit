using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

namespace OdfKit.Compliance;

public static partial class OdfSchemaPatternValidator
{
    #region Content Matching - Repetition

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
