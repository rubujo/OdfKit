using System;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.Text;

/// <summary>
/// 表示段落中的文字片段（Span）。
/// </summary>
public class OdfTextRun
{
    internal OdfTextRun(OdfNode node, TextDocument doc)
    {
        Node = node ?? throw new ArgumentNullException(nameof(node));
        _doc = doc ?? throw new ArgumentNullException(nameof(doc));
    }

    /// <summary>
    /// 取得與此文字片段相關聯的 OdfNode 節點。
    /// </summary>
    internal OdfNode Node { get; }

    private readonly TextDocument _doc;

    /// <summary>
    /// 取得或設定文字片段的內文。
    /// </summary>
    public string Text
    {
        get => Node.TextContent;
        set => Node.TextContent = value;
    }

    /// <summary>
    /// 取得或設定文字片段的樣式名稱。
    /// </summary>
    public string? StyleName
    {
        get => Node.GetAttribute("style-name", OdfNamespaces.Text);
        set
        {
            if (_doc.TrackedChanges)
            {
                _doc.TrackFormatChange(Node, "text");
            }
            Node.SetAttribute("style-name", OdfNamespaces.Text, value ?? string.Empty, "text");
        }
    }

    /// <summary>
    /// 取得或設定文字片段的西文字型名稱。
    /// </summary>
    public string? FontName
    {
        get => _doc.StyleEngine.GetStyleProperty(GetStyleName(), "font-name", OdfNamespaces.Style, "text");
        set => _doc.StyleEngine.SetLocalStyleProperty(Node, "text", "text-properties", "font-name", OdfNamespaces.Style, value ?? string.Empty, "style");
    }

    /// <summary>
    /// 取得或設定文字片段的西文字型大小。
    /// </summary>
    public string? FontSize
    {
        get => _doc.StyleEngine.GetStyleProperty(GetStyleName(), "font-size", OdfNamespaces.Fo, "text");
        set => _doc.StyleEngine.SetLocalStyleProperty(Node, "text", "text-properties", "font-size", OdfNamespaces.Fo, value ?? string.Empty, "fo");
    }

    /// <summary>
    /// 設定文字片段的字型名稱。
    /// </summary>
    /// <param name="westernFont">西文字型名稱</param>
    /// <param name="asianFont">東亞（中日韓）字型名稱</param>
    /// <param name="complexFont">複雜文字字型名稱</param>
    public void SetFont(string westernFont, string? asianFont = null, string? complexFont = null)
    {
        FontName = westernFont;
        FontNameAsian = asianFont ?? westernFont;
        FontNameComplex = complexFont ?? westernFont;
    }

    /// <summary>
    /// 設定文字片段的字型大小。
    /// </summary>
    /// <param name="westernSize">西文字型大小</param>
    /// <param name="asianSize">東亞字型大小</param>
    /// <param name="complexSize">複雜文字字型大小</param>
    public void SetFontSize(string westernSize, string? asianSize = null, string? complexSize = null)
    {
        FontSize = westernSize;
        FontSizeAsian = asianSize ?? westernSize;
        FontSizeComplex = complexSize ?? westernSize;
    }

    /// <summary>
    /// 取得或設定一個值，指出文字片段是否為粗體。
    /// </summary>
    public bool IsBold
    {
        get => _doc.StyleEngine.GetStyleProperty(GetStyleName(), "font-weight", OdfNamespaces.Fo, "text") == "bold";
        set => _doc.StyleEngine.SetLocalStyleProperty(Node, "text", "text-properties", "font-weight", OdfNamespaces.Fo, value ? "bold" : "normal", "fo");
    }

    /// <summary>
    /// 取得或設定一個值，指出文字片段是否為斜體。
    /// </summary>
    public bool IsItalic
    {
        get => _doc.StyleEngine.GetStyleProperty(GetStyleName(), "font-style", OdfNamespaces.Fo, "text") == "italic";
        set => _doc.StyleEngine.SetLocalStyleProperty(Node, "text", "text-properties", "font-style", OdfNamespaces.Fo, value ? "italic" : "normal", "fo");
    }

    /// <summary>
    /// 取得或設定一個值，指出文字片段是否加上底線。
    /// </summary>
    public bool IsUnderline
    {
        get => _doc.StyleEngine.GetStyleProperty(GetStyleName(), "text-underline-style", OdfNamespaces.Style, "text") == "solid";
        set => _doc.StyleEngine.SetLocalStyleProperty(Node, "text", "text-properties", "text-underline-style", OdfNamespaces.Style, value ? "solid" : "none", "style");
    }

    /// <summary>
    /// 取得或設定一個值，指出文字片段是否加上刪除線。
    /// </summary>
    public bool IsStrikethrough
    {
        get => _doc.StyleEngine.GetStyleProperty(GetStyleName(), "text-line-through-style", OdfNamespaces.Style, "text") == "solid";
        set => _doc.StyleEngine.SetLocalStyleProperty(Node, "text", "text-properties", "text-line-through-style", OdfNamespaces.Style, value ? "solid" : "none", "style");
    }

    /// <summary>
    /// 取得或設定文字片段的位置，例如 <c>baseline</c>、<c>super</c> 或 <c>sub</c>。
    /// </summary>
    public string? TextPosition
    {
        get => _doc.StyleEngine.GetStyleProperty(GetStyleName(), "text-position", OdfNamespaces.Style, "text");
        set => _doc.StyleEngine.SetLocalStyleProperty(Node, "text", "text-properties", "text-position", OdfNamespaces.Style, value ?? string.Empty, "style");
    }

