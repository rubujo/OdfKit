using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace OdfKit.Compliance;

/// <summary>
/// 針對 XML 元素評估保留的 RELAX NG 模式樹中繼資料。
/// </summary>
public static class OdfSchemaPatternValidator
{
    /// <summary>
    /// 根據具名的結構描述模式驗證 XML 元素。
    /// </summary>
    /// <param name="element">XML 元素</param>
    /// <param name="schema">結構描述集</param>
    /// <param name="patternName">模式名稱</param>
    /// <returns>模式驗證結果</returns>
    public static OdfSchemaPatternValidationResult ValidateElement(
        XElement element,
        OdfSchemaSet schema,
        string patternName)
    {
        if (element is null) throw new ArgumentNullException(nameof(element));
        if (schema is null) throw new ArgumentNullException(nameof(schema));
        if (string.IsNullOrWhiteSpace(patternName)) throw new ArgumentException("Pattern name cannot be empty.", nameof(patternName));

        OdfSchemaPatternDefinition? pattern = schema.FindPattern(patternName);
        if (pattern is null)
        {
            return OdfSchemaPatternValidationResult.Fail(
                "ODF3100",
                $"Schema pattern '{patternName}' is not available.");
        }

        var context = new MatchContext(schema);
        foreach (OdfSchemaPatternNode root in pattern.Roots)
        {
            if (MatchesRootNode(root, element, context))
            {
                return OdfSchemaPatternValidationResult.Success();
            }
        }

        return OdfSchemaPatternValidationResult.Fail(
            "ODF3101",
            $"Element '{{{element.Name.NamespaceName}}}{element.Name.LocalName}' does not match schema pattern '{patternName}'.");
    }

        private static bool MatchesRootNode(
            OdfSchemaPatternNode node,
            XElement element,
            MatchContext context)
        {
            var syntheticDocument = new XElement(XName.Get("document", "urn:odfkit:internal"));
            var rootElements = new[] { element };
            return MatchContentNode(node, syntheticDocument, rootElements, 0, context).Contains(rootElements.Length);
        }

        private static bool MatchesElementNode(
            OdfSchemaPatternNode node,
            XElement element,
            MatchContext context)
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
            List<OdfSchemaPatternNode> attributeNodes = GetAttributeNodes(node.Children, context);
            if (!MatchesAttributePatterns(attributeNodes, element, context))
            {
                return false;
            }

            bool childNameClassesDescribeElementName = node.Kind == OdfSchemaPatternNodeKind.Element &&
                string.IsNullOrEmpty(node.NamespaceUri) &&
                string.IsNullOrEmpty(node.LocalName);
            var contentNodes = node.Children
                .Select(child => childNameClassesDescribeElementName && IsAttributeNameClassPattern(child)
                    ? null
                    : StripAttributePatterns(child, context))
                .Where(child => child != null)
                .Cast<OdfSchemaPatternNode>()
                .ToList();
            if (HasSignificantDirectText(element) &&
                !ContentAllowsDirectText(contentNodes, childElements.Count > 0, context))
            {
                return false;
            }

            if (contentNodes.Count == 0)
            {
                return childElements.Count == 0;
            }

