using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.Image;

public partial class OdfImageDocument
{
    /// <summary>
    /// 取得文件中所有影像框架的摘要清單。
    /// </summary>
    public IReadOnlyList<OdfImageFrameInfo> GetImageFrames() =>
        OdfImageDocumentReadEngine.GetImageFrames(this);

    /// <summary>
    /// 新增一個影像框架（不取代既有框架）。
    /// </summary>
    /// <param name="imageBytes">圖片位元組陣列</param>
    /// <param name="x">X 軸座標位置</param>
    /// <param name="y">Y 軸座標位置</param>
    /// <param name="width">框架寬度</param>
    /// <param name="height">框架高度</param>
    /// <param name="preferredName">選用的偏好檔名</param>
    /// <param name="name">選用的框架名稱</param>
    /// <param name="title">選用的框架標題</param>
    /// <param name="description">選用的框架描述</param>
    /// <returns>影像在 ODF 封裝中的路徑</returns>
    public string AddImageFrame(
        byte[] imageBytes,
        OdfLength x,
        OdfLength y,
        OdfLength width,
        OdfLength height,
        string? preferredName = null,
        string? name = null,
        string? title = null,
        string? description = null)
    {
        if (imageBytes is null)
        {
            throw new System.ArgumentNullException(nameof(imageBytes));
        }

        OdfMediaManager mediaManager = new(Package);
        string href = mediaManager.AddImage(imageBytes, preferredName ?? "image.png");

        OdfNode frame = OdfNodeFactory.CreateElement("frame", OdfNamespaces.Draw, "draw");
        frame.SetAttribute("x", OdfNamespaces.Svg, x.ToString(), "svg");
        frame.SetAttribute("y", OdfNamespaces.Svg, y.ToString(), "svg");
        frame.SetAttribute("width", OdfNamespaces.Svg, width.ToString(), "svg");
        frame.SetAttribute("height", OdfNamespaces.Svg, height.ToString(), "svg");
        if (!string.IsNullOrWhiteSpace(name))
        {
            frame.SetAttribute("name", OdfNamespaces.Draw, name!, "draw");
        }

        if (!string.IsNullOrWhiteSpace(title))
        {
            OdfNode titleNode = OdfNodeFactory.CreateElement("title", OdfNamespaces.Svg, "svg");
            titleNode.TextContent = title!;
            frame.AppendChild(titleNode);
        }

        if (!string.IsNullOrWhiteSpace(description))
        {
            OdfNode descNode = OdfNodeFactory.CreateElement("desc", OdfNamespaces.Svg, "svg");
            descNode.TextContent = description!;
            frame.AppendChild(descNode);
        }

        OdfNode image = OdfNodeFactory.CreateElement("image", OdfNamespaces.Draw, "draw");
        image.SetAttribute("href", OdfNamespaces.XLink, href, "xlink");
        image.SetAttribute("type", OdfNamespaces.XLink, "simple", "xlink");
        image.SetAttribute("show", OdfNamespaces.XLink, "embed", "xlink");
        image.SetAttribute("actuate", OdfNamespaces.XLink, "onLoad", "xlink");
        frame.AppendChild(image);
        GetImageNode().AppendChild(frame);
        return href;
    }
}
