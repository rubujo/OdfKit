using System;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Presentation;

/// <summary>
/// Represents a media object linked to a presentation slide.
/// 表示與簡報投影片連結的媒體物件。
/// </summary>
public sealed class OdfMediaObject
{
    private readonly OdfNode _pluginNode;

    internal OdfMediaObject(OdfNode frameNode, OdfSlide slide)
    {
        FrameNode = frameNode ?? throw new ArgumentNullException(nameof(frameNode));
        Slide = slide ?? throw new ArgumentNullException(nameof(slide));
        _pluginNode = FindPlugin(frameNode) ?? throw new ArgumentException(
            OdfKit.Compliance.OdfLocalizer.GetMessage("Err_OdfImageDocument_FrameCannotBeEmpty"),
            nameof(frameNode));
    }

    internal OdfNode FrameNode { get; }

    internal OdfSlide Slide { get; }

    /// <summary>
    /// Gets the drawing object identifier.
    /// 取得繪圖物件識別碼。
    /// </summary>
    public string Id =>
        FrameNode.GetAttribute("id", OdfNamespaces.Draw) ??
        FrameNode.GetAttribute("id", OdfNamespaces.Xml) ??
        string.Empty;

    /// <summary>
    /// Gets or sets the media path inside the package.
    /// 取得或設定媒體在封裝包內的路徑。
    /// </summary>
    public string PackagePath
    {
        get => _pluginNode.GetAttribute("href", OdfNamespaces.XLink) ?? string.Empty;
        set => _pluginNode.SetAttribute("href", OdfNamespaces.XLink, value, "xlink");
    }

    /// <summary>
    /// Gets or sets the media MIME type.
    /// 取得或設定媒體 MIME 類型。
    /// </summary>
    public string MimeType
    {
        get => _pluginNode.GetAttribute("mime-type", OdfNamespaces.Draw) ?? string.Empty;
        set => _pluginNode.SetAttribute("mime-type", OdfNamespaces.Draw, value, "draw");
    }

    private static OdfNode? FindPlugin(OdfNode node)
    {
        foreach (OdfNode child in node.Children)
        {
            if (child.NodeType == OdfNodeType.Element &&
                child.LocalName == "plugin" &&
                child.NamespaceUri == OdfNamespaces.Draw)
            {
                return child;
            }

            OdfNode? descendant = FindPlugin(child);
            if (descendant is not null)
                return descendant;
        }

        return null;
    }
}