            var matchesResult = MatchSequence(contentNodes, element, childElements, 0, context);
            bool isSeqMatch = matchesResult.Contains(childElements.Count);
            return isSeqMatch;
        }

        private static bool HasSignificantDirectText(XElement element)
        {
            return element.Nodes()
                .OfType<XText>()
                .Any(text => !string.IsNullOrWhiteSpace(text.Value));
        }

        private static bool ContentAllowsDirectText(
            IReadOnlyList<OdfSchemaPatternNode> nodes,
            bool hasChildElements,
            MatchContext context)
        {
            return nodes.Any(node => ContentAllowsDirectText(node, hasChildElements, context));
        }

        private static bool ContentAllowsDirectText(
            OdfSchemaPatternNode node,
            bool hasChildElements,
            MatchContext context)
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
                case OdfSchemaPatternNodeKind.Group:
                case OdfSchemaPatternNodeKind.Interleave:
                case OdfSchemaPatternNodeKind.Choice:
                case OdfSchemaPatternNodeKind.Optional:
                case OdfSchemaPatternNodeKind.ZeroOrMore:
                case OdfSchemaPatternNodeKind.OneOrMore:
                case OdfSchemaPatternNodeKind.Other:
                    return node.Children.Any(child => ContentAllowsDirectText(child, hasChildElements, context));
                default:
                    return false;
            }
        }

        private static bool ReferenceAllowsDirectText(
            string referenceName,
            bool hasChildElements,
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
                    pattern.Roots.Any(root => ContentAllowsDirectText(root, hasChildElements, context));
            }
            finally
            {
                context.LeaveReference(referenceName);
            }
        }

        private static bool MatchesReference(
            string referenceName,
            XElement element,
            MatchContext context)
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
                    List<OdfSchemaPatternNode> nameClassNodes = GetNameClassNodes(node.Children);
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

                var matches = new HashSet<string>(StringComparer.Ordinal);
                foreach (OdfSchemaPatternNode root in pattern.Roots)
                {
                    foreach (string matched in MatchAttributePatternNode(root, attributes, state, context))
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
                IsAttributeReference(node, context) ||
                node.Children.Any(child => ContainsAttributePattern(child, context));
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
                return pattern != null &&
                    pattern.Roots.Any(root => ContainsAttributePattern(root, context));
            }
            finally
            {
                context.LeaveReference(node.ReferenceName);
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
                    return IsSimpleTextNode(parent)
                        ? new HashSet<int> { index }
                        : new HashSet<int>();
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

            return MatchesElementNode(node, childElements[index], context)
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

            if (InterleaveRequirementsSatisfied(nodes, used, oneOrMoreSatisfied))
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
            IReadOnlyList<bool> oneOrMoreSatisfied)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                OdfSchemaPatternNode node = nodes[i];
                if (node.Occurrence == "optional" || node.Occurrence == "zeroOrMore")
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

        private static string[] SplitListTokens(string value)
        {
            return (value ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        }

        private static bool MatchesLiteralValue(OdfSchemaPatternNode node, string value)
        {
            if (!MatchesDataType(node, value))
            {
                return false;
            }

            return LiteralValuesEqual(node.DataType, value, node.Value);
        }

        private static bool MatchesDataValue(
            OdfSchemaPatternNode node,
            string value,
            MatchContext context)
        {
            if (!MatchesDataType(node, value))
            {
                return false;
            }

            return !node.Children
                .Where(child => child.Kind == OdfSchemaPatternNodeKind.Except)
                .Any(child => MatchesDataExcept(child, value, context));
        }

        private static bool MatchesDataExcept(
            OdfSchemaPatternNode node,
            string value,
            MatchContext context)
        {
            return node.Children.Any(child => MatchAttributeValueNode(child, value, context));
        }

        private static bool MatchesDataType(OdfSchemaPatternNode node, string value)
        {
            return IsSupportedDatatypeLibrary(node.DataTypeLibrary) &&
                MatchesDataType(node.DataType, value) &&
                MatchesDataParameters(node.DataParameters, value);
        }

        private static bool IsSupportedDatatypeLibrary(string dataTypeLibrary)
        {
            return string.IsNullOrWhiteSpace(dataTypeLibrary) ||
                string.Equals(
                    dataTypeLibrary,
                    "http://www.w3.org/2001/XMLSchema-datatypes",
                    StringComparison.Ordinal);
        }

        private static bool MatchesDataType(string dataType, string value)
        {
            string type = dataType ?? string.Empty;
            if (type.Length == 0 ||
                string.Equals(type, "string", StringComparison.Ordinal) ||
                string.Equals(type, "xsd:string", StringComparison.Ordinal))
            {
                return true;
            }

            if (string.Equals(type, "token", StringComparison.Ordinal) ||
                string.Equals(type, "xsd:token", StringComparison.Ordinal))
            {
                return value != null;
            }

            if (string.Equals(type, "normalizedString", StringComparison.Ordinal) ||
                string.Equals(type, "xsd:normalizedString", StringComparison.Ordinal))
            {
                return IsNormalizedString(value);
            }

            try
            {
                switch (type)
                {
                    case "boolean":
                    case "xsd:boolean":
                        XmlConvert.ToBoolean(value);
                        return true;
                    case "byte":
                    case "xsd:byte":
                        XmlConvert.ToSByte(value);
                        return true;
                    case "unsignedByte":
                    case "xsd:unsignedByte":
                        XmlConvert.ToByte(value);
                        return true;
                    case "short":
                    case "xsd:short":
                        XmlConvert.ToInt16(value);
                        return true;
                    case "unsignedShort":
                    case "xsd:unsignedShort":
                        XmlConvert.ToUInt16(value);
                        return true;
                    case "int":
                    case "xsd:int":
                        XmlConvert.ToInt32(value);
                        return true;
                    case "integer":
                    case "xsd:integer":
                        return TryParseXmlInteger(value, out _);
                    case "unsignedInt":
                    case "xsd:unsignedInt":
                        XmlConvert.ToUInt32(value);
                        return true;
                    case "nonNegativeInteger":
                    case "xsd:nonNegativeInteger":
                        return TryParseXmlInteger(value, out BigInteger nonNegativeInteger) &&
                            nonNegativeInteger >= BigInteger.Zero;
                    case "long":
                    case "xsd:long":
                        XmlConvert.ToInt64(value);
                        return true;
                    case "unsignedLong":
                    case "xsd:unsignedLong":
                        XmlConvert.ToUInt64(value);
                        return true;
                    case "positiveInteger":
                    case "xsd:positiveInteger":
                        return TryParseXmlInteger(value, out BigInteger positiveInteger) &&
                            positiveInteger > BigInteger.Zero;
                    case "negativeInteger":
                    case "xsd:negativeInteger":
                        return TryParseXmlInteger(value, out BigInteger negativeInteger) &&
                            negativeInteger < BigInteger.Zero;
                    case "nonPositiveInteger":
                    case "xsd:nonPositiveInteger":
                        return TryParseXmlInteger(value, out BigInteger nonPositiveInteger) &&
                            nonPositiveInteger <= BigInteger.Zero;
                    case "decimal":
                    case "xsd:decimal":
                        return IsXmlDecimal(value);
                    case "float":
                    case "xsd:float":
                        XmlConvert.ToSingle(value);
                        return true;
                    case "double":
                    case "xsd:double":
                        XmlConvert.ToDouble(value);
                        return true;
                    case "date":
                    case "xsd:date":
                        XmlConvert.ToDateTime(value, XmlDateTimeSerializationMode.RoundtripKind);
                        return value.IndexOf('T') < 0;
                    case "dateTime":
                    case "xsd:dateTime":
                        XmlConvert.ToDateTime(value, XmlDateTimeSerializationMode.RoundtripKind);
                        return value.IndexOf('T') >= 0;
                    case "time":
                    case "xsd:time":
                        return IsXmlTime(value);
                    case "duration":
                    case "xsd:duration":
                        XmlConvert.ToTimeSpan(value);
                        return true;
                    case "anyURI":
                    case "xsd:anyURI":
                        return IsAnyUri(value);
                    case "hexBinary":
                    case "xsd:hexBinary":
                        return IsHexBinary(value);
                    case "base64Binary":
                    case "xsd:base64Binary":
                        return IsBase64Binary(value);
                    case "language":
                    case "xsd:language":
                        return IsLanguage(value);
                    case "Name":
                    case "xsd:Name":
                        XmlConvert.VerifyName(value);
                        return true;
                    case "NCName":
                    case "xsd:NCName":
                    case "ID":
                    case "xsd:ID":
                    case "IDREF":
                    case "xsd:IDREF":
                    case "ENTITY":
                    case "xsd:ENTITY":
                        XmlConvert.VerifyNCName(value);
                        return true;
                    case "IDREFS":
                    case "xsd:IDREFS":
                    case "ENTITIES":
                    case "xsd:ENTITIES":
                        return MatchesXmlTokenList(value, IsNCName);
                    case "NMTOKEN":
                    case "xsd:NMTOKEN":
                        XmlConvert.VerifyNMTOKEN(value);
                        return true;
                    case "NMTOKENS":
                    case "xsd:NMTOKENS":
                        return MatchesXmlTokenList(value, IsNmtoken);
                    case "QName":
                    case "xsd:QName":
                        return IsQName(value);
                    default:
                        return false;
                }
            }
            catch (FormatException)
            {
                return false;
            }
            catch (OverflowException)
            {
                return false;
            }
            catch (XmlException)
            {
                return false;
            }
        }

        private static bool IsAnyUri(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Any(char.IsWhiteSpace))
            {
                return false;
            }

            return Uri.TryCreate(value, UriKind.RelativeOrAbsolute, out _);
        }

        private static bool TryParseXmlInteger(string value, out BigInteger integer)
        {
            integer = BigInteger.Zero;
            string text = value ?? string.Empty;
            if (!Regex.IsMatch(text, "^[+-]?[0-9]+$"))
            {
                return false;
            }

            return BigInteger.TryParse(
                text,
                NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out integer);
        }

        private static bool IsXmlDecimal(string value)
        {
            return Regex.IsMatch(
                value ?? string.Empty,
                "^[+-]?(?:[0-9]+(?:\\.[0-9]*)?|\\.[0-9]+)$");
        }

        private static bool IsHexBinary(string value)
        {
            return value.Length % 2 == 0 && value.All(IsHexDigit);
        }

        private static bool IsHexDigit(char value)
        {
            return (value >= '0' && value <= '9') ||
                (value >= 'A' && value <= 'F') ||
                (value >= 'a' && value <= 'f');
        }

        private static bool IsBase64Binary(string value)
        {
            try
            {
                Convert.FromBase64String(NormalizeValue(value));
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        private static bool IsLanguage(string value)
        {
            return Regex.IsMatch(value ?? string.Empty, "^[A-Za-z]{1,8}(-[A-Za-z0-9]{1,8})*$");
        }

        private static bool IsNormalizedString(string value)
        {
            return value != null &&
                value.IndexOf('\t') < 0 &&
                value.IndexOf('\n') < 0 &&
                value.IndexOf('\r') < 0;
        }

        private static bool LiteralValuesEqual(string dataType, string value, string literal)
        {
            string type = dataType ?? string.Empty;
            if (type.Length == 0 ||
                string.Equals(type, "token", StringComparison.Ordinal) ||
                string.Equals(type, "xsd:token", StringComparison.Ordinal))
            {
                return string.Equals(NormalizeValue(value), NormalizeValue(literal), StringComparison.Ordinal);
            }

            if (string.Equals(type, "string", StringComparison.Ordinal) ||
                string.Equals(type, "xsd:string", StringComparison.Ordinal) ||
                string.Equals(type, "normalizedString", StringComparison.Ordinal) ||
                string.Equals(type, "xsd:normalizedString", StringComparison.Ordinal))
            {
                return string.Equals(value, literal, StringComparison.Ordinal);
            }

            if (IsNumericDatatype(type) &&
                TryCompareXmlDecimals(value, literal, out int numericComparison))
            {
                return numericComparison == 0;
            }

            if ((string.Equals(type, "boolean", StringComparison.Ordinal) ||
                string.Equals(type, "xsd:boolean", StringComparison.Ordinal)) &&
                TryParseXmlBoolean(value, out bool leftBoolean) &&
                TryParseXmlBoolean(literal, out bool rightBoolean))
            {
                return leftBoolean == rightBoolean;
            }

            return string.Equals(value, literal, StringComparison.Ordinal);
        }

        private static bool IsNumericDatatype(string type)
        {
            switch (type)
            {
                case "decimal":
                case "xsd:decimal":
                case "integer":
                case "xsd:integer":
                case "nonNegativeInteger":
                case "xsd:nonNegativeInteger":
                case "positiveInteger":
                case "xsd:positiveInteger":
                case "negativeInteger":
                case "xsd:negativeInteger":
                case "nonPositiveInteger":
                case "xsd:nonPositiveInteger":
                case "byte":
                case "xsd:byte":
                case "unsignedByte":
                case "xsd:unsignedByte":
                case "short":
                case "xsd:short":
                case "unsignedShort":
                case "xsd:unsignedShort":
                case "int":
                case "xsd:int":
                case "unsignedInt":
                case "xsd:unsignedInt":
                case "long":
                case "xsd:long":
                case "unsignedLong":
                case "xsd:unsignedLong":
                    return true;
                default:
                    return false;
            }
        }

        private static bool TryParseXmlBoolean(string value, out bool parsed)
        {
            try
            {
                parsed = XmlConvert.ToBoolean(value);
                return true;
            }
            catch (FormatException)
            {
                parsed = false;
                return false;
            }
        }

        private static bool IsXmlTime(string value)
        {
            Match match = Regex.Match(
                value ?? string.Empty,
                @"^(?<hour>\d{2}):(?<minute>\d{2}):(?<second>\d{2})(\.\d+)?(?<timezone>Z|[+-]\d{2}:\d{2})?$");
            if (!match.Success)
            {
                return false;
            }

            int hour = int.Parse(match.Groups["hour"].Value, CultureInfo.InvariantCulture);
            int minute = int.Parse(match.Groups["minute"].Value, CultureInfo.InvariantCulture);
            int second = int.Parse(match.Groups["second"].Value, CultureInfo.InvariantCulture);
            if (hour > 23 || minute > 59 || second > 59)
            {
                return false;
            }

            string timezone = match.Groups["timezone"].Value;
            if (timezone.Length == 0 || timezone == "Z")
            {
                return true;
            }

            int timezoneHour = int.Parse(timezone.Substring(1, 2), CultureInfo.InvariantCulture);
            int timezoneMinute = int.Parse(timezone.Substring(4, 2), CultureInfo.InvariantCulture);
            if (timezoneMinute > 59)
            {
                return false;
            }

            return timezoneHour < 14 ||
                (timezoneHour == 14 && timezoneMinute == 0);
        }

        private static bool IsQName(string value)
        {
            string[] parts = (value ?? string.Empty).Split(':');
            if (parts.Length == 1)
            {
                return IsNCName(parts[0]);
            }

            return parts.Length == 2 &&
                IsNCName(parts[0]) &&
                IsNCName(parts[1]);
        }

        private static bool MatchesXmlTokenList(string value, Func<string, bool> predicate)
        {
            string[] tokens = (value ?? string.Empty)
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            return tokens.Length > 0 && tokens.All(predicate);
        }

        private static bool IsNCName(string value)
        {
            try
            {
                XmlConvert.VerifyNCName(value);
                return true;
            }
            catch (XmlException)
            {
                return false;
            }
        }

        private static bool IsNmtoken(string value)
        {
            try
            {
                XmlConvert.VerifyNMTOKEN(value);
                return true;
            }
            catch (XmlException)
            {
                return false;
            }
        }

        private static string NormalizeValue(string value)
        {
            return string.Join(" ", (value ?? string.Empty)
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        }

        private static bool MatchesDataParameters(IReadOnlyList<OdfSchemaDatatypeParameter> parameters, string value)
        {
            var enumerations = new List<string>();
            foreach (OdfSchemaDatatypeParameter parameter in parameters)
            {
                if (string.Equals(parameter.Name, "enumeration", StringComparison.Ordinal))
                {
                    enumerations.Add(parameter.Value);
                    continue;
                }

                if (!MatchesDataParameter(parameter.Name, parameter.Value, value))
                {
                    return false;
                }
            }

            return enumerations.Count == 0 ||
                enumerations.Any(item => string.Equals(item, value, StringComparison.Ordinal));
        }

        private static bool MatchesDataParameter(string name, string parameterValue, string value)
        {
            switch (name)
            {
                case "minInclusive":
                    return CompareFacetValues(value, parameterValue) >= 0;
                case "maxInclusive":
                    return CompareFacetValues(value, parameterValue) <= 0;
                case "minExclusive":
                    return CompareFacetValues(value, parameterValue) > 0;
                case "maxExclusive":
                    return CompareFacetValues(value, parameterValue) < 0;
                case "length":
                    return value.Length == ParseNonNegativeInt(parameterValue);
                case "minLength":
                    return value.Length >= ParseNonNegativeInt(parameterValue);
                case "maxLength":
                    return value.Length <= ParseNonNegativeInt(parameterValue);
                case "pattern":
                    try
                    {
                        return Regex.IsMatch(value, parameterValue);
                    }
                    catch (ArgumentException)
                    {
                        return false;
                    }
                case "enumeration":
                    return string.Equals(value, parameterValue, StringComparison.Ordinal);
                default:
                    return true;
            }
        }

        private static int CompareFacetValues(string value, string parameterValue)
        {
            if (TryCompareXmlDecimals(value, parameterValue, out int decimalComparison))
            {
                return decimalComparison;
            }

            DateTimeOffset leftDate;
            DateTimeOffset rightDate;
            if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out leftDate) &&
                DateTimeOffset.TryParse(parameterValue, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out rightDate))
            {
                return leftDate.CompareTo(rightDate);
            }

            return string.CompareOrdinal(value, parameterValue);
        }

        private static int ParseNonNegativeInt(string value)
        {
            return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int parsed) && parsed >= 0
                ? parsed
                : -1;
        }

        private static bool TryCompareXmlDecimals(string value, string parameterValue, out int comparison)
        {
            comparison = 0;
            if (!TryParseXmlDecimal(value, out BigInteger left, out int leftScale) ||
                !TryParseXmlDecimal(parameterValue, out BigInteger right, out int rightScale))
            {
                return false;
            }

            if (leftScale < rightScale)
            {
                left *= BigInteger.Pow(10, rightScale - leftScale);
            }
            else if (rightScale < leftScale)
            {
                right *= BigInteger.Pow(10, leftScale - rightScale);
            }

            comparison = left.CompareTo(right);
            return true;
        }

        private static bool TryParseXmlDecimal(string value, out BigInteger unscaledValue, out int scale)
        {
            unscaledValue = BigInteger.Zero;
            scale = 0;
            if (!IsXmlDecimal(value))
            {
                return false;
            }

            string text = value;
            bool isNegative = text[0] == '-';
            if (isNegative || text[0] == '+')
            {
                text = text.Substring(1);
            }

            int decimalPoint = text.IndexOf('.');
            if (decimalPoint >= 0)
            {
                scale = text.Length - decimalPoint - 1;
                text = text.Remove(decimalPoint, 1);
            }

            unscaledValue = BigInteger.Parse(text, NumberStyles.None, CultureInfo.InvariantCulture);
            if (isNegative)
            {
                unscaledValue = -unscaledValue;
            }

            return true;
        }

        internal static bool PatternMatchesElementName(
            OdfSchemaPatternDefinition pattern,
            XElement element,
            OdfSchemaSet schema)
        {
            var context = new MatchContext(schema);
            foreach (OdfSchemaPatternNode root in pattern.Roots)
            {
                if (PatternMatchesElementName(root, element, context))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool PatternMatchesElementName(
            OdfSchemaPatternNode node,
            XElement element,
            MatchContext context)
        {
            if (node.Kind == OdfSchemaPatternNodeKind.Ref)
            {
                if (string.IsNullOrWhiteSpace(node.ReferenceName) || !context.EnterReference(node.ReferenceName))
                {
                    return false;
                }

                try
                {
                    OdfSchemaPatternDefinition? pattern = context.Schema.FindPattern(node.ReferenceName);
                    if (pattern == null)
                    {
                        return false;
                    }

                    foreach (OdfSchemaPatternNode root in pattern.Roots)
                    {
                        if (PatternMatchesElementName(root, element, context))
                        {
                            return true;
                        }
                    }

                    return false;
                }
                finally
                {
                    context.LeaveReference(node.ReferenceName);
                }
            }

            if (node.Kind == OdfSchemaPatternNodeKind.Element)
            {
                if (string.IsNullOrEmpty(node.LocalName) && string.IsNullOrEmpty(node.NamespaceUri))
                {
                    return true;
                }

                return string.Equals(node.NamespaceUri, element.Name.NamespaceName, StringComparison.Ordinal) &&
                    string.Equals(node.LocalName, element.Name.LocalName, StringComparison.Ordinal);
            }

            if (node.Kind == OdfSchemaPatternNodeKind.Choice ||
                node.Kind == OdfSchemaPatternNodeKind.Group ||
                node.Kind == OdfSchemaPatternNodeKind.Interleave ||
                node.Kind == OdfSchemaPatternNodeKind.Optional ||
                node.Kind == OdfSchemaPatternNodeKind.ZeroOrMore ||
                node.Kind == OdfSchemaPatternNodeKind.OneOrMore)
            {
                foreach (OdfSchemaPatternNode child in node.Children)
                {
                    if (PatternMatchesElementName(child, element, context))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private sealed class MatchContext
        {
            private readonly HashSet<string> _activeReferences = new HashSet<string>(StringComparer.Ordinal);

            public MatchContext(OdfSchemaSet schema)
            {
                Schema = schema;
            }

            public OdfSchemaSet Schema { get; }

            public bool EnterReference(string referenceName) => _activeReferences.Add(referenceName);

            public void LeaveReference(string referenceName) => _activeReferences.Remove(referenceName);
        }
}

/// <summary>
/// 代表結構描述模式驗證的結果。
/// </summary>
public sealed class OdfSchemaPatternValidationResult
{
    private OdfSchemaPatternValidationResult(bool isMatch, IReadOnlyList<OdfValidationIssue> issues)
    {
        IsMatch = isMatch;
        Issues = issues;
    }

    /// <summary>
    /// 取得一個值，表示 XML 元素是否符合結構描述模式。
    /// </summary>
    public bool IsMatch { get; }

    /// <summary>
    /// 取得模式驗證的問題。
    /// </summary>
    public IReadOnlyList<OdfValidationIssue> Issues { get; }

    internal static OdfSchemaPatternValidationResult Success()
    {
        return new OdfSchemaPatternValidationResult(true, []);
    }

    internal static OdfSchemaPatternValidationResult Fail(string ruleId, string message)
    {
        return new OdfSchemaPatternValidationResult(
            false,
            [
                new OdfValidationIssue(OdfIssueSeverity.Error, ruleId, message)
            ]);
    }
}
