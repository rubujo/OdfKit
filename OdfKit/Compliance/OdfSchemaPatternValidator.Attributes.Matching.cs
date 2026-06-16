using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml.Linq;

namespace OdfKit.Compliance;

internal static partial class OdfSchemaPatternAttributeMatcher
{
    #region Attribute Patterns - Matching

    internal static bool MatchesAttributePatterns(
        IReadOnlyList<OdfSchemaPatternNode> attributeNodes,
        XElement element,
        OdfSchemaPatternMatchContext context)
    {
        var attributes = element.Attributes()
            .Where(attribute => !attribute.IsNamespaceDeclaration)
            .ToList();

        if (attributeNodes.Count == 0)
        {
            return attributes.Count == 0;
        }

        HashSet<string> matches = MatchAttributePatternSequence(
            attributeNodes,
            attributes,
            string.Empty,
            context);

        return matches.Any(state => AllAttributesConsumed(state, attributes.Count));
    }

    private static HashSet<string> MatchAttributePatternSequence(
        IReadOnlyList<OdfSchemaPatternNode> nodes,
        IReadOnlyList<XAttribute> attributes,
        string state,
        OdfSchemaPatternMatchContext context)
    {
        var states = new HashSet<string>(StringComparer.Ordinal) { state };
        foreach (OdfSchemaPatternNode node in nodes)
        {
            var next = new HashSet<string>(StringComparer.Ordinal);
            foreach (string current in states)
            {
                foreach (string matched in MatchAttributePatternNode(node, attributes, current, context))
                {
                    next.Add(matched);
                }
            }

            if (next.Count == 0)
            {
                return next;
            }

            states = next;
        }

        return states;
    }

    private static HashSet<string> MatchAttributePatternNode(
        OdfSchemaPatternNode node,
        IReadOnlyList<XAttribute> attributes,
        string state,
        OdfSchemaPatternMatchContext context)
    {
        switch (node.Kind)
        {
            case OdfSchemaPatternNodeKind.Attribute:
                return MatchSingleAttributePattern(node, attributes, state, context);
            case OdfSchemaPatternNodeKind.NotAllowed:
                return new HashSet<string>(StringComparer.Ordinal);
            case OdfSchemaPatternNodeKind.Ref:
                return MatchAttributePatternReference(node.ReferenceName, attributes, state, context);
            case OdfSchemaPatternNodeKind.Group:
            case OdfSchemaPatternNodeKind.Interleave:
            case OdfSchemaPatternNodeKind.Mixed:
            case OdfSchemaPatternNodeKind.Other:
                return MatchAttributePatternSequence(node.Children, attributes, state, context);
            case OdfSchemaPatternNodeKind.Choice:
                return MatchAttributePatternChoice(node, attributes, state, context);
            case OdfSchemaPatternNodeKind.Optional:
                return MatchOptionalAttributePattern(node, attributes, state, context);
            case OdfSchemaPatternNodeKind.ZeroOrMore:
                return MatchRepeatedAttributePattern(node, attributes, state, context, requireOne: false);
            case OdfSchemaPatternNodeKind.OneOrMore:
                return MatchRepeatedAttributePattern(node, attributes, state, context, requireOne: true);
            case OdfSchemaPatternNodeKind.Empty:
                return new HashSet<string>(StringComparer.Ordinal) { state };
            default:
                return new HashSet<string>(StringComparer.Ordinal);
        }
    }

    private static HashSet<string> MatchSingleAttributePattern(
        OdfSchemaPatternNode node,
        IReadOnlyList<XAttribute> attributes,
        string state,
        OdfSchemaPatternMatchContext context)
    {
        var matches = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < attributes.Count; i++)
        {
            if (IsAttributeConsumed(state, i))
            {
                continue;
            }

            XAttribute attribute = attributes[i];
            if (!MatchesAttributeName(node, attribute))
            {
                continue;
            }

            List<OdfSchemaPatternNode> valueNodes = GetAttributeValueNodes(node.Children);
            if (valueNodes.Count == 0 || OdfSchemaPatternValidator.MatchAttributeValueNodes(valueNodes, attribute.Value, context))
            {
                matches.Add(ConsumeAttribute(state, i));
            }
        }

