using System;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;

using OdfKit.Compliance;
namespace OdfKit.Presentation;

public partial class PresentationDocument
{
    /// <summary>
    /// Gets summaries for all presentation page layouts in the presentation.
    /// 取得簡報中所有投影片版面配置摘要清單。
    /// </summary>
    public IReadOnlyList<OdfPresentationPageLayoutInfo> GetPresentationPageLayouts()
    {
        Dictionary<string, OdfPresentationPageLayout> layouts = [];
        CollectPresentationPageLayouts(ContentRoot, layouts);
        CollectPresentationPageLayouts(StylesRoot, layouts);

        List<OdfPresentationPageLayoutInfo> result = [];
        foreach (KeyValuePair<string, OdfPresentationPageLayout> entry in layouts)
            result.Add(new OdfPresentationPageLayoutInfo(entry.Key, entry.Value.Placeholders.Count));

        return result.AsReadOnly();
    }

    /// <summary>
    /// Applies the named presentation page layout to a slide and instantiates placeholders from its template.
    /// 將指定名稱的投影片版面配置套用至投影片，並依範本實例化預留位置。
    /// </summary>
    /// <param name="slideIndex">The slide index. / 投影片索引位置。</param>
    /// <param name="layoutName">The layout name from <c>presentation:page-layout-name</c>. / <c>presentation:page-layout-name</c> 的版面配置名稱。</param>
    /// <exception cref="ArgumentException">Thrown when the specified layout cannot be found. / 找不到指定版面配置時擲出。</exception>
    public void ApplyPresentationPageLayout(int slideIndex, string layoutName)
    {
        if (string.IsNullOrWhiteSpace(layoutName))
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_PresentationDocument_LayoutCannotBeEmpty"), nameof(layoutName));
        if (slideIndex < 0 || slideIndex >= Slides.Count)
            throw new ArgumentOutOfRangeException(nameof(slideIndex), OdfLocalizer.GetMessage("Err_PresentationDocument_SlideIndexOutRange"));

        OdfPresentationPageLayout? layout = FindPresentationPageLayout(layoutName)
            ?? throw new ArgumentException(OdfLocalizer.GetMessage("Err_PresentationDocument_LayoutNotFound", layoutName), nameof(layoutName));

        OdfSlide slide = Slides[slideIndex];
        slide.PresentationPageLayoutName = layoutName;
        ClearSlidePlaceholders(slide);
        InstantiatePlaceholdersFromLayout(slide, layout);
    }

    internal void EnsureStandardPresentationPageLayout(string layoutName, OdfPresentationLayout layout)
    {
        if (FindPresentationPageLayout(layoutName) is not null)
            return;

        OdfPresentationPageLayout pageLayout = CreatePresentationPageLayout(layoutName);
        switch (layout)
        {
            case OdfPresentationLayout.TitleOnly:
                pageLayout.AddPlaceholder(
                    OdfPlaceholderType.Title,
                    OdfLength.Parse("2.0cm"), OdfLength.Parse("1.5cm"),
                    OdfLength.Parse("24.0cm"), OdfLength.Parse("3.0cm"));
                break;
            case OdfPresentationLayout.TitleAndSubtitle:
                pageLayout.AddPlaceholder(
                    OdfPlaceholderType.Title,
                    OdfLength.Parse("2.0cm"), OdfLength.Parse("1.5cm"),
                    OdfLength.Parse("24.0cm"), OdfLength.Parse("3.0cm"));
                pageLayout.AddPlaceholder(
                    OdfPlaceholderType.Subtitle,
                    OdfLength.Parse("2.0cm"), OdfLength.Parse("5.0cm"),
                    OdfLength.Parse("24.0cm"), OdfLength.Parse("10.0cm"));
                break;
            case OdfPresentationLayout.TitleAndBody:
                pageLayout.AddPlaceholder(
                    OdfPlaceholderType.Title,
                    OdfLength.Parse("2.0cm"), OdfLength.Parse("1.5cm"),
                    OdfLength.Parse("24.0cm"), OdfLength.Parse("3.0cm"));
                pageLayout.AddPlaceholder(
                    OdfPlaceholderType.Outline,
                    OdfLength.Parse("2.0cm"), OdfLength.Parse("5.0cm"),
                    OdfLength.Parse("24.0cm"), OdfLength.Parse("12.0cm"));
                break;
            default:
                break;
        }
    }

    private static void CollectPresentationPageLayouts(
        OdfNode root,
        Dictionary<string, OdfPresentationPageLayout> layouts)
    {
        OdfNode? autoStyles = null;
        foreach (OdfNode child in root.Children)
        {
            if (child.LocalName == "automatic-styles" && child.NamespaceUri == OdfNamespaces.Office)
            {
                autoStyles = child;
                break;
            }
        }

        if (autoStyles is null)
            return;

        foreach (OdfNode child in autoStyles.Children)
        {
            if (child.LocalName is not "presentation-page-layout" || child.NamespaceUri != OdfNamespaces.Style)
                continue;

            string? name = child.GetAttribute("name", OdfNamespaces.Style);
            if (string.IsNullOrEmpty(name))
                continue;

            layouts[name!] = new OdfPresentationPageLayout(child);
        }
    }

    private static void ClearSlidePlaceholders(OdfSlide slide)
    {
        List<OdfNode> toRemove = [];
        foreach (OdfNode child in slide.Node.Children)
        {
            if (child.NodeType is not OdfNodeType.Element || child.NamespaceUri != OdfNamespaces.Draw)
                continue;

            if (child.GetAttribute("placeholder", OdfNamespaces.Presentation) == "true")
                toRemove.Add(child);
        }

        foreach (OdfNode node in toRemove)
            slide.Node.RemoveChild(node);
    }

    private static void InstantiatePlaceholdersFromLayout(OdfSlide slide, OdfPresentationPageLayout layout)
    {
        foreach (OdfPlaceholderTemplate template in layout.Placeholders)
        {
            if (template.X is null || template.Y is null || template.Width is null || template.Height is null)
                continue;

            slide.AddPlaceholder(
                template.PlaceholderType,
                template.X.Value,
                template.Y.Value,
                template.Width.Value,
                template.Height.Value);
        }
    }
}
