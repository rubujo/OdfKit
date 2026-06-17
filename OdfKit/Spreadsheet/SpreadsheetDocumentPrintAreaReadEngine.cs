using System.Collections.Generic;

namespace OdfKit.Spreadsheet;

/// <summary>
/// 試算表列印範圍讀取引擎（內部協作者）。
/// </summary>
internal static class SpreadsheetDocumentPrintAreaReadEngine
{
    internal static IReadOnlyList<OdfSheetPrintAreaInfo> GetPrintAreas(SpreadsheetDocument document)
    {
        List<OdfSheetPrintAreaInfo> areas = [];

        foreach (OdfTableSheet sheet in document.Worksheets)
        {
            OdfCellRange? range = sheet.GetPrintArea();
            if (range is null)
                continue;

            areas.Add(new OdfSheetPrintAreaInfo(sheet.Name, range.Value.ToOdfString()));
        }

        return areas.AsReadOnly();
    }
}
