using System;
using System.Collections.Generic;
using System.Xml.Linq;

namespace OdfKit.Compliance;

internal static partial class OdfSchemaPatternContentMatcher
{
    #region Content Matching - Interleave

    private static void MatchInterleaveRecursive(
        IReadOnlyList<OdfSchemaPatternNode> nodes,
        XElement parent,
        IReadOnlyList<XElement> childElements,
        int index,
        OdfSchemaPatternMatchContext context,
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

        if (InterleaveRequirementsSatisfied(nodes, used, oneOrMoreSatisfied, context))
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
        IReadOnlyList<bool> oneOrMoreSatisfied,
        OdfSchemaPatternMatchContext context)
    {
        for (int i = 0; i < nodes.Count; i++)
        {
            OdfSchemaPatternNode node = nodes[i];
            if (node.Occurrence == "optional" ||
                node.Occurrence == "zeroOrMore" ||
                ContentNodeCanMatchEmpty(node, context))
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

    #endregion
}
