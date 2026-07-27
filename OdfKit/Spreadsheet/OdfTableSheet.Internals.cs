using System.Collections.Generic;
using OdfKit.DOM;

namespace OdfKit.Spreadsheet;
/// <summary>
/// Provides the OdfTableSheet API.
/// 提供 OdfTableSheet API。
/// </summary>

public partial class OdfTableSheet
{
    #region Internals

    internal List<OdfNode> GetRowsList()
        => OdfTableSheetDomAccessEngine.GetRowsList(TableNode);

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Instance collaborator contract; callers intentionally dispatch through the owning document.")]
    internal List<OdfNode> GetCellsInRow(OdfNode rowNode)
        => OdfTableSheetDomAccessEngine.GetCellsInRow(rowNode);

    internal OdfNode GetOrCreateRowNodeInternal(int row, bool forWrite)
        => OdfTableSheetDomAccessEngine.GetOrCreateRowNode(TableNode, row, forWrite);

    #endregion
}
