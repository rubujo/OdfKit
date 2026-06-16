using System;
using System.Collections.Generic;
using System.IO;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.Image;

/// <summary>
/// 表示 ODF 影像文件 (.odi) 的最小封裝 wrapper。
/// </summary>
public partial class OdfImageDocument : OdfDocument
{
    /// <summary>
    /// 使用指定的 ODF 封裝初始化 <see cref="OdfImageDocument"/> 類別的新執行個體。
    /// </summary>
    /// <param name="package">ODF 封裝。</param>
    public OdfImageDocument(OdfPackage package) : base(package)
    {
        if (string.IsNullOrEmpty(package.MimeType))
        {
            package.SetMimeType("application/vnd.oasis.opendocument.image");
        }
    }

    /// <summary>
    /// 建立新的 ODI 影像文件。
    /// </summary>
    /// <returns>新的 <see cref="OdfImageDocument"/> 執行個體。</returns>
    public static OdfImageDocument Create()
    {
        return (OdfImageDocument)OdfDocumentFactory.CreateDocument(OdfDocumentKind.Image);
    }

    /// <summary>
    /// 從指定路徑載入 ODI 影像文件。
    /// </summary>
    /// <param name="path">ODI 文件路徑。</param>
    /// <returns>載入完成的 <see cref="OdfImageDocument"/> 執行個體。</returns>
    /// <exception cref="InvalidOperationException">當指定文件不是 ODI 影像時擲出。</exception>
    public new static OdfImageDocument Load(string path)
    {
        return EnsureImage(OdfDocumentFactory.LoadDocument(path));
    }

    /// <summary>
    /// 從指定資料流載入 ODI 影像文件。
    /// </summary>
    /// <param name="stream">包含 ODI 文件內容的資料流。</param>
    /// <param name="fileName">選用的檔案名稱，用於輔助格式偵測。</param>
    /// <returns>載入完成的 <see cref="OdfImageDocument"/> 執行個體。</returns>
    /// <exception cref="InvalidOperationException">當指定文件不是 ODI 影像時擲出。</exception>
    public new static OdfImageDocument Load(Stream stream, string? fileName = null)
    {
        return EnsureImage(OdfDocumentFactory.LoadDocument(stream, fileName));
    }

    /// <summary>
    /// 取得主要影像容器節點。
    /// </summary>
    public OdfNode ImageNode => GetImageNode();

    /// <summary>
    /// 取得主要影像框架節點。
    /// </summary>
    public OdfNode? ImageFrame => FindPrimaryFrame(GetImageNode());

    /// <summary>
    /// 取得主要影像參照路徑。
    /// </summary>
    public string? ImageHref
    {
        get
        {
            OdfNode? image = FindDescendant(GetImageNode(), "image", OdfNamespaces.Draw);
            return image?.GetAttribute("href", OdfNamespaces.XLink);
        }
    }

    /// <summary>
    /// 取得主要影像的封裝摘要資訊。
    /// </summary>
    public OdfImageInfo? ImageInfo
    {
        get
        {
            string? href = ImageHref;
            if (string.IsNullOrWhiteSpace(href))
            {
                return null;
            }

            string path = href!;
            if (!Package.HasEntry(path))
            {
                return null;
            }

            string mediaType = Package.Manifest.TryGetValue(path, out string? manifestMediaType)
                ? manifestMediaType
                : string.Empty;
            long size = Package.ReadEntry(path).LongLength;
            return new OdfImageInfo(path, mediaType, size);
        }
    }

    /// <summary>
    /// 取得或設定主要影像框架名稱。
    /// </summary>
    public string? FrameName
    {
        get => ImageFrame?.GetAttribute("name", OdfNamespaces.Draw);
        set => SetOptionalAttribute(EnsurePrimaryFrame(), "name", OdfNamespaces.Draw, value, "draw");
    }

    /// <summary>
    /// 取得或設定主要影像框架標題。
    /// </summary>
    public string? FrameTitle
    {
        get => GetOptionalChildText(ImageFrame, "title", OdfNamespaces.Svg);
        set => SetOptionalChildText(EnsurePrimaryFrame(), "title", OdfNamespaces.Svg, "svg", value);
    }

    /// <summary>
    /// 取得或設定主要影像框架描述。
    /// </summary>
    public string? FrameDescription
    {
        get => GetOptionalChildText(ImageFrame, "desc", OdfNamespaces.Svg);
        set => SetOptionalChildText(EnsurePrimaryFrame(), "desc", OdfNamespaces.Svg, "svg", value);
    }

