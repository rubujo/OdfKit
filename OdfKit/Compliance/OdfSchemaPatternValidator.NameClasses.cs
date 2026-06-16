using System;
using System.Collections.Generic;
using System.Xml.Linq;

namespace OdfKit.Compliance;

public static partial class OdfSchemaPatternValidator
{
    #region Name Classes & Lists

    private static bool MatchesNameClasses(
        IReadOnlyList<OdfSchemaNameClass> nameClasses,
        string namespaceUri,
        string localName)
    {
        return MatchesNameClasses(nameClasses, namespaceUri, localName, honorExcept: true);
    }

    private static bool MatchesNameClasses(
        IReadOnlyList<OdfSchemaNameClass> nameClasses,
        string namespaceUri,
        string localName,
        bool honorExcept)
    {
        bool allowed = false;
        foreach (OdfSchemaNameClass nameClass in nameClasses)
        {
            if (!nameClass.Matches(namespaceUri, localName))
            {
                continue;
            }

            if (honorExcept && nameClass.IsExcept)
            {
                return false;
            }

            allowed = true;
        }

        return allowed;
    }

    private static bool MatchesNameClassNode(
        OdfSchemaPatternNode node,
        string namespaceUri,
        string localName)
    {
        return MatchesNameClassNode(node, namespaceUri, localName, insideExcept: false);
    }

    private static bool MatchesNameClassNode(
        OdfSchemaPatternNode node,
        string namespaceUri,
        string localName,
        bool insideExcept)
    {
        if (node.Kind == OdfSchemaPatternNodeKind.Except)
        {
            return node.Children.Any(child => MatchesNameClassNode(child, namespaceUri, localName, insideExcept: true));
        }

        if (node.Kind == OdfSchemaPatternNodeKind.Choice)
        {
            return node.Children.Any(child => MatchesNameClassNode(child, namespaceUri, localName, insideExcept));
        }

        if (node.Kind != OdfSchemaPatternNodeKind.AnyName &&
            node.Kind != OdfSchemaPatternNodeKind.NamespaceName &&
            node.Kind != OdfSchemaPatternNodeKind.Name)
        {
            return false;
        }

        if (!MatchesNameClasses(node.NameClasses, namespaceUri, localName, honorExcept: !insideExcept))
        {
            return false;
        }

        return !node.Children
            .Where(child => child.Kind == OdfSchemaPatternNodeKind.Except)
            .Any(child => MatchesNameClassNode(child, namespaceUri, localName, insideExcept: false));
    }

    private static bool MatchAttributeValueNodes(
        IReadOnlyList<OdfSchemaPatternNode> nodes,
        string value,
        MatchContext context)
    {
        foreach (OdfSchemaPatternNode node in nodes)
        {
            if (!MatchAttributeValueNode(node, value, context))
            {
                return false;
            }
        }

        return true;
    }

    private static bool MatchAttributeValueNode(
        OdfSchemaPatternNode node,
        string value,
        MatchContext context)
    {
        switch (node.Kind)
        {
            case OdfSchemaPatternNodeKind.Text:
                return true;
            case OdfSchemaPatternNodeKind.Data:
                return MatchesDataValue(node, value, context);
            case OdfSchemaPatternNodeKind.Value:
                return MatchesLiteralValue(node, value);
            case OdfSchemaPatternNodeKind.NotAllowed:
                return false;
            case OdfSchemaPatternNodeKind.Group:
            case OdfSchemaPatternNodeKind.Interleave:
            case OdfSchemaPatternNodeKind.Mixed:
            case OdfSchemaPatternNodeKind.Other:
                return MatchAttributeValueNodes(node.Children, value, context);
            case OdfSchemaPatternNodeKind.List:
                return MatchesListValue(node.Children, value, context);
            case OdfSchemaPatternNodeKind.Choice:
                return node.Children.Any(child => MatchAttributeValueNode(child, value, context));
            case OdfSchemaPatternNodeKind.Optional:
            case OdfSchemaPatternNodeKind.ZeroOrMore:
                return node.Children.Count == 0 || node.Children.Any(child => MatchAttributeValueNode(child, value, context));
            case OdfSchemaPatternNodeKind.OneOrMore:
                return node.Children.Count > 0 && node.Children.Any(child => MatchAttributeValueNode(child, value, context));
            case OdfSchemaPatternNodeKind.Ref:
                OdfSchemaPatternDefinition? pattern = context.Schema.FindPattern(node.ReferenceName);
                return pattern != null && pattern.Roots.Any(root => MatchAttributeValueNode(root, value, context));
            default:
                return false;
        }
    }

