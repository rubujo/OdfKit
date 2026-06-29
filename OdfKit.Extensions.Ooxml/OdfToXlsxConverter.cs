using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ClosedXML.Excel;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Spreadsheet;
using A = DocumentFormat.OpenXml.Drawing;
using C = DocumentFormat.OpenXml.Drawing.Charts;
using S = DocumentFormat.OpenXml.Spreadsheet;
using Xdr = DocumentFormat.OpenXml.Drawing.Spreadsheet;
namespace OdfKit.Conversion;

/// <summary>
/// Converts ODF spreadsheets to XLSX.
/// 將 SpreadsheetDocument 轉換為 XLSX 格式的轉換器。
/// </summary>
public static class OdfToXlsxConverter
{
    /// <summary>
    /// Converts an ODF spreadsheet to XLSX.
    /// 將 ODS 工作簿轉換並寫入 XLSX 資料流。
    /// </summary>
    /// <param name="odsWorkbook">The value to use. / 來源 ODS 工作簿</param>
    /// <param name="xlsxStream">The source or target object. / 要寫入 XLSX 的目標資料流</param>
    /// <exception cref="ArgumentNullException">Thrown when the documented condition occurs. / 當任一必要參數為 null 時引發</exception>
    public static void Convert(OdfKit.Spreadsheet.SpreadsheetDocument odsWorkbook, Stream xlsxStream)
    {
        if (odsWorkbook is null)
            throw new ArgumentNullException(nameof(odsWorkbook));
        if (xlsxStream is null)
            throw new ArgumentNullException(nameof(xlsxStream));

        using var xlWorkbook = new XLWorkbook();
        var chartSpecs = new List<ChartSpec>();
        var pivotSpecs = new List<PivotSpec>();

        foreach (var odsSheet in odsWorkbook.Worksheets)
        {
            var xlSheet = xlWorkbook.Worksheets.Add(odsSheet.Name);
            CopySheetData(odsSheet, xlSheet);
            chartSpecs.AddRange(ReadChartSpecs(odsWorkbook, odsSheet));
        }

        // 依 ODF 1.4 schema，table:data-pilot-tables 是 office:spreadsheet 的直接子節點
        // （文件層級，與所有工作表同層），因此改透過 SpreadsheetDocument.GetPivotTables()
        // 在文件層級一次讀取全部樞紐分析表，而非逐工作表於 table:table 節點內尋找。
        foreach (OdfPivotTableInfo pivotInfo in odsWorkbook.GetPivotTables())
        {
            OdfTableSheet? pivotSheet = odsWorkbook.FindSheet(pivotInfo.SheetName);
            if (pivotSheet is null || !pivotInfo.TryGetSourceRange(out OdfCellRange sourceRange))
            {
                continue;
            }

            var fields = ReadPivotFields(pivotSheet, pivotInfo, sourceRange);
            if (fields.Count == 0)
            {
                continue;
            }

            pivotSpecs.Add(new PivotSpec
            {
                Name = pivotInfo.Name,
                SheetName = pivotInfo.SheetName,
                SourceRef = sourceRange.ToExcelString(),
                TargetRef = NormalizePivotTargetRef(pivotInfo.TargetRangeAddress, pivotInfo.SheetName),
                Fields = fields
            });
        }

        if (chartSpecs.Count == 0 && pivotSpecs.Count == 0)
        {
            xlWorkbook.SaveAs(xlsxStream);
            return;
        }

        using var workbookStream = new MemoryStream();
        xlWorkbook.SaveAs(workbookStream);
        if (chartSpecs.Count > 0)
        {
            InjectCharts(workbookStream, chartSpecs);
            NormalizeChartPartLocations(workbookStream);
        }

        if (pivotSpecs.Count > 0)
        {
            InjectPivotTables(workbookStream, pivotSpecs);
        }

        workbookStream.Position = 0;
        workbookStream.CopyTo(xlsxStream);
    }

    private static void CopySheetData(OdfTableSheet odsSheet, IXLWorksheet xlSheet)
    {
        foreach (var (row, col, data) in EnumerateSheetCells(odsSheet))
        {
            var val = data.Value;
            var xlCell = xlSheet.Cell(row + 1, col + 1);
            if (val is not null && !(val is string s && s.Length == 0))
            {
                SetXlCell(xlCell, val);
            }

            ApplyCellStyle(odsSheet, xlCell, data.StyleName);
        }

        CopyDataValidations(odsSheet, xlSheet);
        CopyConditionalFormats(odsSheet, xlSheet);
    }

    private static IEnumerable<ChartSpec> ReadChartSpecs(OdfKit.Spreadsheet.SpreadsheetDocument document, OdfTableSheet sheet)
    {
        var visitedPaths = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in sheet.TableNode.Descendants())
        {
            if (node.LocalName != "object" || node.NamespaceUri != OdfNamespaces.Draw)
            {
                continue;
            }

            string? href = node.GetAttribute("href", OdfNamespaces.XLink);
            if (string.IsNullOrEmpty(href))
            {
                continue;
            }

            string objectPath = href!.TrimStart('.', '/').TrimEnd('/');
            string contentPath = objectPath + "/content.xml";
            if (!document.Package.HasEntry(contentPath) || !visitedPaths.Add(contentPath))
            {
                continue;
            }

            ChartSpec? spec = ReadChartSpec(document.Package, contentPath, sheet.Name, node.Parent);
            if (spec is not null)
            {
                yield return spec;
            }
        }