    /// <summary>
    /// 取得或設定主要影像框架的 X 軸座標位置。
    /// </summary>
    public OdfLength? FrameX
    {
        get => GetOptionalLength(ImageFrame, "x");
        set => SetOptionalLength(EnsurePrimaryFrame(), "x", value);
    }

    /// <summary>
    /// 取得或設定主要影像框架的 Y 軸座標位置。
    /// </summary>
    public OdfLength? FrameY
    {
        get => GetOptionalLength(ImageFrame, "y");
        set => SetOptionalLength(EnsurePrimaryFrame(), "y", value);
    }

    /// <summary>
    /// 取得或設定主要影像框架寬度。
    /// </summary>
    public OdfLength? FrameWidth
    {
        get => GetOptionalLength(ImageFrame, "width");
        set => SetOptionalLength(EnsurePrimaryFrame(), "width", value);
    }

    /// <summary>
    /// 取得或設定主要影像框架高度。
    /// </summary>
    public OdfLength? FrameHeight
    {
        get => GetOptionalLength(ImageFrame, "height");
        set => SetOptionalLength(EnsurePrimaryFrame(), "height", value);
    }

    /// <summary>
    /// 取得主要影像的位元組內容。
    /// </summary>
    /// <returns>主要影像位元組；若文件未參照封裝內影像則為 <see langword="null"/>。</returns>
    public byte[]? GetImageBytes()
    {
        string? href = ImageHref;
        if (string.IsNullOrWhiteSpace(href) || !Package.HasEntry(href!))
        {
            return null;
        }

        return Package.ReadEntry(href!);
    }

    /// <summary>
    /// 設定 ODI 文件的主要影像。
    /// </summary>
    /// <param name="imageBytes">圖片位元組陣列。</param>
    /// <param name="preferredName">選用的偏好檔名。</param>
    /// <returns>影像在 ODF 封裝中的路徑。</returns>
    public string SetImage(byte[] imageBytes, string? preferredName = "image.png")
    {
        OdfMediaManager mediaManager = new(Package);
        string href = mediaManager.AddImage(imageBytes, preferredName);

        OdfNode imageRoot = GetImageNode();
        OdfNode? existingFrame = FindPrimaryFrame(imageRoot);
        foreach (OdfNode child in new List<OdfNode>(imageRoot.Children))
        {
            if (!ReferenceEquals(child, existingFrame))
            {
                imageRoot.RemoveChild(child);
            }
        }

        OdfNode frame = existingFrame ?? OdfNodeFactory.CreateElement("frame", OdfNamespaces.Draw, "draw");
        foreach (OdfNode child in new List<OdfNode>(frame.Children))
        {
            if (child.NodeType is OdfNodeType.Element &&
                child.LocalName is "title" or "desc" &&
                child.NamespaceUri == OdfNamespaces.Svg)
            {
                continue;
            }

            frame.RemoveChild(child);
        }

        OdfNode image = OdfNodeFactory.CreateElement("image", OdfNamespaces.Draw, "draw");
        image.SetAttribute("href", OdfNamespaces.XLink, href, "xlink");
        image.SetAttribute("type", OdfNamespaces.XLink, "simple", "xlink");
        image.SetAttribute("show", OdfNamespaces.XLink, "embed", "xlink");
        image.SetAttribute("actuate", OdfNamespaces.XLink, "onLoad", "xlink");
        frame.AppendChild(image);
        if (frame.Parent is null)
        {
            imageRoot.AppendChild(frame);
        }

        return href;
    }

    /// <summary>
    /// 設定主要影像框架的版面與替代文字。
    /// </summary>
    /// <param name="x">X 軸座標位置。</param>
    /// <param name="y">Y 軸座標位置。</param>
    /// <param name="width">框架寬度。</param>
    /// <param name="height">框架高度。</param>
    /// <param name="name">選用的框架名稱。</param>
    /// <param name="title">選用的框架標題。</param>
    /// <param name="description">選用的框架描述。</param>
    public void SetImageLayout(
        OdfLength x,
        OdfLength y,
        OdfLength width,
        OdfLength height,
        string? name = null,
        string? title = null,
        string? description = null)
    {
        OdfNode frame = EnsurePrimaryFrame();
        SetOptionalLength(frame, "x", x);
        SetOptionalLength(frame, "y", y);
        SetOptionalLength(frame, "width", width);
        SetOptionalLength(frame, "height", height);
        SetOptionalAttribute(frame, "name", OdfNamespaces.Draw, name, "draw");
        SetOptionalChildText(frame, "title", OdfNamespaces.Svg, "svg", title);
        SetOptionalChildText(frame, "desc", OdfNamespaces.Svg, "svg", description);
    }
}
