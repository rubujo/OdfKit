using System;
using System.Globalization;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.Text;

public partial class OdfParagraph
{
    #region Embedded Content & Layout

    /// <summary>
    /// Adds an image in the paragraph.
    /// 在段落中新增圖片。
    /// </summary>
    /// <param name="packagePath">The image's path within the package. / 圖片在封裝包內的路徑。</param>
    /// <param name="width">The image width. / 圖片寬度。</param>
    /// <param name="height">The image height. / 圖片高度。</param>
    /// <param name="name">The image name. / 圖片名稱。</param>
    public OdfImage AddImage(string packagePath, OdfLength width, OdfLength height, string? name = null)
        => Doc.AddImage(this, packagePath, width, height, name);

    /// <summary>
    /// Adds a floating text box in the paragraph.
    /// 在段落中新增浮動文字框。
    /// </summary>
    /// <param name="x">The X-axis position. / X 軸座標位置。</param>
    /// <param name="y">The Y-axis position. / Y 軸座標位置。</param>
    /// <param name="width">The text box width. / 文字框寬度。</param>
    /// <param name="height">The text box height. / 文字框高度。</param>
    /// <param name="anchorType">The anchor type. / 錨定類型。</param>
    /// <param name="wrap">The text wrap mode. / 文字環繞方式。</param>
    /// <returns>The newly created floating text box. / 新建立的浮動文字框。</returns>
    public OdfFloatingTextBox AddFloatingTextBox(
        OdfLength x,
        OdfLength y,
        OdfLength width,
        OdfLength height,
        OdfAnchorType anchorType = OdfAnchorType.Paragraph,
        OdfTextWrap wrap = OdfTextWrap.Parallel)
    {
        var frame = OdfNodeFactory.CreateElement("frame", OdfNamespaces.Draw, "draw");
        frame.SetAttribute("name", OdfNamespaces.Draw, "TextBox_" + Guid.NewGuid().ToString("N").Substring(0, 8), "draw");
        frame.SetAttribute("anchor-type", OdfNamespaces.Text, ToAnchorTypeValue(anchorType), "text");
        frame.SetAttribute("x", OdfNamespaces.Svg, x.ToString(), "svg");
        frame.SetAttribute("y", OdfNamespaces.Svg, y.ToString(), "svg");
        frame.SetAttribute("width", OdfNamespaces.Svg, width.ToString(), "svg");
        frame.SetAttribute("height", OdfNamespaces.Svg, height.ToString(), "svg");
        frame.SetAttribute("wrap", OdfNamespaces.Style, ToWrapValue(wrap), "style");

        var textBox = OdfNodeFactory.CreateElement("text-box", OdfNamespaces.Draw, "draw");
        frame.AppendChild(textBox);
        Node.AppendChild(frame);

        return new OdfFloatingTextBox(textBox, Doc);
    }

    private static string ToAnchorTypeValue(OdfAnchorType anchorType) => anchorType switch
    {
        OdfAnchorType.Page => "page",
        OdfAnchorType.Character => "char",
        OdfAnchorType.AsChar => "as-char",
        _ => "paragraph",
    };

    private static string ToWrapValue(OdfTextWrap wrap) => wrap switch
    {
        OdfTextWrap.None => "none",
        OdfTextWrap.Left => "left",
        OdfTextWrap.Right => "right",
        OdfTextWrap.Through => "run-through",
        _ => "parallel",
    };

    /// <summary>
    /// Adds a ruby (phonetic) annotation in the paragraph.
    /// 在段落中新增旁註標記（注音）。
    /// </summary>
    /// <param name="baseText">The base text. / 基礎文字。</param>
    /// <param name="rubyText">The ruby (phonetic) text. / 注音文字。</param>
    public OdfRuby AddRuby(string baseText, string rubyText) => Doc.AddRuby(this, baseText, rubyText);

    /// <summary>
    /// Adds a formula object (MathML) in the paragraph.
    /// 在段落中新增公式物件（MathML）。
    /// </summary>
    /// <param name="mathMlXmlString">The MathML XML string. / MathML XML 字串。</param>
    public void AddFormula(string mathMlXmlString) => Doc.AddFormula(this, mathMlXmlString);

    /// <summary>
    /// Adds a comment in the paragraph.
    /// 在段落中新增批注。
    /// </summary>
    /// <param name="comment">The comment object. / 批注物件。</param>
    public void AddComment(OdfComment comment) => Doc.AddComment(this, comment);

    /// <summary>
    /// Parses and adds an HTML fragment in the paragraph.
    /// 在段落中解析並新增 HTML 片段。
    /// </summary>
    /// <param name="html">The HTML fragment string. / HTML 字串片段。</param>
    public void AddHtmlFragment(string html) => Doc.AddHtmlFragment(this, html);

    /// <summary>
    /// Parses and appends an inline HTML rich-text fragment at the end of the paragraph.
    /// 在段落結尾解析並追加 HTML 行內富文字片段。
    /// </summary>
    /// <param name="html">The inline HTML fragment to parse. / 要解析的 HTML 行內片段。</param>
    /// <returns>The current paragraph, for chained calls. / 目前段落，方便鏈式呼叫。</returns>
    public OdfParagraph AppendHtml(string html)
    {
        Doc.AddHtmlFragment(this, html);
        return this;
    }

    /// <summary>
    /// Parses and appends an inline Markdown rich-text fragment at the end of the paragraph.
    /// 在段落結尾解析並追加 Markdown 行內富文字片段。
    /// </summary>
    /// <param name="markdown">The inline Markdown text to parse. / 要解析的 Markdown 行內文字。</param>
    /// <returns>The current paragraph, for chained calls. / 目前段落，方便鏈式呼叫。</returns>
    public OdfParagraph AppendMarkdown(string markdown)
    {
        Doc.AddHtmlFragment(this, TextDocumentHtmlFragmentEngine.ConvertMarkdownInlineToHtml(markdown));
        return this;
    }

    /// <summary>
    /// Adds a page number field in the paragraph.
    /// 在段落中新增頁碼欄位。
    /// </summary>
    public void AddPageNumberField() => Doc.AddPageNumberField(this);

    /// <summary>
    /// Adds a page count field in the paragraph.
    /// 在段落中新增總頁數欄位。
    /// </summary>
    public void AddPageCountField() => Doc.AddPageCountField(this);

    /// <summary>
    /// Inserts a page break before this paragraph, optionally switching the page style.
    /// 在此段落前插入分頁符號，並可選擇性地切換頁面樣式。
    /// </summary>
    /// <param name="masterPageName">The master page style name to switch to; <see langword="null"/> means only inserting the page break. / 要切換的主頁面樣式名稱；null 表示只插入分頁。</param>
    /// <param name="pageNumber">The new starting page number; <see langword="null"/> means continuing. / 新頁碼起始值；null 表示繼續。</param>
    public void BreakPageBefore(string? masterPageName = null, int? pageNumber = null)
    {
        if (!string.IsNullOrEmpty(masterPageName))
            Doc.StyleEngine.GetOrCreateLocalStyle(Node, "paragraph").SetAttribute("master-page-name", OdfNamespaces.Style, masterPageName!, "style");
        Doc.StyleEngine.SetLocalStyleProperty(Node, "paragraph", "paragraph-properties", "break-before", OdfNamespaces.Fo, "page", "fo");
        if (pageNumber.HasValue)
            Doc.StyleEngine.SetLocalStyleProperty(Node, "paragraph", "paragraph-properties", "page-number", OdfNamespaces.Style, pageNumber.Value.ToString(CultureInfo.InvariantCulture), "style");
    }

    #endregion
}
