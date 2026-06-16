using System;
using System.IO;
using System.Xml.Linq;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Packaging;
using OdfKit.Chart;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Spreadsheet;
using S = DocumentFormat.OpenXml.Spreadsheet;

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
    public static OdfKit.Spreadsheet.SpreadsheetDocument Convert(Stream xlsxStream)
    {
        if (xlsxStream is null) throw new ArgumentNullException(nameof(xlsxStream));

        var odsWorkbook = OdfKit.Spreadsheet.SpreadsheetDocument.Create();
        using var workbookStream = new MemoryStream();
        xlsxStream.CopyTo(workbookStream);
        workbookStream.Position = 0;

        using var xlWorkbook = new XLWorkbook(workbookStream);
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
            CopyDataValidations(xlSheet, odsSheet);
            CopyConditionalFormats(xlSheet, odsSheet);
        }

        workbookStream.Position = 0;
        CopyCharts(workbookStream, odsWorkbook);

        return odsWorkbook;
    }

    private static void CopyCharts(Stream xlsxStream, OdfKit.Spreadsheet.SpreadsheetDocument odsWorkbook)
    {
        try
        {
            using var spreadsheet = DocumentFormat.OpenXml.Packaging.SpreadsheetDocument.Open(xlsxStream, false);
            WorkbookPart? workbookPart = spreadsheet.WorkbookPart;
            if (workbookPart?.Workbook is null)
            {
                return;
            }

            S.Sheets? sheets = workbookPart.Workbook.Sheets;
            if (sheets is null)
            {
                return;
            }

            foreach (S.Sheet sheet in sheets.Elements<S.Sheet>())
            {
                string? sheetName = sheet.Name?.Value;
                string? sheetRelationshipId = sheet.Id?.Value;
                if (string.IsNullOrEmpty(sheetName) || string.IsNullOrEmpty(sheetRelationshipId))
                {
                    continue;
                }

                if (workbookPart.GetPartById(sheetRelationshipId!) is not WorksheetPart worksheetPart ||
                    worksheetPart.Worksheet is null)
                {
                    continue;
                }

                OdfTableSheet? odsSheet = odsWorkbook.Worksheets.Find(sheetName!);
                if (odsSheet is null)
                {
                    continue;
                }

                S.Drawing? drawing = worksheetPart.Worksheet.Elements<S.Drawing>().FirstOrDefault();
                if (drawing?.Id?.Value is null ||
                    worksheetPart.GetPartById(drawing.Id.Value) is not DrawingsPart drawingsPart)
                {
                    continue;
                }

                foreach (ChartPart chartPart in drawingsPart.ChartParts)
                {
                    ChartSpec? spec = ReadChartSpec(chartPart, sheetName!);
                    if (spec is null)
                    {
                        continue;
                    }

                    odsWorkbook.AddChart(
                        odsSheet.Name,
                        new OdfCellAddress(4, 0, odsSheet.Name),
                        new OdfChartDefinition
                        {
                            ChartType = spec.ChartType,
                            Title = spec.Title,
                            DataRange = spec.DataRange
                        });
                }
            }
        }
        catch (Exception)
        {
            // 保留工作表資料轉換；圖表解析失敗時不應中斷整份 XLSX 匯入。
        }
    }

    private static ChartSpec? ReadChartSpec(ChartPart chartPart, string sheetName)
    {
        using Stream stream = chartPart.GetStream(FileMode.Open, FileAccess.Read);
        XDocument xml = XDocument.Load(stream);
        XNamespace chartNs = "http://schemas.openxmlformats.org/drawingml/2006/chart";
        XNamespace drawingNs = "http://schemas.openxmlformats.org/drawingml/2006/main";

        OdfChartType chartType;
        if (xml.Descendants(chartNs + "lineChart").Any())
        {
            chartType = OdfChartType.Line;
        }
        else if (xml.Descendants(chartNs + "pieChart").Any())
        {
            chartType = OdfChartType.Pie;
        }
        else if (xml.Descendants(chartNs + "barChart").Any())
        {
            chartType = OdfChartType.Bar;
        }
        else
        {
            return null;
        }

        // 收集所有系列公式（SeriesText、CategoryRange、Values），計算聯集邊界框以還原完整資料範圍
        var allRanges = xml.Descendants(chartNs + "f")
            .Select(e => e.Value)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => { OdfCellRange.TryParse(v!, out var r); return (OdfCellRange?)r; })
            .Where(r => r.HasValue)
            .Select(r => r!.Value)
            .ToList();

        if (allRanges.Count == 0)
            return null;

        string? commonSheet = allRanges.Select(r => r.StartAddress.SheetName ?? r.EndAddress.SheetName)
                                        .FirstOrDefault(s => s != null) ?? sheetName;
        int minRow = allRanges.Min(r => Math.Min(r.StartAddress.Row, r.EndAddress.Row));
        int maxRow = allRanges.Max(r => Math.Max(r.StartAddress.Row, r.EndAddress.Row));
        int minCol = allRanges.Min(r => Math.Min(r.StartAddress.Column, r.EndAddress.Column));
        int maxCol = allRanges.Max(r => Math.Max(r.StartAddress.Column, r.EndAddress.Column));
        OdfCellRange dataRange = new OdfCellRange(
            new OdfCellAddress(minRow, minCol, commonSheet),
            new OdfCellAddress(maxRow, maxCol, commonSheet));

        string title = string.Concat(xml.Descendants(chartNs + "title")
            .Descendants(drawingNs + "t")
            .Select(element => element.Value));

        return new ChartSpec
        {
            ChartType = chartType,
            Title = title,
            DataRange = dataRange
        };
    }

    private static OdfCellRange EnsureRangeSheet(OdfCellRange range, string sheetName)
    {
        string actualSheet = range.StartAddress.SheetName ?? sheetName;
        return new OdfCellRange(
            new OdfCellAddress(range.StartAddress.Row, range.StartAddress.Column, actualSheet),
            new OdfCellAddress(range.EndAddress.Row, range.EndAddress.Column, actualSheet));
    }

    private sealed class ChartSpec
    {
        public OdfChartType ChartType { get; init; }
        public string Title { get; init; } = string.Empty;
        public OdfCellRange DataRange { get; init; }
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
                OdfCell odsCell = odsSheet.Cells[r, c];
                if (!string.IsNullOrWhiteSpace(xlCell.FormulaA1))
                {
                    odsCell.Formula = TranslateFormulaToOdf(xlCell.FormulaA1);
                }

                if (val is not null)
                    odsCell.CellValue = val;

                CopyCellStyle(xlCell, odsCell);
            }
        }
    }

    private static void CopyCellStyle(IXLCell xlCell, OdfCell odsCell)
    {
        if (xlCell.Style.Font.Bold)
        {
            SetCellStyleProperty(odsCell, "text-properties", "font-weight", OdfNamespaces.Fo, "bold", "fo");
        }

        if (xlCell.Style.Font.Italic)
        {
            SetCellStyleProperty(odsCell, "text-properties", "font-style", OdfNamespaces.Fo, "italic", "fo");
        }

        if (xlCell.Style.Font.Underline != XLFontUnderlineValues.None)
        {
            SetCellStyleProperty(odsCell, "text-properties", "text-underline-style", OdfNamespaces.Style, "solid", "style");
        }

        if (TryGetHexColor(xlCell.Style.Font.FontColor, out string? fontColor))
        {
            SetCellStyleProperty(odsCell, "text-properties", "color", OdfNamespaces.Fo, fontColor!, "fo");
        }

        if (TryGetHexColor(xlCell.Style.Fill.BackgroundColor, out string? backgroundColor))
        {
            SetCellStyleProperty(odsCell, "table-cell-properties", "background-color", OdfNamespaces.Fo, backgroundColor!, "fo");
        }

        CopyBorder(odsCell, "border-top", xlCell.Style.Border.TopBorder);
        CopyBorder(odsCell, "border-bottom", xlCell.Style.Border.BottomBorder);
        CopyBorder(odsCell, "border-left", xlCell.Style.Border.LeftBorder);
        CopyBorder(odsCell, "border-right", xlCell.Style.Border.RightBorder);
    }

    private static void CopyBorder(OdfCell odsCell, string propertyName, XLBorderStyleValues style)
    {
        if (style == XLBorderStyleValues.None)
        {
            return;
        }

        string lineStyle = style == XLBorderStyleValues.Dashed || style == XLBorderStyleValues.DashDot
            ? "dashed"
            : "solid";
        SetCellStyleProperty(odsCell, "table-cell-properties", propertyName, OdfNamespaces.Fo, $"0.75pt {lineStyle} #000000", "fo");
    }

    private static void SetCellStyleProperty(OdfCell odsCell, string propertiesElement, string propertyName, string propertyNamespace, string value, string prefix)
    {
        odsCell.Document.StyleEngine.SetLocalStyleProperty(odsCell.Node, "table-cell", propertiesElement, propertyName, propertyNamespace, value, prefix);
    }

    private static bool TryGetHexColor(XLColor color, out string? hex)
    {
        hex = null;
        if (color.ColorType != XLColorType.Color)
        {
            return false;
        }

        var systemColor = color.Color;
        hex = $"#{systemColor.R:X2}{systemColor.G:X2}{systemColor.B:X2}";
        return true;
    }

    private static void CopyDataValidations(IXLWorksheet xlSheet, OdfTableSheet odsSheet)
    {
        foreach (var validation in xlSheet.DataValidations)
        {
            if (validation.Operator != XLOperator.Between)
            {
                continue;
            }

            OdfValidationCondition? condition = validation.AllowedValues switch
            {
                XLAllowedValues.WholeNumber => OdfValidationCondition.IntegerBetween,
                XLAllowedValues.Decimal => OdfValidationCondition.DecimalBetween,
                XLAllowedValues.TextLength => OdfValidationCondition.TextLengthBetween,
                _ => null
            };

            if (!condition.HasValue)
            {
                continue;
            }

            foreach (var range in validation.Ranges)
            {
                var address = range.RangeAddress;
                var odfRange = new OdfCellRange(
                    address.FirstAddress.RowNumber - 1,
                    address.FirstAddress.ColumnNumber - 1,
                    address.LastAddress.RowNumber - 1,
                    address.LastAddress.ColumnNumber - 1,
                    odsSheet.Name);

                odsSheet.Document.AddDataValidation(odsSheet.Name, new OdfDataValidation
                {
                    ApplyTo = odfRange,
                    Condition = condition.Value,
                    Formula1 = validation.MinValue,
                    Formula2 = validation.MaxValue,
                    ErrorTitle = validation.ErrorTitle,
                    ErrorMessage = validation.ErrorMessage,
                    AlertStyle = validation.ErrorStyle switch
                    {
                        XLErrorStyle.Warning => OdfValidationAlertStyle.Warning,
                        XLErrorStyle.Information => OdfValidationAlertStyle.Information,
                        _ => OdfValidationAlertStyle.Stop
                    }
                });
            }
        }
    }

    private static void CopyConditionalFormats(IXLWorksheet xlSheet, OdfTableSheet odsSheet)
    {
        foreach (var conditionalFormat in xlSheet.ConditionalFormats)
        {
            OdfCellRange range = ToOdfRange(conditionalFormat.Range, odsSheet.Name);
            if (conditionalFormat.ConditionalFormatType == XLConditionalFormatType.ColorScale)
            {
                string minColor = GetConditionalColor(conditionalFormat, 1, "#FF0000");
                string maxColor = GetConditionalColor(conditionalFormat, 2, "#00FF00");
                string? midColor = conditionalFormat.Colors.ContainsKey(3)
                    ? GetConditionalColor(conditionalFormat, 2, "#FFFF00")
                    : null;

                if (midColor is null)
                {
                    odsSheet.AddColorScaleFormat(range, new OdfColor(minColor), new OdfColor(maxColor));
                }
                else
                {
                    string maxThreeColor = GetConditionalColor(conditionalFormat, 3, "#00FF00");
                    odsSheet.AddColorScaleFormat(range, new OdfColor(minColor), new OdfColor(maxThreeColor), new OdfColor(midColor));
                }
            }
            else if (conditionalFormat.ConditionalFormatType == XLConditionalFormatType.DataBar)
            {
                string positiveColor = GetConditionalColor(conditionalFormat, 1, "#638EC6");
                string? negativeColor = conditionalFormat.Colors.ContainsKey(2)
                    ? GetConditionalColor(conditionalFormat, 2, "#FF0000")
                    : null;

                odsSheet.AddDataBarFormat(
                    range,
                    new OdfColor(positiveColor),
                    negativeColor is null ? null : new OdfColor(negativeColor));
            }
            else if (conditionalFormat.ConditionalFormatType == XLConditionalFormatType.IconSet)
            {
                odsSheet.AddIconSetFormat(range, MapIconSetStyle(conditionalFormat.IconSetStyle));
            }
        }
    }

    private static OdfCellRange ToOdfRange(IXLRange range, string sheetName)
    {
        var address = range.RangeAddress;
        return new OdfCellRange(
            address.FirstAddress.RowNumber - 1,
            address.FirstAddress.ColumnNumber - 1,
            address.LastAddress.RowNumber - 1,
            address.LastAddress.ColumnNumber - 1,
            sheetName);
    }

    private static string GetConditionalColor(IXLConditionalFormat conditionalFormat, int key, string fallback)
    {
        return conditionalFormat.Colors.ContainsKey(key)
            ? ToHexColor(conditionalFormat.Colors[key], fallback)
            : fallback;
    }

    private static string ToHexColor(XLColor color, string fallback)
    {
        if (color.ColorType != XLColorType.Color)
        {
            return fallback;
        }

        var systemColor = color.Color;
        return $"#{systemColor.R:X2}{systemColor.G:X2}{systemColor.B:X2}";
    }

    private static OdfIconSetType MapIconSetStyle(XLIconSetStyle style)
    {
        return style switch
        {
            XLIconSetStyle.ThreeTrafficLights1 => OdfIconSetType.ThreeTrafficLights,
            XLIconSetStyle.FourRating => OdfIconSetType.FourRating,
            XLIconSetStyle.FiveRating => OdfIconSetType.FiveRating,
            _ => OdfIconSetType.ThreeArrows
        };
    }

    /// <summary>
    /// 將 Excel A1 格式公式翻譯為 OpenFormula 格式。
    /// </summary>
    /// <param name="excelFormula">Excel A1 格式公式。</param>
    /// <returns>含 <c>of:</c> 前綴的 OpenFormula 公式。</returns>
    public static string TranslateFormulaToOdf(string excelFormula)
    {
        if (string.IsNullOrWhiteSpace(excelFormula))
        {
            return string.Empty;
        }

        string formula = excelFormula.Trim();
        if (!formula.StartsWith("=", StringComparison.Ordinal))
        {
            formula = "=" + formula;
        }

        formula = formula.Replace("!", ".");
        return "of:" + formula;
    }
}
