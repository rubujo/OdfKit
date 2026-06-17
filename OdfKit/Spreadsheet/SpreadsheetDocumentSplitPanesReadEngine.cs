using System.Collections.Generic;

namespace OdfKit.Spreadsheet;

/// <summary>
/// 試算表分割窗格讀取引擎（內部協作者）。
/// </summary>
internal static class SpreadsheetDocumentSplitPanesReadEngine
{
    internal static IReadOnlyList<OdfSheetSplitPanesInfo> GetSplitPanes(SpreadsheetDocument document)
    {
        List<OdfSheetSplitPanesInfo> splitPanes = [];

        foreach (OdfTableSheet sheet in document.Worksheets)
        {
            OdfSplitPanes panes = sheet.ViewSplitPanes;
            if (!panes.IsSplit)
                continue;

            splitPanes.Add(new OdfSheetSplitPanesInfo(sheet.Name, panes));
        }

        return splitPanes.AsReadOnly();
    }
}
