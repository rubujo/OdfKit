using System;
using OdfKit.Styles;

namespace OdfKit.Spreadsheet;

public partial class OdfCell
{
    /// <summary>
    /// Sets text with the specified font fallback options.
    /// 使用指定的字型遞補選項設定文字。
    /// </summary>
    /// <param name="text">The text content to write. / 要寫入的文字內容。</param>
    /// <param name="options">The font fallback options. / 字型遞補選項。</param>
    public void SetText(string text, OdfTextFontFallbackOptions options)
    {
        if (text is null)
        {
            throw new ArgumentNullException(nameof(text));
        }

        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        if (options.DeclareDefaultCjkFallbackFonts)
        {
            OdfCjkFontFallbackEngine.ApplyFontFallback(_doc, options);
        }

        var richText = new OdfRichText();
        foreach ((string segmentText, string fontName) in OdfFontSegmenter.SegmentText(text, options.BaseFont))
        {
            richText.AddRun(segmentText, fontFamily: fontName);
        }

        SetRichText(richText);
    }
}
