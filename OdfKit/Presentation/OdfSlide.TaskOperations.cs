using System.Collections.Generic;
using System.Linq;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Presentation;

/// <summary>
/// Provides task-oriented slide operations.
/// 提供任務導向的投影片作業。
/// </summary>
public partial class OdfSlide
{
    /// <summary>
    /// Sets the visible text of the first placeholder with the requested type.
    /// 設定第一個指定類型預留位置的可見文字。
    /// </summary>
    /// <param name="placeholderType">The placeholder type. / 預留位置類型。</param>
    /// <param name="text">The replacement text. / 取代文字。</param>
    /// <returns>The placeholder update result. / 預留位置更新結果。</returns>
    public OdpPlaceholderUpdateResult SetPlaceholderText(OdfPlaceholderType placeholderType, string? text)
    {
        OdfPlaceholder[] matches = Placeholders
            .Where(placeholder => placeholder.PlaceholderType == placeholderType)
            .ToArray();
        var result = new OdpPlaceholderUpdateResult();
        if (matches.Length == 0)
        {
            result.MissingPlaceholderTypes.Add(placeholderType);
            return result;
        }

        OdfNode paragraph = FindDescendant(matches[0].Node, "p", OdfNamespaces.Text) ??
            CreatePlaceholderParagraph(matches[0].Node);
        paragraph.TextContent = text ?? string.Empty;
        result.UpdatedCount = 1;
        if (matches.Length > 1)
            result.AmbiguousPlaceholderTypes.Add(placeholderType);
        return result;
    }

    private static OdfNode CreatePlaceholderParagraph(OdfNode placeholder)
    {
        OdfNode? textBox = FindDescendant(placeholder, "text-box", OdfNamespaces.Draw);
        if (textBox is null)
        {
            textBox = new OdfNode(OdfNodeType.Element, "text-box", OdfNamespaces.Draw, "draw");
            placeholder.AppendChild(textBox);
        }
        var paragraph = new OdfNode(OdfNodeType.Element, "p", OdfNamespaces.Text, "text");
        textBox.AppendChild(paragraph);
        return paragraph;
    }

}
