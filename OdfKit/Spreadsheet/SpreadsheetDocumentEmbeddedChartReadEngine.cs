using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using OdfKit.Chart;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Spreadsheet;

/// <summary>
/// 試算表嵌入圖表讀取引擎（內部協作者）。
/// </summary>
internal static class SpreadsheetDocumentEmbeddedChartReadEngine
{
    internal static IReadOnlyList<OdfEmbeddedChartInfo> GetEmbeddedCharts(SpreadsheetDocument document)
    {
        List<OdfEmbeddedChartInfo> charts = [];

        foreach (OdfTableSheet sheet in document.Worksheets)
        {
            OdfNode? shapesNode = OdfTableSheetDomHelper.FindChildElement(
                sheet.TableNode, "shapes", OdfNamespaces.Table);
            if (shapesNode is null)
                continue;

            foreach (OdfNode frameNode in shapesNode.Children)
            {
                if (frameNode.NodeType is not OdfNodeType.Element ||
                    frameNode.LocalName is not "frame" ||
                    frameNode.NamespaceUri != OdfNamespaces.Draw)
                    continue;

                OdfNode? objectNode = null;
                foreach (OdfNode child in frameNode.Children)
                {
                    if (child.NodeType is OdfNodeType.Element &&
                        child.LocalName == "object" &&
                        child.NamespaceUri == OdfNamespaces.Draw)
                    {
                        objectNode = child;
                        break;
                    }
                }

                if (objectNode is null)
                    continue;

                string? href = objectNode.GetAttribute("href", OdfNamespaces.XLink);
                if (string.IsNullOrEmpty(href))
                    continue;

                string objectPath = NormalizeObjectPath(href!);
                string anchorAddress = frameNode.GetAttribute("start-cell-address", OdfNamespaces.Table) ?? string.Empty;
                if (!TryReadChartMetadata(document.Package, objectPath, out OdfChartType chartType, out string? title, out string? dataRange))
                    continue;

                charts.Add(new OdfEmbeddedChartInfo(
                    sheet.Name,
                    anchorAddress,
                    objectPath,
                    chartType,
                    title,
                    dataRange));
            }
        }

        return charts.AsReadOnly();
    }

    private static string NormalizeObjectPath(string href)
    {
        string path = href.Trim();
        if (path.StartsWith("./", StringComparison.Ordinal))
            path = path.Substring(2);
        if (!path.EndsWith("/", StringComparison.Ordinal))
            path += "/";
        return path;
    }

    private static bool TryReadChartMetadata(
        OdfPackage package,
        string objectPath,
        out OdfChartType chartType,
        out string? title,
        out string? dataRange)
    {
        chartType = OdfChartType.Bar;
        title = null;
        dataRange = null;

        string contentPath = objectPath + "content.xml";
        if (!package.HasEntry(contentPath))
            return false;

        try
        {
            using Stream stream = package.GetEntryStream(contentPath);
            using var boundedStream = new MemoryStream();
            OdfBoundedStreamReader.CopyTo(
                stream,
                boundedStream,
                package.LoadOptions.MaxEntrySize,
                "Err_SpreadsheetDocumentEmbeddedChartReadEngine_ChartXmlSizeLimitExceeded");
            string xml = Encoding.UTF8.GetString(boundedStream.GetBuffer(), 0, (int)boundedStream.Length);

            using var reader = XmlReader.Create(new StringReader(xml), new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = package.LoadOptions.MaxEntrySize,
            });

            bool chartFound = false;
            List<OdfCellRange> ranges = [];
            while (reader.Read())
            {
                if (reader.NodeType is not XmlNodeType.Element)
                {
                    continue;
                }

                if (reader.LocalName == "chart" && reader.NamespaceURI == OdfNamespaces.Chart)
                {
                    chartFound = true;
                    string? chartClass = reader.GetAttribute("class", OdfNamespaces.Chart);
                    chartType = ParseChartType(chartClass);
                    dataRange = reader.GetAttribute("cell-range-address", OdfNamespaces.Table);
                }

                AddRange(reader.GetAttribute("values-cell-range-address", OdfNamespaces.Chart), ranges);
                AddRange(reader.GetAttribute("label-cell-address", OdfNamespaces.Chart), ranges);
                if (reader.NamespaceURI == OdfNamespaces.Chart && reader.LocalName is "categories" or "domain")
                {
                    AddRange(reader.GetAttribute("cell-range-address", OdfNamespaces.Table), ranges);
                }
            }

            dataRange ??= CombineRanges(ranges);

            int titleStart = xml.IndexOf("<text:p>", StringComparison.Ordinal);
            if (titleStart >= 0)
            {
                titleStart += "<text:p>".Length;
                int titleEnd = xml.IndexOf("</text:p>", titleStart, StringComparison.Ordinal);
                if (titleEnd > titleStart)
                    title = xml.Substring(titleStart, titleEnd - titleStart);
            }

            return chartFound;
        }
        catch
        {
            return false;
        }
    }

    private static void AddRange(string? address, List<OdfCellRange> ranges)
    {
        if (!string.IsNullOrWhiteSpace(address) && OdfCellRange.TryParse(address!, out OdfCellRange range))
        {
            ranges.Add(range);
        }
    }

    private static string? CombineRanges(List<OdfCellRange> ranges)
    {
        if (ranges.Count == 0)
        {
            return null;
        }

        string? sheetName = ranges[0].StartAddress.SheetName ?? ranges[0].EndAddress.SheetName;
        int minRow = int.MaxValue;
        int minColumn = int.MaxValue;
        int maxRow = int.MinValue;
        int maxColumn = int.MinValue;
        foreach (OdfCellRange range in ranges)
        {
            minRow = Math.Min(minRow, Math.Min(range.StartAddress.Row, range.EndAddress.Row));
            minColumn = Math.Min(minColumn, Math.Min(range.StartAddress.Column, range.EndAddress.Column));
            maxRow = Math.Max(maxRow, Math.Max(range.StartAddress.Row, range.EndAddress.Row));
            maxColumn = Math.Max(maxColumn, Math.Max(range.StartAddress.Column, range.EndAddress.Column));
        }

        return new OdfCellRange(minRow, minColumn, maxRow, maxColumn, sheetName).ToOdfString(false);
    }

    private static OdfChartType ParseChartType(string? chartClass) => chartClass switch
    {
        "chart:line" => OdfChartType.Line,
        "chart:circle" or "chart:pie" => OdfChartType.Pie,
        "chart:area" => OdfChartType.Area,
        "chart:scatter" => OdfChartType.Scatter,
        "chart:bubble" => OdfChartType.Bubble,
        "chart:ring" => OdfChartType.Ring,
        "chart:radar" => OdfChartType.Radar,
        "chart:stock" => OdfChartType.Stock,
        _ => OdfChartType.Bar,
    };
}
