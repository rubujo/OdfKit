using System.Collections.Generic;
using OdfKit.Spreadsheet;

namespace OdfKit.Formula;

/// <summary>
/// 提供公式參照運算所需的具名範圍解析服務。
/// </summary>
internal interface IOdfFormulaReferenceContext
{
    bool TryGetNamedRanges(
        string name,
        out IReadOnlyList<OdfCellRange> ranges);
}
