using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    /// <param name="document">目標試算表文件。</param>
    public static void RenderChartsToFallbackImages(this SpreadsheetDocument document)
    {
        if (document is null)
            throw new ArgumentNullException(nameof(document));

        foreach (var sheet in document.Worksheets)
        {
            var frames = sheet.TableNode.Descendants()
                .Where(c => c.NodeType == OdfNodeType.Element &&
                            c.LocalName == "frame" &&
                            c.NamespaceUri == OdfNamespaces.Draw)
                .ToList();

            foreach (var frame in frames)
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

                // 移除 ./ 前綴取得物件目錄名稱（例如 Object 1）
                string objectName = href!.TrimStart('.', '/');
                string objectDir = $"{objectName}/";

                if (!document.Package.HasEntry($"{objectDir}content.xml"))
                    continue;

                // 1. 載入嵌入的 OdfChartDocument
                var chartDoc = new OdfChartDocument(document.Package, objectDir);

                // 2. 讀取圖表配置
                string chartClass = chartDoc.ChartClass;
                string? title = chartDoc.ChartTitle;
                string? categoriesAddr = chartDoc.CategoriesCellRangeAddress;
                var seriesList = chartDoc.Series;

                // 3. 讀取單元格資料
                var categoriesData = GetRangeValues(document, categoriesAddr);
                string[] categories = categoriesData.Select(c => c?.ToString() ?? string.Empty).ToArray();

                var plot = new ScottPlot.Plot();
                if (!string.IsNullOrEmpty(title))
                {
                    plot.Title(title!);
                }

                // 4. 依圖表類型使用 ScottPlot 5.1.58 進行繪圖
                if (chartClass.Contains("pie"))
                {
                    // 圓餅圖：一般只取第一組數值
                    if (seriesList.Count > 0)
                    {
                        var series = seriesList[0];
                        var valuesData = GetRangeValues(document, series.ValuesCellRangeAddress);
                        double[] values = valuesData.Select(ToDouble).ToArray();

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
                else if (chartClass.Contains("line") || chartClass.Contains("scatter"))
                {
                    // 折線圖
                    for (int s = 0; s < seriesList.Count; s++)
                    {
                        var series = seriesList[s];
                        var valuesData = GetRangeValues(document, series.ValuesCellRangeAddress);
                        double[] values = valuesData.Select(ToDouble).ToArray();
                        double[] xs = Enumerable.Range(0, values.Length).Select(x => (double)x).ToArray();

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
                    // 預設與長條圖 (chart:bar)
                    for (int s = 0; s < seriesList.Count; s++)
                    {
                        var series = seriesList[s];
                        var valuesData = GetRangeValues(document, series.ValuesCellRangeAddress);
                        double[] values = valuesData.Select(ToDouble).ToArray();

                        string seriesName = GetSeriesName(document, series.LabelCellAddress) ?? $"Series {s + 1}";
                        var barPlot = plot.Add.Bars(values);
                        barPlot.LegendText = seriesName;

                        if (s == 0 && categories.Length == values.Length)
                        {
                            double[] xs = Enumerable.Range(0, values.Length).Select(x => (double)x).ToArray();
                            plot.Axes.Bottom.SetTicks(xs, categories);
                        }
                    }
                    plot.ShowLegend();
                }

                // 5. 輸出靜態 PNG 並寫回封裝
                (int wPx, int hPx) = ParseDimensions(frame);
                byte[] pngBytes = plot.GetImageBytes(wPx, hPx, ScottPlot.ImageFormat.Png);

                string fallbackImagePath = $"Pictures/chart-fallback-{objectName}.png";
                document.Package.WriteEntry(fallbackImagePath, pngBytes, "image/png");

                // 6. 在 XML 裡插入 draw:image 節點
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

    private static List<object?> GetRangeValues(SpreadsheetDocument doc, string? rangeAddress)
    {
        var values = new List<object?>();
        if (string.IsNullOrEmpty(rangeAddress))
            return values;

        if (OdfCellRange.TryParse(rangeAddress!, out var cellRange))
        {
            string? sheetName = cellRange.StartAddress.SheetName ?? cellRange.EndAddress.SheetName;
            if (string.IsNullOrEmpty(sheetName))
            {
                var firstSheet = doc.Worksheets.FirstOrDefault();
                sheetName = firstSheet?.Name;
            }

            if (!string.IsNullOrEmpty(sheetName))
            {
                var sheet = doc.GetSheet(sheetName!);
                if (sheet is not null)
                {
                    int minRow = Math.Min(cellRange.StartAddress.Row, cellRange.EndAddress.Row);
                    int maxRow = Math.Max(cellRange.StartAddress.Row, cellRange.EndAddress.Row);
                    int minCol = Math.Min(cellRange.StartAddress.Column, cellRange.EndAddress.Column);
                    int maxCol = Math.Max(cellRange.StartAddress.Column, cellRange.EndAddress.Column);

                    for (int r = minRow; r <= maxRow; r++)
                    {
                        for (int c = minCol; c <= maxCol; c++)
                        {
                            var cell = sheet.GetCell(r, c);
                            values.Add(cell?.CellValue);
                        }
                    }
                }
            }
        }
        return values;
    }

    private static string? GetSeriesName(SpreadsheetDocument doc, string? labelCellAddress)
    {
        if (string.IsNullOrEmpty(labelCellAddress))
            return null;

        if (OdfCellAddress.TryParse(labelCellAddress!, out var address))
        {
            string? sheetName = address.SheetName;
            if (string.IsNullOrEmpty(sheetName))
            {
                var firstSheet = doc.Worksheets.FirstOrDefault();
                sheetName = firstSheet?.Name;
            }

            if (!string.IsNullOrEmpty(sheetName))
            {
                var sheet = doc.GetSheet(sheetName!);
                var cell = sheet?.GetCell(address.Row, address.Column);
                return cell?.CellValue?.ToString();
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

        if (double.TryParse(val.ToString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double parsed))
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
            return double.TryParse(s.Substring(0, s.Length - 2), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out cm);
        }
        if (s.EndsWith("in"))
        {
            if (double.TryParse(s.Substring(0, s.Length - 2), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double inches))
            {
                cm = inches * 2.54;
                return true;
            }
        }
        return double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out cm);
    }
}
