using System;
using System.Collections;
using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.Text;

/// <summary>
/// Adds images to text documents.
/// 提供圖片新增入口。
/// </summary>
public sealed class OdfTextImageCollection : IEnumerable<OdfImage>
{
    private readonly TextDocument _document;

    /// <summary>
    /// Initializes a new instance of the <see cref="OdfTextImageCollection"/> class.
    /// 初始化 <see cref="OdfTextImageCollection"/> 類別的新執行個體。
    /// </summary>
    /// <param name="document">The owning text document. / 所屬文字文件。</param>
    public OdfTextImageCollection(TextDocument document)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
    }

    /// <summary>
    /// Adds an image to a new paragraph.
    /// 新增圖片至新的段落。
    /// </summary>
    /// <param name="imageBytes">The binary image content. / 圖片二進位內容。</param>
    /// <param name="width">The image width. / 圖片寬度。</param>
    /// <param name="height">The image height. / 圖片高度。</param>
    /// <param name="name">The optional image name. / 選用的圖片名稱。</param>
    /// <returns>The newly added image. / 新增完成的圖片。</returns>
    public OdfImage Add(byte[] imageBytes, OdfLength width, OdfLength height, string? name = null)
    {
        var media = new OdfMediaManager(_document.Package);
        string path = media.AddImage(imageBytes, name);
        OdfParagraph paragraph = _document.AddParagraph();
        return _document.AddImage(paragraph, path, width, height, name);
    }

    /// <summary>
    /// Gets the list of images in the document body.
    /// 取得文件本文中的圖片清單。
    /// </summary>
    public IReadOnlyList<OdfImage> Items
    {
        get
        {
            List<OdfImage> images = [];
            CollectImages(_document.BodyTextRoot, images);
            return images.AsReadOnly();
        }
    }

    /// <summary>
    /// Gets an enumerator over the images, for use with LINQ queries.
    /// 取得圖片列舉器，供 LINQ 查詢使用。
    /// </summary>
    /// <returns>The image enumerator. / 圖片列舉器。</returns>
    public IEnumerator<OdfImage> GetEnumerator()
    {
        return Items.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    private void CollectImages(OdfNode node, List<OdfImage> images)
    {
        foreach (OdfNode child in node.Children)
        {
            if (child.NodeType is OdfNodeType.Element &&
                child.LocalName == "frame" &&
                child.NamespaceUri == OdfNamespaces.Draw)
            {
                OdfNode? image = FindDescendant(child, "image", OdfNamespaces.Draw);
                if (image is not null)
                {
                    images.Add(new OdfImage(child, image, _document));
                }
            }

            CollectImages(child, images);
        }
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
