using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Spreadsheet;

namespace OdfKit.Conversion;

/// <summary>
/// 將 SpreadsheetDocument 轉換為 XLSX 格式的轉換器。
/// </summary>
public static class OdfToXlsxConverter
{
    /// <summary>
    /// 將 ODS 工作簿轉換並寫入 XLSX 資料流。
    /// </summary>
    /// <param name="odsWorkbook">來源 ODS 工作簿。</param>
    /// <param name="xlsxStream">要寫入 XLSX 的目標資料流。</param>
    /// <exception cref="ArgumentNullException">當任一必要參數為 null 時引發。</exception>
    public static void Convert(SpreadsheetDocument odsWorkbook, Stream xlsxStream)
    {
        if (odsWorkbook is null) throw new ArgumentNullException(nameof(odsWorkbook));
        if (xlsxStream is null) throw new ArgumentNullException(nameof(xlsxStream));

        using var xlWorkbook = new XLWorkbook();

        foreach (var odsSheet in odsWorkbook.Worksheets)
        {
            var xlSheet = xlWorkbook.Worksheets.Add(odsSheet.Name);
            CopySheetData(odsSheet, xlSheet);
        }

        xlWorkbook.SaveAs(xlsxStream);
    }

    private static void CopySheetData(OdfTableSheet odsSheet, IXLWorksheet xlSheet)
    {
        var cellValues = ScanCellValues(odsSheet);
        foreach (var pair in cellValues)
        {
            var val = pair.Value;
            if (val is not null && !(val is string s && s.Length == 0))
            {
                SetXlCell(xlSheet.Cell(pair.Key.Row + 1, pair.Key.Col + 1), val);
            }
        }
    }

    private sealed class CellFormula
    {
        public string ExcelFormula { get; }
        public CellFormula(string f) => ExcelFormula = f;
    }

    private static void SetXlCell(IXLCell cell, object? value)
    {
        switch (value)
        {
            case CellFormula formula: cell.SetFormulaA1(formula.ExcelFormula); break;
            case double d: cell.Value = d; break;
            case int i: cell.Value = i; break;
            case bool b: cell.Value = b; break;
            case DateTime dt: cell.Value = dt; break;
            case string str: cell.Value = str; break;
            default: cell.Value = value?.ToString() ?? string.Empty; break;
        }
    }

    private static Dictionary<(int Row, int Col), object> ScanCellValues(OdfTableSheet sheet)
    {
        const int MaxRepeat = 1_048_576; // Excel 最大列數，防止 OOM DoS
        var values = new Dictionary<(int Row, int Col), object>();
        int currentRowIndex = 0;
        foreach (var rowChild in sheet.TableNode.Children)
        {
            if (rowChild.LocalName == "table-row" && rowChild.NamespaceUri == OdfNamespaces.Table)
            {
                int rowRepeatedCount = 1;
                string? rowRepStr = rowChild.GetAttribute("number-rows-repeated", OdfNamespaces.Table);
                if (!string.IsNullOrEmpty(rowRepStr) && int.TryParse(rowRepStr, out int rrc) && rrc > 0)
                {
                    rowRepeatedCount = Math.Min(rrc, MaxRepeat);
                }

                var rowCells = new List<(int Col, object Val)>();
                int currentColIndex = 0;
                foreach (var cellChild in rowChild.Children)
                {
                    if ((cellChild.LocalName == "table-cell" || cellChild.LocalName == "covered-table-cell") && cellChild.NamespaceUri == OdfNamespaces.Table)
                    {
                        int colRepeatedCount = 1;
                        string? colRepStr = cellChild.GetAttribute("number-columns-repeated", OdfNamespaces.Table);
                        if (!string.IsNullOrEmpty(colRepStr) && int.TryParse(colRepStr, out int crc) && crc > 0)
                        {
                            colRepeatedCount = Math.Min(crc, MaxRepeat);
                        }

                        object? cellValue = GetCellValueFromNode(cellChild);
                        if (cellValue is not null)
                        {
                            for (int i = 0; i < colRepeatedCount; i++)
                            {
                                rowCells.Add((currentColIndex + i, cellValue));
                            }
                        }

                        currentColIndex += colRepeatedCount;
                    }
                }

                for (int r = 0; r < rowRepeatedCount; r++)
                {
                    int actualRow = currentRowIndex + r;
                    foreach (var cell in rowCells)
                    {
                        values[(actualRow, cell.Col)] = cell.Val;
                    }
                }

                currentRowIndex += rowRepeatedCount;
            }
        }
        return values;
    }

    private static object? GetCellValueFromNode(OdfNode node)
    {
        // 公式優先：若有 table:formula 屬性，翻譯後回傳
        string? rawFormula = node.GetAttribute("formula", OdfNamespaces.Table);
        if (!string.IsNullOrEmpty(rawFormula))
            return new CellFormula(TranslateFormula(rawFormula!));

        string valueType = node.GetAttribute("value-type", OdfNamespaces.Office) ?? string.Empty;
        string value = node.GetAttribute("value", OdfNamespaces.Office) ?? string.Empty;

        string textContent = GetTextContent(node);

        switch (valueType)
        {
            case "float":
                if (double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double number))
                    return number;
                return null;
            case "boolean":
                string? boolVal = node.GetAttribute("boolean-value", OdfNamespaces.Office);
                if (bool.TryParse(boolVal, out bool flag))
                    return flag;
                return null;
            case "date":
                return node.GetAttribute("date-value", OdfNamespaces.Office);
            case "string":
                return textContent;
            default:
                return string.IsNullOrEmpty(textContent) ? null : textContent;
        }
    }

    private static readonly Regex SheetRefRegex = new Regex(
        @"([A-Za-z_一-龥][A-Za-z0-9_ 一-龥]*)\.(\$?[A-Z]+\$?[0-9]+(:\$?[A-Z]+\$?[0-9]+)?)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// 將 OpenFormula 格式公式（of:= 或 oooc:= 前綴）翻譯為 Excel A1 格式公式。
    /// </summary>
    public static string TranslateFormula(string odtFormula)
    {
        string f = odtFormula;
        // 去除命名空間前綴
        if (f.StartsWith("of:=", StringComparison.Ordinal)) f = f.Substring(3); // 保留 "="
        else if (f.StartsWith("of:", StringComparison.Ordinal)) f = "=" + f.Substring(3);
        else if (f.StartsWith("oooc:=", StringComparison.Ordinal)) f = f.Substring(5);
        else if (f.StartsWith("oooc:", StringComparison.Ordinal)) f = "=" + f.Substring(5);

        if (!f.StartsWith("=", StringComparison.Ordinal)) f = "=" + f;

        // 轉換工作表參照：Sheet.A1 → Sheet!A1
        f = SheetRefRegex.Replace(f, "$1!$2");

        return f;
    }

    private static string GetTextContent(OdfNode node)
    {
        foreach (var child in node.Children)
        {
            if (child.LocalName == "p" && child.NamespaceUri == OdfNamespaces.Text)
            {
                return child.TextContent;
            }
        }
        return node.TextContent;
    }
}
