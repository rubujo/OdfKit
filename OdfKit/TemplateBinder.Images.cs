using System;
using System.Collections.Generic;
using System.Linq;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit;

/// <summary>
/// Provides image placeholder binding helpers for <see cref="TemplateBinder"/>.
/// 提供 <see cref="TemplateBinder"/> 的圖片占位符繫結 helper。
/// </summary>
public static partial class TemplateBinder
{
    private static int ReplaceImageParagraphs(
        OdfPackage package,
        OdfNode root,
        IReadOnlyDictionary<string, object?> values,
        OdfTemplateBindOptions options,
        OdfTemplateBindReport report)
    {
        int changed = 0;
        foreach (OdfNode paragraph in root.Descendants()
            .Where(static node => node.NodeType is OdfNodeType.Element &&
                node.LocalName == "p" &&
                node.NamespaceUri == OdfNamespaces.Text)
            .ToArray())
        {
            if (!TryResolveImagePlaceholder(paragraph.TextContent, values, report, out string? expression, out OdfTemplateImageValue? image))
            {
                ReportNonExclusiveImagePlaceholder(paragraph.TextContent, "TextDocument", paragraph.LocalName, report);
                continue;
            }

            AddHit(report, expression!);
            report.ImageReplacementCount++;
            report.ReplacementCount++;
            if (!options.DryRun)
            {
                ReplaceNodeChildrenWithImage(package, paragraph, image!, textAnchored: true);
            }

            changed++;
        }

        return changed;
    }

    private static int ReplaceImageFrame(
        OdfPackage package,
        OdfNode frame,
        IReadOnlyDictionary<string, object?> values,
        OdfTemplateBindOptions options,
        OdfTemplateBindReport report,
        string documentKind)
    {
        if (!TryResolveImagePlaceholder(frame.TextContent, values, report, out string? expression, out OdfTemplateImageValue? image))
        {
            ReportNonExclusiveImagePlaceholder(frame.TextContent, documentKind, frame.LocalName, report);
            return 0;
        }

        AddHit(report, expression!);
        report.ImageReplacementCount++;
        report.ReplacementCount++;
        if (!options.DryRun)
        {
            ReplaceNodeChildrenWithImage(package, frame, image!, textAnchored: false);
        }

        return 1;
    }

    private static bool TryResolveImagePlaceholder(
        string text,
        IReadOnlyDictionary<string, object?> values,
        OdfTemplateBindReport report,
        out string? expression,
        out OdfTemplateImageValue? image)
    {
        expression = null;
        image = null;
        string trimmed = (text ?? string.Empty).Trim();
        if (!trimmed.StartsWith("{{", StringComparison.Ordinal) ||
            !trimmed.EndsWith("}}", StringComparison.Ordinal))
        {
            return false;
        }

        expression = trimmed.Substring(2, trimmed.Length - 4).Trim();
        if (!expression.StartsWith("Image:", StringComparison.Ordinal))
        {
            return false;
        }

        string key = expression.Substring("Image:".Length).Trim();
        if (string.IsNullOrWhiteSpace(key))
        {
            AddUnresolved(report, expression);
            AddImageWarning(report, "TPLIMG0001: empty image placeholder key");
            return false;
        }

        object? value = ResolvePath(values, key);
        if (value is OdfTemplateImageValue imageValue)
        {
            image = imageValue;
            return true;
        }

        AddUnresolved(report, expression);
        AddImageWarning(
            report,
            value is null
                ? "TPLIMG0002: missing image value " + key
                : "TPLIMG0003: image placeholder value must be OdfTemplateImageValue " + key);
        return false;
    }

    private static void ReportNonExclusiveImagePlaceholder(
        string text,
        string documentKind,
        string locationHint,
        OdfTemplateBindReport report)
    {
        foreach (string expression in EnumerateImagePlaceholderExpressions(text))
        {
            if (!string.Equals((text ?? string.Empty).Trim(), BuildToken(expression), StringComparison.Ordinal))
            {
                AddUnresolved(report, expression);
                AddImageWarning(report, "TPLIMG0004: image placeholder must occupy the whole paragraph or text box");
                AddUnresolvedDetail(report, expression, documentKind, locationHint);
            }
        }
    }

    private static void ReportUnsupportedImagePlaceholder(
        string text,
        string documentKind,
        string locationHint,
        OdfTemplateBindReport report)
    {
        foreach (string expression in EnumerateImagePlaceholderExpressions(text))
        {
            AddUnresolved(report, expression);
            AddImageWarning(report, "TPLIMG0005: image placeholders are not supported in spreadsheets");
            AddUnresolvedDetail(report, expression, documentKind, locationHint);
        }
    }

    private static IEnumerable<string> EnumerateImagePlaceholderExpressions(string text)
    {
        string source = text ?? string.Empty;
        int index = 0;
        while (index < source.Length)
        {
            int start = source.IndexOf("{{", index, StringComparison.Ordinal);
            if (start < 0)
            {
                yield break;
            }

            int end = source.IndexOf("}}", start + 2, StringComparison.Ordinal);
            if (end < 0)
            {
                yield break;
            }

            string expression = source.Substring(start + 2, end - start - 2).Trim();
            if (expression.StartsWith("Image:", StringComparison.Ordinal))
            {
                yield return expression;
            }

            index = end + 2;
        }
    }

    private static void ReplaceNodeChildrenWithImage(
        OdfPackage package,
        OdfNode container,
        OdfTemplateImageValue image,
        bool textAnchored)
    {
        foreach (OdfNode child in container.Children.ToArray())
        {
            container.RemoveChild(child);
        }

        var media = new OdfMediaManager(package);
        string href = media.AddImage(image.Bytes, image.FileName);
        OdfNode frame = textAnchored
            ? OdfNodeFactory.CreateElement("frame", OdfNamespaces.Draw, "draw")
            : container;
        if (textAnchored)
        {
            frame.SetAttribute("anchor-type", OdfNamespaces.Text, "paragraph", "text");
        }

        frame.SetAttribute("width", OdfNamespaces.Svg, (image.Width ?? OdfLength.FromCentimeters(4)).ToString(), "svg");
        frame.SetAttribute("height", OdfNamespaces.Svg, (image.Height ?? OdfLength.FromCentimeters(3)).ToString(), "svg");

        OdfNode imageNode = OdfNodeFactory.CreateElement("image", OdfNamespaces.Draw, "draw");
        imageNode.SetAttribute("href", OdfNamespaces.XLink, href, "xlink");
        imageNode.SetAttribute("type", OdfNamespaces.XLink, "simple", "xlink");
        imageNode.SetAttribute("show", OdfNamespaces.XLink, "embed", "xlink");
        imageNode.SetAttribute("actuate", OdfNamespaces.XLink, "onLoad", "xlink");
        if (!string.IsNullOrWhiteSpace(image.AltText))
        {
            OdfNode desc = OdfNodeFactory.CreateElement("desc", OdfNamespaces.Svg, "svg");
            desc.TextContent = image.AltText!;
            frame.AppendChild(desc);
        }

        frame.AppendChild(imageNode);
        if (textAnchored)
        {
            container.AppendChild(frame);
        }
    }
}
