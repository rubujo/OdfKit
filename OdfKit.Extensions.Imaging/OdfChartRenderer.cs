using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using OdfKit.Chart;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Spreadsheet;
using ScottPlot;

namespace OdfKit.Extensions.Imaging;

/// <summary>
/// 提供試算表圖表靜態影像渲染（ScottPlot 5.1.58）與 ODF 封裝 fallback 回寫的工具類別。
/// </summary>
public static class OdfChartRenderer
{
    /// <summary>
    /// 自動將試算表文件內的 Chart 物件繪製為 PNG fallback 影像並回寫至 OdfPackage 封裝中，提升跨平台開檔相容性。
    /// </summary>
    /// <param name="document">目標試算表文件</param>
    public static void RenderChartsToFallbackImages(this SpreadsheetDocument document)
    {
        if (document is null)
            throw new ArgumentNullException(nameof(document));

        foreach (var sheet in document.Worksheets)
        {
            foreach (var frame in EnumerateDrawFrames(sheet.TableNode))
            {
                var objNode = frame.Children.Find(c =>
                    c.NodeType == OdfNodeType.Element &&
                    c.LocalName == "object" &&
                    c.NamespaceUri == OdfNamespaces.Draw);

                if (objNode is null)
                    continue;

                string? href = objNode.GetAttribute("href", OdfNamespaces.XLink);
                if (string.IsNullOrEmpty(href))
                    continue;

                string objectName = href!.TrimStart('.', '/');
                string objectDir = $"{objectName}/";

                if (!document.Package.HasEntry($"{objectDir}content.xml"))
                    continue;

                var chartDoc = new OdfChartDocument(document.Package, objectDir);
                string chartClass = chartDoc.ChartClass;
                string? title = chartDoc.ChartTitle;
                string? categoriesAddr = chartDoc.CategoriesCellRangeAddress;
                var seriesList = chartDoc.Series;

                string[] categories = GetRangeStrings(document, categoriesAddr);

                var plot = new ScottPlot.Plot();
                if (!string.IsNullOrEmpty(title))
                {
                    plot.Title(title!);
                }

                if (chartClass.Contains("pie", StringComparison.OrdinalIgnoreCase))
                {
                    if (seriesList.Count > 0)
                    {
                        var series = seriesList[0];
                        double[] values = GetRangeDoubles(document, series.ValuesCellRangeAddress);
                        var pies = plot.Add.Pie(values);
                        if (categories.Length == values.Length)
                        {
                            for (int i = 0; i < pies.Slices.Count; i++)
                            {
                                pies.Slices[i].Label = categories[i];
                            }

                            plot.ShowLegend();
                        }
                    }
                }
                else if (chartClass.Contains("line", StringComparison.OrdinalIgnoreCase) ||
                    chartClass.Contains("scatter", StringComparison.OrdinalIgnoreCase))
                {
                    for (int s = 0; s < seriesList.Count; s++)
                    {
                        var series = seriesList[s];
                        double[] values = GetRangeDoubles(document, series.ValuesCellRangeAddress);
                        double[] xs = CreateIndexAxis(values.Length);

                        string seriesName = GetSeriesName(document, series.LabelCellAddress) ?? $"Series {s + 1}";
                        var scatter = plot.Add.Scatter(xs, values);
                        scatter.LegendText = seriesName;

                        if (s == 0 && categories.Length == values.Length)
                        {
                            plot.Axes.Bottom.SetTicks(xs, categories);
                        }
                    }

                    plot.ShowLegend();
                }
                else
                {
                    for (int s = 0; s < seriesList.Count; s++)
                    {
                        var series = seriesList[s];
                        double[] values = GetRangeDoubles(document, series.ValuesCellRangeAddress);

                        string seriesName = GetSeriesName(document, series.LabelCellAddress) ?? $"Series {s + 1}";
                        var barPlot = plot.Add.Bars(values);
                        barPlot.LegendText = seriesName;

                        if (s == 0 && categories.Length == values.Length)
                        {
                            plot.Axes.Bottom.SetTicks(CreateIndexAxis(values.Length), categories);
                        }
                    }

                    plot.ShowLegend();
                }

                (int wPx, int hPx) = ParseDimensions(frame);
                byte[] pngBytes = plot.GetImageBytes(wPx, hPx, ScottPlot.ImageFormat.Png);

                string fallbackImagePath = $"Pictures/chart-fallback-{objectName}.png";
                document.Package.WriteEntry(fallbackImagePath, pngBytes, "image/png");

                var imgNode = frame.Children.Find(c =>
                    c.NodeType == OdfNodeType.Element &&
                    c.LocalName == "image" &&
                    c.NamespaceUri == OdfNamespaces.Draw);

                if (imgNode is null)
                {
                    imgNode = OdfNodeFactory.CreateElement("image", OdfNamespaces.Draw, "draw");
                    imgNode.SetAttribute("type", OdfNamespaces.XLink, "simple", "xlink");
                    imgNode.SetAttribute("show", OdfNamespaces.XLink, "embed", "xlink");
                    imgNode.SetAttribute("actuate", OdfNamespaces.XLink, "onLoad", "xlink");
                    frame.AppendChild(imgNode);
                }

                imgNode.SetAttribute("href", OdfNamespaces.XLink, fallbackImagePath, "xlink");
            }
        }
    }

