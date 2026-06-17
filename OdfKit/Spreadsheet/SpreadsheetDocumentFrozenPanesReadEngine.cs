using System.Collections.Generic;

namespace OdfKit.Spreadsheet;

/// <summary>
/// 試算表凍結窗格讀取引擎（內部協作者）。
/// </summary>
internal static class SpreadsheetDocumentFrozenPanesReadEngine
{
    internal static IReadOnlyList<OdfSheetFrozenPanesInfo> GetFrozenPanes(SpreadsheetDocument document)
    {
        List<OdfSheetFrozenPanesInfo> frozenPanes = [];

        foreach (OdfTableSheet sheet in document.Worksheets)
        {
            OdfFrozenPanes panes = sheet.FrozenPanes;
            if (!panes.IsFrozen)
                continue;

            frozenPanes.Add(new OdfSheetFrozenPanesInfo(sheet.Name, panes));
        }

        return frozenPanes.AsReadOnly();
    }
}
