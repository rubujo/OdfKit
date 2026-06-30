using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace OdfKit.Compliance;

internal static partial class OdfSchemaPatternAttributeMatcher
{
    #region Attribute Patterns - Validation

    private static bool AllowsAllAttributes(
        IReadOnlyList<OdfSchemaPatternNode> attributeNodes,
        XElement element,
        OdfSchemaPatternMatchContext context)
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
        OdfSchemaPatternMatchContext context)
    {
        switch (node.Kind)
        {
            case OdfSchemaPatternNodeKind.Attribute:
                if (!MatchesAttributeName(node, attribute))
                {
                    return false;
                }

                List<OdfSchemaPatternNode> valueNodes = GetAttributeValueNodes(node.Children);
                return valueNodes.Count == 0 || OdfSchemaPatternValidator.MatchAttributeValueNodes(valueNodes, attribute.Value, context);
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
                pattern.Roots.Any(root => AttributePatternAllowsAttribute(root, attribute, context));
        }
        finally
        {
            context.LeaveReference(referenceName);
        }
    }

    internal static List<OdfSchemaPatternNode> GetAttributeNodes(
        IReadOnlyList<OdfSchemaPatternNode> nodes,
        OdfSchemaPatternMatchContext context)
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
    internal static OdfSchemaPatternNode? StripAttributePatterns(
        OdfSchemaPatternNode node,
        OdfSchemaPatternMatchContext context)
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

        var strippedNode = new OdfSchemaPatternNode(
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

        // RNG 的 <choice> 可能混合「純屬性分支」（例如 common-draw-data-attlist，僅描述 xlink 屬性）
        // 與「實際元素內容分支」（例如 office-binary-data，要求一個子元素）。剝離掉純屬性分支後，
        // 若直接把僅存的內容分支當作強制要求，會誤判「選擇了屬性分支、因此沒有任何子內容」的合法情況為
        // 不合規（因為原始 choice 本來就能單靠屬性分支以零內容滿足）。因此只要有任何分支被剝離掉，
        // 代表這個 choice 存在一條不需要消耗子內容即可成立的路徑，剩餘內容分支須視為可選，而非必要。
        if (node.Kind == OdfSchemaPatternNodeKind.Choice && strippedChildren.Count < node.Children.Count)
        {
            return new OdfSchemaPatternNode(
                OdfSchemaPatternNodeKind.Optional,
                "optional",
                "",
                "",
                "",
                "",
                "",
                Array.Empty<OdfSchemaNameClass>(),
                new[] { strippedNode },
                Array.Empty<KeyValuePair<string, string>>(),
                null);
        }

        return strippedNode;
    }

    private static bool ContainsAttributePattern(
        OdfSchemaPatternNode node,
        OdfSchemaPatternMatchContext context)
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
        OdfSchemaPatternMatchContext context)
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
        OdfSchemaPatternMatchContext context)
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
        OdfSchemaPatternMatchContext context)
    {
        switch (node.Kind)
        {
            case OdfSchemaPatternNodeKind.Attribute:
            case OdfSchemaPatternNodeKind.Empty:
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

    #endregion

    #region Attribute Patterns - Candidate Matching

    private static bool MatchesAttributeReference(
        string referenceName,
        XElement element,
        OdfSchemaPatternMatchContext context)
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
            return OdfSchemaPatternValidator.MatchesNameClassNode(node, attribute.Name.NamespaceName, attribute.Name.LocalName);
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

    internal static List<OdfSchemaPatternNode> GetNameClassNodes(IReadOnlyList<OdfSchemaPatternNode> nodes)
    {
        return nodes.Where(IsNameClassSyntaxNode).ToList();
    }

    private static List<OdfSchemaPatternNode> GetAttributeValueNodes(IReadOnlyList<OdfSchemaPatternNode> nodes)
    {
        return nodes
            .Where(node => !IsAttributeNameClassPattern(node))
            .ToList();
    }

    internal static bool IsAttributeNameClassPattern(OdfSchemaPatternNode node)
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
        OdfSchemaPatternMatchContext context)
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
        OdfSchemaPatternMatchContext context)
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
