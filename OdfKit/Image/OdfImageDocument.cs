using System;
using System.Collections.Generic;
using System.IO;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Image;

/// <summary>
/// 表示 ODF 影像文件 (.odi) 的最小封裝 wrapper。
/// </summary>
public class OdfImageDocument : OdfDocument
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
        foreach (OdfNode child in new List<OdfNode>(imageRoot.Children))
        {
            imageRoot.RemoveChild(child);
        }

        OdfNode frame = OdfNodeFactory.CreateElement("frame", OdfNamespaces.Draw, "draw");
        OdfNode image = OdfNodeFactory.CreateElement("image", OdfNamespaces.Draw, "draw");
        image.SetAttribute("href", OdfNamespaces.XLink, href, "xlink");
        image.SetAttribute("type", OdfNamespaces.XLink, "simple", "xlink");
        image.SetAttribute("show", OdfNamespaces.XLink, "embed", "xlink");
        image.SetAttribute("actuate", OdfNamespaces.XLink, "onLoad", "xlink");
        frame.AppendChild(image);
        imageRoot.AppendChild(frame);

        return href;
    }

    private static OdfImageDocument EnsureImage(OdfDocument document)
    {
        if (document is OdfImageDocument image)
        {
            return image;
        }

        document.Dispose();
        throw new InvalidOperationException("指定的 ODF 文件不是 ODI 影像。");
    }

    /// <summary>
    /// 取得預設的內容 XML 字串。
    /// </summary>
    /// <returns>預設的內容 XML 字串。</returns>
    protected override string GetDefaultContentXml()
    {
        return "<office:document-content " +
            "xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" " +
            "xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\" " +
            "xmlns:xlink=\"http://www.w3.org/1999/xlink\" " +
            "office:version=\"" + OdfVersionInfo.DefaultVersionString + "\">" +
            "<office:body><office:image /></office:body>" +
            "</office:document-content>";
    }

    /// <summary>
    /// 取得預設的樣式 XML 字串。
    /// </summary>
    /// <returns>預設的樣式 XML 字串。</returns>
    protected override string GetDefaultStylesXml()
    {
        return "<office:document-styles " +
            "xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" " +
            "xmlns:style=\"urn:oasis:names:tc:opendocument:xmlns:style:1.0\" " +
            "office:version=\"" + OdfVersionInfo.DefaultVersionString + "\">" +
            "<office:styles /></office:document-styles>";
    }

    /// <summary>
    /// 合併來源影像文件的內容節點至此文件。
    /// </summary>
    /// <param name="sourceDoc">來源文件。</param>
    /// <param name="options">合併選項。</param>
    /// <param name="renameMap">樣式重新命名對照表。</param>
    /// <exception cref="ArgumentException">當來源文件不是 <see cref="OdfImageDocument"/> 時擲出。</exception>
    protected override void MergeContentNodes(OdfDocument sourceDoc, OdfMergeOptions options, Dictionary<string, string> renameMap)
    {
        var source = sourceDoc as OdfImageDocument ?? throw new ArgumentException("來源文件必須是 OdfImageDocument。", nameof(sourceDoc));
        OdfNode sourceBody = source.FindOrCreateChild(source.ContentDom, "body", OdfNamespaces.Office, "office");
        OdfNode sourceImage = source.FindOrCreateChild(sourceBody, "image", OdfNamespaces.Office, "office");
        OdfNode body = FindOrCreateChild(ContentDom, "body", OdfNamespaces.Office, "office");
        OdfNode image = FindOrCreateChild(body, "image", OdfNamespaces.Office, "office");

        foreach (OdfNode child in sourceImage.Children)
        {
            if (child.NodeType == OdfNodeType.Element)
            {
                image.AppendChild(OdfNode.ImportNode(child, source.Package, Package));
            }
        }
    }

    private OdfNode GetImageNode()
    {
        OdfNode body = FindOrCreateChild(ContentDom, "body", OdfNamespaces.Office, "office");
        return FindOrCreateChild(body, "image", OdfNamespaces.Office, "office");
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

/// <summary>
/// 表示 ODI 主要影像的高階摘要。
/// </summary>
/// <param name="path">影像在 ODF 封裝中的路徑。</param>
/// <param name="mediaType">影像媒體類型。</param>
/// <param name="size">影像項目位元組大小。</param>
public sealed class OdfImageInfo(string path, string mediaType, long size)
{
    /// <summary>
    /// 取得影像在 ODF 封裝中的路徑。
    /// </summary>
    public string Path { get; } = path ?? throw new ArgumentNullException(nameof(path));

    /// <summary>
    /// 取得影像媒體類型。
    /// </summary>
    public string MediaType { get; } = mediaType ?? string.Empty;

    /// <summary>
    /// 取得影像項目位元組大小。
    /// </summary>
    public long Size { get; } = size;
}
