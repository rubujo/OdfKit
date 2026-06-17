using System;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.Presentation;

/// <summary>
/// 表示投影片高階版面配置型態的列舉。
/// </summary>
public enum OdfPresentationLayout
{
    /// <summary>
    /// 空白版面。
    /// </summary>
    Blank,

    /// <summary>
    /// 僅標題。
    /// </summary>
    TitleOnly,

    /// <summary>
    /// 標題與副標題。
    /// </summary>
    TitleAndSubtitle,

    /// <summary>
    /// 標題與內容主體。
    /// </summary>
    TitleAndBody
}

/// <summary>
/// 表示高階投影片母片定義的類別。
/// </summary>
public sealed class OdfMasterPageDefinition
{
    /// <summary>
    /// 取得或設定母片名稱。
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 取得或設定選用的母片背景顏色（例如 "#ffffff"）。
    /// </summary>
    public string? BackgroundColor { get; init; }
}

/// <summary>
/// 表示投影片母片包裝的類別。
/// </summary>
public sealed class OdfMasterPage
{
    /// <summary>
    /// 取得底層的 ODF 節點。
    /// </summary>
    internal OdfNode Node { get; }

    /// <summary>
    /// 取得母片名稱。
    /// </summary>
    public string Name => Node.GetAttribute("name", OdfNamespaces.Style) ?? string.Empty;

    /// <summary>
    /// 初始化 <see cref="OdfMasterPage"/> 類別的新執行個體。
    /// </summary>
    /// <param name="node">底層的 <see cref="OdfNode"/> 執行個體。</param>
    public OdfMasterPage(OdfNode node)
    {
        Node = node ?? throw new ArgumentNullException(nameof(node));
    }
}

public partial class PresentationDocument
{
    /// <summary>
    /// 取得簡報中所有投影片母片的清單。
    /// </summary>
    /// <returns>依 <c>office:master-styles</c> 內 <c>style:master-page</c> 順序排列的母片清單。</returns>
    public IReadOnlyList<OdfMasterPage> GetMasterPages()
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
    /// 在簡報中新增一個投影片母片。
    /// </summary>
    /// <param name="name">母片名稱。</param>
    /// <param name="def">母片定義與設定。</param>
    /// <returns>新增的母片執行個體。</returns>
    public OdfMasterPage AddMasterPage(string name, OdfMasterPageDefinition def)
    {
        if (string.IsNullOrEmpty(name))
            throw new ArgumentException("母片名稱不可為空。", nameof(name));
        if (def is null)
            throw new ArgumentNullException(nameof(def));

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
    /// 設定指定索引投影片所使用的母片。
    /// </summary>
    /// <param name="slideIndex">投影片索引位置。</param>
    /// <param name="masterPageName">母片名稱。</param>
    public void SetMasterPage(int slideIndex, string masterPageName)
    {
        if (slideIndex < 0 || slideIndex >= Slides.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(slideIndex), "投影片索引超出範圍。");
        }
        if (string.IsNullOrEmpty(masterPageName))
        {
            throw new ArgumentException("母片名稱不可為空。", nameof(masterPageName));
        }
        Slides[slideIndex].MasterPageName = masterPageName;
    }

    /// <summary>
    /// 取得指定索引投影片的版面配置型態。
    /// </summary>
    /// <param name="slideIndex">投影片索引位置。</param>
    /// <returns>投影片版面配置型態。</returns>
    public OdfPresentationLayout GetLayout(int slideIndex)
    {
        if (slideIndex < 0 || slideIndex >= Slides.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(slideIndex), "投影片索引超出範圍。");
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
    /// 設定指定索引投影片的版面配置型態，並自動配置對應的預留位置。
    /// </summary>
    /// <param name="slideIndex">投影片索引位置。</param>
    /// <param name="layout">投影片版面配置型態。</param>
    public void SetLayout(int slideIndex, OdfPresentationLayout layout)
    {
        if (slideIndex < 0 || slideIndex >= Slides.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(slideIndex), "投影片索引超出範圍。");
        }

        var slide = Slides[slideIndex];

        // 1. 設定版面配置屬性值
        string layoutName = layout switch
        {
            OdfPresentationLayout.TitleOnly => "layout_TitleOnly",
            OdfPresentationLayout.TitleAndSubtitle => "layout_TitleAndSubtitle",
            OdfPresentationLayout.TitleAndBody => "layout_TitleAndBody",
            _ => "layout_Blank"
        };
        slide.PresentationPageLayoutName = layoutName;

        // 2. 清除該投影片既有之預留位置（Placeholder）圖形節點
        var toRemove = new List<OdfNode>();
        foreach (var child in slide.Node.Children)
        {
            if (child.NodeType == OdfNodeType.Element && child.NamespaceUri == OdfNamespaces.Draw)
            {
                if (child.GetAttribute("placeholder", OdfNamespaces.Presentation) == "true")
                {
                    toRemove.Add(child);
                }
            }
        }
        foreach (var node in toRemove)
        {
            slide.Node.RemoveChild(node);
        }

        // 3. 依據版面重新建立預留位置
        switch (layout)
        {
            case OdfPresentationLayout.TitleOnly:
                slide.AddPlaceholder(OdfPlaceholderType.Title, OdfLength.Parse("2.0cm"), OdfLength.Parse("1.5cm"), OdfLength.Parse("24.0cm"), OdfLength.Parse("3.0cm"));
                break;
            case OdfPresentationLayout.TitleAndSubtitle:
                slide.AddPlaceholder(OdfPlaceholderType.Title, OdfLength.Parse("2.0cm"), OdfLength.Parse("1.5cm"), OdfLength.Parse("24.0cm"), OdfLength.Parse("3.0cm"));
                slide.AddPlaceholder(OdfPlaceholderType.Subtitle, OdfLength.Parse("2.0cm"), OdfLength.Parse("5.0cm"), OdfLength.Parse("24.0cm"), OdfLength.Parse("10.0cm"));
                break;
            case OdfPresentationLayout.TitleAndBody:
                slide.AddPlaceholder(OdfPlaceholderType.Title, OdfLength.Parse("2.0cm"), OdfLength.Parse("1.5cm"), OdfLength.Parse("24.0cm"), OdfLength.Parse("3.0cm"));
                slide.AddPlaceholder(OdfPlaceholderType.Outline, OdfLength.Parse("2.0cm"), OdfLength.Parse("5.0cm"), OdfLength.Parse("24.0cm"), OdfLength.Parse("12.0cm"));
                break;
            default: // Blank
                break;
        }
    }
}
