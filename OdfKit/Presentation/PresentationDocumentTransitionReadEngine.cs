using System.Collections.Generic;
using OdfKit.Core;

namespace OdfKit.Presentation;

/// <summary>
/// 簡報投影片切換效果讀取引擎（內部協作者）。
/// </summary>
internal static class PresentationDocumentTransitionReadEngine
{
    internal static IReadOnlyList<OdfSlideTransitionInfo> GetSlideTransitions(PresentationDocument document)
    {
        List<OdfSlideTransitionInfo> transitions = [];

        for (int slideIndex = 0; slideIndex < document.Slides.Count; slideIndex++)
        {
            OdfSlide slide = document.Slides[slideIndex];
            OdfSlideTransition transition = document.GetSlideTransition(slideIndex);
            if (transition == OdfSlideTransition.None)
                continue;

            string? durAttr = null;
            string? styleName = slide.Node.GetAttribute("style-name", OdfNamespaces.Draw);
            if (!string.IsNullOrWhiteSpace(styleName))
            {
                // 優先自樣式中的 drawing-page-properties 讀取轉場持續時間
                durAttr = document.StyleEngine.GetStyleProperty(styleName!, "duration", OdfNamespaces.Presentation, "drawing-page");
            }

            // 回退自 draw:page 屬性讀取，以確保舊版本相容性
            if (string.IsNullOrEmpty(durAttr))
            {
                durAttr = slide.Node.GetAttribute("dur", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0");
            }

            transitions.Add(new OdfSlideTransitionInfo(
                slideIndex,
                slide.Name,
                transition,
                durAttr));
        }

        return transitions.AsReadOnly();
    }
}
