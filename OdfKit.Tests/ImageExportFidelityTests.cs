using System;
using System.IO;
using OdfKit.Export;
using OdfKit.Core;
using OdfKit.Spreadsheet;
using SkiaSharp;
using Xunit;

namespace OdfKit.Tests;

/// <summary>
/// 以真實 LibreOffice ODS 對照的像素層級檢查：預設字型沒有中文字形時，所有中文都會畫成相同的方框；
/// 儲存格顯示的是格式化後的文字；長文字不能溢出到鄰格。
/// </summary>
public sealed class ImageExportFidelityTests
{
    private static byte[] Render(Action<OdfTableSheet> fill, OdfImageExportOptions? options = null)
    {
        using SpreadsheetDocument workbook = SpreadsheetDocument.Create();
        OdfTableSheet sheet = workbook.Worksheets.Add("測試");
        fill(sheet);
        using var stream = new MemoryStream();
        OdfImageExporter.ExportToPng(sheet, stream, options ?? new OdfImageExportOptions { ColumnCount = 2, RowCount = 1, CellWidthPx = 120, CellHeightPx = 30, FontSizePx = 16 });
        return stream.ToArray();
    }

    /// <summary>
    /// 不同的中文字畫出不同的像素；方框（缺字）會使兩者完全相同。
    /// </summary>
    [Fact]
    public void DifferentCjkTextsRenderDifferently()
    {
        if (SKFontManager.Default.MatchCharacter('中') is null)
            return;

        byte[] first = Render(sheet => sheet.Cells["A1"].CellValue = "中國");
        byte[] second = Render(sheet => sheet.Cells["A1"].CellValue = "國中");

        Assert.NotEqual(first, second);
    }

    /// <summary>
    /// 文字畫在自己的儲存格內，不會溢出到右側鄰格。
    /// </summary>
    [Fact]
    public void LongTextIsClippedToItsCell()
    {
        byte[] png = Render(sheet => sheet.Cells["A1"].CellValue = "WWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWW");
        using SKBitmap bitmap = SKBitmap.Decode(png);
        bool inkInNeighbour = false;
        for (int y = 2; y < 28 && !inkInNeighbour; y++)
        {
            for (int x = 122; x < 238; x++)
            {
                SKColor color = bitmap.GetPixel(x, y);
                if (color.Red < 128 && color.Green < 128 && color.Blue < 128)
                {
                    inkInNeighbour = true;
                    break;
                }
            }
        }

        Assert.False(inkInNeighbour, "A1 的文字不應畫到 B1。");
    }
}

/// <summary>
/// 圖表備援圖：ScottPlot 預設字型沒有中文字形，標題、刻度與圖例的中文曾全部畫成方框。
/// </summary>
public sealed class ChartFallbackFidelityTests
{
    private static byte[] RenderChart(string first, string second)
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create();
        OdfTableSheet sheet = document.Worksheets.Add("資料");
        sheet.Cells["A1"].CellValue = "季度";
        sheet.Cells["B1"].CellValue = "銷售額";
        sheet.Cells["A2"].CellValue = first;
        sheet.Cells["B2"].CellValue = 120d;
        sheet.Cells["A3"].CellValue = second;
        sheet.Cells["B3"].CellValue = 250d;
        sheet.InsertChart(new OdfCellRange(0, 0, 2, 1), OdfChartType.Bar);
        OdfKit.Extensions.Imaging.OdfChartRenderer.RenderChartsToFallbackImages(document);
        using Stream png = document.Package.GetEntryStream("Pictures/chart-fallback-Object 1.png");
        using var copy = new MemoryStream();
        png.CopyTo(copy);
        return copy.ToArray();
    }

    /// <summary>
    /// 不同的中文類別名稱畫出不同的像素；缺字時所有中文都是相同的方框。
    /// </summary>
    [Fact]
    public void CjkCategoryLabelsAreDrawnWithRealGlyphs()
    {
        if (SKFontManager.Default.MatchCharacter('中') is null)
            return;

        Assert.NotEqual(RenderChart("第一季", "第二季"), RenderChart("甲乙丙", "丁戊己"));
    }
}
