using System.Collections.Generic;

namespace OdfKit.Presentation;

/// <summary>
/// 簡報動畫讀取引擎（內部協作者）。
/// </summary>
internal static class PresentationDocumentAnimationReadEngine
{
    internal static IReadOnlyList<OdfSlideAnimationInfo> GetAnimations(PresentationDocument document)
    {
        List<OdfSlideAnimationInfo> animations = [];

        for (int slideIndex = 0; slideIndex < document.Slides.Count; slideIndex++)
        {
            OdfSlide slide = document.Slides[slideIndex];
            foreach (OdfAnimationInfo animation in slide.GetAnimations())
            {
                animations.Add(new OdfSlideAnimationInfo(slideIndex, slide.Name, animation));
            }
        }

        return animations.AsReadOnly();
    }
}
