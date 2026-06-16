using OdfKit.Chart;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.Spreadsheet;

/// <summary>
/// 工作表圖表插入引擎（內部協作者）。
/// </summary>
internal static class OdfTableSheetChartEngine
{
    /// <summary>
    /// 在工作表中插入與儲存格範圍資料繫結的圖表。
    /// </summary>
    internal static OdfChartDocument InsertChart(
        OdfTableSheetMutationContext context,
        OdfCellRange dataRange,
        OdfChartType chartType,
        OdfLength? x,
        OdfLength? y,
        OdfLength? width,
        OdfLength? height,
        bool firstRowAsHeader,
        bool firstColumnAsLabel)
    {
        string xStr = (x ?? OdfLength.FromCentimeters(1)).ToString();
        string yStr = (y ?? OdfLength.FromCentimeters(1)).ToString();
        string wStr = (width ?? OdfLength.FromCentimeters(12)).ToString();
        string hStr = (height ?? OdfLength.FromCentimeters(7)).ToString();

        int objectIndex = 1;
        while (context.Document.Package.HasEntry($"Object {objectIndex}/content.xml"))
            objectIndex++;
        string objectName = $"Object {objectIndex}";
        string objectDir = $"{objectName}/";

        OdfNode shapesNode = FindOrCreateShapesNode(context.TableNode);
        var frame = new OdfNode(OdfNodeType.Element, "frame", OdfNamespaces.Draw, "draw");
        frame.SetAttribute("z-index", OdfNamespaces.Draw, "0", "draw");
        frame.SetAttribute("width", OdfNamespaces.Svg, wStr, "svg");
        frame.SetAttribute("height", OdfNamespaces.Svg, hStr, "svg");
        frame.SetAttribute("x", OdfNamespaces.Svg, xStr, "svg");
        frame.SetAttribute("y", OdfNamespaces.Svg, yStr, "svg");

        string anchorAddr = new OdfCellAddress(
            dataRange.StartAddress.Row, dataRange.StartAddress.Column).ToOdfString(false);
        frame.SetAttribute("start-cell-address", OdfNamespaces.Table, anchorAddr, "table");
        frame.SetAttribute("end-cell-address", OdfNamespaces.Table, anchorAddr, "table");
        frame.SetAttribute("start-x", OdfNamespaces.Table, xStr, "table");
        frame.SetAttribute("start-y", OdfNamespaces.Table, yStr, "table");
        frame.SetAttribute("end-x", OdfNamespaces.Table, wStr, "table");
        frame.SetAttribute("end-y", OdfNamespaces.Table, hStr, "table");

        var objectNode = new OdfNode(OdfNodeType.Element, "object", OdfNamespaces.Draw, "draw");
        objectNode.SetAttribute("href", OdfNamespaces.XLink, $"./{objectName}", "xlink");
        objectNode.SetAttribute("type", OdfNamespaces.XLink, "simple", "xlink");
        objectNode.SetAttribute("show", OdfNamespaces.XLink, "embed", "xlink");
        objectNode.SetAttribute("actuate", OdfNamespaces.XLink, "onLoad", "xlink");
        frame.AppendChild(objectNode);
        shapesNode.AppendChild(frame);

        byte[] mimeBytes = System.Text.Encoding.UTF8.GetBytes("application/vnd.oasis.opendocument.chart");
        context.Document.Package.WriteEntry($"{objectDir}mimetype", mimeBytes, string.Empty);

        var chartDoc = new OdfChartDocument(context.Document.Package, objectDir);

        chartDoc.ChartClass = chartType switch
        {
            OdfChartType.Line => "chart:line",
            OdfChartType.Pie => "chart:pie",
            OdfChartType.Area => "chart:area",
            OdfChartType.Scatter => "chart:scatter",
            OdfChartType.Bubble => "chart:bubble",
            _ => "chart:bar"
        };
        chartDoc.SetDataRange(context.SheetName, dataRange, firstRowAsHeader, firstColumnAsLabel);
        chartDoc.Save();

        return chartDoc;
    }

    private static OdfNode FindOrCreateShapesNode(OdfNode tableNode)
    {
        foreach (var child in tableNode.Children)
        {
            if (child.LocalName == "shapes" && child.NamespaceUri == OdfNamespaces.Table)
                return child;
        }
        var shapesNode = new OdfNode(OdfNodeType.Element, "shapes", OdfNamespaces.Table, "table");
        tableNode.AppendChild(shapesNode);
        return shapesNode;
    }
}
