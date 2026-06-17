using System.Collections.Generic;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Presentation;

/// <summary>
/// 投影片預留位置讀取引擎（內部協作者）。
/// </summary>
internal static class OdfSlidePlaceholderReadEngine
{
    internal static IReadOnlyList<OdfPlaceholderInfo> GetPlaceholders(OdfSlide slide)
    {
        List<OdfPlaceholderInfo> placeholders = [];

        foreach (OdfNode child in slide.Node.Children)
        {
            if (child.NodeType is not OdfNodeType.Element ||
                child.NamespaceUri != OdfNamespaces.Draw ||
                child.GetAttribute("placeholder", OdfNamespaces.Presentation) != "true")
                continue;

            string? classValue = child.GetAttribute("class", OdfNamespaces.Presentation);
            placeholders.Add(new OdfPlaceholderInfo(
                child.GetAttribute("id", OdfNamespaces.Draw) ?? string.Empty,
                OdfPlaceholderTemplate.KebabToType(classValue ?? "text"),
                child.GetAttribute("x", OdfNamespaces.Svg),
                child.GetAttribute("y", OdfNamespaces.Svg),
                child.GetAttribute("width", OdfNamespaces.Svg),
                child.GetAttribute("height", OdfNamespaces.Svg)));
        }

        return placeholders.AsReadOnly();
    }
}
