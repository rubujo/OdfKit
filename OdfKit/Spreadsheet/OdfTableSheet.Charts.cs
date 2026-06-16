using System;
using OdfKit.Chart;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.Spreadsheet;

public partial class OdfTableSheet
{
    #region 圖表


    /// <summary>
    /// 在此工作表中插入一個與儲存格範圍資料繫結的圖表。
    /// </summary>
    /// <param name="dataRange">資料繫結的儲存格範圍。</param>
    /// <param name="chartType">圖表類型，預設為條形圖。</param>
    /// <param name="x">圖表框左邊距，預設 1cm。</param>
    /// <param name="y">圖表框上邊距，預設 1cm。</param>
    /// <param name="width">圖表框寬度，預設 12cm。</param>
    /// <param name="height">圖表框高度，預設 7cm。</param>
    /// <param name="firstRowAsHeader">資料首列作為序列標題，預設 true。</param>
    /// <param name="firstColumnAsLabel">資料首欄作為 X 軸分類標籤，預設 true。</param>
    /// <returns>
    /// 可進一步設定的 <see cref="OdfChartDocument"/>。
    /// 呼叫端修改後須呼叫 <c>Save()</c> 並在父文件儲存前保持物件存活；
    /// 請勿對此物件呼叫 <c>Dispose()</c>（生命週期由父文件管理）。
    /// </returns>
    public OdfChartDocument InsertChart(
        OdfCellRange dataRange,
        OdfChartType chartType = OdfChartType.Bar,
        OdfLength? x = null,
        OdfLength? y = null,
        OdfLength? width = null,
        OdfLength? height = null,
        bool firstRowAsHeader = true,
        bool firstColumnAsLabel = true)
    {
        string xStr = (x ?? OdfLength.FromCentimeters(1)).ToString();
        string yStr = (y ?? OdfLength.FromCentimeters(1)).ToString();
        string wStr = (width ?? OdfLength.FromCentimeters(12)).ToString();
        string hStr = (height ?? OdfLength.FromCentimeters(7)).ToString();

        // 1. 唯一物件名稱
        int objectIndex = 1;
        while (_doc.Package.HasEntry($"Object {objectIndex}/content.xml"))
            objectIndex++;
        string objectName = $"Object {objectIndex}";
        string objectDir = $"{objectName}/";

        // 2. 建立 table:shapes > draw:frame > draw:object
        OdfNode shapesNode = FindOrCreateShapesNode();
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

        // 3. 寫入嵌入物件的 mimetype
        byte[] mimeBytes = System.Text.Encoding.UTF8.GetBytes("application/vnd.oasis.opendocument.chart");
        _doc.Package.WriteEntry($"{objectDir}mimetype", mimeBytes, string.Empty);

        // 4. 建立 OdfChartDocument（共享父套件；以預設 content.xml 起始）
        var chartDoc = new OdfChartDocument(_doc.Package, objectDir);

        // 5. 設定圖表類型與資料繫結
        chartDoc.ChartClass = chartType switch
        {
            OdfChartType.Line => "chart:line",
            OdfChartType.Pie => "chart:pie",
            OdfChartType.Area => "chart:area",
            OdfChartType.Scatter => "chart:scatter",
            OdfChartType.Bubble => "chart:bubble",
            _ => "chart:bar"
        };
        chartDoc.SetDataRange(Name, dataRange, firstRowAsHeader, firstColumnAsLabel);

        // 6. 將圖表 DOM 持久化至套件
        chartDoc.Save();

        return chartDoc;
    }

    private OdfNode FindOrCreateShapesNode()
    {
        foreach (var child in TableNode.Children)
        {
            if (child.LocalName == "shapes" && child.NamespaceUri == OdfNamespaces.Table)
                return child;
        }
        var shapesNode = new OdfNode(OdfNodeType.Element, "shapes", OdfNamespaces.Table, "table");
        TableNode.AppendChild(shapesNode);
        return shapesNode;
    }


    #endregion
}
