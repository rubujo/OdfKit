using System;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.Presentation;

public partial class PresentationDocument
{
    #region Slide Transitions

    /// <summary>
    /// 設定指定索引投影片的切換效果。
    /// </summary>
    /// <param name="slideIndex">投影片索引位置。</param>
    /// <param name="transition">投影片切換效果類型。</param>
    public void SetSlideTransition(int slideIndex, OdfSlideTransition transition)
    {
        if (slideIndex < 0 || slideIndex >= Slides.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(slideIndex), "投影片索引超出範圍。");
        }

        var slide = Slides[slideIndex];
        var slideNode = slide.Node;

        if (transition == OdfSlideTransition.None)
        {
            slideNode.RemoveAttribute("type", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0");
            slideNode.RemoveAttribute("subtype", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0");
            slideNode.RemoveAttribute("dur", "urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0");
            slideNode.RemoveAttribute("transition-type", OdfKit.Core.OdfNamespaces.Presentation);
        }
        else
        {
            OdfTransitionType type = transition switch
            {
                OdfSlideTransition.Push => OdfTransitionType.Push,
                OdfSlideTransition.Wipe => OdfTransitionType.Wipe,
                OdfSlideTransition.Zoom => OdfTransitionType.Zoom,
                _ => OdfTransitionType.Fade
            };
            slide.SetTransition(type, OdfLength.Parse("72pt"));
        }
    }

    #endregion
}
