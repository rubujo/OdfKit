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
    private readonly Dictionary<string, List<RangeDependency>> _rangeDependents = new(StringComparer.Ordinal);
    private readonly Dictionary<OdfCellAddress, List<RangeDependency>> _rangesByFormula = new();
    private readonly HashSet<OdfCellAddress> _formulaCells = new();
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

    internal IReadOnlyCollection<OdfCellAddress> FormulaCells => _formulaCells;

    /// <summary>
    /// Adds or updates formula dependencies for a cell.
    /// 新增或更新儲存格的公式相依關係。
    /// </summary>
    /// <param name="cell">The cell address. / 儲存格位址。</param>
    /// <param name="formula">The formula string. / 公式字串。</param>
    /// <param name="context">The evaluation context. / 評估內容。</param>
    public void UpdateFormulaDependencies(
        OdfCellAddress cell,
        string formula,
        IEvaluationContext context) =>
        UpdateFormulaDependencies(cell, formula, context, propagateDirty: true);

    internal void UpdateFormulaDependencies(
        OdfCellAddress cell,
        string formula,
        IEvaluationContext context,
        bool propagateDirty)
    {
        RemoveFormulaDependencies(cell);
        _formulaCells.Add(cell);

        var depsSet = new HashSet<OdfCellAddress>();
        if (!string.IsNullOrEmpty(formula))
        {
            try
            {
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
                    var indexedRange = new RangeDependency(
                        cell,
                        sheetName ?? string.Empty,
                        startRow,
                        endRow,
                        startCol,
                        endCol);
                    AddRangeDependency(indexedRange);

                    if (context is OdfDomEvaluationContext domContext)
                    {
                        foreach (OdfCellAddress formulaAddress in domContext.CellFormulas.Keys)
                        {
                            if (formulaAddress != cell &&
                                string.Equals(
                                    formulaAddress.SheetName,
                                    sheetName,
                                    StringComparison.Ordinal) &&
                                formulaAddress.Row >= startRow &&
                                formulaAddress.Row <= endRow &&
                                formulaAddress.Column >= startCol &&
                                formulaAddress.Column <= endCol &&
                                depsSet.Add(formulaAddress))
                            {
                                domContext.Budget?.ChargeDependencyEdge();
                            }
                        }
                    }
                    else
                    {
                        for (int r = startRow; r <= endRow; r++)
                        {
                            for (int c = startCol; c <= endCol; c++)
                            {
                                var depAddress = new OdfCellAddress(r, c, sheetName);
                                if (depAddress != cell && depsSet.Add(depAddress))
                                {
                                    // 公開自訂內容模型維持舊行為；文件評估器使用上方的緊湊公式索引。
                                }
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

        if (propagateDirty)
        {
            MarkDirty(cell);
        }
        else
        {
            _dirtyCells.Add(cell);
        }
    }

    internal void RemoveFormula(OdfCellAddress cell)
    {
        RemoveFormulaDependencies(cell);
        _formulaCells.Remove(cell);
        _dirtyCells.Remove(cell);
        _circularCells.Remove(cell);
    }

    /// <summary>
    /// Recursively marks the specified cell and all affected downstream cells as dirty.
    /// 將指定儲存格及其所有受影響的下游儲存格遞迴標記為 Dirty。
    /// </summary>
    /// <param name="cell">The modified or affected cell address. / 被修改或受影響的儲存格位址。</param>
    public void MarkDirty(OdfCellAddress cell)
    {
        var pending = new Stack<OdfCellAddress>();
        var visited = new HashSet<OdfCellAddress>();
        pending.Push(cell);
        while (pending.Count > 0)
        {
            OdfCellAddress current = pending.Pop();
            if (!visited.Add(current))
                continue;

            if (_formulaCells.Contains(current))
            {
                _dirtyCells.Add(current);
                _circularCells.Remove(current);
            }

            if (_dependents.TryGetValue(current, out var dependents))
            {
                foreach (OdfCellAddress dependent in dependents)
                {
                    pending.Push(dependent);
                }
            }

            string sheetName = current.SheetName ?? string.Empty;
            if (_rangeDependents.TryGetValue(sheetName, out List<RangeDependency>? ranges))
            {
                foreach (RangeDependency range in ranges)
                {
                    if (range.Contains(current))
                    {
                        pending.Push(range.Formula);
                    }
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
        _rangeDependents.Clear();
        _rangesByFormula.Clear();
        _formulaCells.Clear();
        _dirtyCells.Clear();
        _circularCells.Clear();
    }

    internal OdfFormulaDependencyGraph Clone()
    {
        var clone = new OdfFormulaDependencyGraph();
        CopySets(_dependencies, clone._dependencies);
        CopySets(_dependents, clone._dependents);
        foreach (KeyValuePair<string, List<RangeDependency>> pair in _rangeDependents)
        {
            clone._rangeDependents[pair.Key] = [.. pair.Value];
        }
        foreach (KeyValuePair<OdfCellAddress, List<RangeDependency>> pair in _rangesByFormula)
        {
            clone._rangesByFormula[pair.Key] = [.. pair.Value];
        }
        clone._formulaCells.UnionWith(_formulaCells);
        clone._dirtyCells.UnionWith(_dirtyCells);
        clone._circularCells.UnionWith(_circularCells);
        return clone;
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
        var sortedList = new List<OdfCellAddress>();
        foreach (List<OdfCellAddress> level in GetTopologicalDirtyLevels())
        {
            sortedList.AddRange(level);
        }

        foreach (OdfCellAddress cell in _circularCells)
        {
            sortedList.Add(cell);
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

    private void AddRangeDependency(RangeDependency range)
    {
        if (!_rangeDependents.TryGetValue(range.SheetName, out List<RangeDependency>? bySheet))
        {
            bySheet = [];
            _rangeDependents[range.SheetName] = bySheet;
        }
        bySheet.Add(range);

        if (!_rangesByFormula.TryGetValue(range.Formula, out List<RangeDependency>? byFormula))
        {
            byFormula = [];
            _rangesByFormula[range.Formula] = byFormula;
        }
        byFormula.Add(range);
    }

    private void RemoveFormulaDependencies(OdfCellAddress cell)
    {
        if (_dependencies.TryGetValue(cell, out HashSet<OdfCellAddress>? oldDependencies))
        {
            _dependencies.Remove(cell);
            foreach (OdfCellAddress dependency in oldDependencies)
            {
                if (_dependents.TryGetValue(dependency, out HashSet<OdfCellAddress>? dependents))
                {
                    dependents.Remove(cell);
                    if (dependents.Count == 0)
                    {
                        _dependents.Remove(dependency);
                    }
                }
            }
        }

        if (_rangesByFormula.TryGetValue(cell, out List<RangeDependency>? ranges))
        {
            _rangesByFormula.Remove(cell);
            foreach (RangeDependency range in ranges)
            {
                if (_rangeDependents.TryGetValue(range.SheetName, out List<RangeDependency>? bySheet))
                {
                    bySheet.Remove(range);
                    if (bySheet.Count == 0)
                    {
                        _rangeDependents.Remove(range.SheetName);
                    }
                }
            }
        }
    }

    private static void CopySets(
        Dictionary<OdfCellAddress, HashSet<OdfCellAddress>> source,
        Dictionary<OdfCellAddress, HashSet<OdfCellAddress>> destination)
    {
        foreach (KeyValuePair<OdfCellAddress, HashSet<OdfCellAddress>> pair in source)
        {
            destination[pair.Key] = [.. pair.Value];
        }
    }

    private readonly record struct RangeDependency(
        OdfCellAddress Formula,
        string SheetName,
        int StartRow,
        int EndRow,
        int StartColumn,
        int EndColumn)
    {
        internal bool Contains(OdfCellAddress address) =>
            address.Row >= StartRow &&
            address.Row <= EndRow &&
            address.Column >= StartColumn &&
            address.Column <= EndColumn;
    }
}
