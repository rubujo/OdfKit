using System;
using System.Collections.Generic;
using OdfKit.DOM;
using OdfKit.Styles;
using OdfKit.Core;
using OdfNamespaces = OdfKit.Core.OdfNamespaces;

namespace OdfKit.Presentation;

/// <summary>
/// 表示預留位置（Placeholder）型態的列舉。
/// </summary>
public enum OdfPlaceholderType
{
    /// <summary>
    /// 標題。
    /// </summary>
    Title,

    /// <summary>
    /// 副標題。
    /// </summary>
    Subtitle,

    /// <summary>
    /// 大綱。
    /// </summary>
    Outline,

    /// <summary>
    /// 文字。
    /// </summary>
    Text,

    /// <summary>
    /// 圖形。
    /// </summary>
    Graphic,

    /// <summary>
    /// 物件。
    /// </summary>
    Object,

    /// <summary>
    /// 圖表。
    /// </summary>
    Chart,

    /// <summary>
    /// 表格。
    /// </summary>
    Table,

    /// <summary>
    /// 組織圖。
    /// </summary>
    Orgchart,

    /// <summary>
    /// 頁碼。
    /// </summary>
    PageNumber,

    /// <summary>
    /// 頁首。
    /// </summary>
    Header,

    /// <summary>
    /// 頁尾。
    /// </summary>
    Footer,

    /// <summary>
    /// 日期與時間。
    /// </summary>
    DateTime,

    /// <summary>
    /// 備忘錄。
    /// </summary>
    Notes,

    /// <summary>
    /// 講義。
    /// </summary>
    Handout
}

/// <summary>
/// 表示預留位置範本的類別。
/// </summary>
/// <param name="node">底層的 <see cref="OdfNode"/> 執行個體</param>
public class OdfPlaceholderTemplate(OdfNode node)
{
    /// <summary>
    /// 取得底層的 ODF 節點。
    /// </summary>
    internal OdfNode Node { get; } = node;

    /// <summary>
    /// 取得或設定預留位置的型態。
    /// </summary>
    public OdfPlaceholderType PlaceholderType
    {
        get => KebabToType(Node.GetAttribute("class", OdfNamespaces.Presentation) ?? "text");
        set => Node.SetAttribute("class", OdfNamespaces.Presentation, TypeToKebab(value), "presentation");
    }

    /// <summary>
    /// 取得或設定預留位置的 X 軸座標位置。
    /// </summary>
    public OdfLength? X
    {
        get
        {
            string? val = Node.GetAttribute("x", OdfNamespaces.Svg);
            if (val is not null)
            {
                return OdfLength.Parse(val);
            }
            return null;
        }
        set => Node.SetAttribute("x", OdfNamespaces.Svg, value?.ToString() ?? string.Empty, "svg");
    }

    /// <summary>
    /// 取得或設定預留位置的 Y 軸座標位置。
    /// </summary>
    public OdfLength? Y
    {
        get
        {
            string? val = Node.GetAttribute("y", OdfNamespaces.Svg);
            if (val is not null)
            {
                return OdfLength.Parse(val);
            }
            return null;
        }
        set => Node.SetAttribute("y", OdfNamespaces.Svg, value?.ToString() ?? string.Empty, "svg");
    }

    /// <summary>
    /// 取得或設定預留位置的寬度。
    /// </summary>
    public OdfLength? Width
    {
        get
        {
            string? val = Node.GetAttribute("width", OdfNamespaces.Svg);
            if (val is not null)
            {
                return OdfLength.Parse(val);
            }
            return null;
        }
        set => Node.SetAttribute("width", OdfNamespaces.Svg, value?.ToString() ?? string.Empty, "svg");
    }

    /// <summary>
    /// 取得或設定預留位置的高度。
    /// </summary>
    public OdfLength? Height
    {
        get
        {
            string? val = Node.GetAttribute("height", OdfNamespaces.Svg);
            if (val is not null)
            {
                return OdfLength.Parse(val);
            }
            return null;
        }
        set => Node.SetAttribute("height", OdfNamespaces.Svg, value?.ToString() ?? string.Empty, "svg");
    }

    internal static string TypeToKebab(OdfPlaceholderType type)
    {
        return type switch
        {
            OdfPlaceholderType.Title => "title",
            OdfPlaceholderType.Subtitle => "subtitle",
            OdfPlaceholderType.Outline => "outline",
            OdfPlaceholderType.Text => "text",
            OdfPlaceholderType.Graphic => "graphic",
            OdfPlaceholderType.Object => "object",
            OdfPlaceholderType.Chart => "chart",
            OdfPlaceholderType.Table => "table",
            OdfPlaceholderType.Orgchart => "orgchart",
            OdfPlaceholderType.PageNumber => "page-number",
            OdfPlaceholderType.Header => "header",
            OdfPlaceholderType.Footer => "footer",
            OdfPlaceholderType.DateTime => "date-time",
            OdfPlaceholderType.Notes => "notes",
            OdfPlaceholderType.Handout => "handout",
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
    }

    internal static OdfPlaceholderType KebabToType(string kebab)
    {
        return kebab switch
        {
            "title" => OdfPlaceholderType.Title,
            "subtitle" => OdfPlaceholderType.Subtitle,
            "outline" => OdfPlaceholderType.Outline,
            "text" => OdfPlaceholderType.Text,
            "graphic" => OdfPlaceholderType.Graphic,
            "object" => OdfPlaceholderType.Object,
            "chart" => OdfPlaceholderType.Chart,
            "table" => OdfPlaceholderType.Table,
            "orgchart" => OdfPlaceholderType.Orgchart,
            "page-number" => OdfPlaceholderType.PageNumber,
            "header" => OdfPlaceholderType.Header,
            "footer" => OdfPlaceholderType.Footer,
            "date-time" => OdfPlaceholderType.DateTime,
            "notes" => OdfPlaceholderType.Notes,
            "handout" => OdfPlaceholderType.Handout,
            _ => OdfPlaceholderType.Text
        };
    }
}

/// <summary>
/// 表示簡報頁面版面配置的類別。
/// </summary>
/// <param name="node">底層的 <see cref="OdfNode"/> 執行個體</param>
public class OdfPresentationPageLayout(OdfNode node)
{
    /// <summary>
    /// 取得底層的 ODF 節點。
    /// </summary>
    public OdfNode Node { get; } = node;

