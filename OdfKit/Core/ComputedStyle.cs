using System;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.Core;

/// <summary>
/// Represents the effective style values resolved for an ODF element.
/// 表示為 ODF 元素解析後的有效樣式值。
/// </summary>
public sealed class ComputedStyle
{
    /// <summary>
    /// Gets the resolved font family name.
    /// 取得解析後的字型家族名稱。
    /// </summary>
    public string? FontName { get; init; }

    /// <summary>
    /// Gets the resolved font size token.
    /// 取得解析後的字型大小語彙。
    /// </summary>
    public string? FontSize { get; init; }

    /// <summary>
    /// Gets a value indicating the Bold state.
    /// 取得一個值，指出字型是否為粗體。
    /// </summary>
    public bool Bold { get; init; }

    /// <summary>
    /// Gets a value indicating the Italic state.
    /// 取得一個值，指出字型是否為斜體。
    /// </summary>
    public bool Italic { get; init; }

    /// <summary>
    /// Gets a value indicating the Underline state.
    /// 取得一個值，指出字型是否具有底線。
    /// </summary>
    public bool Underline { get; init; }

    /// <summary>
    /// Gets the resolved text color.
    /// 取得解析後的文字顏色。
    /// </summary>
    public string? Color { get; init; }

    /// <summary>
    /// Gets the resolved background color.
    /// 取得解析後的背景顏色。
    /// </summary>
    public string? BackgroundColor { get; init; }

    /// <summary>
    /// Gets the resolved text alignment value.
    /// 取得解析後的文字對齊值。
    /// </summary>
    public string? TextAlignment { get; init; }

    /// <summary>
    /// Resolves the effective style values for the specified ODF element.
    /// 解析指定 ODF 元素的有效樣式值。
    /// </summary>
    /// <param name="element">The ODF element to inspect. / 要檢查的 ODF 元素。</param>
    /// <returns>The resolved effective style values. / 解析後的有效樣式值。</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="element"/> is <see langword="null"/>. / 當 <paramref name="element"/> 為 <see langword="null"/> 時擲出。</exception>
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