    private static IEnumerable<OdfNode> EnumerateDrawFrames(OdfNode root)
    {
        var stack = new Stack<OdfNode>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            OdfNode node = stack.Pop();
            foreach (var child in node.Children)
            {
                if (child.NodeType != OdfNodeType.Element)
                {
                    continue;
                }

                if (child.LocalName == "frame" && child.NamespaceUri == OdfNamespaces.Draw)
                {
                    yield return child;
                }

                stack.Push(child);
            }
        }
    }

    private static string[] GetRangeStrings(SpreadsheetDocument doc, string? rangeAddress)
    {
        if (!TryGetRangeBounds(doc, rangeAddress, out OdfTableSheet? sheet, out int minRow, out int maxRow, out int minCol, out int maxCol))
        {
            return Array.Empty<string>();
        }

        int length = (maxRow - minRow + 1) * (maxCol - minCol + 1);
        var result = new string[length];
        int index = 0;
        for (int r = minRow; r <= maxRow; r++)
        {
            for (int c = minCol; c <= maxCol; c++)
            {
                object? value = sheet!.GetCell(r, c)?.CellValue;
                result[index++] = value?.ToString() ?? string.Empty;
            }
        }

        return result;
    }

    private static double[] GetRangeDoubles(SpreadsheetDocument doc, string? rangeAddress)
    {
        if (!TryGetRangeBounds(doc, rangeAddress, out OdfTableSheet? sheet, out int minRow, out int maxRow, out int minCol, out int maxCol))
        {
            return Array.Empty<double>();
        }

        int length = (maxRow - minRow + 1) * (maxCol - minCol + 1);
        var result = new double[length];
        int index = 0;
        for (int r = minRow; r <= maxRow; r++)
        {
            for (int c = minCol; c <= maxCol; c++)
            {
                result[index++] = ToDouble(sheet!.GetCell(r, c)?.CellValue);
            }
        }

        return result;
    }

    private static bool TryGetRangeBounds(
        SpreadsheetDocument doc,
        string? rangeAddress,
        out OdfTableSheet? sheet,
        out int minRow,
        out int maxRow,
        out int minCol,
        out int maxCol)
    {
        sheet = null;
        minRow = maxRow = minCol = maxCol = 0;
        if (string.IsNullOrEmpty(rangeAddress) || !OdfCellRange.TryParse(rangeAddress!, out var cellRange))
        {
            return false;
        }

        string? sheetName = cellRange.StartAddress.SheetName ?? cellRange.EndAddress.SheetName;
        if (string.IsNullOrEmpty(sheetName))
        {
            sheetName = doc.Worksheets.Count > 0 ? doc.Worksheets[0].Name : null;
        }

        if (string.IsNullOrEmpty(sheetName))
        {
            return false;
        }

        sheet = doc.GetSheet(sheetName!);
        if (sheet is null)
        {
            return false;
        }

        minRow = Math.Min(cellRange.StartAddress.Row, cellRange.EndAddress.Row);
        maxRow = Math.Max(cellRange.StartAddress.Row, cellRange.EndAddress.Row);
        minCol = Math.Min(cellRange.StartAddress.Column, cellRange.EndAddress.Column);
        maxCol = Math.Max(cellRange.StartAddress.Column, cellRange.EndAddress.Column);
        return true;
    }

    private static double[] CreateIndexAxis(int length)
    {
        var xs = new double[length];
        for (int i = 0; i < length; i++)
        {
            xs[i] = i;
        }

        return xs;
    }

    private static string? GetSeriesName(SpreadsheetDocument doc, string? labelCellAddress)
    {
        if (string.IsNullOrEmpty(labelCellAddress))
            return null;

        if (OdfCellAddress.TryParse(labelCellAddress!, out var address))
        {
            string? sheetName = address.SheetName;
            if (string.IsNullOrEmpty(sheetName) && doc.Worksheets.Count > 0)
            {
                sheetName = doc.Worksheets[0].Name;
            }

            if (!string.IsNullOrEmpty(sheetName))
            {
                var sheet = doc.GetSheet(sheetName!);
                return sheet?.GetCell(address.Row, address.Column)?.CellValue?.ToString();
            }
        }

        return null;
    }

    private static double ToDouble(object? val)
    {
        if (val is null)
            return 0.0;
        if (val is double d)
            return d;
        if (val is float f)
            return f;
        if (val is int i)
            return i;
        if (val is decimal dec)
            return (double)dec;

        if (double.TryParse(val.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double parsed))
        {
            return parsed;
        }

        return 0.0;
    }

    private static (int width, int height) ParseDimensions(OdfNode frame)
    {
        double wCm = 12;
        double hCm = 7;
        string? wStr = frame.GetAttribute("width", OdfNamespaces.Svg);
        string? hStr = frame.GetAttribute("height", OdfNamespaces.Svg);

        if (wStr is not null && TryParseCm(wStr, out double w))
            wCm = w;
        if (hStr is not null && TryParseCm(hStr, out double h))
            hCm = h;

        int widthPx = (int)(wCm * 37.795);
        int heightPx = (int)(hCm * 37.795);

        if (widthPx < 100)
            widthPx = 800;
        if (heightPx < 100)
            heightPx = 500;
        return (widthPx, heightPx);
    }

    private static bool TryParseCm(string s, out double cm)
    {
        cm = 0;
        s = s.Trim().ToLowerInvariant();
        if (s.EndsWith("cm"))
        {
            return double.TryParse(s.Substring(0, s.Length - 2), NumberStyles.Float, CultureInfo.InvariantCulture, out cm);
        }

        if (s.EndsWith("in"))
        {
            if (double.TryParse(s.Substring(0, s.Length - 2), NumberStyles.Float, CultureInfo.InvariantCulture, out double inches))
            {
                cm = inches * 2.54;
                return true;
            }
        }

        return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out cm);
    }
}
