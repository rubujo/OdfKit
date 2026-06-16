using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml.Linq;

namespace OdfKit.Compliance;

public static partial class OdfSchemaPatternValidator
{
    #region Attribute Patterns

    private static bool MatchesAttributePatterns(
        IReadOnlyList<OdfSchemaPatternNode> attributeNodes,
        XElement element,
        MatchContext context)
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
        MatchContext context)
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
        MatchContext context)
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
        MatchContext context)
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
            if (valueNodes.Count == 0 || MatchAttributeValueNodes(valueNodes, attribute.Value, context))
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
        MatchContext context)
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
        MatchContext context)
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
        MatchContext context)
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
        MatchContext context,
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

    private static bool MatchesAttributeNode(
        OdfSchemaPatternNode node,
        XElement element,
        MatchContext context)
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
                if (valueNodes.Count == 0 || MatchAttributeValueNodes(valueNodes, attribute.Value, context))
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

    private static bool AllowsAllAttributes(
        IReadOnlyList<OdfSchemaPatternNode> attributeNodes,
        XElement element,
        MatchContext context)
    {
        foreach (XAttribute attribute in element.Attributes().Where(attribute => !attribute.IsNamespaceDeclaration))
        {
            if (!attributeNodes.Any(node => AttributePatternAllowsAttribute(node, attribute, context)))
            {
                return false;
            }
        }

        return true;
    }

    private static bool AttributePatternAllowsAttribute(
        OdfSchemaPatternNode node,
        XAttribute attribute,
        MatchContext context)
    {
        switch (node.Kind)
        {
            case OdfSchemaPatternNodeKind.Attribute:
                if (!MatchesAttributeName(node, attribute))
                {
                    return false;
                }

                List<OdfSchemaPatternNode> valueNodes = GetAttributeValueNodes(node.Children);
                return valueNodes.Count == 0 || MatchAttributeValueNodes(valueNodes, attribute.Value, context);
            case OdfSchemaPatternNodeKind.NotAllowed:
                return false;
            case OdfSchemaPatternNodeKind.Ref:
                return AttributeReferenceAllowsAttribute(node.ReferenceName, attribute, context);
            case OdfSchemaPatternNodeKind.Group:
            case OdfSchemaPatternNodeKind.Interleave:
            case OdfSchemaPatternNodeKind.Choice:
            case OdfSchemaPatternNodeKind.Optional:
            case OdfSchemaPatternNodeKind.ZeroOrMore:
            case OdfSchemaPatternNodeKind.OneOrMore:
            case OdfSchemaPatternNodeKind.Other:
                return node.Children.Any(child => AttributePatternAllowsAttribute(child, attribute, context));
            default:
                return false;
        }
    }

    private static bool AttributeReferenceAllowsAttribute(
        string referenceName,
        XAttribute attribute,
        MatchContext context)
    {
        if (string.IsNullOrWhiteSpace(referenceName) || !context.EnterReference(referenceName))
        {
            return false;
        }

        try
        {
            OdfSchemaPatternDefinition? pattern = context.Schema.FindPattern(referenceName);
            return pattern != null &&
                pattern.Roots.Any(root => AttributePatternAllowsAttribute(root, attribute, context));
        }
        finally
        {
            context.LeaveReference(referenceName);
        }
    }

    private static List<OdfSchemaPatternNode> GetAttributeNodes(
        IReadOnlyList<OdfSchemaPatternNode> nodes,
        MatchContext context)
    {
        var attributes = new List<OdfSchemaPatternNode>();
        foreach (OdfSchemaPatternNode node in nodes)
        {
            if (node.Kind == OdfSchemaPatternNodeKind.Attribute)
            {
                attributes.Add(node);
            }
            else
            {
                if (ContainsAttributePattern(node, context))
                {
                    attributes.Add(node);
                }
            }
        }

        return attributes;
    }
    private static OdfSchemaPatternNode? StripAttributePatterns(
        OdfSchemaPatternNode node,
        MatchContext context)
    {
        if (node.Kind == OdfSchemaPatternNodeKind.Element ||
            node.Kind == OdfSchemaPatternNodeKind.AnyName ||
            node.Kind == OdfSchemaPatternNodeKind.NamespaceName ||
            node.Kind == OdfSchemaPatternNodeKind.Name)
        {
            return node;
        }

        if (node.Kind == OdfSchemaPatternNodeKind.Attribute ||
            IsAttributeReference(node, context))
        {
            return null;
        }

        if (node.Kind != OdfSchemaPatternNodeKind.Group &&
            node.Kind != OdfSchemaPatternNodeKind.Interleave &&
            node.Kind != OdfSchemaPatternNodeKind.Choice &&
            node.Kind != OdfSchemaPatternNodeKind.Optional &&
            node.Kind != OdfSchemaPatternNodeKind.ZeroOrMore &&
            node.Kind != OdfSchemaPatternNodeKind.OneOrMore &&
            node.Kind != OdfSchemaPatternNodeKind.Other)
        {
            return node;
        }

        List<OdfSchemaPatternNode> strippedChildren = node.Children
            .Select(child => StripAttributePatterns(child, context))
            .Where(child => child != null)
            .Cast<OdfSchemaPatternNode>()
            .ToList();
        if (strippedChildren.Count == 0)
        {
            return null;
        }

        return new OdfSchemaPatternNode(
            node.Kind,
            node.Occurrence,
            node.NamespaceUri,
            node.LocalName,
            node.ReferenceName,
            node.DataType,
            node.Value,
            node.NameClasses,
            strippedChildren,
            node.DataParameters.Select(parameter => new KeyValuePair<string, string>(parameter.Name, parameter.Value)),
            node.DataTypeLibrary);
    }

    private static bool ContainsAttributePattern(
        OdfSchemaPatternNode node,
        MatchContext context)
    {
        if (node.Kind == OdfSchemaPatternNodeKind.Element ||
            node.Kind == OdfSchemaPatternNodeKind.AnyName ||
            node.Kind == OdfSchemaPatternNodeKind.NamespaceName ||
            node.Kind == OdfSchemaPatternNodeKind.Name)
        {
            return false;
        }

        return node.Kind == OdfSchemaPatternNodeKind.Attribute ||
            ReferenceContainsAttributePattern(node, context) ||
            node.Children.Any(child => ContainsAttributePattern(child, context));
    }

    private static bool ReferenceContainsAttributePattern(
        OdfSchemaPatternNode node,
        MatchContext context)
    {
        if (node.Kind != OdfSchemaPatternNodeKind.Ref ||
            string.IsNullOrWhiteSpace(node.ReferenceName) ||
            !context.EnterReference(node.ReferenceName))
        {
            return false;
        }

        try
        {
            OdfSchemaPatternDefinition? pattern = context.Schema.FindPattern(node.ReferenceName);
            return pattern is not null &&
                pattern.Roots.Any(root => ContainsAttributePattern(root, context));
        }
        finally
        {
            context.LeaveReference(node.ReferenceName);
        }
    }

    private static bool IsAttributeReference(
        OdfSchemaPatternNode node,
        MatchContext context)
    {
        if (node.Kind != OdfSchemaPatternNodeKind.Ref ||
            string.IsNullOrWhiteSpace(node.ReferenceName) ||
            !context.EnterReference(node.ReferenceName))
        {
            return false;
        }

        try
        {
            OdfSchemaPatternDefinition? pattern = context.Schema.FindPattern(node.ReferenceName);
            return pattern is not null &&
                pattern.Roots.Count > 0 &&
                pattern.Roots.All(root => IsPureAttributePattern(root, context));
        }
        finally
        {
            context.LeaveReference(node.ReferenceName);
        }
    }

    private static bool IsPureAttributePattern(
        OdfSchemaPatternNode node,
        MatchContext context)
    {
        switch (node.Kind)
        {
            case OdfSchemaPatternNodeKind.Attribute:
                return true;
            case OdfSchemaPatternNodeKind.Ref:
                return IsAttributeReference(node, context);
            case OdfSchemaPatternNodeKind.Group:
            case OdfSchemaPatternNodeKind.Interleave:
            case OdfSchemaPatternNodeKind.Choice:
            case OdfSchemaPatternNodeKind.Optional:
            case OdfSchemaPatternNodeKind.ZeroOrMore:
            case OdfSchemaPatternNodeKind.OneOrMore:
            case OdfSchemaPatternNodeKind.Other:
                return node.Children.Count > 0 &&
                    node.Children.All(child => IsPureAttributePattern(child, context));
            default:
                return false;
        }
    }

    private static bool MatchesAttributeReference(
        string referenceName,
        XElement element,
        MatchContext context)
    {
        if (string.IsNullOrWhiteSpace(referenceName) || !context.EnterReference(referenceName))
        {
            return false;
        }

        try
        {
            OdfSchemaPatternDefinition? pattern = context.Schema.FindPattern(referenceName);
            return pattern != null && pattern.Roots.Any(root => MatchesAttributeNode(root, element, context));
        }
        finally
        {
            context.LeaveReference(referenceName);
        }
    }

    private static IEnumerable<XAttribute> GetCandidateAttributes(OdfSchemaPatternNode node, XElement element)
    {
        foreach (XAttribute attribute in element.Attributes().Where(attribute => !attribute.IsNamespaceDeclaration))
        {
            if (MatchesAttributeName(node, attribute))
            {
                yield return attribute;
            }
        }
    }

    private static bool MatchesAttributeName(OdfSchemaPatternNode node, XAttribute attribute)
    {
        if (!string.IsNullOrEmpty(node.LocalName) || !string.IsNullOrEmpty(node.NamespaceUri))
        {
            return string.Equals(node.NamespaceUri, attribute.Name.NamespaceName, StringComparison.Ordinal) &&
                string.Equals(node.LocalName, attribute.Name.LocalName, StringComparison.Ordinal);
        }

        List<OdfSchemaPatternNode> nameClassNodes = GetAttributeNameClassNodes(node.Children);
        return nameClassNodes.Count > 0 &&
            nameClassNodes.Any(child => MatchesAttributeNameClassNode(child, attribute));
    }

    private static bool MatchesAttributeNameClassNode(OdfSchemaPatternNode node, XAttribute attribute)
    {
        if (node.Kind == OdfSchemaPatternNodeKind.AnyName ||
            node.Kind == OdfSchemaPatternNodeKind.NamespaceName ||
            node.Kind == OdfSchemaPatternNodeKind.Name)
        {
            return MatchesNameClassNode(node, attribute.Name.NamespaceName, attribute.Name.LocalName);
        }

        if (node.Kind == OdfSchemaPatternNodeKind.Choice)
        {
            return node.Children.Any(child => MatchesAttributeNameClassNode(child, attribute));
        }

        if (node.Kind == OdfSchemaPatternNodeKind.Except)
        {
            return !node.Children.Any(child => MatchesAttributeNameClassNode(child, attribute));
        }

        return false;
    }

    private static List<OdfSchemaPatternNode> GetAttributeNameClassNodes(IReadOnlyList<OdfSchemaPatternNode> nodes)
    {
        var nameClassNodes = new List<OdfSchemaPatternNode>();
        foreach (OdfSchemaPatternNode node in nodes)
        {
            if (node.Kind == OdfSchemaPatternNodeKind.AnyName ||
                node.Kind == OdfSchemaPatternNodeKind.NamespaceName ||
                node.Kind == OdfSchemaPatternNodeKind.Name ||
                ContainsNameClassPattern(node))
            {
                nameClassNodes.Add(node);
            }
        }

        return nameClassNodes;
    }

    private static List<OdfSchemaPatternNode> GetNameClassNodes(IReadOnlyList<OdfSchemaPatternNode> nodes)
    {
        return nodes.Where(IsNameClassSyntaxNode).ToList();
    }

    private static List<OdfSchemaPatternNode> GetAttributeValueNodes(IReadOnlyList<OdfSchemaPatternNode> nodes)
    {
        return nodes
            .Where(node => !IsAttributeNameClassPattern(node))
            .ToList();
    }

    private static bool IsAttributeNameClassPattern(OdfSchemaPatternNode node)
    {
        if (IsNameClassSyntaxNode(node))
        {
            return true;
        }

        if (node.Kind == OdfSchemaPatternNodeKind.Group ||
            node.Kind == OdfSchemaPatternNodeKind.Interleave ||
            node.Kind == OdfSchemaPatternNodeKind.Choice ||
            node.Kind == OdfSchemaPatternNodeKind.Optional ||
            node.Kind == OdfSchemaPatternNodeKind.ZeroOrMore ||
            node.Kind == OdfSchemaPatternNodeKind.OneOrMore ||
            node.Kind == OdfSchemaPatternNodeKind.Other)
        {
            return node.Children.Count > 0 && node.Children.All(IsAttributeNameClassPattern);
        }

        return false;
    }

    private static bool ContainsNameClassPattern(OdfSchemaPatternNode node)
    {
        return IsNameClassSyntaxNode(node) ||
            node.Children.Any(ContainsNameClassPattern);
    }

    private static bool IsNameClassSyntaxNode(OdfSchemaPatternNode node)
    {
        return node.Kind == OdfSchemaPatternNodeKind.AnyName ||
            node.Kind == OdfSchemaPatternNodeKind.NamespaceName ||
            node.Kind == OdfSchemaPatternNodeKind.Name ||
            node.Kind == OdfSchemaPatternNodeKind.Except;
    }

    private static bool AttributePatternHasCandidate(
        IReadOnlyList<OdfSchemaPatternNode> nodes,
        XElement element,
        MatchContext context)
    {
        foreach (OdfSchemaPatternNode node in nodes)
        {
            if (AttributePatternHasCandidate(node, element, context))
            {
                return true;
            }
        }

        return false;
    }

    private static bool AttributePatternHasCandidate(
        OdfSchemaPatternNode node,
        XElement element,
        MatchContext context)
    {
        if (node.Kind == OdfSchemaPatternNodeKind.Attribute)
        {
            return GetCandidateAttributes(node, element).Any();
        }

        if (node.Kind == OdfSchemaPatternNodeKind.Ref)
        {
            if (string.IsNullOrWhiteSpace(node.ReferenceName) || !context.EnterReference(node.ReferenceName))
            {
                return false;
            }

            try
            {
                OdfSchemaPatternDefinition? pattern = context.Schema.FindPattern(node.ReferenceName);
                return pattern != null && pattern.Roots.Any(root => AttributePatternHasCandidate(root, element, context));
            }
            finally
            {
                context.LeaveReference(node.ReferenceName);
            }
        }

        return node.Children.Any(child => AttributePatternHasCandidate(child, element, context));
    }


    #endregion
}
