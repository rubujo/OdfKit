using System;
using System.Collections.Generic;
using System.Numerics;
using System.Xml.Linq;

namespace OdfKit.Compliance;

internal static partial class OdfSchemaPatternAttributeMatcher
{
    #region Attribute Patterns - Matching

    // 屬性消耗狀態以 BigInteger 位元遮罩表示（第 i 位為 1 代表索引 i 之屬性已被消耗），
    // 取代先前以逗號分隔字串表示、每次比對節點都需重新 Split／int.TryParse／排序並重新
    // 組字串的做法。BigInteger 不像 ulong 受限於 64 位元，屬性數量不論多少皆可正確表示。

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

        HashSet<BigInteger> matches = MatchAttributePatternSequence(
            attributeNodes,
            attributes,
            BigInteger.Zero,
            context);

        BigInteger allConsumedMask = (BigInteger.One << attributes.Count) - BigInteger.One;
        return matches.Contains(allConsumedMask);
    }

    private static HashSet<BigInteger> MatchAttributePatternSequence(
        IReadOnlyList<OdfSchemaPatternNode> nodes,
        IReadOnlyList<XAttribute> attributes,
        BigInteger state,
        OdfSchemaPatternMatchContext context)
    {
        var states = new HashSet<BigInteger> { state };
        for (int nodeIndex = 0; nodeIndex < nodes.Count; nodeIndex++)
        {
            OdfSchemaPatternNode node = nodes[nodeIndex];
            var next = new HashSet<BigInteger>();
            foreach (BigInteger current in states)
            {
                HashSet<BigInteger> nodeMatches =
                    MatchAttributePatternNode(node, attributes, current, context);
                if (nodeMatches.Contains(current) &&
                    nodeMatches.Any(matched => matched != current &&
                        NewlyConsumedAttributesCannotMatchLater(
                            nodes,
                            nodeIndex + 1,
                            attributes,
                            current,
                            matched,
                            context)))
                {
                    nodeMatches.Remove(current);
                }

                foreach (BigInteger matched in nodeMatches)
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

    private static bool NewlyConsumedAttributesCannotMatchLater(
        IReadOnlyList<OdfSchemaPatternNode> nodes,
        int nextNodeIndex,
        IReadOnlyList<XAttribute> attributes,
        BigInteger previousState,
        BigInteger matchedState,
        OdfSchemaPatternMatchContext context)
    {
        BigInteger newlyConsumed = matchedState & ~previousState;
        for (int attributeIndex = 0; attributeIndex < attributes.Count; attributeIndex++)
        {
            BigInteger bit = BigInteger.One << attributeIndex;
            if ((newlyConsumed & bit) == BigInteger.Zero)
            {
                continue;
            }

            XAttribute attribute = attributes[attributeIndex];
            for (int nodeIndex = nextNodeIndex; nodeIndex < nodes.Count; nodeIndex++)
            {
                if (AttributePatternAllowsAttribute(nodes[nodeIndex], attribute, context))
                {
                    return false;
                }
            }
        }

        return newlyConsumed != BigInteger.Zero;
    }

    private static HashSet<BigInteger> MatchAttributePatternNode(
        OdfSchemaPatternNode node,
        IReadOnlyList<XAttribute> attributes,
        BigInteger state,
        OdfSchemaPatternMatchContext context)
    {
        switch (node.Kind)
        {
            case OdfSchemaPatternNodeKind.Attribute:
                return MatchSingleAttributePattern(node, attributes, state, context);
            case OdfSchemaPatternNodeKind.NotAllowed:
                return new HashSet<BigInteger>();
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
                return new HashSet<BigInteger> { state };
            default:
                // 元素、文字與資料型別節點描述內容，不會消耗屬性。它們可能與屬性節點
                // 同處於 group 或 choice；在屬性投影中必須保留目前狀態，否則合法的
                // 「屬性 + 文字值」分支會被誤判為無法匹配。
                return new HashSet<BigInteger> { state };
        }
    }

    private static HashSet<BigInteger> MatchSingleAttributePattern(
        OdfSchemaPatternNode node,
        IReadOnlyList<XAttribute> attributes,
        BigInteger state,
        OdfSchemaPatternMatchContext context)
    {
        var matches = new HashSet<BigInteger>();
        for (int i = 0; i < attributes.Count; i++)
        {
            BigInteger bit = BigInteger.One << i;
            if ((state & bit) != BigInteger.Zero)
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
                matches.Add(state | bit);
            }
        }

        return matches;
    }

    private static HashSet<BigInteger> MatchAttributePatternReference(
        string referenceName,
        IReadOnlyList<XAttribute> attributes,
        BigInteger state,
        OdfSchemaPatternMatchContext context)
    {
        if (string.IsNullOrWhiteSpace(referenceName))
        {
            return new HashSet<BigInteger>();
        }

        if (!context.EnterReference(referenceName))
        {
            // 同名參照仍在作用中堆疊上，不必然代表真正的無窮遞迴（例如合法共用屬性群組
            // 被兩個不同分支各自參照一次），改建立新的巢狀內容並限制其遞迴深度，
            // 而非直接視為循環而拒絕比對。
            OdfSchemaPatternMatchContext? recursiveContext = context.CreateRecursiveContext();
            return recursiveContext is null
                ? new HashSet<BigInteger>()
                : MatchAttributePatternReferenceCore(referenceName, attributes, state, recursiveContext);
        }

        try
        {
            return MatchAttributePatternReferenceCore(referenceName, attributes, state, context);
        }
        finally
        {
            context.LeaveReference(referenceName);
        }
    }

    private static HashSet<BigInteger> MatchAttributePatternReferenceCore(
        string referenceName,
        IReadOnlyList<XAttribute> attributes,
        BigInteger state,
        OdfSchemaPatternMatchContext context)
    {
        OdfSchemaPatternDefinition? pattern = context.Schema.FindPattern(referenceName);
        if (pattern == null)
        {
            return new HashSet<BigInteger>();
        }

        return MatchAttributePatternSequence(pattern.Roots, attributes, state, context);
    }

    private static HashSet<BigInteger> MatchAttributePatternChoice(
        OdfSchemaPatternNode node,
        IReadOnlyList<XAttribute> attributes,
        BigInteger state,
        OdfSchemaPatternMatchContext context)
    {
        var matches = new HashSet<BigInteger>();
        foreach (OdfSchemaPatternNode child in node.Children)
        {
            foreach (BigInteger matched in MatchAttributePatternNode(child, attributes, state, context))
            {
                matches.Add(matched);
            }
        }

        return matches;
    }

    private static HashSet<BigInteger> MatchOptionalAttributePattern(
        OdfSchemaPatternNode node,
        IReadOnlyList<XAttribute> attributes,
        BigInteger state,
        OdfSchemaPatternMatchContext context)
    {
        var matches = new HashSet<BigInteger> { state };
        foreach (BigInteger matched in MatchAttributePatternSequence(node.Children, attributes, state, context))
        {
            matches.Add(matched);
        }

        return matches;
    }

    private static HashSet<BigInteger> MatchRepeatedAttributePattern(
        OdfSchemaPatternNode node,
        IReadOnlyList<XAttribute> attributes,
        BigInteger state,
        OdfSchemaPatternMatchContext context,
        bool requireOne)
    {
        return OdfSchemaPatternFrontierMatcher.ExpandRepeated(
            state,
            requireOne,
            current => MatchAttributePatternSequence(node.Children, attributes, current, context));
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
