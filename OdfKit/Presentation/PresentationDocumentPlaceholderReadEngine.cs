using System.Collections.Generic;

namespace OdfKit.Presentation;

/// <summary>
/// 簡報預留位置讀取引擎（內部協作者）。
/// </summary>
internal static class PresentationDocumentPlaceholderReadEngine
{
    internal static IReadOnlyList<OdfSlidePlaceholderInfo> GetPlaceholders(PresentationDocument document)
    {
        List<OdfSlidePlaceholderInfo> placeholders = [];

        for (int slideIndex = 0; slideIndex < document.Slides.Count; slideIndex++)
        {
            OdfSlide slide = document.Slides[slideIndex];
            foreach (OdfPlaceholderInfo placeholder in OdfSlidePlaceholderReadEngine.GetPlaceholders(slide))
            {
                placeholders.Add(new OdfSlidePlaceholderInfo(slideIndex, slide.Name, placeholder));
            }
        }

        return placeholders.AsReadOnly();
    }
}
