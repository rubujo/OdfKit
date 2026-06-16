using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace OdfKit.Compliance;

public static partial class OdfSchemaPatternValidator
{
    #region Attribute Patterns - Candidate Matching

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