    /// <summary>
    /// 取得或設定一個值，指出文字片段是否為上標。
    /// </summary>
    public bool IsSuperscript
    {
        get => string.Equals(TextPosition, "super", StringComparison.OrdinalIgnoreCase);
        set => TextPosition = value ? "super" : "baseline";
    }

    /// <summary>
    /// 取得或設定一個值，指出文字片段是否為下標。
    /// </summary>
    public bool IsSubscript
    {
        get => string.Equals(TextPosition, "sub", StringComparison.OrdinalIgnoreCase);
        set => TextPosition = value ? "sub" : "baseline";
    }

    /// <summary>
    /// 取得或設定文字片段的字色。
    /// </summary>
    public string? Color
    {
        get => _doc.StyleEngine.GetStyleProperty(GetStyleName(), "color", OdfNamespaces.Fo, "text");
        set => _doc.StyleEngine.SetLocalStyleProperty(Node, "text", "text-properties", "color", OdfNamespaces.Fo, value ?? string.Empty, "fo");
    }

    /// <summary>
    /// 取得或設定文字片段的東亞（中日韓）字型名稱。
    /// </summary>
    public string? FontNameAsian
    {
        get => _doc.StyleEngine.GetStyleProperty(GetStyleName(), "font-name-asian", OdfNamespaces.Style, "text");
        set => _doc.StyleEngine.SetLocalStyleProperty(Node, "text", "text-properties", "font-name-asian", OdfNamespaces.Style, value ?? string.Empty, "style");
    }

    /// <summary>
    /// 取得或設定文字片段的複雜文字字型名稱。
    /// </summary>
    public string? FontNameComplex
    {
        get => _doc.StyleEngine.GetStyleProperty(GetStyleName(), "font-name-complex", OdfNamespaces.Style, "text");
        set => _doc.StyleEngine.SetLocalStyleProperty(Node, "text", "text-properties", "font-name-complex", OdfNamespaces.Style, value ?? string.Empty, "style");
    }

    /// <summary>
    /// 取得或設定文字片段的東亞（中日韓）字型大小。
    /// </summary>
    public string? FontSizeAsian
    {
        get => _doc.StyleEngine.GetStyleProperty(GetStyleName(), "font-size-asian", OdfNamespaces.Fo, "text");
        set => _doc.StyleEngine.SetLocalStyleProperty(Node, "text", "text-properties", "font-size-asian", OdfNamespaces.Fo, value ?? string.Empty, "fo");
    }

    /// <summary>
    /// 取得或設定文字片段的複雜文字字型大小。
    /// </summary>
    public string? FontSizeComplex
    {
        get => _doc.StyleEngine.GetStyleProperty(GetStyleName(), "font-size-complex", OdfNamespaces.Fo, "text");
        set => _doc.StyleEngine.SetLocalStyleProperty(Node, "text", "text-properties", "font-size-complex", OdfNamespaces.Fo, value ?? string.Empty, "fo");
    }

    private string GetStyleName() => Node.GetAttribute("style-name", OdfNamespaces.Text) ?? string.Empty;

    /// <summary>
    /// 刪除此文字片段。
    /// </summary>
    public void Delete()
    {
        _doc.DeleteNode(Node);
    }

    /// <summary>
    /// 設定此文字片段是否為粗體。
    /// </summary>
    /// <param name="bold">是否粗體。</param>
    /// <returns>文字片段本身。</returns>
    public OdfTextRun WithBold(bool bold = true)
    {
        IsBold = bold;
        return this;
    }

    /// <summary>
    /// 設定此文字片段是否為斜體。
    /// </summary>
    /// <param name="italic">是否斜體。</param>
    /// <returns>文字片段本身。</returns>
    public OdfTextRun WithItalic(bool italic = true)
    {
        IsItalic = italic;
        return this;
    }

    /// <summary>
    /// 設定此文字片段是否加上刪除線。
    /// </summary>
    /// <param name="strikethrough">是否加上刪除線。</param>
    /// <returns>文字片段本身。</returns>
    public OdfTextRun WithStrikethrough(bool strikethrough = true)
    {
        IsStrikethrough = strikethrough;
        return this;
    }

    /// <summary>
    /// 設定此文字片段是否為上標。
    /// </summary>
    /// <param name="superscript">是否為上標。</param>
    /// <returns>文字片段本身。</returns>
    public OdfTextRun WithSuperscript(bool superscript = true)
    {
        IsSuperscript = superscript;
        return this;
    }

    /// <summary>
    /// 設定此文字片段是否為下標。
    /// </summary>
    /// <param name="subscript">是否為下標。</param>
    /// <returns>文字片段本身。</returns>
    public OdfTextRun WithSubscript(bool subscript = true)
    {
        IsSubscript = subscript;
        return this;
    }

    /// <summary>
    /// 設定此文字片段的西文、東亞及複雜字型大小。
    /// </summary>
    /// <param name="size">字型大小，例如 <c>12pt</c>。</param>
    /// <returns>文字片段本身。</returns>
    public OdfTextRun WithFontSize(string size)
    {
        SetFontSize(size);
        return this;
    }

    /// <summary>
    /// 設定此文字片段的字色。
    /// </summary>
    /// <param name="hexColor">十六進位顏色字串，例如 <c>#FF0000</c>。</param>
    /// <returns>文字片段本身。</returns>
    public OdfTextRun WithColor(string hexColor)
    {
        Color = hexColor;
        return this;
    }
}
