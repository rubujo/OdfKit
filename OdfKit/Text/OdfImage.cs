using System;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text;

/// <summary>
/// 表示文字文件中的圖片。
/// </summary>
/// <param name="frameNode">圖片的外框節點</param>
/// <param name="imageNode">圖片的影像節點</param>
public class OdfImage(OdfNode frameNode, OdfNode imageNode)
{
    /// <summary>
    /// 取得圖片的外框節點。
    /// </summary>
    public OdfNode FrameNode { get; } = frameNode;

    /// <summary>
    /// 取得圖片的影像節點。
    /// </summary>
    public OdfNode ImageNode { get; } = imageNode;

    /// <summary>
    /// 將此圖片標記為裝飾性，輔助技術應略過此物件。
    /// </summary>
    /// <param name="decorative">是否標記為裝飾性。</param>
    /// <returns>目前圖片執行個體。</returns>
    public OdfImage MarkAsDecorative(bool decorative = true)
    {
        if (decorative)
        {
            FrameNode.SetAttribute("decorative", OdfNamespaces.Draw, "true", "draw");
        }
        else
        {
            FrameNode.RemoveAttribute("decorative", OdfNamespaces.Draw);
        }

        return this;
    }

    /// <summary>
    /// 取得或設定圖片的名稱。
    /// </summary>
    public string? Name
    {
        get => FrameNode.GetAttribute("name", OdfNamespaces.Draw);
        set => FrameNode.SetAttribute("name", OdfNamespaces.Draw, value ?? string.Empty, "draw");
    }

    /// <summary>
    /// 取得圖片在 ODF 封裝中的參照路徑。
    /// </summary>
    public string? ImageHref => ImageNode.GetAttribute("href", OdfNamespaces.XLink);

    /// <summary>
    /// 取得或設定圖片的錨定類型。
    /// </summary>
    public string? AnchorType
    {
        get => FrameNode.GetAttribute("anchor-type", OdfNamespaces.Text);
        set => FrameNode.SetAttribute("anchor-type", OdfNamespaces.Text, value ?? "paragraph", "text");
    }

    /// <summary>
    /// 取得或設定圖片的寬度。
    /// </summary>
    public string? Width
    {
        get => FrameNode.GetAttribute("width", OdfNamespaces.Svg);
        set => FrameNode.SetAttribute("width", OdfNamespaces.Svg, value ?? string.Empty, "svg");
    }

    /// <summary>
    /// 取得或設定圖片的高度。
    /// </summary>
    public string? Height
    {
        get => FrameNode.GetAttribute("height", OdfNamespaces.Svg);
        set => FrameNode.SetAttribute("height", OdfNamespaces.Svg, value ?? string.Empty, "svg");
    }

    /// <summary>
    /// 取得或設定圖片的文繞圖樣式。
    /// </summary>
    public string? WrapStyle
    {
        get => FrameNode.GetAttribute("wrap-style", OdfNamespaces.Style);
        set => FrameNode.SetAttribute("wrap-style", OdfNamespaces.Style, value ?? "none", "style");
    }

    /// <summary>
    /// 取得或設定圖片的裁剪邊界。
    /// </summary>
    public string? CropTop
    {
        get => ImageNode.GetAttribute("clip", OdfNamespaces.Fo);
        set => ImageNode.SetAttribute("clip", OdfNamespaces.Fo, value ?? string.Empty, "fo");
    }

    /// <summary>
    /// 取得或設定圖片的無障礙替代文字（對應 <c>&lt;svg:desc&gt;</c>）。
    /// </summary>
    public string? AltText
    {
        get => FindSvgChildText("desc");
        set => SetSvgChildText("desc", value);
    }

    /// <summary>
    /// 取得或設定圖片的無障礙標題（對應 <c>&lt;svg:title&gt;</c>）。
    /// </summary>
    public string? AccessibilityTitle
    {
        get => FindSvgChildText("title");
        set => SetSvgChildText("title", value);
    }

    private string? FindSvgChildText(string localName)
    {
        foreach (var child in FrameNode.Children)
        {
            if (child.LocalName == localName && child.NamespaceUri == OdfNamespaces.Svg)
                return child.TextContent;
        }
        return null;
    }

    private void SetSvgChildText(string localName, string? text)
    {
        foreach (var child in FrameNode.Children)
        {
            if (child.LocalName == localName && child.NamespaceUri == OdfNamespaces.Svg)
            {
                if (string.IsNullOrEmpty(text))
                    FrameNode.RemoveChild(child);
                else
                    child.TextContent = text!;
                return;
            }
        }
        if (!string.IsNullOrEmpty(text))
        {
            var node = OdfNodeFactory.CreateElement(localName, OdfNamespaces.Svg, "svg");
            node.TextContent = text!;
            FrameNode.AppendChild(node);
        }
    }
}
