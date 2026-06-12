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
    public OdfNode Node { get; } = node;

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
