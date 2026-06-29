using System;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Text;

/// <summary>
/// Represents an image in a text document.
/// 表示文字文件中的圖片。
/// </summary>
public class OdfImage
{
    private readonly OdfDocument? _document;
    private OdfImageLayout? _layout;

    /// <summary>
    /// Gets the image's frame node.
    /// 取得圖片的外框節點。
    /// </summary>
    public OdfNode FrameNode { get; }

    /// <summary>
    /// Gets the image's picture node.
    /// 取得圖片的影像節點。
    /// </summary>
    public OdfNode ImageNode { get; }

    /// <summary>
    /// Gets the image's layout settings (e.g. border, margin, wrap, crop, and opacity).
    /// 取得影像的版面配置設定（如框線、邊距、環繞、裁剪與透明度）。
    /// </summary>
    public OdfImageLayout Layout => _layout ??= new OdfImageLayout(this, _document);

    /// <summary>
    /// Initializes a new instance of the <see cref="OdfImage"/> class.
    /// 初始化 <see cref="OdfImage"/> 類別的新執行個體。
    /// </summary>
    public OdfImage(OdfNode frameNode, OdfNode imageNode) : this(frameNode, imageNode, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OdfImage"/> class with the specified nodes and document.
    /// 使用指定的節點與文件初始化 <see cref="OdfImage"/> 類別的新執行個體。
    /// </summary>
    public OdfImage(OdfNode frameNode, OdfNode imageNode, OdfDocument? document)
    {
        FrameNode = frameNode ?? throw new ArgumentNullException(nameof(frameNode));
        ImageNode = imageNode ?? throw new ArgumentNullException(nameof(imageNode));
        _document = document;
    }

    /// <summary>
    /// Gets whether this image is marked as decorative (with LibreOffice <c>loext:decorative</c> compatibility reading).
    /// 取得此圖片是否標記為裝飾性（含 LibreOffice <c>loext:decorative</c> 相容讀取）。
    /// </summary>
    public bool IsDecorative => OdfLoExtInteropEngine.IsDecorative(FrameNode);

    /// <summary>
    /// Marks this image as decorative so assistive technologies skip over it.
    /// 將此圖片標記為裝飾性，輔助技術應略過此物件。
    /// </summary>
    /// <param name="decorative">Whether to mark it as decorative. / 是否標記為裝飾性。</param>
    /// <returns>The current image instance. / 目前圖片執行個體。</returns>
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

        FrameNode.RemoveAttribute("decorative", OdfNamespaces.LoExt);
        return this;
    }

    /// <summary>
    /// Gets or sets the image's name.
    /// 取得或設定圖片的名稱。
    /// </summary>
    public string? Name
    {
        get => FrameNode.GetAttribute("name", OdfNamespaces.Draw);
        set => FrameNode.SetAttribute("name", OdfNamespaces.Draw, value ?? string.Empty, "draw");
    }

    /// <summary>
    /// Gets the image's reference path within the ODF package.
    /// 取得圖片在 ODF 封裝中的參照路徑。
    /// </summary>
    public string? ImageHref => ImageNode.GetAttribute("href", OdfNamespaces.XLink);

    /// <summary>
    /// Gets or sets the image's anchor type.
    /// 取得或設定圖片的錨定類型。
    /// </summary>
    public string? AnchorType
    {
        get => FrameNode.GetAttribute("anchor-type", OdfNamespaces.Text);
        set => FrameNode.SetAttribute("anchor-type", OdfNamespaces.Text, value ?? "paragraph", "text");
    }

    /// <summary>
    /// Gets or sets the image's width.
    /// 取得或設定圖片的寬度。
    /// </summary>
    public string? Width
    {
        get => FrameNode.GetAttribute("width", OdfNamespaces.Svg);
        set => FrameNode.SetAttribute("width", OdfNamespaces.Svg, value ?? string.Empty, "svg");
    }

    /// <summary>
    /// Gets or sets the image's height.
    /// 取得或設定圖片的高度。
    /// </summary>
    public string? Height
    {
        get => FrameNode.GetAttribute("height", OdfNamespaces.Svg);
        set => FrameNode.SetAttribute("height", OdfNamespaces.Svg, value ?? string.Empty, "svg");
    }

    /// <summary>
    /// Gets or sets the image's text-wrap style.
    /// 取得或設定圖片的文繞圖樣式。
    /// </summary>
    public string? WrapStyle
    {
        get => FrameNode.GetAttribute("wrap-style", OdfNamespaces.Style);
        set => FrameNode.SetAttribute("wrap-style", OdfNamespaces.Style, value ?? "none", "style");
    }

    /// <summary>
    /// Gets or sets the image's crop boundary.
    /// 取得或設定圖片的裁剪邊界。
    /// </summary>
    public string? CropTop
    {
        get => ImageNode.GetAttribute("clip", OdfNamespaces.Fo);
        set => ImageNode.SetAttribute("clip", OdfNamespaces.Fo, value ?? string.Empty, "fo");
    }

    /// <summary>
    /// Gets or sets the image's accessible alternative text (maps to <c>&lt;svg:desc&gt;</c>).
    /// 取得或設定圖片的無障礙替代文字（對應 <c>&lt;svg:desc&gt;</c>）。
    /// </summary>
    public string? AltText
    {
        get => FindSvgChildText("desc");
        set => SetSvgChildText("desc", value);
    }

    /// <summary>
    /// Gets or sets the image's accessibility title (maps to <c>&lt;svg:title&gt;</c>).
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
