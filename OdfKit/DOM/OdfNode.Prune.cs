using System;

using OdfKit.Styles;

namespace OdfKit.DOM;

public partial class OdfNode
{
    /// <summary>
    /// 釋放此節點已載入子樹中的可重建查詢快取，而不移除 DOM 節點或具現化延遲 XML。
    /// </summary>
    /// <returns>已掃描並釋放快取的已載入節點數，包含此節點本身。</returns>
    /// <remarks>
    /// 此方法是 <see cref="PruneAndCollect(bool)"/> 的非破壞式版本；適合在大型文件遍歷後主動釋放 wrapper
    /// 尋覽快取。它不會讀取 <see cref="Children"/> 的 Count 或索引器，因此不會觸發 lazy XML materialization。
    /// </remarks>
    public int ReleaseUnusedNodes()
    {
        return ReleaseUnusedNodesCore();
    }

    /// <summary>
    /// 將此節點子樹自目前 DOM 樹斷開，並釋放其子節點、屬性與延遲載入 XML 參照。
    /// </summary>
    /// <param name="collectGarbage">是否在剪裁後要求執行一次最佳化 GC 收集</param>
    /// <returns>已剪裁的節點數，包含此節點本身</returns>
    /// <remarks>
    /// 此方法不會為了清理而具現化延遲載入的子樹；未解析的延遲 XML 緩衝區會直接解除參照。
    /// 呼叫後，此節點物件仍可被持有，但已不再代表原本的文件子樹。
    /// </remarks>
    public int PruneAndCollect(bool collectGarbage = false)
    {
        OdfStyleEngine? styleEngine = Document?.StyleEngine;
        Parent?.Children.Remove(this);
        int prunedCount = PruneSubtreeCore(styleEngine);

        if (collectGarbage)
        {
            GC.Collect(2, GCCollectionMode.Optimized, blocking: false, compacting: false);
        }

        return prunedCount;
    }

    private int ReleaseUnusedNodesCore()
    {
        int count = 1;
        Children.ReleaseIndexCache();
        SiblingIndex = Parent is null ? -1 : SiblingIndex;

        int childIndex = 0;
        for (OdfNode? child = _firstChild; child is not null; child = child.NextSibling)
        {
            child.SiblingIndex = childIndex++;
            count += child.ReleaseUnusedNodesCore();
        }

        return count;
    }

    private int PruneSubtreeCore(OdfStyleEngine? styleEngine)
    {
        int count = 1;
        styleEngine?.ReleaseLocalStyle(this);
        for (OdfNode? child = _firstChild; child is not null;)
        {
            OdfNode? next = child.NextSibling;
            count += child.PruneSubtreeCore(styleEngine);
            child = next;
        }

        _firstChild = null;
        _lastChild = null;
        Children.ResetAfterPrune();
        Attributes.Clear();
        _attributePrefixes.Clear();
        Parent = null;
        PreviousSibling = null;
        NextSibling = null;
        SiblingIndex = -1;
        _lazyXmlMemory = default;
        _lazyXmlPtr = IntPtr.Zero;
        _lazyXmlLen = 0;
        _xmlByteRange = null;
        _isLazy = false;
        IsModified = false;
        Document = null;

        return count;
    }
}