        foreach (var entry in document.Package.GetEntries())
        {
            if (!entry.Path.StartsWith("Object ", StringComparison.Ordinal) ||
                !entry.Path.EndsWith("/content.xml", StringComparison.Ordinal) ||
                !visitedPaths.Add(entry.Path))
            {
                continue;
            }

            ChartSpec? spec = ReadChartSpec(document.Package, entry.Path, sheet.Name, null);
            if (spec is not null)
            {
                yield return spec;
            }
        }
    }

    private static ChartSpec? ReadChartSpec(OdfPackage package, string contentPath, string sheetName, OdfNode? frameNode)
    {
        try
        {
            using Stream stream = package.GetEntryStream(contentPath);
            var xml = XDocument.Load(stream);
            XNamespace chartNs = OdfNamespaces.Chart;
            XNamespace tableNs = OdfNamespaces.Table;
            XNamespace textNs = OdfNamespaces.Text;

            XElement? chart = xml.Descendants(chartNs + "chart").FirstOrDefault();
            if (chart is null)
            {
                return null;
            }

            string chartClass = ((string?)chart.Attribute(chartNs + "class") ?? "chart:bar").Replace("chart:", string.Empty);
            string dataRange = (string?)chart.Attribute(tableNs + "cell-range-address") ?? string.Empty;
            if (string.IsNullOrEmpty(dataRange))
            {
                return null;
            }

            string title = chart.Descendants(chartNs + "title")
                .Descendants(textNs + "p")
                .FirstOrDefault()
                ?.Value ?? string.Empty;

            bool hasLegend = chart.Elements(chartNs + "legend").Any();

            OdfCellRange? parsedRange = OdfCellRange.TryParse(dataRange.Trim('[', ']'), out OdfCellRange range)
                ? range
                : null;
            var anchor = ReadChartAnchor(frameNode);

            return new ChartSpec
            {
                SheetName = sheetName,
                ChartType = chartClass,
                Title = title,
                HasLegend = hasLegend,
                DataRange = NormalizeChartRange(dataRange),
                ParsedRange = parsedRange,
                AnchorColumn = anchor.Column,
                AnchorRow = anchor.Row,
                AnchorColumnSpan = anchor.ColumnSpan,
                AnchorRowSpan = anchor.RowSpan
            };
        }
        catch (Exception ex)
        {
            OdfKitDiagnostics.Warn(OdfLocalizer.GetMessage("Diag_OdfToXlsxConverter_ChartExportSkipped", ex.Message), ex);
            return null;
        }
    }

    private static string NormalizeChartRange(string odfRange)
    {
        string value = odfRange.Trim('[', ']');
        return OdfCellRange.TryParse(value, out OdfCellRange range)
            ? range.ToExcelString()
            : value.Replace('.', '!');
    }

    private static ChartAnchor ReadChartAnchor(OdfNode? frameNode)
    {
        if (frameNode is null)
        {
            return ChartAnchor.Default;
        }

        string? startAddress = frameNode.GetAttribute("start-cell-address", OdfNamespaces.Table);
        if (string.IsNullOrWhiteSpace(startAddress))
        {
            return ChartAnchor.Default;
        }

        string normalizedStartAddress = startAddress!.Trim('[', ']');
        if (!OdfCellAddress.TryParse(normalizedStartAddress, out OdfCellAddress anchor))
        {
            return ChartAnchor.Default;
        }

        int columnSpan = EstimateChartColumnSpan(frameNode.GetAttribute("end-x", OdfNamespaces.Table));
        int rowSpan = EstimateChartRowSpan(frameNode.GetAttribute("end-y", OdfNamespaces.Table));
        return new ChartAnchor(anchor.Column, anchor.Row, columnSpan, rowSpan);
    }

    private static int EstimateChartColumnSpan(string? length)
    {
        return TryReadCentimeters(length, out double centimeters)
            ? Math.Max(1, (int)Math.Ceiling(centimeters / 1.5d))
            : ChartAnchor.Default.ColumnSpan;
    }

    private static int EstimateChartRowSpan(string? length)
    {
        return TryReadCentimeters(length, out double centimeters)
            ? Math.Max(1, (int)Math.Ceiling(centimeters / 0.5d))
            : ChartAnchor.Default.RowSpan;
    }

    private static bool TryReadCentimeters(string? value, out double centimeters)
    {
        centimeters = 0;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string text = value!.Trim();
        if (text.EndsWith("cm", StringComparison.OrdinalIgnoreCase))
        {
            return double.TryParse(text.Substring(0, text.Length - 2), NumberStyles.Float, CultureInfo.InvariantCulture, out centimeters);
        }

        if (text.EndsWith("mm", StringComparison.OrdinalIgnoreCase) &&
            double.TryParse(text.Substring(0, text.Length - 2), NumberStyles.Float, CultureInfo.InvariantCulture, out double millimeters))
        {
            centimeters = millimeters / 10d;
            return true;
        }

        if (text.EndsWith("in", StringComparison.OrdinalIgnoreCase) &&
            double.TryParse(text.Substring(0, text.Length - 2), NumberStyles.Float, CultureInfo.InvariantCulture, out double inches))
        {
            centimeters = inches * 2.54d;
            return true;
        }

        if (text.EndsWith("pt", StringComparison.OrdinalIgnoreCase) &&
            double.TryParse(text.Substring(0, text.Length - 2), NumberStyles.Float, CultureInfo.InvariantCulture, out double points))
        {
            centimeters = points * 2.54d / 72d;
            return true;
        }

        return false;
    }

    private static void InjectCharts(Stream xlsxStream, IReadOnlyList<ChartSpec> chartSpecs)
    {
        xlsxStream.Position = 0;
        using var spreadsheet = DocumentFormat.OpenXml.Packaging.SpreadsheetDocument.Open(xlsxStream, true);
        WorkbookPart workbookPart = spreadsheet.WorkbookPart
            ?? throw new InvalidDataException(OdfLocalizer.GetMessage("Err_OdfToXlsxConverter_XlsxNotFound"));
        S.Workbook workbook = workbookPart.Workbook
            ?? throw new InvalidDataException(OdfLocalizer.GetMessage("Err_OdfToXlsxConverter_XlsxNotFound_2"));
        S.Sheets sheets = workbook.Sheets
            ?? throw new InvalidDataException(OdfLocalizer.GetMessage("Err_OdfToXlsxConverter_XlsxNotFound_5"));

        var perSheetIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (ChartSpec spec in chartSpecs)
        {
            S.Sheet? sheet = sheets.Elements<S.Sheet>()
                .FirstOrDefault(s => string.Equals(s.Name?.Value, spec.SheetName, StringComparison.OrdinalIgnoreCase));
            if (sheet?.Id?.Value is null)
            {
                continue;
            }

            if (workbookPart.GetPartById(sheet.Id.Value) is not WorksheetPart worksheetPart)
            {
                continue;
            }

            int index = perSheetIndex.TryGetValue(spec.SheetName, out int current) ? current + 1 : 1;
            perSheetIndex[spec.SheetName] = index;
            AddChartToWorksheet(worksheetPart, spec, index);
        }
    }

    private static void AddChartToWorksheet(WorksheetPart worksheetPart, ChartSpec spec, int chartIndex)
    {
        DrawingsPart drawingsPart;
        S.Worksheet worksheet = worksheetPart.Worksheet
            ?? throw new InvalidDataException(OdfLocalizer.GetMessage("Err_OdfToXlsxConverter_XlsxNotFound_4"));
        S.Drawing? drawing = worksheet.Elements<S.Drawing>().FirstOrDefault();
        if (drawing?.Id?.Value is not null && worksheetPart.GetPartById(drawing.Id.Value) is DrawingsPart existing)
        {
            drawingsPart = existing;
        }
        else
        {
            drawingsPart = worksheetPart.AddNewPart<DrawingsPart>();
            drawingsPart.WorksheetDrawing = new Xdr.WorksheetDrawing();
            string drawingRelId = worksheetPart.GetIdOfPart(drawingsPart);
            InsertWorksheetDrawing(worksheet, new S.Drawing { Id = drawingRelId });
        }

        ChartPart chartPart = drawingsPart.AddNewPart<ChartPart>();
        chartPart.ChartSpace = BuildChartSpace(spec);
        chartPart.ChartSpace.Save();

        string chartRelId = drawingsPart.GetIdOfPart(chartPart);
        var anchor = new Xdr.TwoCellAnchor(
            new Xdr.FromMarker(
                new Xdr.ColumnId(spec.AnchorColumn.ToString(CultureInfo.InvariantCulture)),
                new Xdr.ColumnOffset("0"),
                new Xdr.RowId(spec.AnchorRow.ToString(CultureInfo.InvariantCulture)),
                new Xdr.RowOffset("0")),
            new Xdr.ToMarker(
                new Xdr.ColumnId((spec.AnchorColumn + spec.AnchorColumnSpan).ToString(CultureInfo.InvariantCulture)),
                new Xdr.ColumnOffset("0"),
                new Xdr.RowId((spec.AnchorRow + spec.AnchorRowSpan).ToString(CultureInfo.InvariantCulture)),
                new Xdr.RowOffset("0")),
            new Xdr.GraphicFrame(
                new Xdr.NonVisualGraphicFrameProperties(
                    new Xdr.NonVisualDrawingProperties { Id = (UInt32Value)(uint)chartIndex, Name = "Chart " + chartIndex },
                    new Xdr.NonVisualGraphicFrameDrawingProperties()),
                new Xdr.Transform(
                    new A.Offset { X = 0, Y = 0 },
                    new A.Extents { Cx = 0, Cy = 0 }),
                new A.Graphic(
                    new A.GraphicData(
                        new C.ChartReference { Id = chartRelId })
                    { Uri = "http://schemas.openxmlformats.org/drawingml/2006/chart" })),
            new Xdr.ClientData());

        drawingsPart.WorksheetDrawing ??= new Xdr.WorksheetDrawing();
        drawingsPart.WorksheetDrawing.Append(anchor);
        drawingsPart.WorksheetDrawing.Save();
        worksheet.Save();
    }

    private static void InsertWorksheetDrawing(S.Worksheet worksheet, S.Drawing drawing)
    {
        OpenXmlElement? tailElement = worksheet.ChildElements.FirstOrDefault(element =>
            element is S.LegacyDrawing ||
            element is S.LegacyDrawingHeaderFooter ||
            element is S.Picture ||
            element is S.OleObjects ||
            element is S.Controls ||
            element is S.WebPublishItems ||
            element is S.TableParts ||
            element is S.ExtensionList);

        if (tailElement is null)
        {
            worksheet.AppendChild(drawing);
        }
        else
        {
            worksheet.InsertBefore(drawing, tailElement);
        }
    }

    private static void NormalizeChartPartLocations(Stream xlsxStream)
    {
        xlsxStream.Position = 0;
        using var archive = new ZipArchive(xlsxStream, ZipArchiveMode.Update, leaveOpen: true);
        var chartEntries = archive.Entries
            .Where(entry => entry.FullName.StartsWith("xl/drawings/charts/", StringComparison.OrdinalIgnoreCase) &&
                entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            .OrderBy(entry => entry.FullName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (chartEntries.Count == 0)
        {
            return;
        }

        var mappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        int chartIndex = 1;
        foreach (ZipArchiveEntry entry in chartEntries)
        {
            string targetPath;
            do
            {
                targetPath = "xl/charts/chart" + chartIndex.ToString(CultureInfo.InvariantCulture) + ".xml";
                chartIndex++;
            }
            while (archive.GetEntry(targetPath) is not null);

            ZipArchiveEntry target = archive.CreateEntry(targetPath, CompressionLevel.Optimal);
            using (Stream sourceStream = entry.Open())
            using (Stream targetStream = target.Open())
            {
                sourceStream.CopyTo(targetStream);
            }

            mappings["/" + entry.FullName] = "/" + targetPath;
            mappings[entry.FullName] = targetPath;
            entry.Delete();
        }

        RewriteChartRelationships(archive, mappings);
        RewriteChartContentTypes(archive, mappings);
    }

    private static void RewriteChartRelationships(ZipArchive archive, IReadOnlyDictionary<string, string> mappings)
    {
        XNamespace relNs = "http://schemas.openxmlformats.org/package/2006/relationships";
        foreach (ZipArchiveEntry relEntry in archive.Entries
            .Where(entry => entry.FullName.StartsWith("xl/drawings/_rels/", StringComparison.OrdinalIgnoreCase) &&
                entry.FullName.EndsWith(".rels", StringComparison.OrdinalIgnoreCase))
            .ToList())
        {
            XDocument rels;
            using (Stream stream = relEntry.Open())
            {
                rels = XDocument.Load(stream);
            }

            bool changed = false;
            foreach (XElement relationship in rels.Root!.Elements(relNs + "Relationship"))
            {
                string type = (string?)relationship.Attribute("Type") ?? string.Empty;
                if (!type.EndsWith("/chart", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string target = (string?)relationship.Attribute("Target") ?? string.Empty;
                string absoluteTarget = target.StartsWith("/", StringComparison.Ordinal)
                    ? target
                    : "/xl/drawings/" + target.TrimStart('/');
                if (!mappings.TryGetValue(absoluteTarget, out string? normalizedTarget))
                {
                    continue;
                }

                relationship.SetAttributeValue("Target", "../charts/" + Path.GetFileName(normalizedTarget));
                changed = true;
            }

            if (changed)
            {
                WriteZipXml(archive, relEntry.FullName, rels);
            }
        }
    }

    private static void RewriteChartContentTypes(ZipArchive archive, IReadOnlyDictionary<string, string> mappings)
    {
        XDocument contentTypes = ReadZipXml(archive, "[Content_Types].xml");
        XNamespace contentTypeNs = "http://schemas.openxmlformats.org/package/2006/content-types";
        foreach (XElement element in contentTypes.Root!.Elements(contentTypeNs + "Override"))
        {
            string partName = (string?)element.Attribute("PartName") ?? string.Empty;
            if (mappings.TryGetValue(partName, out string? normalizedPartName))
            {
                element.SetAttributeValue("PartName", EnsurePackagePartName(normalizedPartName));
            }
        }

        foreach (string normalizedPartName in mappings.Values
            .Select(EnsurePackagePartName)
            .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!contentTypes.Root!.Elements(contentTypeNs + "Override")
                .Any(element => string.Equals((string?)element.Attribute("PartName"), normalizedPartName, StringComparison.OrdinalIgnoreCase)))
            {
                AddContentTypeOverride(contentTypes, normalizedPartName, "application/vnd.openxmlformats-officedocument.drawingml.chart+xml");
            }
        }

        WriteZipXml(archive, "[Content_Types].xml", contentTypes);
    }

    private static string EnsurePackagePartName(string partName)
    {
        return partName.StartsWith("/", StringComparison.Ordinal)
            ? partName
            : "/" + partName;
    }

    private static C.ChartSpace BuildChartSpace(ChartSpec spec)
    {
        var plotArea = new C.PlotArea(new C.Layout());
        OpenXmlCompositeElement chartElement = BuildTypedChart(spec);
        plotArea.Append(chartElement);
        plotArea.Append(
            new C.CategoryAxis(
                new C.AxisId { Val = 48650112U },
                new C.Scaling(new C.Orientation { Val = C.OrientationValues.MinMax }),
                new C.AxisPosition { Val = C.AxisPositionValues.Bottom },
                new C.TickLabelPosition { Val = C.TickLabelPositionValues.NextTo },
                new C.CrossingAxis { Val = 48672768U },
                new C.Crosses { Val = C.CrossesValues.AutoZero },
                new C.AutoLabeled { Val = true },
                new C.LabelAlignment { Val = C.LabelAlignmentValues.Center },
                new C.LabelOffset { Val = (UInt16Value)(ushort)100 }),
            new C.ValueAxis(
                new C.AxisId { Val = 48672768U },
                new C.Scaling(new C.Orientation { Val = C.OrientationValues.MinMax }),
                new C.AxisPosition { Val = C.AxisPositionValues.Left },
                new C.MajorGridlines(),
                new C.NumberingFormat { FormatCode = "General", SourceLinked = true },
                new C.TickLabelPosition { Val = C.TickLabelPositionValues.NextTo },
                new C.CrossingAxis { Val = 48650112U },
                new C.Crosses { Val = C.CrossesValues.AutoZero },
                new C.CrossBetween { Val = C.CrossBetweenValues.Between }));

        var chart = new C.Chart();
        if (!string.IsNullOrEmpty(spec.Title))
        {
            chart.Append(BuildChartTitle(spec.Title));
        }
        chart.Append(plotArea);
        if (spec.HasLegend)
        {
            chart.Append(new C.Legend(
                new C.LegendPosition { Val = C.LegendPositionValues.Right },
                new C.Overlay { Val = false }));
        }
        chart.Append(new C.PlotVisibleOnly { Val = true });

        return new C.ChartSpace(
            new C.EditingLanguage { Val = "zh-TW" },
            chart);
    }

    private static OpenXmlCompositeElement BuildTypedChart(ChartSpec spec)
    {
        return spec.ChartType switch
        {
            "line" => new C.LineChart(
                new C.Grouping { Val = C.GroupingValues.Standard },
                new C.VaryColors { Val = false },
                BuildLineSeries(spec),
                new C.AxisId { Val = 48650112U },
                new C.AxisId { Val = 48672768U }),
            // 圓餅圖天生即依資料點（類別）區分色彩，符合 ODF 與 Excel 預設慣例
            // ODF 正式 chart:class 為 chart:circle；chart:pie 為舊版 OdfKit 誤用值，仍相容辨識
            "circle" or "pie" => new C.PieChart(
                new C.VaryColors { Val = true },
                BuildPieSeries(spec)),
            // 長條圖每個資料系列維持單一色彩，否則會與圖例的單一色塊不一致
            _ => new C.BarChart(
                new C.BarDirection { Val = C.BarDirectionValues.Column },
                new C.BarGrouping { Val = C.BarGroupingValues.Clustered },
                new C.VaryColors { Val = false },
                BuildBarSeries(spec),
                new C.AxisId { Val = 48650112U },
                new C.AxisId { Val = 48672768U })
        };
    }

    private static C.BarChartSeries BuildBarSeries(ChartSpec spec)
    {
        ChartSeriesReferences references = GetChartSeriesReferences(spec);
        var series = new C.BarChartSeries(
            new C.Index { Val = 0U },
            new C.Order { Val = 0U });
        AppendSeriesReferences(series, references);
        return series;
    }

    private static C.LineChartSeries BuildLineSeries(ChartSpec spec)
    {
        ChartSeriesReferences references = GetChartSeriesReferences(spec);
        var series = new C.LineChartSeries(
            new C.Index { Val = 0U },
            new C.Order { Val = 0U });
        AppendSeriesReferences(series, references);
        return series;
    }

    private static C.PieChartSeries BuildPieSeries(ChartSpec spec)
    {
        ChartSeriesReferences references = GetChartSeriesReferences(spec);
        var series = new C.PieChartSeries(
            new C.Index { Val = 0U },
            new C.Order { Val = 0U });
        AppendSeriesReferences(series, references);
        return series;
    }

    private static void AppendSeriesReferences(OpenXmlCompositeElement series, ChartSeriesReferences references)
    {
        if (references.SeriesText is not null)
        {
            series.Append(new C.SeriesText(new C.StringReference(new C.Formula(references.SeriesText))));
        }

        if (references.CategoryRange is not null)
        {
            series.Append(new C.CategoryAxisData(new C.StringReference(new C.Formula(references.CategoryRange))));
        }

        series.Append(new C.Values(new C.NumberReference(new C.Formula(references.ValueRange))));
    }

    private static ChartSeriesReferences GetChartSeriesReferences(ChartSpec spec)
    {
        if (spec.ParsedRange is not OdfCellRange range)
        {
            return new ChartSeriesReferences(null, null, spec.DataRange);
        }

        int minRow = Math.Min(range.StartAddress.Row, range.EndAddress.Row);
        int maxRow = Math.Max(range.StartAddress.Row, range.EndAddress.Row);
        int minColumn = Math.Min(range.StartAddress.Column, range.EndAddress.Column);
        int maxColumn = Math.Max(range.StartAddress.Column, range.EndAddress.Column);
        string? sheetName = range.StartAddress.SheetName ?? range.EndAddress.SheetName ?? spec.SheetName;

        if (maxRow > minRow && maxColumn > minColumn)
        {
            string seriesText = ToExcelReference(sheetName, minRow, minColumn + 1);
            string categoryRange = ToExcelReference(sheetName, minRow + 1, minColumn, maxRow, minColumn);
            string valueRange = ToExcelReference(sheetName, minRow + 1, minColumn + 1, maxRow, minColumn + 1);
            return new ChartSeriesReferences(seriesText, categoryRange, valueRange);
        }

        return new ChartSeriesReferences(null, null, spec.DataRange);
    }

    private static string ToExcelReference(string? sheetName, int row, int column)
    {
        return new OdfCellAddress(row, column, sheetName).ToExcelString();
    }

    private static string ToExcelReference(string? sheetName, int startRow, int startColumn, int endRow, int endColumn)
    {
        return new OdfCellRange(startRow, startColumn, endRow, endColumn, sheetName).ToExcelString();
    }

    private static C.Title BuildChartTitle(string title)
    {
        return new C.Title(
            new C.ChartText(
                new C.RichText(
                    new A.BodyProperties(),
                    new A.ListStyle(),
                    new A.Paragraph(
                        new A.Run(
                            new A.RunProperties { Language = "zh-TW" },
                            new A.Text(title))))),
            new C.Overlay { Val = false });
    }

    private static List<PivotFieldSpec> ReadPivotFields(
        OdfTableSheet sheet,
        OdfPivotTableInfo pivotInfo,
        OdfCellRange sourceRange)
    {
        int startCol = Math.Min(sourceRange.StartAddress.Column, sourceRange.EndAddress.Column);
        int endCol = Math.Max(sourceRange.StartAddress.Column, sourceRange.EndAddress.Column);
        int headerRow = Math.Min(sourceRange.StartAddress.Row, sourceRange.EndAddress.Row);
        var fields = new List<PivotFieldSpec>();
        for (int col = startCol; col <= endCol; col++)
        {
            string fallbackName = "Field" + (col - startCol + 1).ToString(CultureInfo.InvariantCulture);
            string name = TryGetCellAt(sheet, headerRow, col, out CellData data)
                ? System.Convert.ToString(data.Value, CultureInfo.InvariantCulture) ?? fallbackName
                : fallbackName;
            fields.Add(new PivotFieldSpec { Name = name });
        }

        foreach (OdfPivotTableFieldInfo fieldInfo in pivotInfo.Fields)
        {
            string name = fieldInfo.SourceFieldName;
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            PivotFieldSpec? field = fields.FirstOrDefault(candidate => string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase));
            if (field is null)
            {
                field = new PivotFieldSpec { Name = name };
                fields.Add(field);
            }

            field.Orientation = fieldInfo.Orientation;
            field.Function = fieldInfo.Function ?? string.Empty;
        }

        return fields;
    }

    private static string NormalizePivotTargetRef(string targetAddress, string sheetName)
    {
        // 依 ODF 1.4 schema，table:target-range-address 型別為 cellRangeAddress（範圍），
        // 優先以範圍格式解析其起點；若為舊版本寫入之單一儲存格位址格式則回退解析，以維持
        // 向下相容。
        OdfCellAddress? address = null;
        if (OdfCellRange.TryParse(targetAddress, out OdfCellRange parsedRange))
        {
            address = parsedRange.StartAddress;
        }
        else if (OdfCellAddress.TryParse(targetAddress, out OdfCellAddress parsedAddress))
        {
            address = parsedAddress;
        }

        if (address.HasValue)
        {
            string actualSheet = address.Value.SheetName ?? sheetName;
            var start = new OdfCellAddress(address.Value.Row, address.Value.Column, actualSheet);
            var end = new OdfCellAddress(address.Value.Row + 15, address.Value.Column + 4, actualSheet);
            return new OdfCellRange(start, end).ToExcelString();
        }

        return string.IsNullOrEmpty(targetAddress)
            ? sheetName + "!A1:E16"
            : targetAddress.Replace('.', '!');
    }

    private static void InjectPivotTables(Stream xlsxStream, IReadOnlyList<PivotSpec> pivotSpecs)
    {
        xlsxStream.Position = 0;
        using var archive = new ZipArchive(xlsxStream, ZipArchiveMode.Update, leaveOpen: true);
        XDocument workbook = ReadZipXml(archive, "xl/workbook.xml");
        XDocument workbookRels = ReadZipXml(archive, "xl/_rels/workbook.xml.rels");
        XDocument contentTypes = ReadZipXml(archive, "[Content_Types].xml");
        XNamespace spreadsheetNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        XNamespace relNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

        XElement sheets = workbook.Root?.Element(spreadsheetNs + "sheets")
            ?? throw new InvalidDataException(OdfLocalizer.GetMessage("Err_OdfToXlsxConverter_XlsxNotFound_5"));
        XElement pivotCaches = workbook.Root!.Element(spreadsheetNs + "pivotCaches")
            ?? new XElement(spreadsheetNs + "pivotCaches");
        if (pivotCaches.Parent is null)
        {
            sheets.AddAfterSelf(pivotCaches);
        }

        int cacheIndex = 1;
        foreach (PivotSpec spec in pivotSpecs)
        {
            XElement? sheetElement = sheets.Elements(spreadsheetNs + "sheet")
                .FirstOrDefault(sheet => string.Equals((string?)sheet.Attribute("name"), spec.SheetName, StringComparison.OrdinalIgnoreCase));
            if (sheetElement is null)
            {
                continue;
            }

            int sheetIndex = sheetElement.ElementsBeforeSelf(spreadsheetNs + "sheet").Count() + 1;
            string worksheetPath = "xl/worksheets/sheet" + sheetIndex.ToString(CultureInfo.InvariantCulture) + ".xml";
            string worksheetRelPath = "xl/worksheets/_rels/sheet" + sheetIndex.ToString(CultureInfo.InvariantCulture) + ".xml.rels";
            string cachePath = "xl/pivotCache/pivotCacheDefinition" + cacheIndex.ToString(CultureInfo.InvariantCulture) + ".xml";
            string pivotPath = "xl/pivotTables/pivotTable" + cacheIndex.ToString(CultureInfo.InvariantCulture) + ".xml";
            string pivotRelPath = "xl/pivotTables/_rels/pivotTable" + cacheIndex.ToString(CultureInfo.InvariantCulture) + ".xml.rels";

            string cacheRelId = AddRelationship(
                workbookRels,
                "http://schemas.openxmlformats.org/officeDocument/2006/relationships/pivotCacheDefinition",
                "pivotCache/pivotCacheDefinition" + cacheIndex.ToString(CultureInfo.InvariantCulture) + ".xml");
            pivotCaches.Add(new XElement(spreadsheetNs + "pivotCache",
                new XAttribute("cacheId", cacheIndex),
                new XAttribute(relNs + "id", cacheRelId)));

            XDocument worksheet = ReadZipXml(archive, worksheetPath);
            XDocument worksheetRels = ReadOrCreateRelsXml(archive, worksheetRelPath);
            string pivotRelId = AddRelationship(
                worksheetRels,
                "http://schemas.openxmlformats.org/officeDocument/2006/relationships/pivotTable",
                "../pivotTables/pivotTable" + cacheIndex.ToString(CultureInfo.InvariantCulture) + ".xml");
            worksheet.Root!.Add(new XElement(spreadsheetNs + "pivotTableDefinition", new XAttribute(relNs + "id", pivotRelId)));

            XDocument pivotRels = ReadOrCreateRelsXml(archive, pivotRelPath);
            AddRelationship(
                pivotRels,
                "http://schemas.openxmlformats.org/officeDocument/2006/relationships/pivotCacheDefinition",
                "../pivotCache/pivotCacheDefinition" + cacheIndex.ToString(CultureInfo.InvariantCulture) + ".xml");

            WriteZipXml(archive, cachePath, BuildPivotCacheDefinitionXml(spec, cacheIndex));
            WriteZipXml(archive, pivotPath, BuildPivotTableDefinitionXml(spec, cacheIndex));
            WriteZipXml(archive, pivotRelPath, pivotRels);
            WriteZipXml(archive, worksheetPath, worksheet);
            WriteZipXml(archive, worksheetRelPath, worksheetRels);
            AddContentTypeOverride(contentTypes, "/" + cachePath, "application/vnd.openxmlformats-officedocument.spreadsheetml.pivotCacheDefinition+xml");
            AddContentTypeOverride(contentTypes, "/" + pivotPath, "application/vnd.openxmlformats-officedocument.spreadsheetml.pivotTable+xml");
            cacheIndex++;
        }

        WriteZipXml(archive, "xl/workbook.xml", workbook);
        WriteZipXml(archive, "xl/_rels/workbook.xml.rels", workbookRels);
        WriteZipXml(archive, "[Content_Types].xml", contentTypes);
    }

    private static XDocument BuildPivotCacheDefinitionXml(PivotSpec spec, int cacheId)
    {
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        int sheetSeparator = spec.SourceRef.IndexOf('!');
        string sheetName = sheetSeparator >= 0 ? spec.SourceRef.Substring(0, sheetSeparator) : spec.SheetName;
        string rangeRef = sheetSeparator >= 0 ? spec.SourceRef.Substring(sheetSeparator + 1) : spec.SourceRef;
        return new XDocument(
            new XElement(ns + "pivotCacheDefinition",
                new XAttribute("refreshOnLoad", "1"),
                new XAttribute("refreshedBy", "OdfKit"),
                new XAttribute("createdVersion", "3"),
                new XAttribute("refreshedVersion", "8"),
                new XAttribute("minRefreshableVersion", "3"),
                new XAttribute("recordCount", "0"),
                new XElement(ns + "cacheSource",
                    new XAttribute("type", "worksheet"),
                    new XElement(ns + "worksheetSource",
                        new XAttribute("ref", rangeRef),
                        new XAttribute("sheet", sheetName.Replace("'", string.Empty)))),
                new XElement(ns + "cacheFields",
                    new XAttribute("count", spec.Fields.Count),
                    spec.Fields.Select(field =>
                        new XElement(ns + "cacheField",
                            new XAttribute("name", field.Name),
                            new XAttribute("numFmtId", "0"),
                            new XElement(ns + "sharedItems"))))));
    }

    private static XDocument BuildPivotTableDefinitionXml(PivotSpec spec, int cacheId)
    {
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var rowFields = IndexedFields(spec, "row").ToList();
        var columnFields = IndexedFields(spec, "column").ToList();
        var pageFields = IndexedFields(spec, "page").ToList();
        var dataFields = IndexedFields(spec, "data").ToList();

        return new XDocument(
            new XElement(ns + "pivotTableDefinition",
                new XAttribute("name", spec.Name),
                new XAttribute("cacheId", cacheId),
                new XAttribute("dataOnRows", "1"),
                new XAttribute("applyNumberFormats", "0"),
                new XAttribute("applyBorderFormats", "0"),
                new XAttribute("applyFontFormats", "0"),
                new XAttribute("applyPatternFormats", "0"),
                new XAttribute("applyAlignmentFormats", "0"),
                new XAttribute("applyWidthHeightFormats", "1"),
                new XAttribute("createdVersion", "3"),
                new XAttribute("updatedVersion", "8"),
                new XAttribute("minRefreshableVersion", "3"),
                new XAttribute("useAutoFormatting", "1"),
                new XAttribute("itemPrintTitles", "1"),
                new XAttribute("indent", "0"),
                new XAttribute("outline", "1"),
                new XAttribute("outlineData", "1"),
                new XAttribute("multipleFieldFilters", "0"),
                new XElement(ns + "location",
                    new XAttribute("ref", spec.TargetRef),
                    new XAttribute("firstHeaderRow", "1"),
                    new XAttribute("firstDataRow", "2"),
                    new XAttribute("firstDataCol", "1")),
                new XElement(ns + "pivotFields",
                    new XAttribute("count", spec.Fields.Count),
                    spec.Fields.Select((field, index) => BuildPivotFieldXml(ns, field, index))),
                BuildIndexedFieldContainer(ns, "rowFields", rowFields),
                BuildIndexedFieldContainer(ns, "colFields", columnFields),
                BuildIndexedFieldContainer(ns, "pageFields", pageFields),
                new XElement(ns + "dataFields",
                    new XAttribute("count", dataFields.Count),
                    dataFields.Select(field =>
                        new XElement(ns + "dataField",
                            new XAttribute("name", BuildDataFieldName(field.Field)),
                            new XAttribute("fld", field.Index),
                            new XAttribute("subtotal", NormalizePivotSubtotal(field.Field.Function))))),
                new XElement(ns + "pivotTableStyleInfo",
                    new XAttribute("name", "PivotStyleLight16"),
                    new XAttribute("showRowHeaders", "1"),
                    new XAttribute("showColHeaders", "1"),
                    new XAttribute("showRowStripes", "0"),
                    new XAttribute("showColStripes", "0"))));
    }

    private static IEnumerable<(int Index, PivotFieldSpec Field)> IndexedFields(PivotSpec spec, string orientation)
    {
        return spec.Fields.Select((field, index) => (Index: index, Field: field))
            .Where(pair => string.Equals(pair.Field.Orientation, orientation, StringComparison.OrdinalIgnoreCase));
    }

    private static XElement BuildPivotFieldXml(XNamespace ns, PivotFieldSpec field, int index)
    {
        string axis = field.Orientation switch
        {
            "row" => "axisRow",
            "column" => "axisCol",
            "page" => "axisPage",
            _ => string.Empty
        };
        var element = new XElement(ns + "pivotField", new XAttribute("showAll", "0"));
        if (!string.IsNullOrEmpty(axis))
        {
            element.SetAttributeValue("axis", axis);
            element.Add(new XElement(ns + "items",
                new XAttribute("count", "1"),
                new XElement(ns + "item", new XAttribute("x", "0"))));
        }

        if (string.Equals(field.Orientation, "data", StringComparison.OrdinalIgnoreCase))
        {
            element.SetAttributeValue("dataField", "1");
        }

        return element;
    }

    private static XElement? BuildIndexedFieldContainer(
        XNamespace ns,
        string elementName,
        IReadOnlyCollection<(int Index, PivotFieldSpec Field)> fields)
    {
        return fields.Count == 0
            ? null
            : new XElement(ns + elementName,
                new XAttribute("count", fields.Count),
                fields.Select(field => new XElement(ns + "field", new XAttribute("x", field.Index))));
    }

    private static string BuildDataFieldName(PivotFieldSpec field)
    {
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(NormalizePivotSubtotal(field.Function)) + " of " + field.Name;
    }

    private static string NormalizePivotSubtotal(string? function)
    {
        return function switch
        {
            "count" => "count",
            "average" => "average",
            "max" => "max",
            "min" => "min",
            _ => "sum"
        };
    }

    private static XDocument ReadZipXml(ZipArchive archive, string path)
    {
        ZipArchiveEntry entry = archive.GetEntry(path)
            ?? throw new FileNotFoundException(OdfLocalizer.GetMessage("Err_OdfToXlsxConverter_XlsxNotFound_6"), path);
        using Stream stream = entry.Open();
        return XDocument.Load(stream);
    }

    private static XDocument ReadOrCreateRelsXml(ZipArchive archive, string path)
    {
        ZipArchiveEntry? entry = archive.GetEntry(path);
        if (entry is null)
        {
            XNamespace relNs = "http://schemas.openxmlformats.org/package/2006/relationships";
            return new XDocument(new XElement(relNs + "Relationships"));
        }

        using Stream stream = entry.Open();
        return XDocument.Load(stream);
    }

    private static void WriteZipXml(ZipArchive archive, string path, XDocument xml)
    {
        archive.GetEntry(path)?.Delete();
        ZipArchiveEntry entry = archive.CreateEntry(path, CompressionLevel.Optimal);
        using Stream stream = entry.Open();
        xml.Save(stream);
    }

    private static string AddRelationship(XDocument rels, string type, string target)
    {
        XNamespace relNs = "http://schemas.openxmlformats.org/package/2006/relationships";
        XElement root = rels.Root ?? throw new InvalidDataException(OdfLocalizer.GetMessage("Err_OdfToXlsxConverter_XlsxNotFound_7"));
        int nextId = root.Elements(relNs + "Relationship")
            .Select(element => (string?)element.Attribute("Id"))
            .Select(id => id is not null && id.StartsWith("rId", StringComparison.OrdinalIgnoreCase) && int.TryParse(id.Substring(3), out int value) ? value : 0)
            .DefaultIfEmpty(0)
            .Max() + 1;
        string relationshipId = "rId" + nextId.ToString(CultureInfo.InvariantCulture);
        root.Add(new XElement(relNs + "Relationship",
            new XAttribute("Id", relationshipId),
            new XAttribute("Type", type),
            new XAttribute("Target", target)));
        return relationshipId;
    }

    private static void AddContentTypeOverride(XDocument contentTypes, string partName, string contentType)
    {
        XNamespace contentTypesNs = "http://schemas.openxmlformats.org/package/2006/content-types";
        XElement root = contentTypes.Root ?? throw new InvalidDataException(OdfLocalizer.GetMessage("Err_OdfToXlsxConverter_XlsxNotFound_8"));
        bool exists = root.Elements(contentTypesNs + "Override")
            .Any(element => string.Equals((string?)element.Attribute("PartName"), partName, StringComparison.OrdinalIgnoreCase));
        if (!exists)
        {
            root.Add(new XElement(contentTypesNs + "Override",
                new XAttribute("PartName", partName),
                new XAttribute("ContentType", contentType)));
        }
    }

    private sealed class CellFormula
    {
        public string ExcelFormula { get; }
        public CellFormula(string f) => ExcelFormula = f;
    }

    private sealed class CellData
    {
        public object? Value { get; set; }
        public string? StyleName { get; set; }
    }

    private enum ValidationKind
    {
        IntegerBetween,
        DecimalBetween,
        TextLengthBetween
    }

    private sealed class ValidationRule
    {
        public ValidationKind Kind { get; set; }
        public string Formula1 { get; set; } = string.Empty;
        public string Formula2 { get; set; } = string.Empty;
        public string ErrorTitle { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public XLErrorStyle ErrorStyle { get; set; } = XLErrorStyle.Stop;
    }

    private sealed class ChartSpec
    {
        public string SheetName { get; init; } = string.Empty;
        public string ChartType { get; init; } = "bar";
        public string Title { get; init; } = string.Empty;
        public bool HasLegend { get; init; }
        public string DataRange { get; init; } = string.Empty;
        public OdfCellRange? ParsedRange { get; init; }
        public int AnchorColumn { get; init; } = ChartAnchor.Default.Column;
        public int AnchorRow { get; init; } = ChartAnchor.Default.Row;
        public int AnchorColumnSpan { get; init; } = ChartAnchor.Default.ColumnSpan;
        public int AnchorRowSpan { get; init; } = ChartAnchor.Default.RowSpan;
    }

    private readonly record struct ChartAnchor(int Column, int Row, int ColumnSpan, int RowSpan)
    {
        public static ChartAnchor Default { get; } = new(4, 15, 8, 14);
    }

    private readonly record struct ChartSeriesReferences(string? SeriesText, string? CategoryRange, string ValueRange);

    private sealed class PivotSpec
    {
        public string Name { get; init; } = string.Empty;
        public string SheetName { get; init; } = string.Empty;
        public string SourceRef { get; init; } = string.Empty;
        public string TargetRef { get; init; } = string.Empty;
        public IReadOnlyList<PivotFieldSpec> Fields { get; init; } = [];
    }

    private sealed class PivotFieldSpec
    {
        public string Name { get; init; } = string.Empty;
        public string Orientation { get; set; } = string.Empty;
        public string Function { get; set; } = string.Empty;
    }

    private static void SetXlCell(IXLCell cell, object? value)
    {
        switch (value)
        {
            case CellFormula formula:
                cell.SetFormulaA1(formula.ExcelFormula);
                break;
            case double d:
                cell.Value = d;
                break;
            case int i:
                cell.Value = i;
                break;
            case bool b:
                cell.Value = b;
                break;
            case DateTime dt:
                cell.Value = dt;
                break;
            case string str:
                cell.Value = str;
                break;
            default:
                cell.Value = value?.ToString() ?? string.Empty;
                break;
        }
    }

    private const int MaxTableRepeat = 1_048_576;

    /// <summary>
    /// 以流式方式列舉工作表儲存格，避免全表 <c>Dictionary</c> 快取（PERF-4g）。
    /// </summary>
    private static IEnumerable<(int Row, int Col, CellData Data)> EnumerateSheetCells(OdfTableSheet sheet)
    {
        int currentRowIndex = 0;
        foreach (var rowChild in sheet.TableNode.Children)
        {
            if (rowChild.LocalName != "table-row" || rowChild.NamespaceUri != OdfNamespaces.Table)
            {
                continue;
            }

            int rowRepeatedCount = GetRepeatCount(rowChild, "number-rows-repeated");
            var rowCells = new List<(int Col, CellData Data)>();
            int currentColIndex = 0;
            foreach (var cellChild in rowChild.Children)
            {
                if ((cellChild.LocalName != "table-cell" && cellChild.LocalName != "covered-table-cell") ||
                    cellChild.NamespaceUri != OdfNamespaces.Table)
                {
                    continue;
                }

                int colRepeatedCount = GetRepeatCount(cellChild, "number-columns-repeated");
                object? cellValue = GetCellValueFromNode(cellChild);
                string? styleName = cellChild.GetAttribute("style-name", OdfNamespaces.Table);
                if (cellValue is not null || !string.IsNullOrEmpty(styleName))
                {
                    var data = new CellData { Value = cellValue, StyleName = styleName };
                    for (int i = 0; i < colRepeatedCount; i++)
                    {
                        rowCells.Add((currentColIndex + i, data));
                    }
                }

                currentColIndex += colRepeatedCount;
            }

            for (int r = 0; r < rowRepeatedCount; r++)
            {
                int actualRow = currentRowIndex + r;
                foreach (var cell in rowCells)
                {
                    yield return (actualRow, cell.Col, cell.Data);
                }
            }

            currentRowIndex += rowRepeatedCount;
        }
    }

    /// <summary>
    /// 按需讀取單一儲存格（供樞紐表標題列等稀疏查詢）。
    /// </summary>
    private static bool TryGetCellAt(OdfTableSheet sheet, int targetRow, int targetCol, out CellData data)
    {
        data = new CellData();
        int currentRowIndex = 0;
        foreach (var rowChild in sheet.TableNode.Children)
        {
            if (rowChild.LocalName != "table-row" || rowChild.NamespaceUri != OdfNamespaces.Table)
            {
                continue;
            }

            int rowRepeatedCount = GetRepeatCount(rowChild, "number-rows-repeated");
            if (targetRow < currentRowIndex || targetRow >= currentRowIndex + rowRepeatedCount)
            {
                currentRowIndex += rowRepeatedCount;
                continue;
            }

            int currentColIndex = 0;
            foreach (var cellChild in rowChild.Children)
            {
                if ((cellChild.LocalName != "table-cell" && cellChild.LocalName != "covered-table-cell") ||
                    cellChild.NamespaceUri != OdfNamespaces.Table)
                {
                    continue;
                }

                int colRepeatedCount = GetRepeatCount(cellChild, "number-columns-repeated");
                if (targetCol < currentColIndex || targetCol >= currentColIndex + colRepeatedCount)
                {
                    currentColIndex += colRepeatedCount;
                    continue;
                }

                object? cellValue = GetCellValueFromNode(cellChild);
                string? styleName = cellChild.GetAttribute("style-name", OdfNamespaces.Table);
                if (cellValue is null && string.IsNullOrEmpty(styleName))
                {
                    return false;
                }

                data = new CellData { Value = cellValue, StyleName = styleName };
                return true;
            }

            return false;
        }

        return false;
    }

    private static int GetRepeatCount(OdfNode node, string attributeLocalName)
    {
        string? repeat = node.GetAttribute(attributeLocalName, OdfNamespaces.Table);
        if (!string.IsNullOrEmpty(repeat) && int.TryParse(repeat, out int count) && count > 0)
        {
            return Math.Min(count, MaxTableRepeat);
        }

        return 1;
    }

    private static void ApplyCellStyle(OdfTableSheet odsSheet, IXLCell xlCell, string? styleName)
    {
        if (string.IsNullOrEmpty(styleName))
        {
            return;
        }

        var styleEngine = odsSheet.Document.StyleEngine;

        if (string.Equals(styleEngine.GetStyleProperty(styleName!, "font-weight", OdfNamespaces.Fo, "table-cell"), "bold", StringComparison.OrdinalIgnoreCase))
        {
            xlCell.Style.Font.Bold = true;
        }

        if (string.Equals(styleEngine.GetStyleProperty(styleName!, "font-style", OdfNamespaces.Fo, "table-cell"), "italic", StringComparison.OrdinalIgnoreCase))
        {
            xlCell.Style.Font.Italic = true;
        }

        string? underline = styleEngine.GetStyleProperty(styleName!, "text-underline-style", OdfNamespaces.Style, "table-cell");
        if (!string.IsNullOrEmpty(underline) && !string.Equals(underline, "none", StringComparison.OrdinalIgnoreCase))
        {
            xlCell.Style.Font.Underline = XLFontUnderlineValues.Single;
        }

        ApplyColor(styleEngine.GetStyleProperty(styleName!, "color", OdfNamespaces.Fo, "table-cell"), color => xlCell.Style.Font.FontColor = color);
        ApplyColor(styleEngine.GetStyleProperty(styleName!, "background-color", OdfNamespaces.Fo, "table-cell"), color => xlCell.Style.Fill.BackgroundColor = color);
        ApplyBorder(styleEngine.GetStyleProperty(styleName!, "border", OdfNamespaces.Fo, "table-cell"), xlCell.Style.Border);
        ApplyBorder(styleEngine.GetStyleProperty(styleName!, "border-top", OdfNamespaces.Fo, "table-cell"), xlCell.Style.Border, "top");
        ApplyBorder(styleEngine.GetStyleProperty(styleName!, "border-bottom", OdfNamespaces.Fo, "table-cell"), xlCell.Style.Border, "bottom");
        ApplyBorder(styleEngine.GetStyleProperty(styleName!, "border-left", OdfNamespaces.Fo, "table-cell"), xlCell.Style.Border, "left");
        ApplyBorder(styleEngine.GetStyleProperty(styleName!, "border-right", OdfNamespaces.Fo, "table-cell"), xlCell.Style.Border, "right");
    }

    private static void ApplyColor(string? value, Action<XLColor> apply)
    {
        if (string.IsNullOrWhiteSpace(value) || string.Equals(value, "transparent", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        string normalized = value!.Trim();
        if (!normalized.StartsWith("#", StringComparison.Ordinal))
        {
            normalized = "#" + normalized;
        }

        apply(XLColor.FromHtml(normalized));
    }

    private static void ApplyBorder(string? value, IXLBorder border, string? side = null)
    {
        if (string.IsNullOrWhiteSpace(value) || string.Equals(value, "none", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var style = value!.IndexOf("dashed", StringComparison.OrdinalIgnoreCase) >= 0
            ? XLBorderStyleValues.Dashed
            : XLBorderStyleValues.Thin;

        if (side is null || side == "top")
            border.TopBorder = style;
        if (side is null || side == "bottom")
            border.BottomBorder = style;
        if (side is null || side == "left")
            border.LeftBorder = style;
        if (side is null || side == "right")
            border.RightBorder = style;
    }

    private static void CopyDataValidations(OdfTableSheet sheet, IXLWorksheet xlSheet)
    {
        Dictionary<string, ValidationRule> rules = ReadValidationRules(sheet);
        if (rules.Count == 0)
        {
            return;
        }

        const int MaxRepeat = 1_048_576;
        int currentRowIndex = 0;
        foreach (var rowChild in sheet.TableNode.Children)
        {
            if (rowChild.LocalName != "table-row" || rowChild.NamespaceUri != OdfNamespaces.Table)
            {
                continue;
            }

            int rowRepeat = ParseRepeat(rowChild.GetAttribute("number-rows-repeated", OdfNamespaces.Table), MaxRepeat);
            int currentColIndex = 0;
            foreach (var cellChild in rowChild.Children)
            {
                if ((cellChild.LocalName != "table-cell" && cellChild.LocalName != "covered-table-cell") ||
                    cellChild.NamespaceUri != OdfNamespaces.Table)
                {
                    continue;
                }

                int colRepeat = ParseRepeat(cellChild.GetAttribute("number-columns-repeated", OdfNamespaces.Table), MaxRepeat);
                string? validationName = cellChild.GetAttribute("content-validation-name", OdfNamespaces.Table);
                if (!string.IsNullOrEmpty(validationName) && rules.TryGetValue(validationName!, out ValidationRule? rule))
                {
                    for (int rowOffset = 0; rowOffset < rowRepeat; rowOffset++)
                    {
                        for (int colOffset = 0; colOffset < colRepeat; colOffset++)
                        {
                            ApplyValidation(
                                xlSheet.Cell(currentRowIndex + rowOffset + 1, currentColIndex + colOffset + 1),
                                rule);
                        }
                    }
                }

                currentColIndex += colRepeat;
            }

            currentRowIndex += rowRepeat;
        }
    }

    private static void CopyConditionalFormats(OdfTableSheet sheet, IXLWorksheet xlSheet)
    {
        foreach (var child in sheet.TableNode.Children)
        {
            if (child.LocalName != "conditional-formats" || child.NamespaceUri != OdfNamespaces.CalcExt)
            {
                continue;
            }

            foreach (var formatNode in child.Children)
            {
                if (formatNode.LocalName != "conditional-format" || formatNode.NamespaceUri != OdfNamespaces.CalcExt)
                {
                    continue;
                }

                string? rangeAddress = formatNode.GetAttribute("target-range-address", OdfNamespaces.CalcExt);
                if (string.IsNullOrEmpty(rangeAddress) || !OdfCellRange.TryParse(rangeAddress!, out OdfCellRange range))
                {
                    continue;
                }

                var xlRange = xlSheet.Range(range.ToExcelString());
                foreach (var ruleNode in formatNode.Children)
                {
                    if (ruleNode.NamespaceUri != OdfNamespaces.CalcExt)
                    {
                        continue;
                    }

                    if (ruleNode.LocalName == "color-scale")
                    {
                        ApplyColorScale(ruleNode, xlRange);
                    }
                    else if (ruleNode.LocalName == "data-bar")
                    {
                        ApplyDataBar(ruleNode, xlRange);
                    }
                    else if (ruleNode.LocalName == "icon-set")
                    {
                        ApplyIconSet(ruleNode, xlRange);
                    }
                }
            }
        }
    }

    private static void ApplyColorScale(OdfNode colorScaleNode, IXLRange xlRange)
    {
        var entries = colorScaleNode.Children
            .Where(node => node.LocalName == "color-scale-entry" && node.NamespaceUri == OdfNamespaces.CalcExt)
            .ToList();
        if (entries.Count < 2)
        {
            return;
        }

        XLColor minColor = ReadConditionalColor(entries[0], "#FF0000");
        XLColor maxColor = ReadConditionalColor(entries[entries.Count - 1], "#00FF00");
        var scale = xlRange.AddConditionalFormat().ColorScale().LowestValue(minColor);

        if (entries.Count >= 3)
        {
            XLColor midColor = ReadConditionalColor(entries[1], "#FFFF00");
            scale.Midpoint(XLCFContentType.Percentile, ReadConditionalNumber(entries[1], 50d), midColor)
                .HighestValue(maxColor);
        }
        else
        {
            scale.HighestValue(maxColor);
        }
    }

    private static void ApplyDataBar(OdfNode dataBarNode, IXLRange xlRange)
    {
        string? positiveColor = dataBarNode.GetAttribute("positive-color", OdfNamespaces.CalcExt);
        string? negativeColor = dataBarNode.GetAttribute("negative-color", OdfNamespaces.CalcExt);
        var conditionalFormat = xlRange.AddConditionalFormat();
        var min = string.IsNullOrEmpty(negativeColor)
            ? conditionalFormat.DataBar(ToXlColor(positiveColor, "#638EC6"), showBarOnly: false)
            : conditionalFormat.DataBar(ToXlColor(positiveColor, "#638EC6"), ToXlColor(negativeColor, "#FF0000"), showBarOnly: false);
        min.LowestValue().HighestValue();
    }

    private static void ApplyIconSet(OdfNode iconSetNode, IXLRange xlRange)
    {
        XLIconSetStyle iconSetStyle = (iconSetNode.GetAttribute("icon-set-type", OdfNamespaces.CalcExt) ?? string.Empty) switch
        {
            "3TrafficLights1" => XLIconSetStyle.ThreeTrafficLights1,
            "4Rating" => XLIconSetStyle.FourRating,
            "5Rating" => XLIconSetStyle.FiveRating,
            _ => XLIconSetStyle.ThreeArrows
        };
        xlRange.AddConditionalFormat().IconSet(iconSetStyle, reverseIconOrder: false, showIconOnly: false);
    }

    private static XLColor ReadConditionalColor(OdfNode node, string fallback)
    {
        return ToXlColor(node.GetAttribute("color", OdfNamespaces.CalcExt), fallback);
    }

    private static XLColor ToXlColor(string? value, string fallback)
    {
        string color = string.IsNullOrWhiteSpace(value) ? fallback : value!.Trim();
        if (!color.StartsWith("#", StringComparison.Ordinal))
        {
            color = "#" + color;
        }

        return XLColor.FromHtml(color);
    }

    private static double ReadConditionalNumber(OdfNode node, double fallback)
    {
        string? value = node.GetAttribute("value", OdfNamespaces.CalcExt);
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)
            ? parsed
            : fallback;
    }

    private static Dictionary<string, ValidationRule> ReadValidationRules(OdfTableSheet sheet)
    {
        var rules = new Dictionary<string, ValidationRule>(StringComparer.Ordinal);
        OdfNode? spreadsheetRoot = sheet.TableNode.Parent;
        if (spreadsheetRoot is null)
        {
            return rules;
        }

        foreach (var child in spreadsheetRoot.Children)
        {
            if (child.LocalName != "content-validations" || child.NamespaceUri != OdfNamespaces.Table)
            {
                continue;
            }

            foreach (var ruleNode in child.Children)
            {
                if (ruleNode.LocalName != "content-validation" || ruleNode.NamespaceUri != OdfNamespaces.Table)
                {
                    continue;
                }

                string? name = ruleNode.GetAttribute("name", OdfNamespaces.Table);
                string? condition = ruleNode.GetAttribute("condition", OdfNamespaces.Table);
                if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(condition))
                {
                    continue;
                }

                ValidationRule? rule = ParseValidationRule(condition!);
                if (rule is null)
                {
                    continue;
                }

                ReadErrorMessage(ruleNode, rule);
                rules[name!] = rule;
            }
        }

        return rules;
    }

    private static ValidationRule? ParseValidationRule(string condition)
    {
        Match match = Regex.Match(condition, @"is-between\(([^,]+),([^)]+)\)", RegexOptions.CultureInvariant);
        if (!match.Success)
        {
            return null;
        }

        ValidationKind kind =
            condition.IndexOf("text-length-is-between", StringComparison.OrdinalIgnoreCase) >= 0 ? ValidationKind.TextLengthBetween :
            condition.IndexOf("is-decimal-number", StringComparison.OrdinalIgnoreCase) >= 0 ? ValidationKind.DecimalBetween :
            ValidationKind.IntegerBetween;

        return new ValidationRule
        {
            Kind = kind,
            Formula1 = match.Groups[1].Value,
            Formula2 = match.Groups[2].Value
        };
    }

    private static void ReadErrorMessage(OdfNode ruleNode, ValidationRule rule)
    {
        foreach (var child in ruleNode.Children)
        {
            if (child.LocalName != "error-message" || child.NamespaceUri != OdfNamespaces.Table)
            {
                continue;
            }

            rule.ErrorMessage = child.GetAttribute("message", OdfNamespaces.Table) ?? string.Empty;
            rule.ErrorTitle = child.GetAttribute("title", OdfNamespaces.Table) ?? string.Empty;
            rule.ErrorStyle = (child.GetAttribute("message-type", OdfNamespaces.Table) ?? string.Empty) switch
            {
                "warning" => XLErrorStyle.Warning,
                "information" => XLErrorStyle.Information,
                _ => XLErrorStyle.Stop
            };
            return;
        }
    }

    private static void ApplyValidation(IXLCell cell, ValidationRule rule)
    {
        IXLDataValidation validation = cell.CreateDataValidation();
        validation.IgnoreBlanks = true;
        validation.ErrorStyle = rule.ErrorStyle;
        validation.ShowErrorMessage = !string.IsNullOrEmpty(rule.ErrorMessage) || !string.IsNullOrEmpty(rule.ErrorTitle);
        validation.ErrorTitle = rule.ErrorTitle;
        validation.ErrorMessage = rule.ErrorMessage;

        switch (rule.Kind)
        {
            case ValidationKind.DecimalBetween:
                validation.Decimal.Between(rule.Formula1, rule.Formula2);
                break;
            case ValidationKind.TextLengthBetween:
                validation.TextLength.Between(rule.Formula1, rule.Formula2);
                break;
            default:
                validation.WholeNumber.Between(rule.Formula1, rule.Formula2);
                break;
        }
    }

    private static int ParseRepeat(string? value, int max)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) && parsed > 0
            ? Math.Min(parsed, max)
            : 1;
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
                if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double number))
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
    /// Translates an ODF spreadsheet formula to an XLSX formula.
    /// 將 OpenFormula 格式公式（of:= 或 oooc:= 前綴）翻譯為 Excel A1 格式公式。
    /// </summary>
    public static string TranslateFormula(string odtFormula)
    {
        string f = odtFormula;
        // 去除命名空間前綴
        if (f.StartsWith("of:=", StringComparison.Ordinal))
            f = f.Substring(3); // 保留 "="
        else if (f.StartsWith("of:", StringComparison.Ordinal))
            f = "=" + f.Substring(3);
        else if (f.StartsWith("oooc:=", StringComparison.Ordinal))
            f = f.Substring(5);
        else if (f.StartsWith("oooc:", StringComparison.Ordinal))
            f = "=" + f.Substring(5);

        if (!f.StartsWith("=", StringComparison.Ordinal))
            f = "=" + f;

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
