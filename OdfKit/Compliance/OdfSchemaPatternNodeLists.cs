using System;
using System.Collections.Generic;

namespace OdfKit.Compliance;

/// <summary>
/// 以索引迴圈走訪圖樣節點清單；取代 LINQ 的 Any／All，避免每次比對配置 enumerator 與 closure
/// （驗證每個元素都會走訪，LINQ 版本每列約配置數十 KB）。
/// </summary>
internal static class OdfSchemaPatternNodeLists
{
    internal static bool AnyNode<TState>(
        IReadOnlyList<OdfSchemaPatternNode> nodes,
        TState state,
        Func<OdfSchemaPatternNode, TState, bool> predicate)
    {
        for (int i = 0; i < nodes.Count; i++)
        {
            if (predicate(nodes[i], state))
            {
                return true;
            }
        }

        return false;
    }

    internal static bool AllNode<TState>(
        IReadOnlyList<OdfSchemaPatternNode> nodes,
        TState state,
        Func<OdfSchemaPatternNode, TState, bool> predicate)
    {
        for (int i = 0; i < nodes.Count; i++)
        {
            if (!predicate(nodes[i], state))
            {
                return false;
            }
        }

        return true;
    }

    internal static bool AnyNode(
        IReadOnlyList<OdfSchemaPatternNode> nodes,
        Func<OdfSchemaPatternNode, bool> predicate)
    {
        for (int i = 0; i < nodes.Count; i++)
        {
            if (predicate(nodes[i]))
            {
                return true;
            }
        }

        return false;
    }

    internal static bool AllNode(
        IReadOnlyList<OdfSchemaPatternNode> nodes,
        Func<OdfSchemaPatternNode, bool> predicate)
    {
        for (int i = 0; i < nodes.Count; i++)
        {
            if (!predicate(nodes[i]))
            {
                return false;
            }
        }

        return true;
    }
}
