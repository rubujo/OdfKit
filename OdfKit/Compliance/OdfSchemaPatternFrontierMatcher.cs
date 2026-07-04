using System;
using System.Collections.Generic;

namespace OdfKit.Compliance;

/// <summary>
/// 共用的「frontier（前緣）」狀態擴展演算法（內部協作者）。
/// </summary>
/// <remarks>
/// RELAX NG 的 <c>zeroOrMore</c>／<c>oneOrMore</c> 重複比對在內容模型（依子元素位置索引）、
/// 清單語彙（依 token 位置索引）與屬性模式（依已消耗屬性位元遮罩）三處各自獨立實作了同一套
/// 「由目前狀態集合反覆展開至不再產生新狀態」的演算法，僅狀態型別與展開函式不同。
/// 此類別將該共用演算法抽出為單一泛型輔助方法，避免三份實作各自演化而產生行為分歧。
/// </remarks>
internal static class OdfSchemaPatternFrontierMatcher
{
    /// <summary>
    /// 從初始狀態開始，反覆以 <paramref name="expand"/> 展開目前的前緣狀態集合，直到不再產生新狀態為止，
    /// 並回傳所有曾經到達過的狀態（RELAX NG <c>zeroOrMore</c>／<c>oneOrMore</c> 重複比對的共用實作）。
    /// </summary>
    /// <typeparam name="TState">狀態型別（例如子元素索引、token 索引，或屬性消耗位元遮罩）</typeparam>
    /// <param name="initialState">初始狀態</param>
    /// <param name="requireOne">是否至少須成功展開一次（<c>oneOrMore</c> 為 <see langword="true"/>；<c>zeroOrMore</c> 為 <see langword="false"/>）</param>
    /// <param name="expand">給定目前狀態，展開出下一輪可達的狀態集合</param>
    /// <returns>所有曾經到達過的狀態；若 <paramref name="requireOne"/> 為 <see langword="true"/> 且從未成功展開，則為空集合</returns>
    internal static HashSet<TState> ExpandRepeated<TState>(
        TState initialState,
        bool requireOne,
        Func<TState, IEnumerable<TState>> expand)
    {
        var matches = new HashSet<TState>();
        var frontier = new HashSet<TState> { initialState };
        bool consumedAny = false;

        if (!requireOne)
        {
            matches.Add(initialState);
        }

        while (frontier.Count > 0)
        {
            var nextFrontier = new HashSet<TState>();
            foreach (TState current in frontier)
            {
                foreach (TState matched in expand(current))
                {
                    if (EqualityComparer<TState>.Default.Equals(matched, current))
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

        return requireOne && !consumedAny ? new HashSet<TState>() : matches;
    }
}
