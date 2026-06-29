using System;
using System.IO;
using System.Runtime.InteropServices;
using HarfBuzzSharp;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;
using SkiaSharp;

namespace OdfKit.Extensions.Imaging;

/// <summary>
/// Provides APIs for odf text measurer.
/// 提供整合 HarfBuzzSharp 與 SkiaSharp 的跨平台文字物理寬度精確量測工具。
/// </summary>
public static class OdfTextMeasurer
{
    /// <summary>
    /// Provides measure width.
    /// 精確量測指定字型、大小與書寫模式下文字的物理寬度（回傳 <see cref="OdfLength"/> 封裝）。
    /// </summary>
    /// <param name="text">The text or value. / 要量測的文字內容</param>
    /// <param name="fontName">The name or identifier. / 字型名稱</param>
    /// <param name="fontSizePoints">The numeric value. / 字型大小（以點 Pt 為單位）</param>
    /// <param name="isBold">The value to use. / 是否為粗體</param>
    /// <param name="isItalic">The value to use. / 是否為斜體</param>
    /// <param name="writingMode">The value to use. / 書寫模式（橫書或直書）</param>
    /// <returns>The result. / 量測後的物理長度 <see cref="OdfLength"/></returns>
    public static OdfLength MeasureWidth(string text, string fontName, double fontSizePoints, bool isBold = false, bool isItalic = false, OdfWritingMode writingMode = OdfWritingMode.LrTb)
    {
        if (string.IsNullOrEmpty(text))
            return OdfLength.FromCentimeters(0);

        // 檢查是否含有高字面字元（第 2 字面或第 15/16 字面）
        bool hasSupplementary = false;
        for (int i = 0; i < text.Length; i++)
        {
            if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                int codePoint = char.ConvertToUtf32(text[i], text[i + 1]);
                int plane = codePoint >> 16;
                if (plane == 2 || plane == 3 || plane == 15 || plane == 16)
                {
                    hasSupplementary = true;
                    break;
                }
            }
        }

        if (hasSupplementary)
        {
            double totalCm = 0;
            var segments = OdfFontSegmenter.SegmentText(text, fontName);
            foreach (var (segText, font) in segments)
            {
                if (font != fontName)
                    OdfFontResolver.WarnIfUnresolvable(font, "CNS 11643 高位字面文字寬度量測");
                var width = MeasureWidthSingle(segText, font, fontSizePoints, isBold, isItalic, writingMode);
                totalCm += width.ToCentimeters();
            }
            return OdfLength.FromCentimeters(totalCm);
        }
        else
        {
            return MeasureWidthSingle(text, fontName, fontSizePoints, isBold, isItalic, writingMode);
        }
    }

    private static OdfLength MeasureWidthSingle(string text, string fontName, double fontSizePoints, bool isBold, bool isItalic, OdfWritingMode writingMode)
    {
        if (string.IsNullOrEmpty(text))
            return OdfLength.FromCentimeters(0);

        // 1. 字型替代對照
        string mappedFont = OdfFontResolver.MapFont(fontName);
        string? fontPath = OdfFontResolver.ResolveFontPath(mappedFont);

        SKTypeface? typeface = null;
        if (fontPath is not null && File.Exists(fontPath))
        {
            typeface = SKTypeface.FromFile(fontPath);
        }

        if (typeface is null)
        {
            var weight = isBold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal;
            var slant = isItalic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright;
            typeface = SKTypeface.FromFamilyName(mappedFont, new SKFontStyle((int)weight, (int)SKFontStyleWidth.Normal, slant)) ?? SKTypeface.Default;
        }

        // 2. 嘗試使用 HarfBuzzSharp 進行 Shaping 量測
        try
        {
            using var stream = typeface.OpenStream(out int ttcIndex);
            if (stream != null)
            {
                byte[] fontData;
                using (var ms = new MemoryStream())
                {
                    byte[] buffer = new byte[8192];
                    int bytesRead;
                    while ((bytesRead = stream.Read(buffer, buffer.Length)) > 0)
                    {
                        ms.Write(buffer, 0, bytesRead);
                    }
                    fontData = ms.ToArray();
                }

                if (fontData.Length > 0)
                {
                    var handle = GCHandle.Alloc(fontData, GCHandleType.Pinned);
                    try
                    {
                        IntPtr dataPtr = handle.AddrOfPinnedObject();
                        using var blob = new Blob(dataPtr, fontData.Length, MemoryMode.ReadOnly);
                        using var hbFace = new Face(blob, ttcIndex);
                        using var hbFont = new HarfBuzzSharp.Font(hbFace);
                        hbFont.SetScale(2048, 2048); // 設定標準 EM 單位

                        using var hbBuffer = new HarfBuzzSharp.Buffer();
                        hbBuffer.AddUtf8(text);
                        hbBuffer.GuessSegmentProperties();

                        // 設定書寫方向
                        if (writingMode == OdfWritingMode.RlTb)
                        {
                            hbBuffer.Direction = Direction.RightToLeft;
                        }
                        else if (writingMode == OdfWritingMode.TbRl || writingMode == OdfWritingMode.TbLr)
                        {
                            hbBuffer.Direction = Direction.TopToBottom;
                        }
                        else
                        {
                            hbBuffer.Direction = Direction.LeftToRight;
                        }

                        hbFont.Shape(hbBuffer);

                        int totalAdvance = 0;
                        var glyphPositions = hbBuffer.GlyphPositions;
                        for (int i = 0; i < glyphPositions.Length; i++)
                        {
                            totalAdvance += (writingMode == OdfWritingMode.TbRl || writingMode == OdfWritingMode.TbLr) ? glyphPositions[i].YAdvance : glyphPositions[i].XAdvance;
                        }

                        double widthInPoints = (totalAdvance / 2048.0) * fontSizePoints;
                        // 1 pt = 2.54 / 72 cm
                        double widthInCm = widthInPoints * (2.54 / 72.0);
                        return OdfLength.FromCentimeters(widthInCm);
                    }
                    finally
                    {
                        if (handle.IsAllocated)
                            handle.Free();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // 發生任何例外時回退至 SkiaSharp 量測
            OdfKit.Core.OdfKitDiagnostics.Warn($"GDI+ 字型量測失敗，回退至 SkiaSharp：{ex.Message}", ex);
        }

        // 3. Fallback 回退：使用 SkiaSharp 量測
#pragma warning disable CS0618
        using var paint = new SKPaint();
        var styleWeight = isBold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal;
        var styleSlant = isItalic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright;
        paint.Typeface = SKTypeface.FromFamilyName(mappedFont, new SKFontStyle((int)styleWeight, (int)SKFontStyleWidth.Normal, styleSlant));
        paint.TextSize = (float)fontSizePoints * 1.3333f; // Points to Pixels (96 DPI)

        float widthInPx = paint.MeasureText(text);
#pragma warning restore CS0618
        double fallbackCm = widthInPx / 96.0 * 2.54; // Pixels to Centimeters
        return OdfLength.FromCentimeters(fallbackCm);
    }
}
