using System.Threading;
using OdfKit.Styles;

namespace OdfKit.Extensions.Imaging;

/// <summary>
/// Measures text using the configured font and rendering options.
/// 提供整合 HarfBuzzSharp 與 SkiaSharp 的跨平台文字物理尺寸精確量測工具。
/// </summary>
public static class OdfTextMeasurer
{
    /// <summary>
    /// Measures the rendered width of text.
    /// 精確量測指定字型、大小與書寫模式下文字的物理寬度。
    /// </summary>
    /// <param name="text">The text or value. / 要量測的文字內容。</param>
    /// <param name="fontName">The font family name. / 字型名稱。</param>
    /// <param name="fontSizePoints">The font size in points. / 字型大小（點）。</param>
    /// <param name="isBold">Whether the text is bold. / 是否為粗體。</param>
    /// <param name="isItalic">Whether the text is italic. / 是否為斜體。</param>
    /// <param name="writingMode">The writing mode. / 書寫模式。</param>
    /// <param name="fontContext">The isolated font context, or null for the default. / 隔離的字型情境；null 表示使用預設值。</param>
    /// <returns>The measured physical width. / 量測後的實體寬度。</returns>
    public static OdfLength MeasureWidth(
        string text,
        string fontName,
        double fontSizePoints,
        bool isBold = false,
        bool isItalic = false,
        OdfWritingMode writingMode = OdfWritingMode.LrTb,
        OdfFontContext? fontContext = null)
    {
        using var session = new OdfTextLayoutSession(
            fontContext ?? OdfFontContext.Default);
        OdfTextMeasureResult result = session.Measure(
            new OdfTextMeasureRequest
            {
                Text = text,
                FontFamily = fontName,
                FontSizePoints = fontSizePoints,
                IsBold = isBold,
                IsItalic = isItalic,
                WritingMode = writingMode
            },
            CancellationToken.None);
        return OdfLength.FromCentimeters(result.WidthCentimeters);
    }

    /// <summary>
    /// Measures rendered text width, height, and line count.
    /// 量測呈現文字的寬度、高度與行數。
    /// </summary>
    /// <param name="request">The text-layout request. / 文字版面量測要求。</param>
    /// <param name="fontContext">The isolated font context. / 隔離的字型情境。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消權杖。</param>
    /// <returns>The physical text-layout result. / 實體文字版面量測結果。</returns>
    public static OdfTextMeasureResult Measure(
        OdfTextMeasureRequest request,
        OdfFontContext fontContext,
        CancellationToken cancellationToken)
    {
        using var session = new OdfTextLayoutSession(fontContext);
        return session.Measure(request, cancellationToken);
    }
}
