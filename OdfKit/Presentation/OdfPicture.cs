using System;
using OdfKit.Core;
using OdfKit.DOM;

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
