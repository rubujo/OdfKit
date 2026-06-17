using System.Collections.Generic;

namespace OdfKit.Presentation;

/// <summary>
/// 簡報投影片版面配置讀取引擎（內部協作者）。
/// </summary>
internal static class PresentationDocumentLayoutReadEngine
{
    internal static IReadOnlyList<OdfSlideLayoutInfo> GetLayouts(PresentationDocument document)
    {
        List<OdfSlideLayoutInfo> layouts = [];

        for (int slideIndex = 0; slideIndex < document.Slides.Count; slideIndex++)
        {
            OdfSlide slide = document.Slides[slideIndex];
            layouts.Add(new OdfSlideLayoutInfo(
                slideIndex,
                slide.Name,
                slide.PresentationPageLayoutName,
                document.GetLayout(slideIndex)));
        }

        return layouts.AsReadOnly();
    }
}
