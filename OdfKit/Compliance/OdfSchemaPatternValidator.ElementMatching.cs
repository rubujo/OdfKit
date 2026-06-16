using System;
using System.Collections.Generic;
using System.Xml.Linq;

namespace OdfKit.Compliance;

public static partial class OdfSchemaPatternValidator
{
    #region Element Matching

    private static bool MatchesRootNode(
        OdfSchemaPatternNode node,
        XElement element,
        OdfSchemaPatternMatchContext context)
    {
        var syntheticDocument = new XElement(XName.Get("document", "urn:odfkit:internal"));
        var rootElements = new[] { element };
        return OdfSchemaPatternContentMatcher.MatchContentNode(node, syntheticDocument, rootElements, 0, context)
            .Contains(rootElements.Length);
    }

    internal static bool MatchesElementNode(
        OdfSchemaPatternNode node,
        XElement element,
        OdfSchemaPatternMatchContext context)
    {
        if (node.Kind == OdfSchemaPatternNodeKind.Ref)
        {
            return MatchesReference(node.ReferenceName, element, context);
        }

        if (!MatchesElementName(node, element))
        {
            return false;
        }

        var childElements = element.Elements().ToList();
        List<OdfSchemaPatternNode> attributeNodes =
            OdfSchemaPatternAttributeMatcher.GetAttributeNodes(node.Children, context);
        if (!OdfSchemaPatternAttributeMatcher.MatchesAttributePatterns(attributeNodes, element, context))
        {
            return false;
        }

        bool childNameClassesDescribeElementName = node.Kind == OdfSchemaPatternNodeKind.Element &&
            string.IsNullOrEmpty(node.NamespaceUri) &&
            string.IsNullOrEmpty(node.LocalName);
        var contentNodes = node.Children
            .Select(child => childNameClassesDescribeElementName &&
                OdfSchemaPatternAttributeMatcher.IsAttributeNameClassPattern(child)
                ? null
                : OdfSchemaPatternAttributeMatcher.StripAttributePatterns(child, context))
            .Where(child => child != null)
            .Cast<OdfSchemaPatternNode>()
            .ToList();
        if (HasSignificantDirectText(element) &&
            !OdfSchemaPatternContentMatcher.ContentAllowsDirectText(contentNodes, childElements.Count > 0, context))
        {
            return false;
        }

        if (contentNodes.Count == 0)
        {
            return childElements.Count == 0;
        }

        var matchesResult = OdfSchemaPatternContentMatcher.MatchSequence(
            contentNodes, element, childElements, 0, context);
        bool isSeqMatch = matchesResult.Contains(childElements.Count);
        return isSeqMatch;
    }

    private static bool HasSignificantDirectText(XElement element)
    {
        return element.Nodes()
            .OfType<XText>()
            .Any(text => !string.IsNullOrWhiteSpace(text.Value));
    }

    private static bool MatchesReference(
        string referenceName,
        XElement element,
        OdfSchemaPatternMatchContext context)
    {
        if (string.IsNullOrWhiteSpace(referenceName))
        {
            return false;
        }

        if (!context.EnterReference(referenceName))
        {
            return false;
        }

        try
        {
            OdfSchemaPatternDefinition? pattern = context.Schema.FindPattern(referenceName);
            if (pattern == null)
            {
                return false;
            }

            foreach (OdfSchemaPatternNode root in pattern.Roots)
            {
                if (MatchesRootNode(root, element, context))
                {
                    return true;
                }
            }

            return false;
        }
        finally
        {
            context.LeaveReference(referenceName);
        }
    }

    private static bool MatchesElementName(OdfSchemaPatternNode node, XElement element)
    {
        string namespaceUri = element.Name.NamespaceName;
        string localName = element.Name.LocalName;

        if (node.Kind == OdfSchemaPatternNodeKind.Element)
        {
            if (string.IsNullOrEmpty(node.LocalName) && string.IsNullOrEmpty(node.NamespaceUri))
            {
                List<OdfSchemaPatternNode> nameClassNodes =
                    OdfSchemaPatternAttributeMatcher.GetNameClassNodes(node.Children);
                return nameClassNodes.Count > 0 &&
                    nameClassNodes.Any(child => MatchesNameClassNode(child, namespaceUri, localName));
            }

            return string.Equals(node.NamespaceUri, namespaceUri, StringComparison.Ordinal) &&
                string.Equals(node.LocalName, localName, StringComparison.Ordinal);
        }

        if (node.Kind == OdfSchemaPatternNodeKind.AnyName ||
            node.Kind == OdfSchemaPatternNodeKind.NamespaceName ||
            node.Kind == OdfSchemaPatternNodeKind.Name)
        {
            return MatchesNameClassNode(node, namespaceUri, localName);
        }

        return false;
    }


    #endregion
}
