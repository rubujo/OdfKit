using System;
using System.IO;
using ClosedXML.Excel;
using OdfKit.Spreadsheet;

namespace OdfKit.Conversion;

/// <summary>
/// 將 XLSX 格式轉換為 SpreadsheetDocument 的轉換器。
/// </summary>
public static class XlsxToOdfConverter
{
    /// <summary>
    /// 從 XLSX 資料流讀取並建立對應的 SpreadsheetDocument。
    /// </summary>
    /// <param name="xlsxStream">XLSX 來源資料流。</param>
    /// <returns>轉換後的 SpreadsheetDocument 執行個體。</returns>
    /// <exception cref="ArgumentNullException">當 xlsxStream 為 null 時引發。</exception>
    public static SpreadsheetDocument Convert(Stream xlsxStream)
    {
        if (xlsxStream is null) throw new ArgumentNullException(nameof(xlsxStream));

        var odsWorkbook = SpreadsheetDocument.Create();

        using var xlWorkbook = new XLWorkbook(xlsxStream);
        bool firstSheet = true;

        foreach (var xlSheet in xlWorkbook.Worksheets)
        {
            OdfTableSheet odsSheet;
            if (firstSheet)
            {
                odsSheet = odsWorkbook.Worksheets.Count > 0
                    ? odsWorkbook.Worksheets[0]
                    : odsWorkbook.Worksheets.Add(xlSheet.Name);
                if (odsSheet.Name != xlSheet.Name)
                    odsSheet.Name = xlSheet.Name;
                firstSheet = false;
            }
            else
            {
                odsSheet = odsWorkbook.Worksheets.Add(xlSheet.Name);
            }

            CopySheetData(xlSheet, odsSheet);
        }

        return odsWorkbook;
    }

    private static void CopySheetData(IXLWorksheet xlSheet, OdfTableSheet odsSheet)
    {
        var usedRange = xlSheet.RangeUsed();
        if (usedRange is null) return;

        foreach (var xlRow in usedRange.Rows())
        {
            int r = xlRow.RowNumber() - 1;
            foreach (var xlCell in xlRow.Cells())
            {
                int c = xlCell.Address.ColumnNumber - 1;
                object? val = xlCell.Value.IsBlank ? null :
                    xlCell.Value.IsNumber ? xlCell.Value.GetNumber() :
                    xlCell.Value.IsBoolean ? xlCell.Value.GetBoolean() :
                    xlCell.Value.IsDateTime ? xlCell.Value.GetDateTime() :
                    (object?)xlCell.Value.GetText();
                if (val is not null)
                    odsSheet.Cells[r, c].CellValue = val;
            }
        }
    }
}
