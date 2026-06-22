using System.Text;
using System;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Spreadsheet;
using OdfKit.Styles;

namespace OdfKit.Text;

public partial class TextDocument
{
    /// <summary>
    /// 在指定的段落中新增影像框架。
    /// </summary>
    /// <param name="paragraph">目標段落</param>
    /// <param name="imageBytes">影像的二進位位元組</param>
    /// <param name="width">影像寬度</param>
    /// <param name="height">影像高度</param>
    /// <param name="name">影像名稱</param>
    /// <returns>代表新建影像的 OdfImage 物件</returns>
    public OdfImage AddImageFrame(OdfParagraph paragraph, byte[] imageBytes, OdfLength width, OdfLength height, string? name = null)
    {
        if (paragraph is null)
            throw new ArgumentNullException(nameof(paragraph));
        if (imageBytes is null)
            throw new ArgumentNullException(nameof(imageBytes));

        var media = new OdfMediaManager(Package);
        string packagePath = media.AddImage(imageBytes, name);

        return AddImage(paragraph, packagePath, width, height, name);
    }

    /// <summary>
    /// 在指定段落中插入嵌入式圖表。
    /// </summary>
    /// <param name="paragraph">目標段落</param>
    /// <param name="chart">圖表設定定義</param>
    /// <param name="width">圖表顯示寬度</param>
    /// <param name="height">圖表顯示高度</param>
    /// <returns>代表圖表物件的 OdfNode 節點</returns>
    public OdfNode AddChart(OdfParagraph paragraph, OdfChartDefinition chart, OdfLength width, OdfLength height)
    {
        if (paragraph is null)
            throw new ArgumentNullException(nameof(paragraph));
        if (chart is null)
            throw new ArgumentNullException(nameof(chart));

        // 1. 計算唯一的 Object 名稱
        int objectIndex = 1;
        while (Package.HasEntry($"Object {objectIndex}/content.xml"))
        {
            objectIndex++;
        }
        string objectName = $"Object {objectIndex}";
        string objectDir = $"{objectName}/";

        // 2. 在段落中建立 draw:frame 與 draw:object
        var frameNode = OdfNodeFactory.CreateElement("frame", OdfNamespaces.Draw, "draw");
        frameNode.SetAttribute("anchor-type", OdfNamespaces.Text, "paragraph", "text");
        frameNode.SetAttribute("width", OdfNamespaces.Svg, width.ToString(), "svg");
        frameNode.SetAttribute("height", OdfNamespaces.Svg, height.ToString(), "svg");

        var objectNode = OdfNodeFactory.CreateElement("object", OdfNamespaces.Draw, "draw");
        objectNode.SetAttribute("href", OdfNamespaces.XLink, $"./{objectName}", "xlink");
        objectNode.SetAttribute("type", OdfNamespaces.XLink, "simple", "xlink");
        objectNode.SetAttribute("show", OdfNamespaces.XLink, "embed", "xlink");
        objectNode.SetAttribute("actuate", OdfNamespaces.XLink, "onLoad", "xlink");

        frameNode.AppendChild(objectNode);
        paragraph.Node.AppendChild(frameNode);

        // 3. 建立子封裝中的檔案
        byte[] mimeBytes = Encoding.UTF8.GetBytes("application/vnd.oasis.opendocument.chart");
        Package.WriteEntry($"{objectDir}mimetype", mimeBytes, string.Empty);

        string stylesXml = "<?xml version=\"1.0\" encoding=\"utf-8\"?><office:document-styles xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:style=\"urn:oasis:names:tc:opendocument:xmlns:style:1.0\" office:version=\"1.3\"><office:styles/><office:automatic-styles/><office:master-styles/></office:document-styles>";
        Package.WriteEntry($"{objectDir}styles.xml", Encoding.UTF8.GetBytes(stylesXml), "text/xml");

        string chartClass = chart.ChartType switch
        {
            OdfChartType.Line => "chart:line",
            OdfChartType.Pie => "chart:pie",
            OdfChartType.Area => "chart:area",
            OdfChartType.Scatter => "chart:scatter",
            OdfChartType.Bubble => "chart:bubble",
            _ => "chart:bar"
        };

        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        sb.Append("<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:chart=\"urn:oasis:names:tc:opendocument:xmlns:chart:1.0\" xmlns:style=\"urn:oasis:names:tc:opendocument:xmlns:style:1.0\" xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\" xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\" xmlns:fo=\"urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" office:version=\"1.3\">");
        sb.Append("<office:body><office:chart>");

        string dataRangeStr = chart.DataRange.ToOdfString(false);
        sb.Append($"<chart:chart chart:class=\"{chartClass}\"");
        if (!string.IsNullOrEmpty(dataRangeStr))
        {
            sb.Append($" table:cell-range-address=\"{dataRangeStr}\"");
        }
        if (chart.HasLegend)
        {
            sb.Append(" chart:legend-position=\"end\"");
        }
        sb.Append(">");

        if (!string.IsNullOrEmpty(chart.Title))
        {
            sb.Append("<chart:title><text:p>");
            sb.Append(System.Security.SecurityElement.Escape(chart.Title));
            sb.Append("</text:p></chart:title>");
        }

        sb.Append("<chart:plot-area chart:data-source-has-labels=\"both\">");
        if (chart.DataRange != default)
        {
            AppendChartSeriesXml(sb, chart, chartClass);
        }
        sb.Append("<chart:axis chart:dimension=\"x\" chart:name=\"primary-x\"/>");
        sb.Append("<chart:axis chart:dimension=\"y\" chart:name=\"primary-y\"/>");
        sb.Append("</chart:plot-area>");

        if (chart.HasLegend)
        {
            sb.Append("<chart:legend chart:legend-position=\"end\"/>");
        }

        if (chart.DataRange != default)
        {
            sb.Append("<table:table table:name=\"LocalTable\">");
            int rows = Math.Abs(chart.DataRange.EndAddress.Row - chart.DataRange.StartAddress.Row) + 1;
            int cols = Math.Abs(chart.DataRange.EndAddress.Column - chart.DataRange.StartAddress.Column) + 1;
            for (int r = 0; r < rows; r++)
            {
                sb.Append("<table:table-row>");
                for (int c = 0; c < cols; c++)
                {
                    sb.Append("<table:table-cell office:value-type=\"float\" office:value=\"10.0\"><text:p>10.0</text:p></table:table-cell>");
                }
                sb.Append("</table:table-row>");
            }
            sb.Append("</table:table>");
        }

        sb.Append("</chart:chart></office:chart></office:body></office:document-content>");

        Package.WriteEntry($"{objectDir}content.xml", Encoding.UTF8.GetBytes(sb.ToString()), "text/xml");

        return frameNode;
    }

