using System.Collections.Generic;

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

            transitions.Add(new OdfSlideTransitionInfo(slideIndex, slide.Name, transition));
        }

        return transitions.AsReadOnly();
    }
}
