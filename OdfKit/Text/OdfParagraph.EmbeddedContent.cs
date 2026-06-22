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
    /// 在段落中新增圖片
    /// </summary>
    /// <param name="packagePath">圖片在封裝包內的路徑</param>
    /// <param name="width">圖片寬度</param>
    /// <param name="height">圖片高度</param>
    /// <param name="name">圖片名稱</param>
    public OdfImage AddImage(string packagePath, OdfLength width, OdfLength height, string? name = null)
        => Doc.AddImage(this, packagePath, width, height, name);

    /// <summary>
    /// 在段落中新增浮動文字框。
    /// </summary>
    /// <param name="x">X 軸座標位置</param>
    /// <param name="y">Y 軸座標位置</param>
    /// <param name="width">文字框寬度</param>
    /// <param name="height">文字框高度</param>
    /// <param name="anchorType">錨定類型</param>
    /// <param name="wrap">文字環繞方式</param>
    /// <returns>新建立的浮動文字框</returns>
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
    /// 在段落中新增旁註標記（注音）
    /// </summary>
    /// <param name="baseText">基礎文字</param>
    /// <param name="rubyText">注音文字</param>
    public OdfRuby AddRuby(string baseText, string rubyText) => Doc.AddRuby(this, baseText, rubyText);

    /// <summary>
    /// 在段落中新增公式物件（MathML）
    /// </summary>
    /// <param name="mathMlXmlString">MathML XML 字串</param>
    public void AddFormula(string mathMlXmlString) => Doc.AddFormula(this, mathMlXmlString);

    /// <summary>
    /// 在段落中新增批注
    /// </summary>
    /// <param name="comment">批注物件</param>
    public void AddComment(OdfComment comment) => Doc.AddComment(this, comment);

    /// <summary>
    /// 在段落中解析並新增 HTML 片段
    /// </summary>
    /// <param name="html">HTML 字串片段</param>
    public void AddHtmlFragment(string html) => Doc.AddHtmlFragment(this, html);

    /// <summary>
    /// 在段落中新增頁碼欄位
    /// </summary>
    public void AddPageNumberField() => Doc.AddPageNumberField(this);

    /// <summary>
    /// 在段落中新增總頁數欄位
    /// </summary>
    public void AddPageCountField() => Doc.AddPageCountField(this);

    /// <summary>
    /// 在此段落前插入分頁符號，並可選擇性地切換頁面樣式。
    /// </summary>
    /// <param name="masterPageName">要切換的主頁面樣式名稱；null 表示只插入分頁</param>
    /// <param name="pageNumber">新頁碼起始值；null 表示繼續</param>
    public void BreakPageBefore(string? masterPageName = null, int? pageNumber = null)
    {
        Doc.StyleEngine.SetLocalStyleProperty(Node, "paragraph", "paragraph-properties", "break-before", OdfNamespaces.Fo, "page", "fo");
        if (pageNumber.HasValue)
            Doc.StyleEngine.SetLocalStyleProperty(Node, "paragraph", "paragraph-properties", "page-number", OdfNamespaces.Style, pageNumber.Value.ToString(CultureInfo.InvariantCulture), "style");
        if (!string.IsNullOrEmpty(masterPageName))
            Doc.StyleEngine.GetOrCreateLocalStyle(Node, "paragraph").SetAttribute("master-page-name", OdfNamespaces.Style, masterPageName!, "style");
    }

    #endregion
}
