using System.Collections.Generic;
using OdfKit.DOM;
using OdfKit.Spreadsheet;

namespace OdfKit.Formula;

/// <summary>
/// 記錄高階試算表 API 產生的公式相關儲存格變更。
/// </summary>
internal sealed class OdfFormulaMutationJournal
{
    private const int MaximumRetainedMutations = 65_536;
    private readonly List<OdfFormulaCellMutation> _mutations = [];
    private long _version;

    internal long Version => _version;

    internal void Record(
        OdfCellAddress address,
        OdfNode node,
        bool formulaChanged)
    {
        _version++;
        _mutations.Add(new OdfFormulaCellMutation(
            _version,
            address,
            node,
            formulaChanged));
        if (_mutations.Count > MaximumRetainedMutations)
        {
            _mutations.RemoveRange(0, MaximumRetainedMutations / 4);
        }
    }

    internal bool TryGetChangesSince(
        long version,
        out IReadOnlyList<OdfFormulaCellMutation> mutations)
    {
        if (version >= _version)
        {
            mutations = [];
            return true;
        }

        if (_mutations.Count == 0 ||
            version < _mutations[0].Version - 1)
        {
            mutations = [];
            return false;
        }

        int start = _mutations.Count;
        while (start > 0 && _mutations[start - 1].Version > version)
        {
            start--;
        }

        mutations = _mutations.GetRange(start, _mutations.Count - start);
        return true;
    }
}

/// <summary>
/// 表示單一公式相關儲存格變更。
/// </summary>
internal readonly record struct OdfFormulaCellMutation(
    long Version,
    OdfCellAddress Address,
    OdfNode Node,
    bool FormulaChanged);
