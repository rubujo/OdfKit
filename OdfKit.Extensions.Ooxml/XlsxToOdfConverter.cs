using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using ClosedXML.Excel;
using DocumentFormat.OpenXml;
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
    /// 以禁用外部 DTD／實體解析的安全設定載入 XML 子部件，防禦 XXE。
    /// </summary>
    private static XDocument LoadXDocumentSafely(Stream stream)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
        };
        using XmlReader reader = XmlReader.Create(stream, settings);
        return XDocument.Load(reader);
    }

    /// <summary>
    /// 從 XLSX 資料流讀取並建立對應的 SpreadsheetDocument。
    /// </summary>
    /// <param name="xlsxStream">XLSX 來源資料流</param>
    /// <returns>轉換後的 SpreadsheetDocument 執行個體</returns>
    /// <exception cref="ArgumentNullException">當 xlsxStream 為 null 時引發</exception>
    public static OdfKit.Spreadsheet.SpreadsheetDocument Convert(Stream xlsxStream)
    {
        if (xlsxStream is null)
            throw new ArgumentNullException(nameof(xlsxStream));

        var odsWorkbook = OdfKit.Spreadsheet.SpreadsheetDocument.Create();
        using var workbookStream = new MemoryStream();
        xlsxStream.CopyTo(workbookStream);
        workbookStream.Position = 0;

        if (HasPivotTables(workbookStream))
        {
            workbookStream.Position = 0;
            CopyWorkbookFromOpenXml(workbookStream, odsWorkbook);
        }
        else
        {
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
                    {
                        odsSheet.Name = xlSheet.Name;
                    }

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
        }

        workbookStream.Position = 0;
        CopyCharts(workbookStream, odsWorkbook);
        workbookStream.Position = 0;
        CopyPivotTables(workbookStream, odsWorkbook);

        return odsWorkbook;
    }

    private static bool HasPivotTables(Stream xlsxStream)
    {
        using var spreadsheet = DocumentFormat.OpenXml.Packaging.SpreadsheetDocument.Open(xlsxStream, false);
        WorkbookPart? workbookPart = spreadsheet.WorkbookPart;
        if (workbookPart?.Workbook is null)
        {
            return false;
        }

        return workbookPart.Workbook.Descendants<S.PivotCaches>()
            .SelectMany(element => element.Elements<S.PivotCache>())
            .Any();
    }

    private static void CopyWorkbookFromOpenXml(Stream xlsxStream, OdfKit.Spreadsheet.SpreadsheetDocument odsWorkbook)
    {
        using var spreadsheet = DocumentFormat.OpenXml.Packaging.SpreadsheetDocument.Open(xlsxStream, false);
        WorkbookPart? workbookPart = spreadsheet.WorkbookPart;
        if (workbookPart?.Workbook?.Sheets is null)
        {
            return;
        }

        S.SharedStringTable? sharedStrings = workbookPart.SharedStringTablePart?.SharedStringTable;
        OpenXmlStylesheetContext? stylesheet = BuildOpenXmlStylesheetContext(workbookPart);
        bool firstSheet = true;

        foreach (S.Sheet sheet in workbookPart.Workbook.Sheets.Elements<S.Sheet>())
        {
            string? sheetName = sheet.Name?.Value;
            string? relationshipId = sheet.Id?.Value;
            if (string.IsNullOrEmpty(sheetName) || string.IsNullOrEmpty(relationshipId))
            {
                continue;
            }

            if (workbookPart.GetPartById(relationshipId!) is not WorksheetPart worksheetPart ||
                worksheetPart.Worksheet is null)
            {
                continue;
            }

            S.SheetData? sheetData = worksheetPart.Worksheet.GetFirstChild<S.SheetData>();
            if (sheetData is null)
            {
                continue;
            }

            OdfTableSheet odsSheet;
            if (firstSheet)
            {
                odsSheet = odsWorkbook.Worksheets.Count > 0
                    ? odsWorkbook.Worksheets[0]
                    : odsWorkbook.Worksheets.Add(sheetName!);
                if (odsSheet.Name != sheetName)
                {
                    odsSheet.Name = sheetName!;
                }

                firstSheet = false;
            }
            else
            {
                odsSheet = odsWorkbook.Worksheets.Add(sheetName!);
            }

            CopySheetDataFromOpenXml(sheetData, odsSheet, sharedStrings, stylesheet);
            CopyDataValidationsFromOpenXml(worksheetPart.Worksheet, odsSheet);
            CopyConditionalFormatsFromOpenXml(worksheetPart.Worksheet, odsSheet);
        }
    }

    private static void CopyDataValidationsFromOpenXml(S.Worksheet worksheet, OdfTableSheet odsSheet)
    {
        S.DataValidations? dataValidations = worksheet.GetFirstChild<S.DataValidations>();
        if (dataValidations is null)
        {
            return;
        }

        foreach (S.DataValidation validation in dataValidations.Elements<S.DataValidation>())
        {
            if (!string.Equals(validation.Operator?.InnerText, "between", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            OdfValidationCondition? condition = ResolveOpenXmlValidationCondition(validation.Type?.InnerText);
            if (!condition.HasValue)
            {
                continue;
            }

            string formula1 = validation.Formula1?.Text ?? string.Empty;
            string formula2 = validation.Formula2?.Text ?? string.Empty;
            string? sqref = validation.SequenceOfReferences?.InnerText;
            if (string.IsNullOrWhiteSpace(sqref))
            {
                continue;
            }

            foreach (string reference in SplitSqref(sqref!))
            {
                if (!OdfCellRange.TryParse(reference, out OdfCellRange range))
                {
                    continue;
                }

                odsSheet.Document.AddDataValidation(odsSheet.Name, new OdfDataValidation
                {
                    ApplyTo = EnsureRangeSheet(range, odsSheet.Name),
                    Condition = condition.Value,
                    Formula1 = formula1,
                    Formula2 = formula2,
                    ErrorTitle = validation.ErrorTitle?.Value ?? string.Empty,
                    ErrorMessage = validation.Error?.Value ?? string.Empty,
                    AlertStyle = ResolveOpenXmlValidationAlertStyle(validation.ErrorStyle?.InnerText)
                });
            }
        }
    }

    private static OdfValidationCondition? ResolveOpenXmlValidationCondition(string? typeText)
    {
        if (string.Equals(typeText, "whole", StringComparison.OrdinalIgnoreCase))
        {
            return OdfValidationCondition.IntegerBetween;
        }

        if (string.Equals(typeText, "decimal", StringComparison.OrdinalIgnoreCase))
        {
            return OdfValidationCondition.DecimalBetween;
        }

        if (string.Equals(typeText, "textLength", StringComparison.OrdinalIgnoreCase))
        {
            return OdfValidationCondition.TextLengthBetween;
        }

        return null;
    }

    private static OdfValidationAlertStyle ResolveOpenXmlValidationAlertStyle(string? errorStyle)
    {
        if (string.Equals(errorStyle, "warning", StringComparison.OrdinalIgnoreCase))
        {
            return OdfValidationAlertStyle.Warning;
        }

        if (string.Equals(errorStyle, "information", StringComparison.OrdinalIgnoreCase))
        {
            return OdfValidationAlertStyle.Information;
        }

        return OdfValidationAlertStyle.Stop;
    }

    private static void CopyConditionalFormatsFromOpenXml(S.Worksheet worksheet, OdfTableSheet odsSheet)
    {
        foreach (S.ConditionalFormatting conditionalFormatting in worksheet.Elements<S.ConditionalFormatting>())
        {
            string? sqref = conditionalFormatting.SequenceOfReferences?.InnerText;
            if (string.IsNullOrWhiteSpace(sqref))
            {
                continue;
            }

            string firstReference = SplitSqref(sqref!).FirstOrDefault() ?? string.Empty;
            if (!OdfCellRange.TryParse(firstReference, out OdfCellRange range))
            {
                continue;
            }

            OdfCellRange odfRange = EnsureRangeSheet(range, odsSheet.Name);
            foreach (S.ConditionalFormattingRule rule in conditionalFormatting.Elements<S.ConditionalFormattingRule>())
            {
                string? ruleType = rule.Type?.InnerText;
                if (string.Equals(ruleType, "colorScale", StringComparison.OrdinalIgnoreCase))
                {
                    CopyColorScaleFromOpenXml(rule, odfRange, odsSheet);
                }
                else if (string.Equals(ruleType, "dataBar", StringComparison.OrdinalIgnoreCase))
                {
                    CopyDataBarFromOpenXml(rule, odfRange, odsSheet);
                }
                else if (string.Equals(ruleType, "iconSet", StringComparison.OrdinalIgnoreCase))
                {
                    CopyIconSetFromOpenXml(rule, odfRange, odsSheet);
                }
            }
        }
    }

    private static void CopyColorScaleFromOpenXml(S.ConditionalFormattingRule rule, OdfCellRange range, OdfTableSheet odsSheet)
    {
        S.ColorScale? colorScale = rule.GetFirstChild<S.ColorScale>();
        if (colorScale is null)
        {
            return;
        }

        List<S.Color> colors = colorScale.Elements<S.Color>().ToList();
        if (colors.Count < 2)
        {
            return;
        }

        string minColor = ParseOpenXmlRgb(colors[0].Rgb?.Value, "#FF0000");
        string maxColor = ParseOpenXmlRgb(colors[colors.Count - 1].Rgb?.Value, "#00FF00");
        if (colors.Count >= 3)
        {
            string midColor = ParseOpenXmlRgb(colors[1].Rgb?.Value, "#FFFF00");
            odsSheet.AddColorScaleFormat(range, new OdfColor(minColor), new OdfColor(maxColor), new OdfColor(midColor));
        }
        else
        {
            odsSheet.AddColorScaleFormat(range, new OdfColor(minColor), new OdfColor(maxColor));
        }
    }

    private static void CopyDataBarFromOpenXml(S.ConditionalFormattingRule rule, OdfCellRange range, OdfTableSheet odsSheet)
    {
        S.DataBar? dataBar = rule.GetFirstChild<S.DataBar>();
        if (dataBar is null)
        {
            return;
        }

        List<S.Color> colors = dataBar.Elements<S.Color>().ToList();
        string positiveColor = colors.Count > 0
            ? ParseOpenXmlRgb(colors[0].Rgb?.Value, "#638EC6")
            : "#638EC6";
        string? negativeColor = colors.Count > 1
            ? ParseOpenXmlRgb(colors[1].Rgb?.Value, "#FF0000")
            : null;

        odsSheet.AddDataBarFormat(
            range,
            new OdfColor(positiveColor),
            negativeColor is null ? null : new OdfColor(negativeColor));
    }

    private static void CopyIconSetFromOpenXml(S.ConditionalFormattingRule rule, OdfCellRange range, OdfTableSheet odsSheet)
    {
        S.IconSet? iconSet = rule.GetFirstChild<S.IconSet>();
        if (iconSet is null)
        {
            return;
        }

        odsSheet.AddIconSetFormat(range, MapOpenXmlIconSet(iconSet.IconSetValue?.InnerText));
    }

    private static OdfIconSetType MapOpenXmlIconSet(string? iconSetValue)
    {
        if (string.Equals(iconSetValue, "3TrafficLights1", StringComparison.OrdinalIgnoreCase))
        {
            return OdfIconSetType.ThreeTrafficLights;
        }

        if (string.Equals(iconSetValue, "4Rating", StringComparison.OrdinalIgnoreCase))
        {
            return OdfIconSetType.FourRating;
        }

        if (string.Equals(iconSetValue, "5Rating", StringComparison.OrdinalIgnoreCase))
        {
            return OdfIconSetType.FiveRating;
        }

        return OdfIconSetType.ThreeArrows;
    }

    private static IEnumerable<string> SplitSqref(string sqref)
    {
        foreach (string part in sqref.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            string trimmed = part.Trim();
            if (trimmed.Length > 0)
            {
                yield return trimmed;
            }
        }
    }

    private static string ParseOpenXmlRgb(string? rgb, string fallback)
    {
        if (string.IsNullOrWhiteSpace(rgb))
        {
            return fallback;
        }

        string value = rgb!.Trim();
        if (value.Length == 8)
        {
            return "#" + value.Substring(2, 6);
        }

        if (value.Length == 6)
        {
            return "#" + value;
        }

        return fallback;
    }

    private static void CopySheetDataFromOpenXml(
        S.SheetData sheetData,
        OdfTableSheet odsSheet,
        S.SharedStringTable? sharedStrings,
        OpenXmlStylesheetContext? stylesheet)
    {
        foreach (S.Row row in sheetData.Elements<S.Row>())
        {
            foreach (S.Cell cell in row.Elements<S.Cell>())
            {
                string? cellReference = cell.CellReference?.Value;
                if (string.IsNullOrEmpty(cellReference))
                {
                    continue;
                }

                OdfCellAddress address = OdfCellAddress.ParseExcel(cellReference!);
                OdfCell odsCell = odsSheet.Cells[address.Row, address.Column];
                string? formulaText = cell.CellFormula?.Text;
                if (!string.IsNullOrWhiteSpace(formulaText))
                {
                    odsCell.Formula = TranslateFormulaToOdf(formulaText!);
                }

                object? value = ReadOpenXmlCellValue(cell, sharedStrings);
                if (value is not null)
                {
                    odsCell.CellValue = value;
                }

                CopyCellStyleFromOpenXml(cell, odsCell, stylesheet);
            }
        }
    }

    private static OpenXmlStylesheetContext? BuildOpenXmlStylesheetContext(WorkbookPart workbookPart)
    {
        S.Stylesheet? stylesheet = workbookPart.WorkbookStylesPart?.Stylesheet;
        if (stylesheet is null)
        {
            return null;
        }

        return new OpenXmlStylesheetContext
        {
            Fonts = stylesheet.Fonts?.Elements<S.Font>().ToList() ?? [],
            Fills = stylesheet.Fills?.Elements<S.Fill>().ToList() ?? [],
            Borders = stylesheet.Borders?.Elements<S.Border>().ToList() ?? [],
            CellFormats = stylesheet.CellFormats?.Elements<S.CellFormat>().ToList() ?? [],
        };
    }

    private static void CopyCellStyleFromOpenXml(S.Cell cell, OdfCell odsCell, OpenXmlStylesheetContext? stylesheet)
    {
        if (stylesheet is null || cell.StyleIndex is null)
        {
            return;
        }

        int styleIndex = (int)cell.StyleIndex.Value;
        if (styleIndex < 0 || styleIndex >= stylesheet.CellFormats.Count)
        {
            return;
        }

        S.CellFormat cellFormat = stylesheet.CellFormats[styleIndex];
        if (cellFormat.FontId is not null)
        {
            int fontId = (int)cellFormat.FontId.Value;
            if (fontId >= 0 && fontId < stylesheet.Fonts.Count)
            {
                CopyFontFromOpenXml(stylesheet.Fonts[fontId], odsCell);
            }
        }

        if (cellFormat.FillId is not null)
        {
            int fillId = (int)cellFormat.FillId.Value;
            if (fillId >= 0 && fillId < stylesheet.Fills.Count)
            {
                CopyFillFromOpenXml(stylesheet.Fills[fillId], odsCell);
            }
        }

        if (cellFormat.BorderId is not null)
        {
            int borderId = (int)cellFormat.BorderId.Value;
            if (borderId >= 0 && borderId < stylesheet.Borders.Count)
            {
                CopyBorderFromOpenXml(stylesheet.Borders[borderId], odsCell);
            }
        }
    }

    private static void CopyFontFromOpenXml(S.Font font, OdfCell odsCell)
    {
        if (font.Bold is not null && (font.Bold.Val is null || font.Bold.Val.Value))
        {
            SetCellStyleProperty(odsCell, "text-properties", "font-weight", OdfNamespaces.Fo, "bold", "fo");
        }

        if (font.Italic is not null && (font.Italic.Val is null || font.Italic.Val.Value))
        {
            SetCellStyleProperty(odsCell, "text-properties", "font-style", OdfNamespaces.Fo, "italic", "fo");
        }

        string? underline = font.Underline?.Val?.InnerText;
        if (!string.IsNullOrEmpty(underline) &&
            !string.Equals(underline, "none", StringComparison.OrdinalIgnoreCase))
        {
            SetCellStyleProperty(odsCell, "text-properties", "text-underline-style", OdfNamespaces.Style, "solid", "style");
        }

        if (font.Color?.Rgb?.Value is { Length: > 0 } fontColor)
        {
            SetCellStyleProperty(odsCell, "text-properties", "color", OdfNamespaces.Fo, ParseOpenXmlRgb(fontColor, "#000000"), "fo");
        }
    }

    private static void CopyFillFromOpenXml(S.Fill fill, OdfCell odsCell)
    {
        S.PatternFill? patternFill = fill.PatternFill;
        if (patternFill is null)
        {
            return;
        }

        string? patternType = patternFill.PatternType?.InnerText;
        if (!string.IsNullOrEmpty(patternType) &&
            !string.Equals(patternType, "solid", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(patternType, "darkGray", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (patternFill.ForegroundColor?.Rgb?.Value is { Length: > 0 } backgroundColor)
        {
            SetCellStyleProperty(
                odsCell,
                "table-cell-properties",
                "background-color",
                OdfNamespaces.Fo,
                ParseOpenXmlRgb(backgroundColor, "#FFFFFF"),
                "fo");
        }
    }

    private static void CopyBorderFromOpenXml(S.Border border, OdfCell odsCell)
    {
        CopyOpenXmlBorderSide(odsCell, "border-top", border.TopBorder);
        CopyOpenXmlBorderSide(odsCell, "border-bottom", border.BottomBorder);
        CopyOpenXmlBorderSide(odsCell, "border-left", border.LeftBorder);
        CopyOpenXmlBorderSide(odsCell, "border-right", border.RightBorder);
    }

    private static void CopyOpenXmlBorderSide(OdfCell odsCell, string propertyName, S.BorderPropertiesType? borderSide)
    {
        string? styleText = borderSide?.Style?.InnerText;
        if (string.IsNullOrEmpty(styleText) || string.Equals(styleText, "none", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        string lineStyle = styleText.Contains("dash", StringComparison.OrdinalIgnoreCase) ? "dashed" : "solid";
        string color = borderSide?.Color?.Rgb?.Value is { Length: > 0 } rgb
            ? ParseOpenXmlRgb(rgb, "#000000")
            : "#000000";
        SetCellStyleProperty(odsCell, "table-cell-properties", propertyName, OdfNamespaces.Fo, $"0.75pt {lineStyle} {color}", "fo");
    }

    private static object? ReadOpenXmlCellValue(S.Cell cell, S.SharedStringTable? sharedStrings)
    {
        string? rawValue = cell.CellValue?.Text;
        if (string.IsNullOrEmpty(rawValue))
        {
            return cell.InlineString?.Text?.Text;
        }

        if (cell.DataType?.Value == S.CellValues.SharedString && sharedStrings is not null &&
            int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int sharedIndex))
        {
            S.SharedStringItem? item = sharedStrings.Elements<S.SharedStringItem>().ElementAtOrDefault(sharedIndex);
            return item?.InnerText;
        }

        if (cell.DataType?.Value == S.CellValues.Boolean)
        {
            return rawValue == "1";
        }

        if (double.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out double number))
        {
            return number;
        }

        return rawValue;
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
        catch (Exception ex)
        {
            // 保留工作表資料轉換；圖表解析失敗時不應中斷整份 XLSX 匯入。
            OdfKit.Core.OdfKitDiagnostics.Warn($"XLSX 圖表解析失敗，已略過圖表匯入：{ex.Message}", ex);
        }
    }

    private static ChartSpec? ReadChartSpec(ChartPart chartPart, string sheetName)
    {
        using Stream stream = chartPart.GetStream(FileMode.Open, FileAccess.Read);
        XDocument xml = LoadXDocumentSafely(stream);
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

    private static void CopyPivotTables(Stream xlsxStream, OdfKit.Spreadsheet.SpreadsheetDocument odsWorkbook)
    {
        try
        {
            using var spreadsheet = DocumentFormat.OpenXml.Packaging.SpreadsheetDocument.Open(xlsxStream, false);
            WorkbookPart? workbookPart = spreadsheet.WorkbookPart;
            if (workbookPart?.Workbook is null)
            {
                return;
            }

            XNamespace spreadsheetNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            XNamespace relationshipNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
            Dictionary<uint, PivotCacheInfo> cacheInfoById = BuildPivotCacheMap(workbookPart, spreadsheetNs, relationshipNs);
            if (cacheInfoById.Count == 0)
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

                if (workbookPart.GetPartById(sheetRelationshipId!) is not WorksheetPart worksheetPart)
                {
                    continue;
                }

                OdfTableSheet? odsSheet = odsWorkbook.Worksheets.Find(sheetName!);
                if (odsSheet is null)
                {
                    continue;
                }

                foreach (PivotTablePart pivotPart in worksheetPart.PivotTableParts)
                {
                    ApplyPivotTable(pivotPart, cacheInfoById, odsSheet, sheetName!, spreadsheetNs);
                }
            }
        }
        catch (Exception ex)
        {
            OdfKitDiagnostics.Warn($"XLSX 樞紐分析表解析失敗，已略過樞紐表匯入：{ex.Message}", ex);
        }
    }

    private static Dictionary<uint, PivotCacheInfo> BuildPivotCacheMap(
        WorkbookPart workbookPart,
        XNamespace spreadsheetNs,
        XNamespace relationshipNs)
    {
        var map = new Dictionary<uint, PivotCacheInfo>();
        using (Stream workbookStream = workbookPart.GetStream(FileMode.Open, FileAccess.Read))
        {
            XDocument workbookXml = LoadXDocumentSafely(workbookStream);
            foreach (XElement cacheElement in workbookXml.Descendants(spreadsheetNs + "pivotCache"))
            {
                string? cacheIdText = cacheElement.Attribute("cacheId")?.Value;
                string? relationshipId = cacheElement.Attribute(relationshipNs + "id")?.Value;
                if (!uint.TryParse(cacheIdText, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint cacheId) ||
                    string.IsNullOrEmpty(relationshipId) ||
                    workbookPart.GetPartById(relationshipId!) is not PivotTableCacheDefinitionPart cachePart)
                {
                    continue;
                }

                PivotCacheInfo? cacheInfo = ReadPivotCacheInfo(cachePart, spreadsheetNs);
                if (cacheInfo is not null)
                {
                    map[cacheId] = cacheInfo;
                }
            }
        }

        return map;
    }

    private static PivotCacheInfo? ReadPivotCacheInfo(PivotTableCacheDefinitionPart cachePart, XNamespace spreadsheetNs)
    {
        using Stream cacheStream = cachePart.GetStream(FileMode.Open, FileAccess.Read);
        XDocument cacheXml = LoadXDocumentSafely(cacheStream);
        XElement? worksheetSource = cacheXml.Descendants(spreadsheetNs + "worksheetSource").FirstOrDefault();
        string? rangeRef = worksheetSource?.Attribute("ref")?.Value;
        if (string.IsNullOrWhiteSpace(rangeRef))
        {
            return null;
        }

        string? sheetName = worksheetSource?.Attribute("sheet")?.Value;
        string sourceRef = string.IsNullOrWhiteSpace(sheetName) || rangeRef!.IndexOf('!') >= 0
            ? rangeRef!
            : sheetName + "!" + rangeRef;

        List<string> fieldNames = cacheXml.Descendants(spreadsheetNs + "cacheField")
            .Select(element => element.Attribute("name")?.Value)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .ToList();
        if (fieldNames.Count == 0)
        {
            return null;
        }

        return new PivotCacheInfo(sourceRef!, fieldNames);
    }

    private static void ApplyPivotTable(
        PivotTablePart pivotPart,
        IReadOnlyDictionary<uint, PivotCacheInfo> cacheInfoById,
        OdfTableSheet odsSheet,
        string sheetName,
        XNamespace spreadsheetNs)
    {
        using Stream pivotStream = pivotPart.GetStream(FileMode.Open, FileAccess.Read);
        XDocument pivotXml = LoadXDocumentSafely(pivotStream);
        XElement? root = pivotXml.Root;
        if (root is null)
        {
            return;
        }

        string name = root.Attribute("name")?.Value ?? "PivotTable";
        string? cacheIdText = root.Attribute("cacheId")?.Value;
        if (!uint.TryParse(cacheIdText, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint cacheId) ||
            !cacheInfoById.TryGetValue(cacheId, out PivotCacheInfo? cacheInfo))
        {
            return;
        }

        if (!OdfCellRange.TryParse(cacheInfo.SourceRef, out OdfCellRange sourceRange))
        {
            return;
        }

        sourceRange = EnsureRangeSheet(sourceRange, sheetName);
        if (!TryParsePivotTargetStart(root.Element(spreadsheetNs + "location")?.Attribute("ref")?.Value, sheetName, out OdfCellAddress targetStart))
        {
            targetStart = new OdfCellAddress(5, 0, sheetName);
        }

        var builder = new OdfPivotTableBuilder(name, sourceRange, targetStart, odsSheet);
        foreach (XElement fieldElement in root.Descendants(spreadsheetNs + "rowFields").Elements(spreadsheetNs + "field"))
        {
            AddPivotFieldByIndex(builder, cacheInfo, fieldElement.Attribute("x")?.Value, PivotFieldOrientation.Row);
        }

        foreach (XElement fieldElement in root.Descendants(spreadsheetNs + "colFields").Elements(spreadsheetNs + "field"))
        {
            AddPivotFieldByIndex(builder, cacheInfo, fieldElement.Attribute("x")?.Value, PivotFieldOrientation.Column);
        }

        foreach (XElement fieldElement in root.Descendants(spreadsheetNs + "pageFields").Elements(spreadsheetNs + "field"))
        {
            AddPivotFieldByIndex(builder, cacheInfo, fieldElement.Attribute("x")?.Value, PivotFieldOrientation.Page);
        }

        foreach (XElement dataFieldElement in root.Descendants(spreadsheetNs + "dataFields").Elements(spreadsheetNs + "dataField"))
        {
            AddPivotDataFieldByIndex(builder, cacheInfo, dataFieldElement);
        }

        builder.Build();
    }

    private static void AddPivotFieldByIndex(
        OdfPivotTableBuilder builder,
        PivotCacheInfo cacheInfo,
        string? indexText,
        PivotFieldOrientation orientation)
    {
        if (!int.TryParse(indexText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index) ||
            index < 0 ||
            index >= cacheInfo.FieldNames.Count)
        {
            return;
        }

        string fieldName = cacheInfo.FieldNames[index];
        switch (orientation)
        {
            case PivotFieldOrientation.Row:
                builder.AddRowField(fieldName);
                break;
            case PivotFieldOrientation.Column:
                builder.AddColumnField(fieldName);
                break;
            case PivotFieldOrientation.Page:
                builder.AddPageField(fieldName);
                break;
        }
    }

    private static void AddPivotDataFieldByIndex(
        OdfPivotTableBuilder builder,
        PivotCacheInfo cacheInfo,
        XElement dataFieldElement)
    {
        string? fieldIndexText = dataFieldElement.Attribute("fld")?.Value;
        if (!int.TryParse(fieldIndexText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index) ||
            index < 0 ||
            index >= cacheInfo.FieldNames.Count)
        {
            return;
        }

        string fieldName = cacheInfo.FieldNames[index];
        string subtotal = dataFieldElement.Attribute("subtotal")?.Value ?? "sum";
        builder.AddDataField(fieldName, MapPivotSubtotal(subtotal));
    }

    private static OdfPivotFunction MapPivotSubtotal(string subtotal)
    {
        return subtotal.ToLowerInvariant() switch
        {
            "count" => OdfPivotFunction.Count,
            "average" => OdfPivotFunction.Average,
            "max" => OdfPivotFunction.Max,
            "min" => OdfPivotFunction.Min,
            _ => OdfPivotFunction.Sum
        };
    }

    private static bool TryParsePivotTargetStart(string? locationRef, string sheetName, out OdfCellAddress targetStart)
    {
        targetStart = default;
        if (string.IsNullOrWhiteSpace(locationRef))
        {
            return false;
        }

        string location = locationRef!;
        int colonIndex = location.IndexOf(':', 0);
        string firstCell = colonIndex >= 0 ? location.Substring(0, colonIndex) : location;
        if (OdfCellAddress.TryParse(firstCell, out targetStart))
        {
            if (targetStart.SheetName is null)
            {
                targetStart = new OdfCellAddress(targetStart.Row, targetStart.Column, sheetName);
            }

            return true;
        }

        if (OdfCellAddress.TryParse(sheetName + "!" + firstCell, out targetStart))
        {
            return true;
        }

        return false;
    }

    private enum PivotFieldOrientation
    {
        Row,
        Column,
        Page
    }

    private sealed class OpenXmlStylesheetContext
    {
        public List<S.Font> Fonts { get; init; } = [];
        public List<S.Fill> Fills { get; init; } = [];
        public List<S.Border> Borders { get; init; } = [];
        public List<S.CellFormat> CellFormats { get; init; } = [];
    }

    private sealed class PivotCacheInfo
    {
        public PivotCacheInfo(string sourceRef, IReadOnlyList<string> fieldNames)
        {
            SourceRef = sourceRef;
            FieldNames = fieldNames;
        }

        public string SourceRef { get; }
        public IReadOnlyList<string> FieldNames { get; }
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
        if (usedRange is null)
            return;

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
    /// <param name="excelFormula">Excel A1 格式公式</param>
    /// <returns>含 <c>of:</c> 前綴的 OpenFormula 公式</returns>
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
