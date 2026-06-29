using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.Image;

/// <summary>
/// Minimal packaging wrapper representing an ODF image document (.odi).
/// 表示 ODF 影像文件 (.odi) 的最小封裝 wrapper。
/// </summary>
public partial class OdfImageDocument : OdfDocument
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OdfImageDocument"/> class with the specified ODF package.
    /// 使用指定的 ODF 封裝初始化 <see cref="OdfImageDocument"/> 類別的新執行個體。
    /// </summary>
    /// <param name="package">The ODF package. / ODF 封裝。</param>
    public OdfImageDocument(OdfPackage package) : base(package)
    {
        if (string.IsNullOrEmpty(package.MimeType))
        {
            package.SetMimeType("application/vnd.oasis.opendocument.image");
        }
    }

    /// <summary>
    /// Creates a new ODI image document.
    /// 建立新的 ODI 影像文件。
    /// </summary>
    /// <returns>A new <see cref="OdfImageDocument"/> instance. / 新的 <see cref="OdfImageDocument"/> 執行個體。</returns>
    public static OdfImageDocument Create()
    {
        return (OdfImageDocument)OdfDocumentFactory.CreateDocument(OdfDocumentKind.Image);
    }

    /// <summary>
    /// Creates a new ODI image document from the specified image template document.
    /// 從指定的影像範本文件建立新的 ODI 影像文件。
    /// </summary>
    /// <param name="template">The image template document. / 影像範本文件。</param>
    /// <returns>The created <see cref="OdfImageDocument"/> instance. / 建立完成的 <see cref="OdfImageDocument"/> 執行個體。</returns>
    public static OdfImageDocument CreateFromTemplate(ImageTemplateDocument template) =>
        (OdfImageDocument)CreateFromTemplateInternal(template, OdfDocumentKind.Image, "application/vnd.oasis.opendocument.image");

    /// <summary>
    /// Creates an equivalent ODI (ZIP package) image document from a FODI flat XML image document, with identical content.
    /// 從 FODI 扁平 XML 影像文件建立等價的 ODI（ZIP 封裝）影像文件，內容完全相同。
    /// </summary>
    /// <param name="document">The source FODI flat XML image document. / 來源 FODI 扁平 XML 影像文件。</param>
    /// <returns>The created <see cref="OdfImageDocument"/> instance. / 建立完成的 <see cref="OdfImageDocument"/> 執行個體。</returns>
    public static OdfImageDocument CreateFromFlatDocument(FlatImageDocument document) =>
        (OdfImageDocument)ConvertFlatVariantInternal(document, OdfDocumentKind.Image, targetIsFlatXml: false);

    /// <summary>
    /// Loads an ODI image document from the specified path.
    /// 從指定路徑載入 ODI 影像文件。
    /// </summary>
    /// <param name="path">The ODI document path. / ODI 文件路徑。</param>
    /// <returns>The loaded <see cref="OdfImageDocument"/> instance. / 載入完成的 <see cref="OdfImageDocument"/> 執行個體。</returns>
    /// <exception cref="InvalidOperationException">When the specified document is not an ODI image. / 當指定文件不是 ODI 影像時擲出。</exception>
    public new static OdfImageDocument Load(string path)
    {
        return EnsureImage(OdfDocumentFactory.LoadDocument(path));
    }

    /// <summary>
    /// Asynchronously loads an ODI image document from the specified path.
    /// 非同步從指定路徑載入 ODI 影像文件。
    /// </summary>
    /// <param name="path">The ODI document path. / ODI 文件路徑。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消語彙基元。</param>
    /// <returns>A task representing the asynchronous load operation, whose result is the loaded <see cref="OdfImageDocument"/>. / 代表非同步載入作業的工作，其結果為載入完成的 <see cref="OdfImageDocument"/>。</returns>
    public new static async Task<OdfImageDocument> LoadAsync(string path, CancellationToken cancellationToken = default) =>
        EnsureImage(await OdfDocumentFactory.LoadDocumentAsync(path, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// Loads an ODI image document from the specified stream.
    /// 從指定資料流載入 ODI 影像文件。
    /// </summary>
    /// <param name="stream">The stream containing the ODI document content. / 包含 ODI 文件內容的資料流。</param>
    /// <param name="fileName">The optional file name, used to assist format detection. / 選用的檔案名稱，用於輔助格式偵測。</param>
    /// <returns>The loaded <see cref="OdfImageDocument"/> instance. / 載入完成的 <see cref="OdfImageDocument"/> 執行個體。</returns>
    /// <exception cref="InvalidOperationException">When the specified document is not an ODI image. / 當指定文件不是 ODI 影像時擲出。</exception>
    public new static OdfImageDocument Load(Stream stream, string? fileName = null)
    {
        return EnsureImage(OdfDocumentFactory.LoadDocument(stream, fileName));
    }

    /// <summary>
    /// Asynchronously loads an ODI image document from the specified stream.
    /// 非同步從指定資料流載入 ODI 影像文件。
    /// </summary>
    /// <param name="stream">The stream containing the ODI document content. / 包含 ODI 文件內容的資料流。</param>
    /// <param name="fileName">The optional file name, used to assist format detection. / 選用的檔案名稱，用於輔助格式偵測。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消語彙基元。</param>
    /// <returns>A task representing the asynchronous load operation, whose result is the loaded <see cref="OdfImageDocument"/>. / 代表非同步載入作業的工作，其結果為載入完成的 <see cref="OdfImageDocument"/>。</returns>
    public new static async Task<OdfImageDocument> LoadAsync(Stream stream, string? fileName = null, CancellationToken cancellationToken = default) =>
        EnsureImage(await OdfDocumentFactory.LoadDocumentAsync(stream, fileName, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// Gets the main image container node.
    /// 取得主要影像容器節點。
    /// </summary>
    public OdfNode ImageNode => GetImageNode();

    /// <summary>
    /// Gets the main image frame node.
    /// 取得主要影像框架節點。
    /// </summary>
    public OdfNode? ImageFrame => FindPrimaryFrame(GetImageNode());

    /// <summary>
    /// Gets the main image reference path.
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
    /// Gets summary packaging information for the main image.
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
    /// Gets or sets the name of the main image frame.
    /// 取得或設定主要影像框架名稱。
    /// </summary>
    public string? FrameName
    {
        get => ImageFrame?.GetAttribute("name", OdfNamespaces.Draw);
        set => SetOptionalAttribute(EnsurePrimaryFrame(), "name", OdfNamespaces.Draw, value, "draw");
    }

    /// <summary>
    /// Gets or sets the title of the main image frame.
    /// 取得或設定主要影像框架標題。
    /// </summary>
    public string? FrameTitle
    {
        get => GetOptionalChildText(ImageFrame, "title", OdfNamespaces.Svg);
        set => SetOptionalChildText(EnsurePrimaryFrame(), "title", OdfNamespaces.Svg, "svg", value);
    }

    /// <summary>
    /// Gets or sets the description of the main image frame.
    /// 取得或設定主要影像框架描述。
    /// </summary>
    public string? FrameDescription
    {
        get => GetOptionalChildText(ImageFrame, "desc", OdfNamespaces.Svg);
        set => SetOptionalChildText(EnsurePrimaryFrame(), "desc", OdfNamespaces.Svg, "svg", value);
    }

    /// <summary>
    /// Gets or sets the X-axis position of the main image frame.
    /// 取得或設定主要影像框架的 X 軸座標位置。
    /// </summary>
    public OdfLength? FrameX
    {
        get => GetOptionalLength(ImageFrame, "x");
        set => SetOptionalLength(EnsurePrimaryFrame(), "x", value);
    }

    /// <summary>
    /// Gets or sets the Y-axis position of the main image frame.
    /// 取得或設定主要影像框架的 Y 軸座標位置。
    /// </summary>
    public OdfLength? FrameY
    {
        get => GetOptionalLength(ImageFrame, "y");
        set => SetOptionalLength(EnsurePrimaryFrame(), "y", value);
    }

    /// <summary>
    /// Gets or sets the width of the main image frame.
    /// 取得或設定主要影像框架寬度。
    /// </summary>
    public OdfLength? FrameWidth
    {
        get => GetOptionalLength(ImageFrame, "width");
        set => SetOptionalLength(EnsurePrimaryFrame(), "width", value);
    }

    /// <summary>
    /// Gets or sets the height of the main image frame.
    /// 取得或設定主要影像框架高度。
    /// </summary>
    public OdfLength? FrameHeight
    {
        get => GetOptionalLength(ImageFrame, "height");
        set => SetOptionalLength(EnsurePrimaryFrame(), "height", value);
    }

    /// <summary>
    /// Gets the byte content of the main image.
    /// 取得主要影像的位元組內容。
    /// </summary>
    /// <returns>The main image bytes, or <see langword="null"/> if the document does not reference an image within the package. / 主要影像位元組；若文件未參照封裝內影像則為 <see langword="null"/>。</returns>
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
    /// Sets the main image of the ODI document.
    /// 設定 ODI 文件的主要影像。
    /// </summary>
    /// <param name="imageBytes">The image byte array. / 圖片位元組陣列。</param>
    /// <param name="preferredName">The optional preferred file name. / 選用的偏好檔名。</param>
    /// <returns>The path of the image within the ODF package. / 影像在 ODF 封裝中的路徑。</returns>
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
    /// Sets the layout and alternative text of the main image frame.
    /// 設定主要影像框架的版面與替代文字。
    /// </summary>
    /// <param name="x">The X-axis position. / X 軸座標位置。</param>
    /// <param name="y">The Y-axis position. / Y 軸座標位置。</param>
    /// <param name="width">The frame width. / 框架寬度。</param>
    /// <param name="height">The frame height. / 框架高度。</param>
    /// <param name="name">The optional frame name. / 選用的框架名稱。</param>
    /// <param name="title">The optional frame title. / 選用的框架標題。</param>
    /// <param name="description">The optional frame description. / 選用的框架描述。</param>
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
