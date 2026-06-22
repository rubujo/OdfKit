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
    internal OdfTableSheetMutationContext MutationContext => new(this);
}
