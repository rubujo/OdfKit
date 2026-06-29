using OdfKit.DOM;

namespace OdfKit.Spreadsheet;

public partial class OdfTableSheet
{
    #region Cell & Column Access

    /// <summary>
    /// Attempts to get the cell XML node at the specified row and column indexes without modifying the DOM.
    /// 嘗試以唯讀方式取得指定列與欄索引的儲存格 XML 節點，不修改 DOM 結構。
    /// </summary>
    /// <param name="row">The zero-based row index. / 以 0 為基準的列索引。</param>
    /// <param name="col">The zero-based column index. / 以 0 為基準的欄索引。</param>
    /// <returns>The cell XML node, or <see langword="null"/> when it does not exist. / 儲存格 XML 節點；不存在時為 <see langword="null"/>。</returns>
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
