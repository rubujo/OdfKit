using System.Text;
using System;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Text;
using OdfKit.Styles;

using OdfKit.Compliance;
namespace OdfKit.Spreadsheet;

public partial class SpreadsheetDocument
{
    /// <summary>
    /// 在指定工作表的儲存格位置插入影像框架。
    /// </summary>
    /// <param name="sheetName">工作表名稱</param>
    /// <param name="anchor">影像左上角錨定的儲存格位置</param>
    /// <param name="imageBytes">影像的二進位資料位元組</param>
    /// <param name="width">影像寬度</param>
    /// <param name="height">影像高度</param>
    /// <param name="name">影像名稱</param>
    /// <returns>代表新建立影像框架的 OdfImage 物件</returns>
    public OdfImage AddImageFrame(string sheetName, OdfCellAddress anchor, byte[] imageBytes, OdfLength width, OdfLength height, string? name = null)
    {
        if (string.IsNullOrEmpty(sheetName))
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_SpreadsheetDocument_WorksheetCannotBeEmpty_4"), nameof(sheetName));
        if (imageBytes is null)
            throw new ArgumentNullException(nameof(imageBytes));

        var sheet = GetSheet(sheetName);
        if (sheet is null)
            throw new KeyNotFoundException(OdfLocalizer.GetMessage("Err_SpreadsheetDocument_SheetNamedCannotFound_4", sheetName));

        // 1. 尋找或建立 table:shapes
        OdfNode? shapesNode = null;
        foreach (var child in sheet.TableNode.Children)
        {
            if (child.LocalName == "shapes" && child.NamespaceUri == OdfNamespaces.Table)
            {
                shapesNode = child;
                break;
            }
        }
        if (shapesNode is null)
        {
            shapesNode = new OdfNode(OdfNodeType.Element, "shapes", OdfNamespaces.Table, "table");
            sheet.TableNode.AppendChild(shapesNode);
        }

        // 2. 新增影像到 Package 中
        var media = new OdfMediaManager(Package);
        string packagePath = media.AddImage(imageBytes, name);

        // 3. 建立 draw:frame 與 draw:image 節點
        var frameNode = new OdfNode(OdfNodeType.Element, "frame", OdfNamespaces.Draw, "draw");
        frameNode.SetAttribute("z-index", OdfNamespaces.Draw, "0", "draw");
        frameNode.SetAttribute("width", OdfNamespaces.Svg, width.ToString(), "svg");
        frameNode.SetAttribute("height", OdfNamespaces.Svg, height.ToString(), "svg");
        frameNode.SetAttribute("x", OdfNamespaces.Svg, "1cm", "svg");
        frameNode.SetAttribute("y", OdfNamespaces.Svg, "1cm", "svg");

        string anchorOdf = anchor.ToOdfString(false);
        frameNode.SetAttribute("start-cell-address", OdfNamespaces.Table, anchorOdf, "table");
        frameNode.SetAttribute("end-cell-address", OdfNamespaces.Table, anchorOdf, "table");
        frameNode.SetAttribute("start-x", OdfNamespaces.Table, "0cm", "table");
        frameNode.SetAttribute("start-y", OdfNamespaces.Table, "0cm", "table");
        frameNode.SetAttribute("end-x", OdfNamespaces.Table, width.ToString(), "table");
        frameNode.SetAttribute("end-y", OdfNamespaces.Table, height.ToString(), "table");
        if (name is not null)
        {
            frameNode.SetAttribute("name", OdfNamespaces.Draw, name, "draw");
        }

        var imageNode = new OdfNode(OdfNodeType.Element, "image", OdfNamespaces.Draw, "draw");
        imageNode.SetAttribute("href", OdfNamespaces.XLink, packagePath, "xlink");
        imageNode.SetAttribute("type", OdfNamespaces.XLink, "simple", "xlink");
        imageNode.SetAttribute("show", OdfNamespaces.XLink, "embed", "xlink");
        imageNode.SetAttribute("actuate", OdfNamespaces.XLink, "onLoad", "xlink");

        frameNode.AppendChild(imageNode);
        shapesNode.AppendChild(frameNode);

        return new OdfImage(frameNode, imageNode, this);
    }

