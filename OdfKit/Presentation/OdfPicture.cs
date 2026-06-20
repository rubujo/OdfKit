using System;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.Presentation;

/// <summary>
/// 表示投影片中的圖片圖形。
/// </summary>
/// <param name="node">底層的 <see cref="OdfNode"/> 執行個體</param>
/// <param name="doc">所屬的文件執行個體</param>
/// <param name="slide">所屬的投影片執行個體，若不屬於簡報投影片則為 <c>null</c></param>
public class OdfPicture(OdfNode node, OdfDocument doc, OdfSlide? slide) : OdfShape(node, doc, slide)
{
    /// <summary>
    /// 初始化 <see cref="OdfPicture"/> 類別的新執行個體。
    /// </summary>
    /// <param name="node">底層的 <see cref="OdfNode"/> 執行個體</param>
    /// <param name="slide">所屬的投影片執行個體</param>
    public OdfPicture(OdfNode node, OdfSlide slide) : this(node, slide?.Document!, slide) { }

    /// <summary>
    /// 初始化 <see cref="OdfPicture"/> 類別的新執行個體。
    /// </summary>
    /// <param name="node">底層的 <see cref="OdfNode"/> 執行個體</param>
    /// <param name="doc">所屬的 ODF 文件執行個體</param>
    public OdfPicture(OdfNode node, OdfDocument doc) : this(node, doc, null) { }

    /// <summary>
    /// 取得圖片在 ODF 封裝中的參照路徑。
    /// </summary>
    public string? ImageHref => FindDescendant(Node, "image", OdfNamespaces.Draw)?.GetAttribute("href", OdfNamespaces.XLink);

    /// <summary>
    /// 取得或設定圖片的 ODF 裁切矩形，對應 <c>fo:clip</c>。
    /// </summary>
    public string? CropClip
    {
        get => FindDescendant(Node, "image", OdfNamespaces.Draw)?.GetAttribute("clip", OdfNamespaces.Fo) ??
            Node.GetAttribute("clip", OdfNamespaces.Fo);
        set
        {
            OdfNode? image = FindDescendant(Node, "image", OdfNamespaces.Draw);
            OdfNode target = image ?? Node;
            if (string.IsNullOrWhiteSpace(value))
            {
                target.RemoveAttribute("clip", OdfNamespaces.Fo);
            }
            else
            {
                target.SetAttribute("clip", OdfNamespaces.Fo, value!.Trim(), "fo");
            }
        }
    }

    /// <summary>
    /// 設定圖片裁切矩形。
    /// </summary>
    /// <param name="top">可見區域上緣。</param>
    /// <param name="right">可見區域右緣。</param>
    /// <param name="bottom">可見區域下緣。</param>
    /// <param name="left">可見區域左緣。</param>
    /// <returns>目前圖片執行個體。</returns>
    public OdfPicture SetCrop(OdfLength top, OdfLength right, OdfLength bottom, OdfLength left)
    {
        CropClip = $"rect({top}, {right}, {bottom}, {left})";
        return this;
    }

    /// <summary>
    /// 清除圖片裁切設定。
    /// </summary>
    /// <returns>目前圖片執行個體。</returns>
    public OdfPicture ClearCrop()
    {
        CropClip = null;
        return this;
    }
    /// <summary>
    /// 取得或設定圖片的替代文字，對應 ODF <c>svg:desc</c>。
    /// </summary>
    public string? AltText
    {
        get => FindDescendant(Node, "desc", OdfNamespaces.Svg)?.TextContent ??
            FindDescendant(Node, "title", OdfNamespaces.Svg)?.TextContent;
        set
        {
            OdfNode? desc = FindChild(Node, "desc", OdfNamespaces.Svg);
            if (string.IsNullOrWhiteSpace(value))
            {
                if (desc is not null)
                {
                    Node.RemoveChild(desc);
                }

                return;
            }

            if (desc is null)
            {
                desc = new OdfNode(OdfNodeType.Element, "desc", OdfNamespaces.Svg, "svg");
                Node.AppendChild(desc);
            }

            desc.TextContent = value!;
        }
    }

    private static OdfNode? FindChild(OdfNode node, string localName, string namespaceUri)
    {
        foreach (OdfNode child in node.Children)
        {
            if (child.NodeType is OdfNodeType.Element &&
                child.LocalName == localName &&
                child.NamespaceUri == namespaceUri)
            {
                return child;
            }
        }

        return null;
    }

    private static OdfNode? FindDescendant(OdfNode node, string localName, string namespaceUri)
    {
        foreach (OdfNode child in node.Children)
        {
            if (child.NodeType is OdfNodeType.Element &&
                child.LocalName == localName &&
                child.NamespaceUri == namespaceUri)
            {
                return child;
            }

            OdfNode? descendant = FindDescendant(child, localName, namespaceUri);
            if (descendant is not null)
            {
                return descendant;
            }
        }

        return null;
    }
}