    /// <summary>
    /// 取得或設定簡報頁面版面配置的名稱。
    /// </summary>
    public string Name
    {
        get => Node.GetAttribute("name", OdfNamespaces.Style) ?? string.Empty;
        set => Node.SetAttribute("name", OdfNamespaces.Style, value, "style");
    }

    /// <summary>
    /// 取得預留位置範本的唯讀清單。
    /// </summary>
    public IReadOnlyList<OdfPlaceholderTemplate> Placeholders
    {
        get
        {
            List<OdfPlaceholderTemplate> list = [];
            foreach (var child in Node.Children)
            {
                if (child.LocalName is "placeholder" && child.NamespaceUri == OdfNamespaces.Presentation)
                {
                    list.Add(new OdfPlaceholderTemplate(child));
                }
            }
            return list.AsReadOnly();
        }
    }

    /// <summary>
    /// 新增預留位置範本。
    /// </summary>
    /// <param name="type">預留位置型態</param>
    /// <param name="x">X 軸座標位置</param>
    /// <param name="y">Y 軸座標位置</param>
    /// <param name="w">寬度</param>
    /// <param name="h">高度</param>
    /// <returns>新增的預留位置範本執行個體</returns>
    public OdfPlaceholderTemplate AddPlaceholder(OdfPlaceholderType type, OdfLength x, OdfLength y, OdfLength w, OdfLength h)
    {
        OdfNode phNode = new(OdfNodeType.Element, "placeholder", OdfNamespaces.Presentation, "presentation");
        phNode.SetAttribute("class", OdfNamespaces.Presentation, OdfPlaceholderTemplate.TypeToKebab(type), "presentation");
        phNode.SetAttribute("x", OdfNamespaces.Svg, x.ToString(), "svg");
        phNode.SetAttribute("y", OdfNamespaces.Svg, y.ToString(), "svg");
        phNode.SetAttribute("width", OdfNamespaces.Svg, w.ToString(), "svg");
        phNode.SetAttribute("height", OdfNamespaces.Svg, h.ToString(), "svg");
        Node.AppendChild(phNode);
        return new OdfPlaceholderTemplate(phNode);
    }

    /// <summary>
    /// 移除指定型態的預留位置範本。
    /// </summary>
    /// <param name="type">要移除的預留位置型態</param>
    public void RemovePlaceholder(OdfPlaceholderType type)
    {
        string clsVal = OdfPlaceholderTemplate.TypeToKebab(type);
        List<OdfNode> toRemove = [];
        foreach (var child in Node.Children)
        {
            if (child.LocalName is "placeholder" && child.NamespaceUri == OdfNamespaces.Presentation)
            {
                if (child.GetAttribute("class", OdfNamespaces.Presentation) == clsVal)
                {
                    toRemove.Add(child);
                }
            }
        }
        foreach (var child in toRemove)
        {
            Node.RemoveChild(child);
        }
    }
}

/// <summary>
/// 表示投影片預留位置的類別。
/// </summary>
/// <param name="node">底層的 <see cref="OdfNode"/> 執行個體</param>
/// <param name="slide">所屬的投影片執行個體</param>
public class OdfPlaceholder(OdfNode node, OdfSlide slide) : OdfShape(node, slide)
{
    /// <summary>
    /// 取得或設定預留位置的型態。
    /// </summary>
    public OdfPlaceholderType PlaceholderType
    {
        get => OdfPlaceholderTemplate.KebabToType(Node.GetAttribute("class", OdfNamespaces.Presentation) ?? "text");
        set => Node.SetAttribute("class", OdfNamespaces.Presentation, OdfPlaceholderTemplate.TypeToKebab(value), "presentation");
    }

    private readonly bool _initialized = InitPlaceholder(node);

    private static bool InitPlaceholder(OdfNode n)
    {
        n.SetAttribute("placeholder", OdfNamespaces.Presentation, "true", "presentation");
        return true;
    }
}

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
    public OdfNode Node { get; }

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
    /// 在簡報中新增一個投影片母片。
    /// </summary>
    /// <param name="name">母片名稱。</param>
    /// <param name="def">母片定義與設定。</param>
    /// <returns>新增的母片執行個體。</returns>
    public OdfMasterPage AddMasterPage(string name, OdfMasterPageDefinition def)
    {
        if (string.IsNullOrEmpty(name)) throw new ArgumentException("母片名稱不可為空。", nameof(name));
        if (def is null) throw new ArgumentNullException(nameof(def));

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