    private static bool IsSimpleTextNode(XElement element)
    {
        return !element.Elements().Any();
    }

    private static bool MatchesListValue(
        IReadOnlyList<OdfSchemaPatternNode> nodes,
        string value,
        MatchContext context)
    {
        string[] tokens = SplitListTokens(value);
        if (nodes.Count == 0)
        {
            return tokens.Length == 0;
        }

        return MatchListSequence(nodes, tokens, 0, context).Contains(tokens.Length);
    }

    private static HashSet<int> MatchListSequence(
        IReadOnlyList<OdfSchemaPatternNode> nodes,
        IReadOnlyList<string> tokens,
        int startIndex,
        MatchContext context)
    {
        var indices = new HashSet<int> { startIndex };
        foreach (OdfSchemaPatternNode node in nodes)
        {
            var next = new HashSet<int>();
            foreach (int index in indices)
            {
                foreach (int matched in MatchListNode(node, tokens, index, context))
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

    private static HashSet<int> MatchListNode(
        OdfSchemaPatternNode node,
        IReadOnlyList<string> tokens,
        int index,
        MatchContext context)
    {
        switch (node.Kind)
        {
            case OdfSchemaPatternNodeKind.Data:
                return index < tokens.Count && MatchesDataValue(node, tokens[index], context)
                    ? new HashSet<int> { index + 1 }
                    : new HashSet<int>();
            case OdfSchemaPatternNodeKind.Value:
                return index < tokens.Count && MatchesLiteralValue(node, tokens[index])
                    ? new HashSet<int> { index + 1 }
                    : new HashSet<int>();
            case OdfSchemaPatternNodeKind.Text:
                return index < tokens.Count
                    ? new HashSet<int> { index + 1 }
                    : new HashSet<int>();
            case OdfSchemaPatternNodeKind.NotAllowed:
                return new HashSet<int>();
            case OdfSchemaPatternNodeKind.Group:
            case OdfSchemaPatternNodeKind.Other:
            case OdfSchemaPatternNodeKind.List:
                return MatchListSequence(node.Children, tokens, index, context);
            case OdfSchemaPatternNodeKind.Choice:
                return MatchListChoice(node, tokens, index, context);
            case OdfSchemaPatternNodeKind.Optional:
                return MatchOptionalList(node, tokens, index, context);
            case OdfSchemaPatternNodeKind.ZeroOrMore:
                return MatchRepeatedList(node, tokens, index, context, requireOne: false);
            case OdfSchemaPatternNodeKind.OneOrMore:
                return MatchRepeatedList(node, tokens, index, context, requireOne: true);
            case OdfSchemaPatternNodeKind.Ref:
                return MatchListReference(node.ReferenceName, tokens, index, context);
            default:
                return new HashSet<int>();
        }
    }

    private static HashSet<int> MatchListChoice(
        OdfSchemaPatternNode node,
        IReadOnlyList<string> tokens,
        int index,
        MatchContext context)
    {
        var matches = new HashSet<int>();
        foreach (OdfSchemaPatternNode child in node.Children)
        {
            foreach (int matched in MatchListNode(child, tokens, index, context))
            {
                matches.Add(matched);
            }
        }

        return matches;
    }

    private static HashSet<int> MatchOptionalList(
        OdfSchemaPatternNode node,
        IReadOnlyList<string> tokens,
        int index,
        MatchContext context)
    {
        var matches = new HashSet<int> { index };
        foreach (int matched in MatchListSequence(node.Children, tokens, index, context))
        {
            matches.Add(matched);
        }

        return matches;
    }

    private static HashSet<int> MatchRepeatedList(
        OdfSchemaPatternNode node,
        IReadOnlyList<string> tokens,
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
                foreach (int matched in MatchListSequence(node.Children, tokens, current, context))
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

    private static HashSet<int> MatchListReference(
        string referenceName,
        IReadOnlyList<string> tokens,
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
                foreach (int matched in MatchListNode(root, tokens, index, context))
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


    #endregion
}