    private static void AppendChartSeriesXml(StringBuilder sb, OdfChartDefinition chart, string chartClass)
    {
        OdfCellRange range = chart.DataRange;
        int minRow = Math.Min(range.StartAddress.Row, range.EndAddress.Row);
        int maxRow = Math.Max(range.StartAddress.Row, range.EndAddress.Row);
        int minColumn = Math.Min(range.StartAddress.Column, range.EndAddress.Column);
        int maxColumn = Math.Max(range.StartAddress.Column, range.EndAddress.Column);
        string? rangeSheetName = range.StartAddress.SheetName ?? range.EndAddress.SheetName;
        if (maxRow <= minRow || maxColumn <= minColumn)
        {
            return;
        }

        string labelAddress = new OdfCellAddress(minRow, minColumn + 1, rangeSheetName).ToOdfString(false);
        string categoryRange = ToFullOdfRange(rangeSheetName, minRow + 1, minColumn, maxRow, minColumn);
        string valueRange = ToFullOdfRange(rangeSheetName, minRow + 1, minColumn + 1, maxRow, minColumn + 1);
        sb.Append("<chart:series chart:class=\"");
        sb.Append(System.Security.SecurityElement.Escape(chartClass));
        sb.Append("\" chart:label-cell-address=\"");
        sb.Append(System.Security.SecurityElement.Escape(labelAddress));
        sb.Append("\" chart:values-cell-range-address=\"");
        sb.Append(System.Security.SecurityElement.Escape(valueRange));
        sb.Append("\"><chart:domain table:cell-range-address=\"");
        sb.Append(System.Security.SecurityElement.Escape(categoryRange));
        sb.Append("\"/></chart:series>");
    }

    private static string ToFullOdfRange(string? sheetName, int startRow, int startColumn, int endRow, int endColumn)
    {
        string start = new OdfCellAddress(startRow, startColumn, sheetName).ToOdfString(false);
        string end = new OdfCellAddress(endRow, endColumn, sheetName).ToOdfString(false);
        return start + ":" + end;
    }
}
