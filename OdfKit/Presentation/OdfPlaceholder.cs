using System;
using OdfKit.Core;
using OdfKit.DOM;

namespace OdfKit.Presentation;

/// <summary>
/// 表示投影片預留位置的類別。
/// </summary>
/// <param name="node">底層的 <see cref="OdfNode"/> 執行個體</param>
/// <param name="slide">所屬的投影片執行個體</param>
public class OdfPlaceholder(OdfNode node, OdfSlide slide) : OdfShape(node, slide)
{
    /// <summary>
    /// 取得或設定預留位置的型態。
    /// </summary>
    public OdfPlaceholderType PlaceholderType
    {
        get => OdfPlaceholderTemplate.KebabToType(Node.GetAttribute("class", OdfNamespaces.Presentation) ?? "text");
        set => Node.SetAttribute("class", OdfNamespaces.Presentation, OdfPlaceholderTemplate.TypeToKebab(value), "presentation");
    }

    private readonly bool _initialized = InitPlaceholder(node);

    private static bool InitPlaceholder(OdfNode n)
    {
        n.SetAttribute("placeholder", OdfNamespaces.Presentation, "true", "presentation");
        return true;
    }
}
