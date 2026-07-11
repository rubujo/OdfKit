using System;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Drawing;

/// <summary>
/// Represents a named ODF line-marker definition.
/// 表示具名稱的 ODF 線段標記定義。
/// </summary>
public sealed class OdfMarker
{
    internal OdfMarker(OdfNode node)
    {
        Node = node ?? throw new ArgumentNullException(nameof(node));
    }

    internal OdfNode Node { get; }

    /// <summary>
    /// Gets the marker name.
    /// 取得標記名稱。
    /// </summary>
    public string Name => Node.GetAttribute("name", OdfNamespaces.Draw) ?? string.Empty;

    /// <summary>
    /// Gets or sets the marker display name.
    /// 取得或設定標記顯示名稱。
    /// </summary>
    public string? DisplayName
    {
        get => Node.GetAttribute("display-name", OdfNamespaces.Draw);
        set
        {
            if (value is null)
                Node.RemoveAttribute("display-name", OdfNamespaces.Draw);
            else
                Node.SetAttribute("display-name", OdfNamespaces.Draw, value, "draw");
        }
    }

    /// <summary>
    /// Gets or sets the SVG view box.
    /// 取得或設定 SVG 檢視方塊。
    /// </summary>
    public string ViewBox
    {
        get => Node.GetAttribute("viewBox", OdfNamespaces.Svg) ?? string.Empty;
        set => Node.SetAttribute("viewBox", OdfNamespaces.Svg, value, "svg");
    }

    /// <summary>
    /// Gets or sets the SVG path data.
    /// 取得或設定 SVG 路徑資料。
    /// </summary>
    public string PathData
    {
        get => Node.GetAttribute("d", OdfNamespaces.Svg) ?? string.Empty;
        set => Node.SetAttribute("d", OdfNamespaces.Svg, value, "svg");
    }
}
