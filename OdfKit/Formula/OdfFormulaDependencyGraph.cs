using System;
using System.Collections.Generic;
using OdfKit.Spreadsheet;

namespace OdfKit.Formula;

/// <summary>
/// Implements a formula cell dependency graph that manages calculation dependencies and dirty-state propagation.
/// 實作公式儲存格相依圖，管理儲存格之間的計算相依性與 Dirty 狀態傳播。
/// </summary>
public sealed class OdfFormulaDependencyGraph
{
    private readonly Dictionary<OdfCellAddress, HashSet<OdfCellAddress>> _dependencies = new();
    private readonly Dictionary<OdfCellAddress, HashSet<OdfCellAddress>> _dependents = new();
    private readonly HashSet<OdfCellAddress> _dirtyCells = new();
    private readonly HashSet<OdfCellAddress> _circularCells = new();

    /// <summary>
    /// Gets all currently dirty cells.
    /// 取得所有目前 Dirty 的儲存格。
    /// </summary>
    public IReadOnlyCollection<OdfCellAddress> DirtyCells => _dirtyCells;

    /// <summary>
    /// Gets cells detected as having circular references.
    /// 取得被偵測到具有循環參照的儲存格。
    /// </summary>
    public IReadOnlyCollection<OdfCellAddress> CircularCells => _circularCells;

    /// <summary>
    /// Adds or updates formula dependencies for a cell.
    /// 新增或更新儲存格的公式相依關係。
    /// </summary>
    /// <param name="cell">The cell address. / 儲存格位址。</param>
    /// <param name="formula">The formula string. / 公式字串。</param>
    /// <param name="context">The evaluation context. / 評估內容。</param>
    public void UpdateFormulaDependencies(OdfCellAddress cell, string formula, IEvaluationContext context)
    {
        // 1. 清除舊相依關係
        if (_dependencies.TryGetValue(cell, out var oldDeps))
        {
            foreach (var oldDep in oldDeps)
            {
                if (_dependents.TryGetValue(oldDep, out var dependents))
                {
                    dependents.Remove(cell);
                }
            }
            _dependencies.Remove(cell);
        }

        // 2. 解析公式並擷取所有相依單元格與範圍
        var depsSet = new HashSet<OdfCellAddress>();
        if (!string.IsNullOrEmpty(formula))
        {
            try
            {
                // 去除前綴
                string cleanFormula = formula;
                if (cleanFormula.StartsWith("oooc:=", StringComparison.OrdinalIgnoreCase) ||
                    cleanFormula.StartsWith("of:=", StringComparison.OrdinalIgnoreCase))
                {
                    cleanFormula = OdfFormulaTranslator.OdfToExcelFormula(cleanFormula);
                }
                cleanFormula = FormulaPrefixNormalizer.RemovePrefix(cleanFormula);

                var parser = new FormulaParser(cleanFormula);
                var ast = parser.Parse();
                var ranges = ast.GetRanges(context);

                foreach (var range in ranges)
                {
                    int startRow = Math.Min(range.StartAddress.Row, range.EndAddress.Row);
                    int endRow = Math.Max(range.StartAddress.Row, range.EndAddress.Row);
                    int startCol = Math.Min(range.StartAddress.Column, range.EndAddress.Column);
                    int endCol = Math.Max(range.StartAddress.Column, range.EndAddress.Column);
                    string? sheetName = range.StartAddress.SheetName ?? cell.SheetName;

                    for (int r = startRow; r <= endRow; r++)
                    {
                        for (int c = startCol; c <= endCol; c++)
                        {
                            var depAddress = new OdfCellAddress(r, c, sheetName);
                            if (depAddress != cell) // 避免自相依
                            {
                                depsSet.Add(depAddress);
                            }
                        }
                    }
                }
            }
            catch
            {
                // 解析失敗時將無相依，此為 Lax 容錯模式之預期行為
            }
        }

        // 3. 儲存新相依關係與反向相依鏈
        if (depsSet.Count > 0)
        {
            _dependencies[cell] = depsSet;
            foreach (var depAddress in depsSet)
            {
                if (!_dependents.TryGetValue(depAddress, out var dependents))
                {
                    dependents = new HashSet<OdfCellAddress>();
                    _dependents[depAddress] = dependents;
                }
                dependents.Add(cell);
            }
        }

        // 4. 將本單元格標記為 Dirty
        MarkDirty(cell);
    }

    /// <summary>
    /// Recursively marks the specified cell and all affected downstream cells as dirty.
    /// 將指定儲存格及其所有受影響的下游儲存格遞迴標記為 Dirty。
    /// </summary>
    /// <param name="cell">The modified or affected cell address. / 被修改或受影響的儲存格位址。</param>
    public void MarkDirty(OdfCellAddress cell)
    {
        if (_dirtyCells.Add(cell))
        {
            // 清除循環參照標籤，重新計算時會重新評估
            _circularCells.Remove(cell);

            // 遞迴將所有依賴於此單元格的下游節點標記為 Dirty
            if (_dependents.TryGetValue(cell, out var dependents))
            {
                foreach (var dependent in dependents)
                {
                    MarkDirty(dependent);
                }
            }
        }
    }