        return matches;
    }

    private static HashSet<string> MatchAttributePatternReference(
        string referenceName,
        IReadOnlyList<XAttribute> attributes,
        string state,
        OdfSchemaPatternMatchContext context)
    {
        if (string.IsNullOrWhiteSpace(referenceName) || !context.EnterReference(referenceName))
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        try
        {
            OdfSchemaPatternDefinition? pattern = context.Schema.FindPattern(referenceName);
            if (pattern == null)
            {
                return new HashSet<string>(StringComparer.Ordinal);
            }

            return MatchAttributePatternSequence(pattern.Roots, attributes, state, context);
        }
        finally
        {
            context.LeaveReference(referenceName);
        }
    }

    private static HashSet<string> MatchAttributePatternChoice(
        OdfSchemaPatternNode node,
        IReadOnlyList<XAttribute> attributes,
        string state,
        OdfSchemaPatternMatchContext context)
    {
        var matches = new HashSet<string>(StringComparer.Ordinal);
        foreach (OdfSchemaPatternNode child in node.Children)
        {
            foreach (string matched in MatchAttributePatternNode(child, attributes, state, context))
            {
                matches.Add(matched);
            }
        }

        return matches;
    }

    private static HashSet<string> MatchOptionalAttributePattern(
        OdfSchemaPatternNode node,
        IReadOnlyList<XAttribute> attributes,
        string state,
        OdfSchemaPatternMatchContext context)
    {
        var matches = new HashSet<string>(StringComparer.Ordinal) { state };
        foreach (string matched in MatchAttributePatternSequence(node.Children, attributes, state, context))
        {
            matches.Add(matched);
        }

        return matches;
    }

    private static HashSet<string> MatchRepeatedAttributePattern(
        OdfSchemaPatternNode node,
        IReadOnlyList<XAttribute> attributes,
        string state,
        OdfSchemaPatternMatchContext context,
        bool requireOne)
    {
        var matches = new HashSet<string>(StringComparer.Ordinal);
        var frontier = new HashSet<string>(StringComparer.Ordinal) { state };
        if (!requireOne)
        {
            matches.Add(state);
        }

        while (frontier.Count > 0)
        {
            var next = new HashSet<string>(StringComparer.Ordinal);
            foreach (string current in frontier)
            {
                foreach (string matched in MatchAttributePatternSequence(node.Children, attributes, current, context))
                {
                    if (string.Equals(matched, current, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (matches.Add(matched))
                    {
                        next.Add(matched);
                    }
                }
            }

            frontier = next;
        }

        return matches;
    }

    private static bool AllAttributesConsumed(string state, int attributeCount)
    {
        return ParseAttributeState(state).Count == attributeCount;
    }

    private static bool IsAttributeConsumed(string state, int index)
    {
        return ParseAttributeState(state).Contains(index);
    }

    private static string ConsumeAttribute(string state, int index)
    {
        HashSet<int> consumed = ParseAttributeState(state);
        consumed.Add(index);
        return string.Join(",", consumed.OrderBy(item => item).Select(item => item.ToString(CultureInfo.InvariantCulture)));
    }

    private static HashSet<int> ParseAttributeState(string state)
    {
        var consumed = new HashSet<int>();
        if (string.IsNullOrEmpty(state))
        {
            return consumed;
        }

        foreach (string item in state.Split(','))
        {
            if (int.TryParse(item, NumberStyles.None, CultureInfo.InvariantCulture, out int index))
            {
                consumed.Add(index);
            }
        }

        return consumed;
    }

    internal static bool MatchesAttributeNode(
        OdfSchemaPatternNode node,
        XElement element,
        OdfSchemaPatternMatchContext context)
    {
        if (node.Kind == OdfSchemaPatternNodeKind.Ref)
        {
            return MatchesAttributeReference(node.ReferenceName, element, context);
        }

        if (node.Kind == OdfSchemaPatternNodeKind.NotAllowed)
        {
            return false;
        }

        if (node.Kind == OdfSchemaPatternNodeKind.Attribute)
        {
            IEnumerable<XAttribute> candidates = GetCandidateAttributes(node, element);
            foreach (XAttribute attribute in candidates)
            {
                List<OdfSchemaPatternNode> valueNodes = GetAttributeValueNodes(node.Children);
                if (valueNodes.Count == 0 || OdfSchemaPatternValidator.MatchAttributeValueNodes(valueNodes, attribute.Value, context))
                {
                    return true;
                }
            }

            return false;
        }

        if (node.Kind == OdfSchemaPatternNodeKind.Group ||
            node.Kind == OdfSchemaPatternNodeKind.Interleave ||
            node.Kind == OdfSchemaPatternNodeKind.Other)
        {
            return node.Children.All(child => MatchesAttributeNode(child, element, context));
        }

        if (node.Kind == OdfSchemaPatternNodeKind.Choice)
        {
            return node.Children.Any(child => MatchesAttributeNode(child, element, context));
        }

        if (node.Kind == OdfSchemaPatternNodeKind.Optional)
        {
            return !AttributePatternHasCandidate(node.Children, element, context) ||
                node.Children.Any(child => MatchesAttributeNode(child, element, context));
        }

        if (node.Kind == OdfSchemaPatternNodeKind.ZeroOrMore)
        {
            return !AttributePatternHasCandidate(node.Children, element, context) ||
                node.Children.All(child => MatchesAttributeNode(child, element, context));
        }

        if (node.Kind == OdfSchemaPatternNodeKind.OneOrMore)
        {
            return AttributePatternHasCandidate(node.Children, element, context) &&
                node.Children.All(child => MatchesAttributeNode(child, element, context));
        }

        return false;
    }

    #endregion
}
