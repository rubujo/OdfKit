using System;

namespace OdfKit.Presentation;

public partial class OdfSlide
{
    /// <summary>
    /// 取得此投影片的版面配置型態。
    /// </summary>
    /// <returns>版面配置型態。</returns>
    public OdfPresentationLayout GetLayout() =>
        Document.GetLayout(RequireSlideIndex());

    /// <summary>
    /// 設定此投影片的版面配置型態，並自動配置對應的預留位置。
    /// </summary>
    /// <param name="layout">版面配置型態。</param>
    public void SetLayout(OdfPresentationLayout layout) =>
        Document.SetLayout(RequireSlideIndex(), layout);

    /// <summary>
    /// 設定此投影片使用的母片名稱。
    /// </summary>
    /// <param name="masterPageName">母片名稱。</param>
    public void SetMasterPage(string masterPageName) =>
        Document.SetMasterPage(RequireSlideIndex(), masterPageName);

    /// <summary>
    /// 將指定名稱的投影片版面配置套用至此投影片。
    /// </summary>
    /// <param name="layoutName">版面配置名稱。</param>
    public void ApplyPresentationPageLayout(string layoutName) =>
        Document.ApplyPresentationPageLayout(RequireSlideIndex(), layoutName);

    private int RequireSlideIndex()
    {
        System.Collections.Generic.IReadOnlyList<OdfSlide> slides = Document.GetSlidesSnapshot();
        for (int index = 0; index < slides.Count; index++)
        {
            if (ReferenceEquals(slides[index], this))
                return index;
        }

        throw new InvalidOperationException("投影片不屬於目前文件。");
    }
}
