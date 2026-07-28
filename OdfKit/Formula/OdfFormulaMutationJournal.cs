using System.Collections.Generic;
using OdfKit.DOM;
using OdfKit.Spreadsheet;

namespace OdfKit.Formula;

/// <summary>
/// 記錄高階試算表 API 產生的公式相關儲存格變更。
/// </summary>
internal sealed class OdfFormulaMutationJournal
{
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
    }

    internal IReadOnlyList<OdfFormulaCellMutation> GetChangesSince(long version)
    {
        if (version >= _version)
        {
            return [];
        }

        int start = _mutations.Count;
        while (start > 0 && _mutations[start - 1].Version > version)
        {
            start--;
        }

        return _mutations.GetRange(start, _mutations.Count - start);
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
