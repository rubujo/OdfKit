using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Spreadsheet;
/// <summary>
/// Provides the OdfTableSheet API.
/// 提供 OdfTableSheet API。
/// </summary>

public partial class OdfTableSheet
{
    #region Cell & Column Access

    // Fast-path cache for the common "build a fresh sheet by walking rows/columns in order" pattern,
    // where OdfTableSheetDomAccessEngine's per-call full-table rescan otherwise dominates cost for
    // large sheets. The cache only ever accelerates lookups that are provably equivalent to the
    // uncompressed (no number-rows/columns-repeated) engine result: it is a verified *prefix* of
    // logical row/column indexes for which a 1:1 index-to-node mapping holds. A row-container element
    // or repeat-compressed node encountered while building or extending the cache marks the current
    // boundary — indexes below it keep being served from the cache, while the boundary index and
    // everything past it always falls back to the original, always-correct engine path. This matters
    // because a single trailing repeat-compressed row (e.g. the empty-row padding LibreOffice writes at
    // the end of nearly every saved sheet, or the compressed block OdfTableSheet.InsertRows(count > 1)
    // itself creates) must not silently disable caching for the entire, otherwise-uncompressed sheet.
    // Writes that resolve (split) a repeat-compressed node at exactly the current boundary naturally
    // extend the prefix by one via TryExtendRowCache/TryExtendCellCache, so a block resolved via
    // sequential access keeps growing the fast path instead of requiring a full rebuild.
    // 「由程式逐列逐欄建立新工作表」這個常見情境的快取加速層——OdfTableSheetDomAccessEngine 每次呼叫
    // 都重新掃描整表，對大型工作表而言是主要成本來源。此快取只在結果與未壓縮
    // （無 number-rows/columns-repeated）情境下的引擎結果可證明一致時才加速：它是邏輯列／欄索引的一段
    // 「已驗證前綴」，在此範圍內索引與節點維持一對一對應。建立或延伸快取時一旦遇到列容器元素或壓縮節點，
    // 即記錄目前邊界——邊界之前的索引繼續由快取服務，邊界索引本身與其後全部索引則永遠改採至原始、永遠
    // 正確的引擎路徑。這點很重要，因為單一尾端壓縮列（例如 LibreOffice 幾乎在每份儲存的工作表結尾都會寫入
    // 的空列填補壓縮，或 OdfTableSheet.InsertRows(count > 1) 本身建立的壓縮區塊）不應讓整張表（原本毫無
    // 壓縮）的快取被靜默停用。若某次寫入剛好在目前邊界處拆分（resolve）了一個壓縮節點，會透過
    // TryExtendRowCache／TryExtendCellCache 自然將前綴向後延伸一格；因此以循序方式逐步解析的壓縮區塊，
    // 其快取前綴會持續成長，而不必整個重建。
    private List<OdfNode>? _rowNodeCache;
    private readonly Dictionary<OdfNode, RowCellCache> _cellNodeCacheByRow = [];

    private sealed class RowCellCache
    {
        internal readonly List<OdfNode> Cells = [];
    }

    /// <summary>
    /// Attempts to get the cell XML node at the specified row and column indexes without modifying the DOM.
    /// 嘗試以唯讀方式取得指定列與欄索引的儲存格 XML 節點，不修改 DOM 結構。
    /// </summary>
    /// <param name="row">The zero-based row index. / 以 0 為基準的列索引。</param>
    /// <param name="col">The zero-based column index. / 以 0 為基準的欄索引。</param>
    /// <returns>The cell XML node, or <see langword="null"/> when it does not exist. / 儲存格 XML 節點；不存在時為 <see langword="null"/>。</returns>
    internal OdfNode? TryGetCellNode(int row, int col)
    {
        if (TryGetCachedRowNode(row, out OdfNode? cachedRowNode) &&
            TryGetCachedCellNode(cachedRowNode, col, out OdfNode? cachedCellNode))
        {
            return cachedCellNode;
        }

        return OdfTableSheetDomAccessEngine.TryGetCellNode(TableNode, row, col);
    }

    private OdfNode GetOrCreateCellNode(int row, int col)
    {
        bool rowWasCached = TryGetCachedRowNode(row, out OdfNode? cachedRowNode);
        if (rowWasCached)
        {
            if (TryGetCachedCellNode(cachedRowNode, col, out OdfNode? cachedCellNode))
            {
                return cachedCellNode;
            }

            // The row is already known, so go straight to the row-scoped cell lookup/creation instead
            // of OdfTableSheetDomAccessEngine.GetOrCreateCellNode(TableNode, row, col), which would
            // otherwise redundantly re-scan the whole table to re-derive the same row node.
            OdfTableSheetDomAccessEngine.EnsureColumnDefinitions(TableNode, col);
            OdfNode rowScopedCellNode = OdfTableSheetDomAccessEngine.GetOrCreateCellNode(cachedRowNode, col, forWrite: true);
            TryExtendCellCache(row, col, rowScopedCellNode);
            return rowScopedCellNode;
        }

        OdfNode cellNode = OdfTableSheetDomAccessEngine.GetOrCreateCellNode(TableNode, row, col);
        TryExtendRowCache(row);
        TryExtendCellCache(row, col, cellNode);
        return cellNode;
    }

    private void ReplaceCellNode(int row, int col, OdfNode newCellNode)
    {
        OdfTableSheetDomAccessEngine.ReplaceCellNode(TableNode, row, col, newCellNode);
        InvalidateAccessCache();
    }

