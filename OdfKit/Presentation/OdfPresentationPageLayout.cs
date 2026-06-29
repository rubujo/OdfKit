using System;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.Presentation;

/// <summary>
/// Represents a presentation page layout.
/// 表示簡報頁面版面配置的類別。
/// </summary>
/// <param name="node">The underlying <see cref="OdfNode"/> instance. / 底層的 <see cref="OdfNode"/> 執行個體。</param>
public class OdfPresentationPageLayout(OdfNode node)
{
    /// <summary>
    /// 取得底層的 ODF 節點。
    /// </summary>
    internal OdfNode Node { get; } = node;

    /// <summary>
    /// Gets or sets the presentation page layout name.
    /// 取得或設定簡報頁面版面配置的名稱。
    /// </summary>
    public string Name
    {
        get => Node.GetAttribute("name", OdfNamespaces.Style) ?? string.Empty;
        set => Node.SetAttribute("name", OdfNamespaces.Style, value, "style");
    }

    /// <summary>
    /// Gets the read-only list of placeholder templates.
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
    /// Adds a placeholder template.
    /// 新增預留位置範本。
    /// </summary>
    /// <param name="type">The placeholder type. / 預留位置型態。</param>
    /// <param name="x">The X-axis coordinate position. / X 軸座標位置。</param>
    /// <param name="y">The Y-axis coordinate position. / Y 軸座標位置。</param>
    /// <param name="w">The width. / 寬度。</param>
    /// <param name="h">The height. / 高度。</param>
    /// <returns>The added placeholder template instance. / 新增的預留位置範本執行個體。</returns>
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
    /// Removes placeholder templates of the specified type.
    /// 移除指定型態的預留位置範本。
    /// </summary>
    /// <param name="type">The placeholder type to remove. / 要移除的預留位置型態。</param>
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