    /// <summary>
    /// 在指定工作表的儲存格位置插入圖表（支援自訂寬高）。
    /// </summary>
    /// <param name="sheetName">工作表名稱</param>
    /// <param name="anchor">圖表左上角錨定的儲存格位置</param>
    /// <param name="chart">圖表設定物件</param>
    /// <param name="width">圖表寬度</param>
    /// <param name="height">圖表高度</param>
    /// <returns>代表圖表物件的 OdfNode 節點</returns>
    public OdfNode AddChart(string sheetName, OdfCellAddress anchor, OdfChartDefinition chart, OdfLength width, OdfLength height)
    {
        if (string.IsNullOrEmpty(sheetName))
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_SpreadsheetDocument_WorksheetCannotBeEmpty_4"), nameof(sheetName));
        if (chart is null)
            throw new ArgumentNullException(nameof(chart));

        var sheet = GetSheet(sheetName);
        if (sheet is null)
            throw new KeyNotFoundException(OdfLocalizer.GetMessage("Err_SpreadsheetDocument_SheetNamedCannotFound_4", sheetName));

        // 1. 尋找或建立 table:shapes
        OdfNode? shapesNode = null;
        foreach (var child in sheet.TableNode.Children)
        {
            if (child.LocalName == "shapes" && child.NamespaceUri == OdfNamespaces.Table)
            {
                shapesNode = child;
                break;
            }
        }
        if (shapesNode is null)
        {
            shapesNode = new OdfNode(OdfNodeType.Element, "shapes", OdfNamespaces.Table, "table");
            sheet.TableNode.AppendChild(shapesNode);
        }

        // 2. 計算唯一的 Object 名稱
        int objectIndex = 1;
        while (Package.HasEntry($"Object {objectIndex}/content.xml"))
        {
            objectIndex++;
        }
        string objectName = $"Object {objectIndex}";
        string objectDir = $"{objectName}/";

        // 3. 在 table:shapes 底下建立 draw:frame 與 draw:object
        var frameNode = new OdfNode(OdfNodeType.Element, "frame", OdfNamespaces.Draw, "draw");
        frameNode.SetAttribute("z-index", OdfNamespaces.Draw, "0", "draw");
        frameNode.SetAttribute("width", OdfNamespaces.Svg, width.ToString(), "svg");
        frameNode.SetAttribute("height", OdfNamespaces.Svg, height.ToString(), "svg");
        frameNode.SetAttribute("x", OdfNamespaces.Svg, "1cm", "svg");
        frameNode.SetAttribute("y", OdfNamespaces.Svg, "1cm", "svg");

        string anchorOdf = anchor.ToOdfString(false);
        frameNode.SetAttribute("start-cell-address", OdfNamespaces.Table, anchorOdf, "table");
        frameNode.SetAttribute("end-cell-address", OdfNamespaces.Table, anchorOdf, "table");
        frameNode.SetAttribute("start-x", OdfNamespaces.Table, "0cm", "table");
        frameNode.SetAttribute("start-y", OdfNamespaces.Table, "0cm", "table");
        frameNode.SetAttribute("end-x", OdfNamespaces.Table, width.ToString(), "table");
        frameNode.SetAttribute("end-y", OdfNamespaces.Table, height.ToString(), "table");

        var objectNode = new OdfNode(OdfNodeType.Element, "object", OdfNamespaces.Draw, "draw");
        objectNode.SetAttribute("href", OdfNamespaces.XLink, $"./{objectName}", "xlink");
        objectNode.SetAttribute("type", OdfNamespaces.XLink, "simple", "xlink");
        objectNode.SetAttribute("show", OdfNamespaces.XLink, "embed", "xlink");
        objectNode.SetAttribute("actuate", OdfNamespaces.XLink, "onLoad", "xlink");

        frameNode.AppendChild(objectNode);
        shapesNode.AppendChild(frameNode);

        // 4. 建立子封裝中的檔案
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

        string dataRangeStr = chart.DataRange.ToOdfString(false);

        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        sb.Append("<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:chart=\"urn:oasis:names:tc:opendocument:xmlns:chart:1.0\" xmlns:style=\"urn:oasis:names:tc:opendocument:xmlns:style:1.0\" xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\" xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\" xmlns:fo=\"urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" office:version=\"1.3\">");
        sb.Append("<office:body><office:chart>");
        sb.Append($"<chart:chart chart:class=\"{chartClass}\" table:cell-range-address=\"{dataRangeStr}\"");
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

        OdfCellRange range = chart.DataRange;
        int minRow = Math.Min(range.StartAddress.Row, range.EndAddress.Row);
        int maxRow = Math.Max(range.StartAddress.Row, range.EndAddress.Row);
        int minColumn = Math.Min(range.StartAddress.Column, range.EndAddress.Column);
        int maxColumn = Math.Max(range.StartAddress.Column, range.EndAddress.Column);
        string? rangeSheetName = range.StartAddress.SheetName ?? range.EndAddress.SheetName;
        if (maxRow > minRow && maxColumn > minColumn)
        {
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

        sb.Append("<chart:axis chart:dimension=\"x\" chart:name=\"primary-x\"/>");
        sb.Append("<chart:axis chart:dimension=\"y\" chart:name=\"primary-y\"/>");
        sb.Append("</chart:plot-area>");

        if (chart.HasLegend)
        {
            sb.Append("<chart:legend chart:legend-position=\"end\"/>");
        }

        sb.Append("</chart:chart></office:chart></office:body></office:document-content>");

        Package.WriteEntry($"{objectDir}content.xml", Encoding.UTF8.GetBytes(sb.ToString()), "text/xml");

        return frameNode;
    }
}
