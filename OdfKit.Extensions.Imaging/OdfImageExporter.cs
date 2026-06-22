using System;
using System.IO;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Spreadsheet;
using SkiaSharp;
namespace OdfKit.Export;

/// <summary>
/// 將 SpreadsheetDocument 的工作表格線渲染為點陣圖影像的工具類別。
/// </summary>
public static class OdfImageExporter
{
    /// <summary>
    /// 將工作表格線渲染並寫入 PNG 資料流。
    /// </summary>
    /// <param name="sheet">來源工作表。</param>
    /// <param name="pngStream">目標 PNG 資料流。</param>
    /// <param name="options">影像匯出選項；若為 null 則使用預設值。</param>
    /// <exception cref="ArgumentNullException">當任一必要參數為 null 時拋出。</exception>
    public static void ExportToPng(OdfTableSheet sheet, Stream pngStream, OdfImageExportOptions? options = null)
    {
        if (sheet is null)
            throw new ArgumentNullException(nameof(sheet));
        if (pngStream is null)
            throw new ArgumentNullException(nameof(pngStream));
        Export(sheet, pngStream, SKEncodedImageFormat.Png, 100, options);
    }

    /// <summary>
    /// 將工作表格線渲染並寫入 JPEG 資料流。
    /// </summary>
    /// <param name="sheet">來源工作表。</param>
    /// <param name="jpegStream">目標 JPEG 資料流。</param>
    /// <param name="quality">JPEG 壓縮品質，範圍為 1 至 100，預設為 90。</param>
    /// <param name="options">影像匯出選項；若為 null 則使用預設值。</param>
    public static void ExportToJpeg(OdfTableSheet sheet, Stream jpegStream, int quality = 90, OdfImageExportOptions? options = null)
    {
        if (sheet is null)
            throw new ArgumentNullException(nameof(sheet));
        if (jpegStream is null)
            throw new ArgumentNullException(nameof(jpegStream));
        if (quality < 1 || quality > 100)
            throw new ArgumentOutOfRangeException(nameof(quality), OdfLocalizer.GetMessage("Err_OdfImageExporter_QualityValueBetween1"));
        Export(sheet, jpegStream, SKEncodedImageFormat.Jpeg, quality, options);
    }

    private static void Export(OdfTableSheet sheet, Stream stream, SKEncodedImageFormat format, int quality, OdfImageExportOptions? options)
    {
        options ??= new OdfImageExportOptions();
        int cols = options.ColumnCount;
        int rows = options.RowCount;
        int colWidth = options.CellWidthPx;
        int rowHeight = options.CellHeightPx;
        int width = cols * colWidth + 1;
        int height = rows * rowHeight + 1;

        var imageInfo = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(imageInfo);
        var canvas = surface.Canvas;

        canvas.Clear(SKColors.White);

        using var gridPaint = new SKPaint
        {
            Color = new SKColor(0xCC, 0xCC, 0xCC),
            StrokeWidth = 1,
            IsStroke = true,
            IsAntialias = false
        };

        using var textPaint = new SKPaint
        {
            Color = SKColors.Black,
            IsAntialias = true
        };

        using var font = new SKFont(SKTypeface.Default, options.FontSizePx);

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                float x = c * colWidth;
                float y = r * rowHeight;
                canvas.DrawRect(x, y, colWidth, rowHeight, gridPaint);

                var cellNode = sheet.TryGetCellNode(r, c);
                if (cellNode is not null)
                {
                    string? text = TryGetCellDisplayText(cellNode);
                    if (!string.IsNullOrEmpty(text))
                    {
                        canvas.DrawText(text, x + 3, y + rowHeight - 4, font, textPaint);
                    }
                }
            }
        }

        using var image = surface.Snapshot();
        using var data = image.Encode(format, quality);
        data.SaveTo(stream);
    }

    private static string? TryGetCellDisplayText(OdfNode cellNode)
    {
        string? valueType = cellNode.GetAttribute("value-type", OdfNamespaces.Office);
        if (valueType is "float")
        {
            return cellNode.GetAttribute("value", OdfNamespaces.Office);
        }

        if (valueType is "boolean")
        {
            return cellNode.GetAttribute("boolean-value", OdfNamespaces.Office);
        }

        if (valueType is "date")
        {
            return cellNode.GetAttribute("date-value", OdfNamespaces.Office);
        }

        foreach (var child in cellNode.Children)
        {
            if (child.LocalName == "p" && child.NamespaceUri == OdfNamespaces.Text)
            {
                return child.TextContent;
            }
        }

        string? text = cellNode.TextContent;
        return string.IsNullOrEmpty(text) ? null : text;
    }
}
