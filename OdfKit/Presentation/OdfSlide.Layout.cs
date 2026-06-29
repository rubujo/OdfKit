using System.Collections.Generic;
using System;

using OdfKit.Compliance;
namespace OdfKit.Presentation;

public partial class OdfSlide
{
    /// <summary>
    /// Gets the layout type of this slide.
    /// 取得此投影片的版面配置型態。
    /// </summary>
    /// <returns>The layout type. / 版面配置型態。</returns>
    public OdfPresentationLayout GetLayout() =>
        Document.GetLayout(RequireSlideIndex());

    /// <summary>
    /// Sets the layout type of this slide and automatically arranges the corresponding placeholders.
    /// 設定此投影片的版面配置型態，並自動配置對應的預留位置。
    /// </summary>
    /// <param name="layout">The layout type. / 版面配置型態。</param>
    public void SetLayout(OdfPresentationLayout layout) =>
        Document.SetLayout(RequireSlideIndex(), layout);

    /// <summary>
    /// Sets the master page name used by this slide.
    /// 設定此投影片使用的母片名稱。
    /// </summary>
    /// <param name="masterPageName">The master page name. / 母片名稱。</param>
    public void SetMasterPage(string masterPageName) =>
        Document.SetMasterPage(RequireSlideIndex(), masterPageName);

    /// <summary>
    /// Applies the presentation page layout with the specified name to this slide.
    /// 將指定名稱的投影片版面配置套用至此投影片。
    /// </summary>
    /// <param name="layoutName">The page layout name. / 版面配置名稱。</param>
    public void ApplyPresentationPageLayout(string layoutName) =>
        Document.ApplyPresentationPageLayout(RequireSlideIndex(), layoutName);

    private int RequireSlideIndex()
    {
        IReadOnlyList<OdfSlide> slides = Document.GetSlidesSnapshot();
        for (int index = 0; index < slides.Count; index++)
        {
            if (ReferenceEquals(slides[index], this))
                return index;
        }

        throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_OdfSlide_SlideshowBelongCurrentFile"));
    }
}
