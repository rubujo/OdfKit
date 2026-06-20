using System;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Text;

namespace OdfKit.Styles;

/// <summary>
/// 實作文字段落的樣式代理 Facade，提供對齊、行距與縮排等高階樣式屬性讀寫。
/// </summary>
public sealed class OdfParagraphStyleProxy
{
    private readonly OdfParagraph _paragraph;

    /// <summary>
    /// 初始化 <see cref="OdfParagraphStyleProxy"/> 類別的新執行個體。
    /// </summary>
    /// <param name="paragraph">目標段落。</param>
    public OdfParagraphStyleProxy(OdfParagraph paragraph)
    {
        _paragraph = paragraph ?? throw new ArgumentNullException(nameof(paragraph));
    }

    /// <summary>
    /// 取得或設定段落的水平對齊方式。
    /// </summary>
    public string? Alignment
    {
        get => _paragraph.HorizontalAlignment;
        set => _paragraph.HorizontalAlignment = value;
    }

    /// <summary>
    /// 取得或設定段落的行高/行距（對應 <c>fo:line-height</c> 屬性）。
    /// </summary>
    public string? LineSpacing
    {
        get => _paragraph.StyleEngine.GetStyleProperty(_paragraph.StyleName ?? string.Empty, "line-height", OdfNamespaces.Fo, "paragraph");
        set => _paragraph.StyleEngine.SetLocalStyleProperty(_paragraph.Node, "paragraph", "paragraph-properties", "line-height", OdfNamespaces.Fo, value ?? string.Empty, "fo");
    }

    /// <summary>
    /// 取得或設定段落的左邊界縮排距離（對應 <c>fo:margin-left</c> 屬性）。
    /// </summary>
    public string? MarginLeft
    {
        get => _paragraph.StyleEngine.GetStyleProperty(_paragraph.StyleName ?? string.Empty, "margin-left", OdfNamespaces.Fo, "paragraph");
        set => _paragraph.StyleEngine.SetLocalStyleProperty(_paragraph.Node, "paragraph", "paragraph-properties", "margin-left", OdfNamespaces.Fo, value ?? string.Empty, "fo");
    }

    /// <summary>
    /// 取得或設定段落的右邊界縮排距離（對應 <c>fo:margin-right</c> 屬性）。
    /// </summary>
    public string? MarginRight
    {
        get => _paragraph.StyleEngine.GetStyleProperty(_paragraph.StyleName ?? string.Empty, "margin-right", OdfNamespaces.Fo, "paragraph");
        set => _paragraph.StyleEngine.SetLocalStyleProperty(_paragraph.Node, "paragraph", "paragraph-properties", "margin-right", OdfNamespaces.Fo, value ?? string.Empty, "fo");
    }

    /// <summary>
    /// 取得或設定段落上方間距（對應 <c>fo:margin-top</c> 屬性）。
    /// </summary>
    public string? MarginTop
    {
        get => _paragraph.StyleEngine.GetStyleProperty(_paragraph.StyleName ?? string.Empty, "margin-top", OdfNamespaces.Fo, "paragraph");
        set => _paragraph.StyleEngine.SetLocalStyleProperty(_paragraph.Node, "paragraph", "paragraph-properties", "margin-top", OdfNamespaces.Fo, value ?? string.Empty, "fo");
    }

    /// <summary>
    /// 取得或設定段落下方間距（對應 <c>fo:margin-bottom</c> 屬性）。
    /// </summary>
    public string? MarginBottom
    {
        get => _paragraph.StyleEngine.GetStyleProperty(_paragraph.StyleName ?? string.Empty, "margin-bottom", OdfNamespaces.Fo, "paragraph");
        set => _paragraph.StyleEngine.SetLocalStyleProperty(_paragraph.Node, "paragraph", "paragraph-properties", "margin-bottom", OdfNamespaces.Fo, value ?? string.Empty, "fo");
    }

    /// <summary>
    /// 取得或設定段落首行縮排（對應 <c>fo:text-indent</c> 屬性）。
    /// </summary>
    public string? TextIndent
    {
        get => _paragraph.TextIndent;
        set => _paragraph.TextIndent = value;
    }
}
