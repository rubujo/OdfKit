using System;
using System.Collections.Generic;
using System.Globalization;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Image;

/// <summary>
/// 影像文件框架讀取引擎（內部協作者）。
/// </summary>
internal static class OdfImageDocumentReadEngine
{
    internal static IReadOnlyList<OdfImageFrameInfo> GetImageFrames(OdfImageDocument document)
    {
        List<OdfImageFrameInfo> frames = [];
        CollectFrames(document.ImageNode, document.Package, frames);
        return frames.AsReadOnly();
    }

    private static void CollectFrames(OdfNode parent, OdfPackage package, List<OdfImageFrameInfo> frames)
    {
        foreach (OdfNode child in parent.Children)
        {
            if (child.NodeType is not OdfNodeType.Element || child.NamespaceUri != OdfNamespaces.Draw)
            {
                continue;
            }

            if (child.LocalName != "frame")
            {
                continue;
            }

            OdfNode? imageNode = FindChild(child, "image", OdfNamespaces.Draw);
            string? href = imageNode?.GetAttribute("href", OdfNamespaces.XLink);
            string? mediaType = null;
            long? size = null;
            if (!string.IsNullOrWhiteSpace(href) && package.HasEntry(href!))
            {
                mediaType = package.Manifest.TryGetValue(href!, out string? manifestMediaType)
                    ? manifestMediaType
                    : string.Empty;
                size = package.ReadEntry(href!).LongLength;
            }

            double? rotationDegrees = ParseRotationDegrees(child.GetAttribute("transform", OdfNamespaces.Draw));
            OdfImageCropInfo? crop = OdfImageCropInfo.TryParse(imageNode?.GetAttribute("clip", OdfNamespaces.Fo), out OdfImageCropInfo? parsedCrop)
                ? parsedCrop
                : null;

            frames.Add(new OdfImageFrameInfo(
                child.GetAttribute("name", OdfNamespaces.Draw),
                GetOptionalChildText(child, "title", OdfNamespaces.Svg),
                GetOptionalChildText(child, "desc", OdfNamespaces.Svg),
                href,
                mediaType,
                size,
                child.GetAttribute("x", OdfNamespaces.Svg),
                child.GetAttribute("y", OdfNamespaces.Svg),
                child.GetAttribute("width", OdfNamespaces.Svg),
                child.GetAttribute("height", OdfNamespaces.Svg),
                rotationDegrees,
                crop));
        }
    }

    private static double? ParseRotationDegrees(string? transform)
    {
        if (string.IsNullOrWhiteSpace(transform))
        {
            return null;
        }

        int start = transform!.IndexOf("rotate(", StringComparison.OrdinalIgnoreCase);
        if (start < 0)
        {
            return null;
        }

        start += "rotate(".Length;
        int end = transform.IndexOf(')', start);
        if (end < 0)
        {
            return null;
        }

        string radiansText = transform.Substring(start, end - start).Trim();
        return double.TryParse(radiansText, NumberStyles.Float, CultureInfo.InvariantCulture, out double radians)
            ? radians * 180.0 / Math.PI
            : null;
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

    private static string? GetOptionalChildText(OdfNode node, string localName, string namespaceUri)
    {
        OdfNode? child = FindChild(node, localName, namespaceUri);
        return child is null || string.IsNullOrEmpty(child.TextContent) ? null : child.TextContent;
    }
}
