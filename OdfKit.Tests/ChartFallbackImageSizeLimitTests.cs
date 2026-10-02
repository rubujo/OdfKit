using System;
using System.IO;
using System.Linq;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Extensions.Imaging;
using OdfKit.Spreadsheet;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 回歸測試：圖表 fallback 影像的像素尺寸不得由文件（svg:width／svg:height）無限制決定。
/// 修正前，300 cm 的圖表框需約 6 秒與 550 MB，約 700 cm 起 Skia 拒絕並擲出 NullReferenceException。
/// </summary>
[Trait(TestCategories.Kind, TestCategories.Boundary)]
public sealed class ChartFallbackImageSizeLimitTests
{
    private const int MaxDimensionPx = 4096;

    private static (int Width, int Height) RenderWithFrameSize(string width, string height)
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.Worksheets.Add("DataSheet");
        sheet.Cells["A1"].CellValue = "Quarter";
        sheet.Cells["B1"].CellValue = "Sales";
        sheet.Cells["A2"].CellValue = "Q1";
        sheet.Cells["B2"].CellValue = 120.5d;
        sheet.Cells["A3"].CellValue = "Q2";
        sheet.Cells["B3"].CellValue = 250d;
        sheet.InsertChart(new OdfCellRange(0, 0, 2, 1), OdfChartType.Bar);

        foreach (OdfNode node in sheet.TableNode.Descendants())
        {
            if (node.NodeType == OdfNodeType.Element &&
                node.LocalName == "frame" &&
                node.NamespaceUri == OdfNamespaces.Draw)
            {
                node.SetAttribute("width", OdfNamespaces.Svg, width);
                node.SetAttribute("height", OdfNamespaces.Svg, height);
            }
        }

        document.RenderChartsToFallbackImages();

        using Stream png = document.Package.GetEntryStream("Pictures/chart-fallback-Object 1.png");
        byte[] header = new byte[24];
        int read = 0;
        while (read < header.Length)
        {
            int n = png.Read(header, read, header.Length - read);
            if (n == 0)
            {
                break;
            }

            read += n;
        }

        Assert.Equal(header.Length, read);
        int pngWidth = (header[16] << 24) | (header[17] << 16) | (header[18] << 8) | header[19];
        int pngHeight = (header[20] << 24) | (header[21] << 16) | (header[22] << 8) | header[23];
        return (pngWidth, pngHeight);
    }

    [Theory]
    [InlineData("300cm", "300cm")]
    [InlineData("3000cm", "3000cm")]
    [InlineData("1e30cm", "1e30cm")]
    public void RenderedImageIsClampedToMaximumDimension(string width, string height)
    {
        (int pngWidth, int pngHeight) = RenderWithFrameSize(width, height);

        Assert.Equal(MaxDimensionPx, pngWidth);
        Assert.Equal(MaxDimensionPx, pngHeight);
    }

    [Fact]
    public void RenderedImageWithNormalFrameSizeKeepsRequestedResolution()
    {
        (int pngWidth, int pngHeight) = RenderWithFrameSize("12cm", "7cm");

        Assert.InRange(pngWidth, 450, 456);
        Assert.InRange(pngHeight, 262, 268);
    }
}
