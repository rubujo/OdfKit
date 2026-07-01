using System.Collections.Generic;
using OdfKit.DOM;

namespace OdfKit.Spreadsheet;

/// <summary>
/// 封裝工作表 DOM 變更所需之內部狀態的協作存取器。
/// </summary>
internal readonly struct OdfTableSheetMutationContext
{
    private readonly OdfTableSheet _sheet;

    internal OdfTableSheetMutationContext(OdfTableSheet sheet) => _sheet = sheet;

    internal OdfNode TableNode => _sheet.TableNode;

    internal SpreadsheetDocument Document => _sheet.Document;

    internal string SheetName => _sheet.Name;

    internal OdfNode GetOrCreateRow(int row, bool forWrite) =>
        OdfTableSheetDomAccessEngine.GetOrCreateRowNode(TableNode, row, forWrite);

    internal OdfNode GetOrCreateColumn(int col) =>
        OdfTableSheetDomAccessEngine.GetOrCreateColumnNode(TableNode, col);

    internal List<OdfNode> GetRowsList() =>
        OdfTableSheetDomAccessEngine.GetRowsList(TableNode);

    internal List<OdfNode> GetCellsInRow(OdfNode rowNode) =>
        OdfTableSheetDomAccessEngine.GetCellsInRow(rowNode);

    internal OdfCell GetCell(int row, int col) => _sheet.GetCell(row, col);
}

public partial class OdfTableSheet
{
    /// <summary>
    /// 供工作表 DOM 變更引擎使用的內部協作存取器。
    /// </summary>
    /// <remarks>
    /// Row/column structural mutation engines (print title rows, row/column grouping, layout, etc.)
    /// operate on <see cref="TableNode"/> directly and can move row or column nodes into or out of
    /// container elements (e.g. <c>table:header-rows</c>) in ways the row/cell access cache does not
    /// track. Accessing this context therefore conservatively invalidates that cache; the hot
    /// <see cref="GetCell(int, int)"/> path never touches this property, so this has no effect on it.
    /// 列／欄結構性變更引擎（列印標題列、列／欄群組、版面配置等）會直接操作 <see cref="TableNode"/>，
    /// 可能將列或欄節點搬入或搬出容器元素（例如 <c>table:header-rows</c>），這類變化不在
    /// 列／儲存格存取快取的追蹤範圍內。因此存取此內容模型時會保守地讓該快取失效；熱路徑
    /// <see cref="GetCell(int, int)"/> 從不存取此屬性，故不受影響。
    /// </remarks>
    internal OdfTableSheetMutationContext MutationContext
    {
        get
        {
            InvalidateAccessCache();
            return new OdfTableSheetMutationContext(this);
        }
    }
}
