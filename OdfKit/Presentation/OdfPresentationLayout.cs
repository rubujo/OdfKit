using System;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;

using OdfKit.Compliance;
namespace OdfKit.Presentation;

/// <summary>
/// Specifies the high-level layout type of a presentation slide.
/// 表示投影片高階版面配置型態的列舉。
/// </summary>
public enum OdfPresentationLayout
{
    /// <summary>
    /// A blank layout.
    /// 空白版面。
    /// </summary>
    Blank,

    /// <summary>
    /// A layout with only a title.
    /// 僅標題。
    /// </summary>
    TitleOnly,

    /// <summary>
    /// A layout with a title and subtitle.
    /// 標題與副標題。
    /// </summary>
    TitleAndSubtitle,

    /// <summary>
    /// A layout with a title and body content.
    /// 標題與內容主體。
    /// </summary>
    TitleAndBody
}

/// <summary>
/// Represents a high-level slide master definition.
/// 表示高階投影片母片定義的類別。
/// </summary>
public sealed class OdfMasterPageDefinition
{
    /// <summary>
    /// Gets or sets the master page name.
    /// 取得或設定母片名稱。
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional master page background color, such as "#ffffff".
    /// 取得或設定選用的母片背景顏色（例如 "#ffffff"）。
    /// </summary>
    public string? BackgroundColor { get; init; }
}
/// <summary>
/// Provides the PresentationDocument API.
/// 提供 PresentationDocument API。
/// </summary>




public partial class PresentationDocument
{
    /// <summary>
    /// Gets the list of all slide master pages in the presentation.
    /// 取得簡報中所有投影片母片的清單。
    /// </summary>
    /// <returns>The master page list ordered by <c>style:master-page</c> entries in <c>office:master-styles</c>. / 依 <c>office:master-styles</c> 內 <c>style:master-page</c> 順序排列的母片清單。</returns>
    public new IReadOnlyList<OdfMasterPage> GetMasterPages()
    {
        OdfNode? masterStyles = null;
        foreach (OdfNode child in StylesRoot.Children)
        {
            if (child.LocalName == "master-styles" && child.NamespaceUri == OdfNamespaces.Office)
            {
                masterStyles = child;
                break;
            }
        }

        if (masterStyles is null)
            return [];

        List<OdfMasterPage> pages = [];
        foreach (OdfNode child in masterStyles.Children)
        {
            if (child.NodeType is not OdfNodeType.Element ||
                child.LocalName is not "master-page" ||
                child.NamespaceUri != OdfNamespaces.Style)
                continue;

            pages.Add(new OdfMasterPage(child));
        }

        return pages.AsReadOnly();
    }

    /// <summary>
    /// Finds a master page by its exact name.
    /// 依精確名稱尋找投影片母片。
    /// </summary>
    /// <param name="name">The exact master-page name. / 母片的精確名稱。</param>
    /// <returns>The matching master page, or <see langword="null"/>. / 相符的母片；若不存在則為 <see langword="null"/>。</returns>
    public OdfMasterPage? FindMasterPage(string name)
    {
        foreach (OdfMasterPage masterPage in GetMasterPages())
        {
            if (string.Equals(masterPage.Name, name, StringComparison.Ordinal))
                return masterPage;
        }
        return null;
    }

    /// <summary>
    /// Gets the slides that reference the specified master page.
    /// 取得引用指定母片的投影片。
    /// </summary>
    /// <param name="name">The exact master-page name. / 母片的精確名稱。</param>
    /// <returns>The referencing slides. / 引用該母片的投影片。</returns>
    public IReadOnlyList<OdfSlide> GetMasterPageReferences(string name)
    {
        List<OdfSlide> references = [];
        foreach (OdfSlide slide in Slides)
        {
            if (string.Equals(slide.MasterPageName, name, StringComparison.Ordinal))
                references.Add(slide);
        }
        return references.AsReadOnly();
    }

