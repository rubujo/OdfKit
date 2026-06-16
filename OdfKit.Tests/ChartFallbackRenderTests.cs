using System.IO;
using System.Linq;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Extensions.Imaging;
using OdfKit.Spreadsheet;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 測試試算表圖表渲染 Fallback 靜態影像的整合測試。
/// </summary>
public class ChartFallbackRenderTests
{
    /// <summary>
    /// 驗證對包含圖表的工作表呼交 RenderChartsToFallbackImages 時，能成功透過 ScottPlot 產生 PNG 影像，
    /// 寫入 OdfPackage 中，並正確更新 XML 以加入 draw:image 節點且不損壞既有的 draw:object 節點。
    /// </summary>
    [Fact]
    public void RenderChartsToFallbackImages_GeneratesPngAndUpdatesXml()
    {
        // 1. 建立試算表並填入資料
        using var doc = SpreadsheetDocument.Create();
        var sheet = doc.Worksheets.Add("DataSheet");

        sheet.Cells["A1"].CellValue = "季度";
        sheet.Cells["B1"].CellValue = "銷售額";
        sheet.Cells["A2"].CellValue = "第一季";
        sheet.Cells["B2"].CellValue = 120.5d;
        sheet.Cells["A3"].CellValue = "第二季";
        sheet.Cells["B3"].CellValue = 250d;
        sheet.Cells["A4"].CellValue = "第三季";
        sheet.Cells["B4"].CellValue = 180.2d;
        sheet.Cells["A5"].CellValue = "第四季";
        sheet.Cells["B5"].CellValue = 300.9d;

        // 2. 插入一個長條圖
        var dataRange = new OdfCellRange(0, 0, 4, 1);
        var chartDoc = sheet.InsertChart(dataRange, OdfChartType.Bar);
        chartDoc.ChartTitle = "季度銷售統計圖";

        // 3. 執行圖表渲染 fallback
        doc.RenderChartsToFallbackImages();

        // 4. 驗證 PNG 影像是否已寫入 package
        string fallbackPath = "Pictures/chart-fallback-Object 1.png";
        Assert.True(doc.Package.HasEntry(fallbackPath), "應該在 package 中生成 fallback PNG 影像。");

        using (var pngStream = doc.Package.GetEntryStream(fallbackPath))
        {
            Assert.True(pngStream.Length > 0, "產生的 PNG 影像檔案大小應該大於 0。");
        }

        // 5. 驗證 Sheet XML 結構中 draw:frame 下同時存在 draw:object 與 draw:image
        var frames = sheet.TableNode.Descendants()
            .Where(c => c.NodeType == OdfNodeType.Element &&
                        c.LocalName == "frame" &&
                        c.NamespaceUri == OdfNamespaces.Draw)
            .ToList();

        Assert.Single(frames);
        var frame = frames[0];

        // 驗證原本的 draw:object 還在
        var objNode = frame.Children.Find(c =>
            c.NodeType == OdfNodeType.Element &&
            c.LocalName == "object" &&
            c.NamespaceUri == OdfNamespaces.Draw);
        Assert.NotNull(objNode);
        Assert.Equal("./Object 1", objNode.GetAttribute("href", OdfNamespaces.XLink));

        // 驗證新產生的 draw:image
        var imgNode = frame.Children.Find(c =>
            c.NodeType == OdfNodeType.Element &&
            c.LocalName == "image" &&
            c.NamespaceUri == OdfNamespaces.Draw);
        Assert.NotNull(imgNode);
        Assert.Equal(fallbackPath, imgNode.GetAttribute("href", OdfNamespaces.XLink));
    }
}
