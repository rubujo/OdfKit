using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Spreadsheet;

public partial class OdfTableSheet
{
    #region Cell & Column Access

    // Fast-path cache for the common "build a fresh sheet by walking rows/columns in order" pattern,
    // where OdfTableSheetDomAccessEngine's per-call full-table rescan otherwise dominates cost for
    // large sheets. The cache only ever accelerates lookups that are provably equivalent to the
    // uncompressed (no number-rows/columns-repeated) engine result; any row-container element or
    // repeat-compressed node encountered disables the cache for the rest of this instance's lifetime,
    // after which every call falls back to the original, always-correct engine path unchanged.
    // 「由程式逐列逐欄建立新工作表」這個常見情境的快取加速層——OdfTableSheetDomAccessEngine 每次呼叫
    // 都重新掃描整表，對大型工作表而言是主要成本來源。此快取只在結果與未壓縮
    // （無 number-rows/columns-repeated）情境下的引擎結果可證明一致時才加速；一旦遇到任何列容器元素
    // 或壓縮節點，即永久停用此執行個體的快取，之後所有呼叫回退至原始、永遠正確的引擎路徑，行為不變。
    private List<OdfNode>? _rowNodeCache;
    private bool _rowCacheDisabled;
    private readonly Dictionary<OdfNode, RowCellCache> _cellNodeCacheByRow = [];

    private sealed class RowCellCache
    {
        internal readonly List<OdfNode> Cells = [];
        internal bool Disabled;
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
        _rowCacheDisabled = false;
        _cellNodeCacheByRow.Clear();
    }

    private bool TryGetCachedRowNode(int row, out OdfNode rowNode)
    {
        rowNode = null!;
        if (_rowCacheDisabled || row < 0)
        {
            return false;
        }

        if (_rowNodeCache is null)
        {
            if (!TryBuildRowCache())
            {
                return false;
            }
        }

        if (row >= _rowNodeCache!.Count)
        {
            return false;
        }

        rowNode = _rowNodeCache[row];
        return true;
    }

    private bool TryBuildRowCache()
    {
        List<OdfNode> rows = [];
        foreach (OdfNode child in TableNode.Children)
        {
            if (OdfTableSheetDomAccessEngine.RowContainerNames.Contains(child.LocalName) && child.NamespaceUri == OdfNamespaces.Table)
            {
                _rowCacheDisabled = true;
                return false;
            }

            if (child.LocalName != "table-row" || child.NamespaceUri != OdfNamespaces.Table)
            {
                continue;
            }

            if (OdfTableSheetRepeatSplitEngine.GetRepeatCount(child, "number-rows-repeated") > 1)
            {
                _rowCacheDisabled = true;
                return false;
            }

            rows.Add(child);
        }

        _rowNodeCache = rows;
        return true;
    }

    private void TryExtendRowCache(int row)
    {
        if (_rowCacheDisabled || _rowNodeCache is null || row != _rowNodeCache.Count)
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
        if (_rowCacheDisabled || _rowNodeCache is null)
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
            _rowCacheDisabled = true;
            _rowNodeCache = null;
            return;
        }

        _rowNodeCache.Add(rowNode);
    }

    private bool TryGetCachedCellNode(OdfNode rowNode, int col, out OdfNode cellNode)
    {
        cellNode = null!;
        if (col < 0 || !_cellNodeCacheByRow.TryGetValue(rowNode, out RowCellCache? cache))
        {
            if (!TryBuildCellCache(rowNode, out cache) || col < 0)
            {
                return false;
            }
        }

        if (cache.Disabled || col >= cache.Cells.Count)
        {
            return false;
        }

        cellNode = cache.Cells[col];
        return true;
    }

    private bool TryBuildCellCache(OdfNode rowNode, out RowCellCache cache)
    {
        cache = new RowCellCache();
        foreach (OdfNode child in rowNode.Children)
        {
            if ((child.LocalName != "table-cell" && child.LocalName != "covered-table-cell") || child.NamespaceUri != OdfNamespaces.Table)
            {
                continue;
            }

            if (OdfTableSheetRepeatSplitEngine.GetRepeatCount(child, "number-columns-repeated") > 1)
            {
                cache.Disabled = true;
                _cellNodeCacheByRow[rowNode] = cache;
                return false;
            }

            cache.Cells.Add(child);
        }

        _cellNodeCacheByRow[rowNode] = cache;
        return true;
    }

    private void TryExtendCellCache(int row, int col, OdfNode cellNode)
    {
        if (!TryGetCachedRowNode(row, out OdfNode rowNode) ||
            !_cellNodeCacheByRow.TryGetValue(rowNode, out RowCellCache? cache) ||
            cache.Disabled)
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
