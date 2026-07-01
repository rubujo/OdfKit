using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Styles;

/// <summary>
/// Builds segmented CJK text spans for shared drawing and presentation flows.
/// 為繪圖與簡報共用流程建立分段的 CJK 文字片段。
/// </summary>
internal static class OdfCjkTextSpanBuilder
{
    /// <summary>
    /// Appends segmented text spans to a paragraph node.
    /// 將分段文字片段附加至段落節點。
    /// </summary>
    /// <param name="document">The owning document. / 所屬文件。</param>
    /// <param name="paragraphNode">The target paragraph node. / 目標段落節點。</param>
    /// <param name="text">The source text. / 來源文字。</param>
    /// <param name="options">The font fallback options. / 字型遞補選項。</param>
    internal static void AppendSegmentedText(OdfDocument document, OdfNode paragraphNode, string text, OdfTextFontFallbackOptions options)
    {
        if (options.DeclareDefaultCjkFallbackFonts)
        {
            OdfCjkFontFallbackEngine.ApplyFontFallback(document, options);
        }

        foreach ((string segmentText, string fontName) in OdfFontSegmenter.SegmentText(text, options.BaseFont))
        {
            var span = new OdfNode(OdfNodeType.Element, "span", OdfNamespaces.Text, "text");
            span.AppendChild(new OdfNode(OdfNodeType.Text, string.Empty, string.Empty)
            {
                TextContent = segmentText,
            });

            document.StyleEngine.SetLocalStyleProperty(
                span,
                "text",
                "text-properties",
                "font-name",
                OdfNamespaces.Style,
                fontName,
                "style",
                deferSave: true);
            document.StyleEngine.SetLocalStyleProperty(
                span,
                "text",
                "text-properties",
                "font-name-asian",
                OdfNamespaces.Style,
                fontName,
                "style");

            paragraphNode.AppendChild(span);
        }
    }
}
