using OdfKit.DOM;

namespace OdfKit.Spreadsheet;

public partial class OdfTableSheet
{
    #region Cell & Column Access

    /// <summary>
    /// 嘗試以唯讀方式取得指定列與欄索引的儲存格 XML 節點，不修改 DOM 結構。
    /// </summary>
    /// <param name="row">以 0 為基準的列索引</param>
    /// <param name="col">以 0 為基準的欄索引</param>
    /// <returns>儲存格 XML 節點，若不存在則為 null</returns>
    internal OdfNode? TryGetCellNode(int row, int col)
        => OdfTableSheetDomAccessEngine.TryGetCellNode(TableNode, row, col);

    private OdfNode GetOrCreateCellNode(int row, int col)
        => OdfTableSheetDomAccessEngine.GetOrCreateCellNode(TableNode, row, col);

    private void ReplaceCellNode(int row, int col, OdfNode newCellNode)
        => OdfTableSheetDomAccessEngine.ReplaceCellNode(TableNode, row, col, newCellNode);

    internal OdfNode GetOrCreateColumnNode(int col)
        => OdfTableSheetDomAccessEngine.GetOrCreateColumnNode(TableNode, col);

    private OdfNode GetOrCreateRowNode(int row)
        => OdfTableSheetDomAccessEngine.GetOrCreateRowNode(TableNode, row, forWrite: true);

    #endregion
}
