using CommunityToolkit.HighPerformance.Buffers;

namespace OdfKit.DOM;

/// <summary>
/// 執行緒專屬 XML 解析字串池，供跨文件重複使用常見標籤名稱以降低 GC 壓力（PERF-5c）。
/// </summary>
internal static class OdfXmlStringPools
{
    private const int MaxGetOrAddBeforeReset = 4096;

    private static readonly ThreadLocal<PoolHolder> ThreadPools = new(() => new PoolHolder(), trackAllValues: true);

    /// <summary>
    /// 取得目前執行緒的字串池並將指定字串加入或取出共用執行個體。
    /// </summary>
    internal static string GetOrAdd(string value)
    {
        PoolHolder holder = ThreadPools.Value!;
        if (++holder.UseCount > MaxGetOrAddBeforeReset)
        {
            holder.Reset();
        }

        return holder.Pool.GetOrAdd(value);
    }

    private sealed class PoolHolder
    {
        internal StringPool Pool = new();
        internal int UseCount;

        internal void Reset()
        {
            Pool = new StringPool();
            UseCount = 0;
        }
    }
}
