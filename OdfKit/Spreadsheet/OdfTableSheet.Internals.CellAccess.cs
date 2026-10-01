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

    // True when _rowNodeCache covers every logical row of the table (no container or repeat-compressed
    // row ended the verified prefix early), so an index equal to the cache count is a brand-new row.
    // 為 true 表示 _rowNodeCache 涵蓋表格全部邏輯列（沒有列容器或壓縮列提早結束已驗證前綴），
    // 因此等於快取數量的索引必為全新的列。
    private bool _rowCacheComplete;
    private readonly Dictionary<OdfNode, RowCellCache> _cellNodeCacheByRow = [];
    private readonly object _accessCacheLock = new();

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
        lock (_accessCacheLock)
        {
            if (TryGetCachedRowNode(row, out OdfNode? cachedRowNode) &&
                TryGetCachedCellNode(cachedRowNode, col, out OdfNode? cachedCellNode))
            {
                return cachedCellNode;
            }

            return OdfTableSheetDomAccessEngine.TryGetCellNode(TableNode, row, col);
        }
    }

    private OdfNode GetOrCreateCellNode(int row, int col)
    {
        lock (_accessCacheLock)
        {
            bool rowWasCached = TryGetCachedRowNode(row, out OdfNode? cachedRowNode);
            if (rowWasCached)
            {
                if (TryGetCachedCellNode(cachedRowNode, col, out OdfNode? cachedCellNode))
                    return cachedCellNode;

                OdfTableSheetDomAccessEngine.EnsureColumnDefinitions(TableNode, col);
                OdfNode rowScopedCellNode = OdfTableSheetDomAccessEngine.GetOrCreateCellNode(cachedRowNode, col, forWrite: true);
                TryExtendCellCache(row, col, rowScopedCellNode);
                return rowScopedCellNode;
            }

            if (TryAppendRowAtEnd(row, col, out OdfNode appendedRow))
            {
                OdfNode appendedCell = OdfTableSheetDomAccessEngine.GetOrCreateCellNode(appendedRow, col, forWrite: true);
                TryExtendCellCache(row, col, appendedCell);
                return appendedCell;
            }

            OdfNode cellNode = OdfTableSheetDomAccessEngine.GetOrCreateCellNode(TableNode, row, col);
            TryExtendRowCache(row);
            TryExtendCellCache(row, col, cellNode);
            return cellNode;
        }
    }

    private void ReplaceCellNode(int row, int col, OdfNode newCellNode)
    {
        lock (_accessCacheLock)
        {
            OdfTableSheetDomAccessEngine.ReplaceCellNode(TableNode, row, col, newCellNode);
            _rowNodeCache = null;
            _cellNodeCacheByRow.Clear();
        }
    }

    internal OdfNode GetOrCreateColumnNode(int col)
    {
        lock (_accessCacheLock)
            return OdfTableSheetDomAccessEngine.GetOrCreateColumnNode(TableNode, col);
    }

    private OdfNode GetOrCreateRowNode(int row)
    {
        lock (_accessCacheLock)
        {
            if (TryGetCachedRowNode(row, out OdfNode? cachedRowNode))
                return cachedRowNode;

            if (TryAppendRowAtEnd(row, 0, out OdfNode appendedRow))
                return appendedRow;

            OdfNode rowNode = OdfTableSheetDomAccessEngine.GetOrCreateRowNode(TableNode, row, forWrite: true);
            TryExtendRowCache(row, rowNode);
            return rowNode;
        }
    }

    /// <summary>
    /// Appends a new row in O(1) when <paramref name="row"/> is exactly one past a fully verified row cache,
    /// which is what the engine would do after a full-table scan (the sequential "build a sheet row by row" pattern).
    /// 當 <paramref name="row"/> 恰好是完整已驗證列快取的下一列時，以 O(1) 附加新列；
    /// 其結果與引擎全表掃描後的行為相同（逐列建立工作表的循序模式）。
    /// </summary>
    /// <param name="row">The zero-based row index to create. / 以 0 為基準、要建立的列索引。</param>
    /// <param name="col">The column index that will be accessed; validated and column definitions ensured. / 即將存取的欄索引；會驗證並確保欄定義。</param>
    /// <param name="rowNode">The appended row node. / 新附加的列節點。</param>
    /// <returns><see langword="true"/> when the fast path applied; otherwise <see langword="false"/> and nothing was changed. / 套用快速路徑時為 <see langword="true"/>；否則為 <see langword="false"/> 且未做任何變更。</returns>
    private bool TryAppendRowAtEnd(int row, int col, out OdfNode rowNode)
    {
        rowNode = null!;
        if (_rowNodeCache is null ||
            !_rowCacheComplete ||
            row != _rowNodeCache.Count ||
            row > OdfSpreadsheetLimits.MaxRowIndex ||
            !IsLastRowLikeChild(_rowNodeCache.Count == 0 ? null : _rowNodeCache[_rowNodeCache.Count - 1]))
        {
            return false;
        }

        // 與引擎相同的順序：先驗證欄索引並補欄定義，再附加列。
        OdfTableSheetDomAccessEngine.EnsureColumnDefinitions(TableNode, col);
        rowNode = OdfTableSheetDomAccessEngine.CreateRowNode();
        TableNode.AppendChild(rowNode);
        _rowNodeCache.Add(rowNode);
        return true;
    }

    // 保護快取：確認表格中最後一個列類子節點確實是快取的最後一列（或表格根本沒有列），
    // 否則代表 DOM 已被快取不知道的路徑改動，交還給引擎處理。從尾端往前掃，通常 O(1)。
    private bool IsLastRowLikeChild(OdfNode? lastCachedRow)
    {
        // 不可用 Children[i]：其索引快取在每次附加後都失效，重建為 O(n)；改走兄弟連結。
        for (OdfNode? child = TableNode.LastChild; child is not null; child = child.PreviousSibling)
        {
            if (child.NamespaceUri == OdfNamespaces.Table &&
                (child.LocalName == "table-row" || OdfTableSheetDomAccessEngine.RowContainerNames.Contains(child.LocalName)))
            {
                return ReferenceEquals(child, lastCachedRow);
            }
        }

        return lastCachedRow is null;
    }

    /// <summary>
    /// Clears the row/cell access cache; must be called after any operation that may add, remove, split,
    /// or reorder row or cell nodes outside the incremental append path this cache understands.
    /// 清除列／儲存格存取快取；任何可能新增、移除、拆分或重排列／儲存格節點，且超出此快取所理解的
    /// 遞增附加路徑之操作後，皆須呼叫此方法。
    /// </summary>
    internal void InvalidateAccessCache()
    {
        lock (_accessCacheLock)
        {
            _rowNodeCache = null;
            _cellNodeCacheByRow.Clear();
        }
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
        bool complete = true;
        foreach (OdfNode child in TableNode.Children)
        {
            if (OdfTableSheetDomAccessEngine.RowContainerNames.Contains(child.LocalName) && child.NamespaceUri == OdfNamespaces.Table)
            {
                // Nested row containers are not indexed by this flat cache; stop extending the
                // verified prefix here but keep whatever plain rows were already collected before it.
                complete = false;
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
                complete = false;
                break;
            }

            rows.Add(child);
        }

        _rowNodeCache = rows;
        _rowCacheComplete = complete;
    }

    private void TryExtendRowCache(int row)
    {
        if (_rowNodeCache is null)
        {
            return;
        }

        if (row != _rowNodeCache.Count)
        {
            // 引擎可能為了到達更後面的索引而補建了多列；快取已不再涵蓋全部列，延後重建。
            if (row > _rowNodeCache.Count)
            {
                _rowNodeCache = null;
            }

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
            _rowCacheComplete = false;
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