    /// <summary>
    /// Clears the dirty marker for the specified cell.
    /// 清除指定儲存格的 Dirty 標記。
    /// </summary>
    /// <param name="cell">The cell address. / 儲存格位址。</param>
    public void ClearDirty(OdfCellAddress cell)
    {
        _dirtyCells.Remove(cell);
    }

    /// <summary>
    /// Determines whether the specified cell is dirty.
    /// 判斷指定儲存格是否為 Dirty 狀態。
    /// </summary>
    /// <param name="cell">The cell address. / 儲存格位址。</param>
    /// <returns>True when the cell is dirty; otherwise, false. / 若儲存格為 Dirty 狀態則為 true，否則為 false。</returns>
    public bool IsDirty(OdfCellAddress cell) => _dirtyCells.Contains(cell);

    /// <summary>
    /// Clears all structures in the dependency graph.
    /// 清除整個相依圖的所有結構。
    /// </summary>
    public void Clear()
    {
        _dependencies.Clear();
        _dependents.Clear();
        _dirtyCells.Clear();
        _circularCells.Clear();
    }

    /// <summary>
    /// Topologically sorts all currently dirty cells and returns their calculation order.
    /// 對所有目前處於 Dirty 狀態的儲存格進行拓撲排序，並傳回其計算順序。
    /// </summary>
    /// <remarks>
    /// When circular dependencies are detected, the related cells are added to <see cref="CircularCells"/>.
    /// 若偵測到循環相依，會將相關儲存格加入 <see cref="CircularCells"/>。
    /// </remarks>
    /// <returns>The sorted cell calculation order. / 已排序的儲存格計算順序清單。</returns>
    public List<OdfCellAddress> GetTopologicallySortedDirtyCells()
    {
        var visited = new HashSet<OdfCellAddress>();
        var tempStack = new HashSet<OdfCellAddress>();
        var sortedList = new List<OdfCellAddress>();

        foreach (var cell in _dirtyCells)
        {
            if (!visited.Contains(cell))
            {
                Visit(cell, visited, tempStack, sortedList);
            }
        }

        return sortedList;
    }

    internal List<List<OdfCellAddress>> GetTopologicalDirtyLevels()
    {
        _circularCells.Clear();

        var indegrees = new Dictionary<OdfCellAddress, int>();
        foreach (OdfCellAddress cell in _dirtyCells)
        {
            int indegree = 0;
            if (_dependencies.TryGetValue(cell, out HashSet<OdfCellAddress>? dependencies))
            {
                foreach (OdfCellAddress dependency in dependencies)
                {
                    if (_dirtyCells.Contains(dependency))
                    {
                        indegree++;
                    }
                }
            }

            indegrees[cell] = indegree;
        }

        var ready = new List<OdfCellAddress>();
        foreach (KeyValuePair<OdfCellAddress, int> pair in indegrees)
        {
            if (pair.Value == 0)
            {
                ready.Add(pair.Key);
            }
        }

        var levels = new List<List<OdfCellAddress>>();
        int processed = 0;
        while (ready.Count > 0)
        {
            var level = new List<OdfCellAddress>(ready);
            levels.Add(level);
            processed += level.Count;
            ready.Clear();

            foreach (OdfCellAddress cell in level)
            {
                if (!_dependents.TryGetValue(cell, out HashSet<OdfCellAddress>? dependents))
                {
                    continue;
                }

                foreach (OdfCellAddress dependent in dependents)
                {
                    if (!indegrees.TryGetValue(dependent, out int indegree))
                    {
                        continue;
                    }

                    indegree--;
                    indegrees[dependent] = indegree;
                    if (indegree == 0)
                    {
                        ready.Add(dependent);
                    }
                }
            }
        }

        if (processed != indegrees.Count)
        {
            foreach (KeyValuePair<OdfCellAddress, int> pair in indegrees)
            {
                if (pair.Value > 0)
                {
                    _circularCells.Add(pair.Key);
                }
            }
        }

        return levels;
    }

    private void Visit(
        OdfCellAddress node,
        HashSet<OdfCellAddress> visited,
        HashSet<OdfCellAddress> tempStack,
        List<OdfCellAddress> sortedList)
    {
        if (tempStack.Contains(node))
        {
            // 偵測到循環相依 (Cycle Detected)
            _circularCells.Add(node);
            return;
        }

        if (!visited.Contains(node))
        {
            tempStack.Add(node);

            if (_dependencies.TryGetValue(node, out var deps))
            {
                foreach (var dep in deps)
                {
                    // 僅需排序與計算目前也為 Dirty 的相依單元格
                    if (_dirtyCells.Contains(dep))
                    {
                        Visit(dep, visited, tempStack, sortedList);
                    }
                }
            }

            tempStack.Remove(node);
            visited.Add(node);
            sortedList.Add(node);
        }
    }
}
