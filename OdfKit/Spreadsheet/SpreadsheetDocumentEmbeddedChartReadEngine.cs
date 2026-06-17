using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using OdfKit.Chart;
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
            string xml = new StreamReader(stream, Encoding.UTF8).ReadToEnd();

            using var reader = XmlReader.Create(new StringReader(xml), new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
            });

            while (reader.Read())
            {
                if (reader.NodeType is not XmlNodeType.Element ||
                    reader.LocalName is not "chart" ||
                    reader.NamespaceURI != OdfNamespaces.Chart)
                    continue;

                string? chartClass = reader.GetAttribute("class", OdfNamespaces.Chart);
                chartType = ParseChartType(chartClass);
                dataRange = reader.GetAttribute("cell-range-address", OdfNamespaces.Table);
                break;
            }

            int titleStart = xml.IndexOf("<text:p>", StringComparison.Ordinal);
            if (titleStart >= 0)
            {
                titleStart += "<text:p>".Length;
                int titleEnd = xml.IndexOf("</text:p>", titleStart, StringComparison.Ordinal);
                if (titleEnd > titleStart)
                    title = xml.Substring(titleStart, titleEnd - titleStart);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static OdfChartType ParseChartType(string? chartClass) => chartClass switch
    {
        "chart:line" => OdfChartType.Line,
        "chart:pie" => OdfChartType.Pie,
        "chart:area" => OdfChartType.Area,
        "chart:scatter" => OdfChartType.Scatter,
        "chart:bubble" => OdfChartType.Bubble,
        _ => OdfChartType.Bar,
    };
}
