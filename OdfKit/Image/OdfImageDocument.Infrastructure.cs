using System;
using System.Collections.Generic;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.Image;

public partial class OdfImageDocument
{
    #region Image Document Infrastructure

    private static OdfImageDocument EnsureImage(OdfDocument document)
    {
        if (document is OdfImageDocument image && document.DocumentKind == OdfDocumentKind.Image)
        {
            return image;
        }

        document.Dispose();
        throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_OdfImageDocument_SpecifiedOdfFileOdi"));
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
            "xmlns:svg=\"urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0\" " +
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
        var source = sourceDoc as OdfImageDocument ?? throw new ArgumentException(OdfLocalizer.GetMessage("Err_OdfImageDocument_SourceFileOdfimagedocument"), nameof(sourceDoc));
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

    private OdfNode EnsurePrimaryFrame()
    {
        OdfNode imageRoot = GetImageNode();
        OdfNode? frame = FindPrimaryFrame(imageRoot);
        if (frame is not null)
        {
            return frame;
        }

        frame = OdfNodeFactory.CreateElement("frame", OdfNamespaces.Draw, "draw");
        imageRoot.AppendChild(frame);
        return frame;
    }

    private static OdfNode? FindPrimaryFrame(OdfNode imageRoot) =>
        FindDescendant(imageRoot, "frame", OdfNamespaces.Draw);

    private static OdfLength? GetOptionalLength(OdfNode? frame, string localName)
    {
        string? value = frame?.GetAttribute(localName, OdfNamespaces.Svg);
        return OdfLength.TryParse(value, out OdfLength length) ? length : (OdfLength?)null;
    }

    private static void SetOptionalLength(OdfNode frame, string localName, OdfLength? value)
    {
        if (value.HasValue)
        {
            frame.SetAttribute(localName, OdfNamespaces.Svg, value.Value.ToString(), "svg");
            return;
        }

        frame.RemoveAttribute(localName, OdfNamespaces.Svg);
    }

    private static void SetOptionalAttribute(OdfNode node, string localName, string namespaceUri, string? value, string prefix)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            node.RemoveAttribute(localName, namespaceUri);
            return;
        }

        node.SetAttribute(localName, namespaceUri, value!, prefix);
    }

    private static string? GetOptionalChildText(OdfNode? node, string localName, string namespaceUri)
    {
        OdfNode? child = node is null ? null : FindChild(node, localName, namespaceUri);
        return child is null || string.IsNullOrEmpty(child.TextContent) ? null : child.TextContent;
    }

    private static void SetOptionalChildText(
        OdfNode node,
        string localName,
        string namespaceUri,
        string prefix,
        string? value)
    {
        OdfNode? child = FindChild(node, localName, namespaceUri);
        if (string.IsNullOrWhiteSpace(value))
        {
            if (child is not null)
            {
                node.RemoveChild(child);
            }

            return;
        }

        child ??= OdfNodeFactory.CreateElement(localName, namespaceUri, prefix);
        child.TextContent = value!;
        if (child.Parent is null)
        {
            OdfNode? image = FindChild(node, "image", OdfNamespaces.Draw);
            if (image is not null)
            {
                node.InsertBefore(child, image);
            }
            else
            {
                node.AppendChild(child);
            }
        }
    }

    private static OdfNode? FindChild(OdfNode node, string localName, string namespaceUri)
    {
        foreach (OdfNode child in node.Children)
        {
            if (child.NodeType is OdfNodeType.Element &&
                child.LocalName == localName &&
                child.NamespaceUri == namespaceUri)
            {
                return child;
            }
        }

        return null;
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

    #endregion
}
