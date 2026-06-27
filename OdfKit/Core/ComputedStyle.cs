using System;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.Core;

/// <summary>
/// 表示元素經過層疊繼承解析後的實質最終樣式。
/// </summary>
public sealed class ComputedStyle
{
    /// <summary>
    /// 取得字型名稱。
    /// </summary>
    public string? FontName { get; init; }

    /// <summary>
    /// 取得字型大小（例如 "12pt"）。
    /// </summary>
    public string? FontSize { get; init; }

    /// <summary>
    /// 取得一個值，指出字型是否為粗體。
    /// </summary>
    public bool Bold { get; init; }

    /// <summary>
    /// 取得一個值，指出字型是否為斜體。
    /// </summary>
    public bool Italic { get; init; }

    /// <summary>
    /// 取得一個值，指出字型是否具有底線。
    /// </summary>
    public bool Underline { get; init; }

    /// <summary>
    /// 取得文字顏色（RGB 十六進位字串，例如 "#ff0000"）。
    /// </summary>
    public string? Color { get; init; }

    /// <summary>
    /// 取得背景顏色。
    /// </summary>
    public string? BackgroundColor { get; init; }

    /// <summary>
    /// 取得文字對齊方式。
    /// </summary>
    public string? TextAlignment { get; init; }

    /// <summary>
    /// 從指定的 OdfElement 解析實質最終樣式。
    /// </summary>
    /// <param name="element">要解析的 ODF 元素</param>
    /// <returns>解析出來的實質最終樣式</returns>
    /// <exception cref="ArgumentNullException">當 <paramref name="element"/> 為 null 時拋出</exception>
    public static ComputedStyle Resolve(OdfElement element)
    {
        if (element is null)
            throw new ArgumentNullException(nameof(element));

        var styleEngine = element.Document?.StyleEngine;
        if (styleEngine is null)
        {
            return new ComputedStyle();
        }

        string? styleName = element.GetAttribute("style-name", OdfNamespaces.Style);

        // 自動推導樣式家族，通常為 paragraph、text 或 table-cell
        string family = element.LocalName switch
        {
            "p" or "h" => "paragraph",
            "span" => "text",
            "table-cell" or "covered-table-cell" => "table-cell",
            _ => "text"
        };

        // 使用 OdfStyleEngine 進行繼承鏈遞迴查詢
        string? fontName = styleEngine.GetStyleProperty(styleName ?? string.Empty, "font-name", OdfNamespaces.Style, family);
        string? fontSize = styleEngine.GetStyleProperty(styleName ?? string.Empty, "font-size", OdfNamespaces.Fo, family);

        string? fontWeight = styleEngine.GetStyleProperty(styleName ?? string.Empty, "font-weight", OdfNamespaces.Fo, family);
        bool bold = string.Equals(fontWeight, "bold", StringComparison.OrdinalIgnoreCase);

        string? fontStyle = styleEngine.GetStyleProperty(styleName ?? string.Empty, "font-style", OdfNamespaces.Fo, family);
        bool italic = string.Equals(fontStyle, "italic", StringComparison.OrdinalIgnoreCase);

        string? textUnderline = styleEngine.GetStyleProperty(styleName ?? string.Empty, "text-underline-style", OdfNamespaces.Style, family);
        bool underline = !string.IsNullOrEmpty(textUnderline) && !string.Equals(textUnderline, "none", StringComparison.OrdinalIgnoreCase);

        string? color = styleEngine.GetStyleProperty(styleName ?? string.Empty, "color", OdfNamespaces.Fo, family);
        string? bgColor = styleEngine.GetStyleProperty(styleName ?? string.Empty, "background-color", OdfNamespaces.Fo, family);
        string? align = styleEngine.GetStyleProperty(styleName ?? string.Empty, "text-align", OdfNamespaces.Fo, family);

        return new ComputedStyle
        {
            FontName = fontName,
            FontSize = fontSize,
            Bold = bold,
            Italic = italic,
            Underline = underline,
            Color = color,
            BackgroundColor = bgColor,
            TextAlignment = align
        };
    }
}