    internal OdfNode GetOrCreateColumnNode(int col)
        => OdfTableSheetDomAccessEngine.GetOrCreateColumnNode(TableNode, col);

    private OdfNode GetOrCreateRowNode(int row)
    {
        if (TryGetCachedRowNode(row, out OdfNode? cachedRowNode))
        {
            return cachedRowNode;
        }

        OdfNode rowNode = OdfTableSheetDomAccessEngine.GetOrCreateRowNode(TableNode, row, forWrite: true);
        TryExtendRowCache(row, rowNode);
        return rowNode;
    }

    /// <summary>
    /// Clears the row/cell access cache; must be called after any operation that may add, remove, split,
    /// or reorder row or cell nodes outside the incremental append path this cache understands.
    /// 清除列／儲存格存取快取；任何可能新增、移除、拆分或重排列／儲存格節點，且超出此快取所理解的
    /// 遞增附加路徑之操作後，皆須呼叫此方法。
    /// </summary>
    internal void InvalidateAccessCache()
    {
        _rowNodeCache = null;
        _cellNodeCacheByRow.Clear();
    }

    private bool TryGetCachedRowNode(int row, out OdfNode rowNode)
    {
        rowNode = null!;
        if (row < 0)
        {
            return false;
        }

        EnsureRowCacheBuilt();
        if (row >= _rowNodeCache!.Count)
        {
            return false;
        }

        rowNode = _rowNodeCache[row];
        return true;
    }

    private void EnsureRowCacheBuilt()
    {
        if (_rowNodeCache is not null)
        {
            return;
        }

        List<OdfNode> rows = [];
        foreach (OdfNode child in TableNode.Children)
        {
            if (OdfTableSheetDomAccessEngine.RowContainerNames.Contains(child.LocalName) && child.NamespaceUri == OdfNamespaces.Table)
            {
                // Nested row containers are not indexed by this flat cache; stop extending the
                // verified prefix here but keep whatever plain rows were already collected before it.
                break;
            }

            if (child.LocalName != "table-row" || child.NamespaceUri != OdfNamespaces.Table)
            {
                continue;
            }

            if (OdfTableSheetRepeatSplitEngine.GetRepeatCount(child, "number-rows-repeated") > 1)
            {
                // A repeat-compressed row breaks the 1:1 logical-row-to-node mapping this cache
                // relies on; stop extending here rather than discarding the prefix already verified.
                // This row and every logical row after it always falls back to the engine path, same
                // as before, but earlier rows (e.g. real data preceding LibreOffice's typical trailing
                // empty-row padding) keep the fast path instead of losing it for the whole sheet.
                break;
            }

            rows.Add(child);
        }

        _rowNodeCache = rows;
    }

    private void TryExtendRowCache(int row)
    {
        if (_rowNodeCache is null || row != _rowNodeCache.Count)
        {
            return;
        }

        OdfNode? appended = OdfTableSheetDomAccessEngine.TryFindRowNode(TableNode, row);
        if (appended is null)
        {
            return;
        }

        TryExtendRowCache(row, appended);
    }

    private void TryExtendRowCache(int row, OdfNode rowNode)
    {
        if (_rowNodeCache is null)
        {
            return;
        }

        if (row != _rowNodeCache.Count)
        {
            // A gap-filling or otherwise non-sequential append happened; rebuild lazily on next access
            // rather than risk an incorrect incremental extension.
            _rowNodeCache = null;
            return;
        }

        if (OdfTableSheetRepeatSplitEngine.GetRepeatCount(rowNode, "number-rows-repeated") > 1)
        {
            // Still repeat-compressed (e.g. a write landed exactly on a not-yet-split repeated node);
            // leave the verified prefix exactly as-is rather than growing past an unsafe boundary.
            return;
        }

        _rowNodeCache.Add(rowNode);
    }

    private bool TryGetCachedCellNode(OdfNode rowNode, int col, out OdfNode cellNode)
    {
        cellNode = null!;
        if (col < 0)
        {
            return false;
        }

        if (!_cellNodeCacheByRow.TryGetValue(rowNode, out RowCellCache? cache))
        {
            cache = BuildCellCache(rowNode);
        }

        if (col >= cache.Cells.Count)
        {
            return false;
        }

        cellNode = cache.Cells[col];
        return true;
    }

    private RowCellCache BuildCellCache(OdfNode rowNode)
    {
        var cache = new RowCellCache();
        foreach (OdfNode child in rowNode.Children)
        {
            if ((child.LocalName != "table-cell" && child.LocalName != "covered-table-cell") || child.NamespaceUri != OdfNamespaces.Table)
            {
                continue;
            }

            if (OdfTableSheetRepeatSplitEngine.GetRepeatCount(child, "number-columns-repeated") > 1)
            {
                // Same prefix-boundary reasoning as EnsureRowCacheBuilt, at the column level within
                // this row: stop extending, but keep the cells already verified before this point.
                break;
            }

            cache.Cells.Add(child);
        }

        _cellNodeCacheByRow[rowNode] = cache;
        return cache;
    }

    private void TryExtendCellCache(int row, int col, OdfNode cellNode)
    {
        if (!TryGetCachedRowNode(row, out OdfNode rowNode) ||
            !_cellNodeCacheByRow.TryGetValue(rowNode, out RowCellCache? cache))
        {
            return;
        }

        if (col != cache.Cells.Count)
        {
            _cellNodeCacheByRow.Remove(rowNode);
            return;
        }

        cache.Cells.Add(cellNode);
    }

    #endregion
}