    /// <summary>
    /// Renames a master page and updates every referencing slide.
    /// 重新命名母片，並更新所有引用該母片的投影片。
    /// </summary>
    /// <param name="currentName">The current exact name. / 目前的精確名稱。</param>
    /// <param name="newName">The replacement name. / 取代用名稱。</param>
    /// <returns><see langword="true"/> if renamed; otherwise <see langword="false"/>. / 若已重新命名則為 <see langword="true"/>，否則為 <see langword="false"/>。</returns>
    public bool RenameMasterPage(string currentName, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName) || FindMasterPage(newName) is not null)
            return false;
        OdfMasterPage? masterPage = FindMasterPage(currentName);
        if (masterPage is null)
            return false;

        masterPage.Name = newName;
        foreach (OdfSlide slide in GetMasterPageReferences(currentName))
            slide.MasterPageName = newName;
        return true;
    }

    /// <summary>
    /// Attempts to remove an unreferenced master page.
    /// 嘗試移除未被引用的母片。
    /// </summary>
    /// <param name="name">The exact master-page name. / 母片的精確名稱。</param>
    /// <param name="references">The blocking slide references. / 造成阻擋的投影片引用。</param>
    /// <returns><see langword="true"/> if removed; otherwise <see langword="false"/>. / 若已移除則為 <see langword="true"/>，否則為 <see langword="false"/>。</returns>
    public bool TryRemoveMasterPage(string name, out IReadOnlyList<OdfSlide> references)
    {
        references = GetMasterPageReferences(name);
        if (references.Count > 0)
            return false;
        OdfMasterPage? masterPage = FindMasterPage(name);
        if (masterPage?.Node.Parent is null)
            return false;
        masterPage.Node.Parent.RemoveChild(masterPage.Node);
        return true;
    }

    /// <summary>
    /// Removes a master page after redirecting all referencing slides to a replacement master page.
    /// 將所有引用投影片重新導向取代母片後，移除指定母片。
    /// </summary>
    /// <param name="name">The exact master-page name to remove. / 要移除的母片精確名稱。</param>
    /// <param name="replacementName">The exact replacement master-page name. / 取代母片的精確名稱。</param>
    /// <returns><see langword="true"/> if removed; otherwise <see langword="false"/>. / 若已移除則為 <see langword="true"/>，否則為 <see langword="false"/>。</returns>
    public bool RemoveMasterPage(string name, string replacementName)
    {
        if (string.Equals(name, replacementName, StringComparison.Ordinal) ||
            FindMasterPage(replacementName) is null)
            return false;
        OdfMasterPage? masterPage = FindMasterPage(name);
        if (masterPage?.Node.Parent is null)
            return false;

        foreach (OdfSlide slide in GetMasterPageReferences(name))
            slide.MasterPageName = replacementName;
        masterPage.Node.Parent.RemoveChild(masterPage.Node);
        return true;
    }

    /// <summary>
    /// Adds a slide master page to the presentation.
    /// 在簡報中新增一個投影片母片。
    /// </summary>
    /// <param name="name">The master page name. / 母片名稱。</param>
    /// <param name="def">The master page definition and settings. / 母片定義與設定。</param>
    /// <returns>The added master page instance. / 新增的母片執行個體。</returns>
    public OdfMasterPage AddMasterPage(string name, OdfMasterPageDefinition def)
    {
        if (string.IsNullOrEmpty(name))
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfPresentationLayout_NameCannotBeEmpty_2"), nameof(name));
        global::OdfKit.Internal.OdfThrowHelper.ThrowIfNull(def, nameof(def));

        // 取得或建立 master-styles 節點
        OdfNode? masterStyles = null;
        foreach (var child in StylesRoot.Children)
        {
            if (child.LocalName == "master-styles" && child.NamespaceUri == OdfNamespaces.Office)
            {
                masterStyles = child;
                break;
            }
        }
        if (masterStyles is null)
        {
            masterStyles = new OdfNode(OdfNodeType.Element, "master-styles", OdfNamespaces.Office, "office");
            StylesRoot.AppendChild(masterStyles);
        }

        // 建立新的 style:master-page 節點
        var masterPage = new OdfNode(OdfNodeType.Element, "master-page", OdfNamespaces.Style, "style");
        masterPage.SetAttribute("name", OdfNamespaces.Style, name, "style");
        masterPage.SetAttribute("page-layout-name", OdfNamespaces.Style, "Default", "style");

        // 若設定背景顏色，則新增 drawing-page-properties 節點
        if (!string.IsNullOrEmpty(def.BackgroundColor))
        {
            var pageProps = new OdfNode(OdfNodeType.Element, "drawing-page-properties", OdfNamespaces.Style, "style");
            pageProps.SetAttribute("fill", OdfNamespaces.Draw, "solid", "draw");
            pageProps.SetAttribute("fill-color", OdfNamespaces.Draw, def.BackgroundColor!, "draw");
            masterPage.AppendChild(pageProps);
        }

        masterStyles.AppendChild(masterPage);
        return new OdfMasterPage(masterPage);
    }

    /// <summary>
    /// Sets the master page used by the slide at the specified index.
    /// 設定指定索引投影片所使用的母片。
    /// </summary>
    /// <param name="slideIndex">The slide index. / 投影片索引位置。</param>
    /// <param name="masterPageName">The master page name. / 母片名稱。</param>
    public void SetMasterPage(int slideIndex, string masterPageName)
    {
        if (slideIndex < 0 || slideIndex >= Slides.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(slideIndex), OdfLocalizer.GetMessage("Err_OdfPresentationLayout_SlideIndexOutRange_3"));
        }
        if (string.IsNullOrEmpty(masterPageName))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfPresentationLayout_NameCannotBeEmpty_2"), nameof(masterPageName));
        }
        Slides[slideIndex].MasterPageName = masterPageName;
    }

    /// <summary>
    /// Gets the layout type of the slide at the specified index.
    /// 取得指定索引投影片的版面配置型態。
    /// </summary>
    /// <param name="slideIndex">The slide index. / 投影片索引位置。</param>
    /// <returns>The slide layout type. / 投影片版面配置型態。</returns>
    public OdfPresentationLayout GetLayout(int slideIndex)
    {
        if (slideIndex < 0 || slideIndex >= Slides.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(slideIndex), OdfLocalizer.GetMessage("Err_OdfPresentationLayout_SlideIndexOutRange_3"));
        }
        var slide = Slides[slideIndex];
        string? layoutName = slide.PresentationPageLayoutName;

        return layoutName switch
        {
            "AL1T1" or "layout_TitleOnly" => OdfPresentationLayout.TitleOnly,
            "AL1T2" or "layout_TitleAndSubtitle" => OdfPresentationLayout.TitleAndSubtitle,
            "AL1T3" or "layout_TitleAndBody" => OdfPresentationLayout.TitleAndBody,
            _ => OdfPresentationLayout.Blank
        };
    }

    /// <summary>
    /// Sets the layout type of the slide at the specified index and automatically configures matching placeholders.
    /// 設定指定索引投影片的版面配置型態，並自動配置對應的預留位置。
    /// </summary>
    /// <param name="slideIndex">The slide index. / 投影片索引位置。</param>
    /// <param name="layout">The slide layout type. / 投影片版面配置型態。</param>
    public void SetLayout(int slideIndex, OdfPresentationLayout layout)
    {
        if (slideIndex < 0 || slideIndex >= Slides.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(slideIndex), OdfLocalizer.GetMessage("Err_OdfPresentationLayout_SlideIndexOutRange_3"));
        }

        string layoutName = layout switch
        {
            OdfPresentationLayout.TitleOnly => "layout_TitleOnly",
            OdfPresentationLayout.TitleAndSubtitle => "layout_TitleAndSubtitle",
            OdfPresentationLayout.TitleAndBody => "layout_TitleAndBody",
            _ => "layout_Blank"
        };
        EnsureStandardPresentationPageLayout(layoutName, layout);
        ApplyPresentationPageLayout(slideIndex, layoutName);
    }
}
