using System;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.Presentation;

/// <summary>
/// Represents a placeholder template.
/// 表示預留位置範本的類別。
/// </summary>
/// <param name="node">The underlying <see cref="OdfNode"/> instance. / 底層的 <see cref="OdfNode"/> 執行個體。</param>
public class OdfPlaceholderTemplate(OdfNode node)
{
    /// <summary>
    /// 取得底層的 ODF 節點。
    /// </summary>
    internal OdfNode Node { get; } = node;

    /// <summary>
    /// Gets or sets the placeholder type.
    /// 取得或設定預留位置的型態。
    /// </summary>
    public OdfPlaceholderType PlaceholderType
    {
        get => KebabToType(Node.GetAttribute("class", OdfNamespaces.Presentation) ?? "text");
        set => Node.SetAttribute("class", OdfNamespaces.Presentation, TypeToKebab(value), "presentation");
    }

    /// <summary>
    /// Gets or sets the X-axis coordinate of the placeholder.
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
    /// Gets or sets the Y-axis coordinate of the placeholder.
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
    /// Gets or sets the placeholder width.
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
    /// Gets or sets the placeholder height.
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
