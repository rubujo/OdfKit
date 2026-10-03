using System;
using System.IO;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Spreadsheet;
using SkiaSharp;
namespace OdfKit.Export;

/// <summary>
/// Exports ODF visual content to raster images.
/// 將 SpreadsheetDocument 的工作表格線渲染為點陣圖影像的工具類別。
/// </summary>
public static class OdfImageExporter
{
    /// <summary>
    /// Exports the specified ODF content to PNG.
    /// 將工作表格線渲染並寫入 PNG 資料流。
    /// </summary>
    /// <param name="sheet">The value to use. / 來源工作表</param>
    /// <param name="pngStream">The source or target object. / 目標 PNG 資料流</param>
    /// <param name="options">The value to use. / 影像匯出選項；若為 null 則使用預設值</param>
    /// <exception cref="ArgumentNullException">Thrown when the documented condition occurs. / 當任一必要參數為 null 時拋出</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when an image export option is outside the supported rendering range. / 當影像匯出選項超出支援的轉譯範圍時擲出。</exception>
    public static void ExportToPng(OdfTableSheet sheet, Stream pngStream, OdfImageExportOptions? options = null)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(sheet, nameof(sheet));
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(pngStream, nameof(pngStream));
        Export(sheet, pngStream, SKEncodedImageFormat.Png, 100, options);
    }

    /// <summary>
    /// Exports the specified ODF content to JPEG.
    /// 將工作表格線渲染並寫入 JPEG 資料流。
    /// </summary>
    /// <param name="sheet">The value to use. / 來源工作表</param>
    /// <param name="jpegStream">The source or target object. / 目標 JPEG 資料流</param>
    /// <param name="quality">The numeric value. / JPEG 壓縮品質，範圍為 1 至 100，預設為 90</param>
    /// <param name="options">The value to use. / 影像匯出選項；若為 null 則使用預設值</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="quality"/> or an image export option is outside its supported range. / 當 <paramref name="quality"/> 或影像匯出選項超出其支援範圍時擲出。</exception>
    public static void ExportToJpeg(OdfTableSheet sheet, Stream jpegStream, int quality = 90, OdfImageExportOptions? options = null)
    {
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(sheet, nameof(sheet));
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(jpegStream, nameof(jpegStream));
        if (quality < 1 || quality > 100)
            throw new ArgumentOutOfRangeException(nameof(quality), OdfLocalizer.GetMessage("Err_OdfImageExporter_QualityValueBetween1"));
        Export(sheet, jpegStream, SKEncodedImageFormat.Jpeg, quality, options);
    }

    private static void Export(OdfTableSheet sheet, Stream stream, SKEncodedImageFormat format, int quality, OdfImageExportOptions? options)
    {
        options ??= new OdfImageExportOptions();
        ValidatePositive(options.ColumnCount, nameof(options), nameof(options.ColumnCount));
        ValidatePositive(options.RowCount, nameof(options), nameof(options.RowCount));
        ValidatePositive(options.CellWidthPx, nameof(options), nameof(options.CellWidthPx));
        ValidatePositive(options.CellHeightPx, nameof(options), nameof(options.CellHeightPx));
        if (!(options.FontSizePx > 0) || float.IsInfinity(options.FontSizePx))
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                OdfLocalizer.GetMessage(
                    "Err_OdfImageExporter_InvalidDimensions",
                    nameof(options.FontSizePx)));
        }

        int cols = options.ColumnCount;
        int rows = options.RowCount;
        int colWidth = options.CellWidthPx;
        int rowHeight = options.CellHeightPx;
        int width;
        int height;
        try
        {
            width = checked(cols * colWidth + 1);
            height = checked(rows * rowHeight + 1);
        }
        catch (OverflowException)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                OdfLocalizer.GetMessage("Err_OdfImageExporter_InvalidDimensions", nameof(options)));
        }

        var imageInfo = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using SKSurface surface = SKSurface.Create(imageInfo) ??
            throw new ArgumentOutOfRangeException(
                nameof(options),
                OdfLocalizer.GetMessage("Err_OdfImageExporter_InvalidDimensions", nameof(options)));
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
                        // 文字不得溢出到鄰格（與試算表軟體的顯示一致）。
                        canvas.Save();
                        canvas.ClipRect(new SKRect(x + 1, y + 1, x + colWidth, y + rowHeight));
                        DrawTextWithFallback(canvas, text!, x + 3, y + rowHeight - 4, font, textPaint);
                        canvas.Restore();
                    }
                }
            }
        }

        using var image = surface.Snapshot();
        using var data = image.Encode(format, quality);
        data.SaveTo(stream);
    }

    private static void ValidatePositive(int value, string parameterName, string optionName)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                OdfLocalizer.GetMessage("Err_OdfImageExporter_InvalidDimensions", optionName));
        }
    }

    // 預設字型沒有的字元（中日韓文字、增補平面字）會畫成方框；缺字的連續字元改用系統備援字型繪製。
    private static void DrawTextWithFallback(SKCanvas canvas, string text, float x, float y, SKFont font, SKPaint paint)
    {
        var run = new System.Text.StringBuilder();
        SKTypeface? runTypeface = null;
        bool ownsRunTypeface = false;
        float cursor = x;

        void Flush()
        {
            if (run.Length == 0)
            {
                return;
            }

            string chunk = run.ToString();
            if (runTypeface is null)
            {
                canvas.DrawText(chunk, cursor, y, SKTextAlign.Left, font, paint);
                cursor += font.MeasureText(chunk);
            }
            else
            {
                using var fallbackFont = new SKFont(runTypeface, font.Size);
                canvas.DrawText(chunk, cursor, y, SKTextAlign.Left, fallbackFont, paint);
                cursor += fallbackFont.MeasureText(chunk);
            }

            run.Clear();
            if (ownsRunTypeface)
            {
                runTypeface?.Dispose();
            }

            runTypeface = null;
            ownsRunTypeface = false;
        }

        int index = 0;
        while (index < text.Length)
        {
            int length = char.IsHighSurrogate(text[index]) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]) ? 2 : 1;
            string element = text.Substring(index, length);
            bool missing = (length == 2 || !char.IsSurrogate(text[index])) &&
                font.GetGlyphs(element)[0] == 0 &&
                !char.IsWhiteSpace(text[index]) &&
                !char.IsControl(text[index]);
            SKTypeface? substitute = null;
            if (missing)
            {
                substitute = SKFontManager.Default.MatchCharacter(
                    font.Typeface.FamilyName,
                    font.Typeface.FontWeight,
                    (int)SKFontStyleWidth.Normal,
                    font.Typeface.FontSlant,
                    null,
                    length == 2 ? char.ConvertToUtf32(text, index) : text[index]);
            }

            bool sameRun = run.Length > 0 &&
                ((substitute is null && runTypeface is null) ||
                 (substitute is not null && runTypeface is not null && substitute.FamilyName == runTypeface.FamilyName));
            if (run.Length > 0 && !sameRun)
            {
                Flush();
            }

            if (run.Length == 0)
            {
                runTypeface = substitute;
                ownsRunTypeface = substitute is not null;
            }
            else
            {
                substitute?.Dispose();
            }

            run.Append(element);
            index += length;
        }

        Flush();
    }

    private static string? TryGetCellDisplayText(OdfNode cellNode)
    {
        // 儲存格顯示的文字（含千分位、百分比、貨幣與日期格式）存在 text:p；原始值只在沒有顯示文字時才用。
        foreach (var paragraph in cellNode.Children)
        {
            if (paragraph.LocalName == "p" && paragraph.NamespaceUri == OdfNamespaces.Text &&
                !string.IsNullOrEmpty(paragraph.TextContent))
            {
                return paragraph.TextContent;
            }
        }

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
