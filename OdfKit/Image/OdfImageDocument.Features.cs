using System;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.Image;

public partial class OdfImageDocument
{
    /// <summary>
    /// Gets a summary list of all image frames in the document.
    /// 取得文件中所有影像框架的摘要清單。
    /// </summary>
    public IReadOnlyList<OdfImageFrameInfo> GetImageFrames() =>
        OdfImageDocumentReadEngine.GetImageFrames(this);

    /// <summary>
    /// Adds an image frame (without replacing existing frames).
    /// 新增一個影像框架（不取代既有框架）。
    /// </summary>
    /// <param name="imageBytes">The image byte array. / 圖片位元組陣列。</param>
    /// <param name="x">The X-axis position. / X 軸座標位置。</param>
    /// <param name="y">The Y-axis position. / Y 軸座標位置。</param>
    /// <param name="width">The frame width. / 框架寬度。</param>
    /// <param name="height">The frame height. / 框架高度。</param>
    /// <param name="preferredName">The optional preferred file name. / 選用的偏好檔名。</param>
    /// <param name="name">The optional frame name. / 選用的框架名稱。</param>
    /// <param name="title">The optional frame title. / 選用的框架標題。</param>
    /// <param name="description">The optional frame description. / 選用的框架描述。</param>
    /// <returns>The path of the image within the ODF package. / 影像在 ODF 封裝中的路徑。</returns>
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

    /// <summary>
    /// Batch-adds multiple image frames (without replacing existing frames).
    /// 批次新增多個影像框架（不取代既有框架）。
    /// </summary>
    /// <param name="requests">The list of image frame requests to add. / 要新增的影像框架請求清單。</param>
    /// <returns>The list of image paths within the ODF package, in request order. / 依請求順序排列的影像在 ODF 封裝中的路徑清單。</returns>
    /// <exception cref="ArgumentNullException">When <paramref name="requests"/> or any request within it is <see langword="null"/>. / 當 <paramref name="requests"/> 或其中任一筆請求為 <see langword="null"/> 時擲出。</exception>
    public IReadOnlyList<string> AddImageFrames(IEnumerable<OdfImageFrameRequest> requests)
    {
        if (requests is null)
        {
            throw new ArgumentNullException(nameof(requests));
        }

        List<string> hrefs = [];
        foreach (OdfImageFrameRequest request in requests)
        {
            if (request is null)
            {
                throw new ArgumentNullException(nameof(requests));
            }

            hrefs.Add(AddImageFrame(
                request.ImageBytes,
                request.X,
                request.Y,
                request.Width,
                request.Height,
                request.PreferredName,
                request.Name,
                request.Title,
                request.Description));
        }

        return hrefs;
    }
}
