using System;
using System.Collections.Generic;
using System.Xml.Linq;

namespace OdfKit.Compliance;
/// <summary>
/// Provides the OdfSchemaPatternValidator API.
/// 提供 OdfSchemaPatternValidator API。
/// </summary>

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

    internal static bool MatchesNameClassNode(
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

    internal static bool MatchAttributeValueNodes(
        IReadOnlyList<OdfSchemaPatternNode> nodes,
        string value,
        OdfSchemaPatternMatchContext context)
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
        OdfSchemaPatternMatchContext context)
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
                if (string.IsNullOrWhiteSpace(node.ReferenceName) || !context.EnterReference(node.ReferenceName))
                {
                    return false;
                }

                try
                {
                    OdfSchemaPatternDefinition? pattern = context.Schema.FindPattern(node.ReferenceName);
                    return pattern != null && pattern.Roots.Any(root => MatchAttributeValueNode(root, value, context));
                }
                finally
                {
                    context.LeaveReference(node.ReferenceName);
                }
            default:
                return false;
        }
    }

    internal static bool IsSimpleTextNode(XElement element)
    {
        return !element.Elements().Any();
    }

    internal static bool MatchesListValue(
        IReadOnlyList<OdfSchemaPatternNode> nodes,
        string value,
        OdfSchemaPatternMatchContext context)
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
        OdfSchemaPatternMatchContext context)
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
        OdfSchemaPatternMatchContext context)
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
        OdfSchemaPatternMatchContext context)
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
        OdfSchemaPatternMatchContext context)
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
        OdfSchemaPatternMatchContext context,
        bool requireOne)
    {
        return OdfSchemaPatternFrontierMatcher.ExpandRepeated(
            index,
            requireOne,
            current => MatchListSequence(node.Children, tokens, current, context));
    }

    private static HashSet<int> MatchListReference(
        string referenceName,
        IReadOnlyList<string> tokens,
        int index,
        OdfSchemaPatternMatchContext context)
    {
        if (string.IsNullOrWhiteSpace(referenceName))
        {
            return new HashSet<int>();
        }

        if (!context.EnterReference(referenceName))
        {
            // 同名參照仍在作用中堆疊上，不必然代表真正的無窮遞迴，改建立新的巢狀內容
            // 並限制其遞迴深度，而非直接視為循環而拒絕比對（比照 Content.Sequence 慣例）。
            OdfSchemaPatternMatchContext? recursiveContext = context.CreateRecursiveContext();
            return recursiveContext is null
                ? new HashSet<int>()
                : MatchListReferenceCore(referenceName, tokens, index, recursiveContext);
        }

        try
        {
            return MatchListReferenceCore(referenceName, tokens, index, context);
        }
        finally
        {
            context.LeaveReference(referenceName);
        }
    }

    private static HashSet<int> MatchListReferenceCore(
        string referenceName,
        IReadOnlyList<string> tokens,
        int index,
        OdfSchemaPatternMatchContext context)
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


    #endregion
}
