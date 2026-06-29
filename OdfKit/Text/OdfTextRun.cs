using System;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.Text;

/// <summary>
/// Represents odf text run.
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
    /// Gets or sets this member.
    /// 取得或設定文字片段的內文。
    /// </summary>
    public string Text
    {
        get => Node.TextContent;
        set => Node.TextContent = value;
    }

    /// <summary>
    /// Gets or sets this member.
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
    /// Gets or sets this member.
    /// 取得或設定文字片段的西文字型名稱。
    /// </summary>
    public string? FontName
    {
        get => _doc.StyleEngine.GetStyleProperty(GetStyleName(), "font-name", OdfNamespaces.Style, "text");
        set => _doc.StyleEngine.SetLocalStyleProperty(Node, "text", "text-properties", "font-name", OdfNamespaces.Style, value ?? string.Empty, "style");
    }

    /// <summary>
    /// Gets or sets this member.
    /// 取得或設定文字片段的西文字型大小。
    /// </summary>
    public string? FontSize
    {
        get => _doc.StyleEngine.GetStyleProperty(GetStyleName(), "font-size", OdfNamespaces.Fo, "text");
        set => _doc.StyleEngine.SetLocalStyleProperty(Node, "text", "text-properties", "font-size", OdfNamespaces.Fo, value ?? string.Empty, "fo");
    }

    /// <summary>
    /// Sets set font.
    /// 設定文字片段的字型名稱。
    /// </summary>
    /// <param name="westernFont">The value to use. / 西文字型名稱</param>
    /// <param name="asianFont">The value to use. / 東亞（中日韓）字型名稱</param>
    /// <param name="complexFont">The value to use. / 複雜文字字型名稱</param>
    public void SetFont(string westernFont, string? asianFont = null, string? complexFont = null)
    {
        FontName = westernFont;
        FontNameAsian = asianFont ?? westernFont;
        FontNameComplex = complexFont ?? westernFont;
    }

    /// <summary>
    /// Sets set font size.
    /// 設定文字片段的字型大小。
    /// </summary>
    /// <param name="westernSize">The numeric value. / 西文字型大小</param>
    /// <param name="asianSize">The numeric value. / 東亞字型大小</param>
    /// <param name="complexSize">The numeric value. / 複雜文字字型大小</param>
    public void SetFontSize(string westernSize, string? asianSize = null, string? complexSize = null)
    {
        FontSize = westernSize;
        FontSizeAsian = asianSize ?? westernSize;
        FontSizeComplex = complexSize ?? westernSize;
    }

    /// <summary>
    /// Gets or sets this member.
    /// 取得或設定一個值，指出文字片段是否為粗體。
    /// </summary>
    public bool IsBold
    {
        get => _doc.StyleEngine.GetStyleProperty(GetStyleName(), "font-weight", OdfNamespaces.Fo, "text") == "bold";
        set => _doc.StyleEngine.SetLocalStyleProperty(Node, "text", "text-properties", "font-weight", OdfNamespaces.Fo, value ? "bold" : "normal", "fo");
    }

    /// <summary>
    /// Gets or sets this member.
    /// 取得或設定一個值，指出文字片段是否為斜體。
    /// </summary>
    public bool IsItalic
    {
        get => _doc.StyleEngine.GetStyleProperty(GetStyleName(), "font-style", OdfNamespaces.Fo, "text") == "italic";
        set => _doc.StyleEngine.SetLocalStyleProperty(Node, "text", "text-properties", "font-style", OdfNamespaces.Fo, value ? "italic" : "normal", "fo");
    }

    /// <summary>
    /// Gets or sets this member.
    /// 取得或設定一個值，指出文字片段是否加上底線。
    /// </summary>
    public bool IsUnderline
    {
        get => _doc.StyleEngine.GetStyleProperty(GetStyleName(), "text-underline-style", OdfNamespaces.Style, "text") == "solid";
        set => _doc.StyleEngine.SetLocalStyleProperty(Node, "text", "text-properties", "text-underline-style", OdfNamespaces.Style, value ? "solid" : "none", "style");
    }

    /// <summary>
    /// Gets or sets this member.
    /// 取得或設定一個值，指出文字片段是否加上刪除線。
    /// </summary>
    public bool IsStrikethrough
    {
        get => _doc.StyleEngine.GetStyleProperty(GetStyleName(), "text-line-through-style", OdfNamespaces.Style, "text") == "solid";
        set => _doc.StyleEngine.SetLocalStyleProperty(Node, "text", "text-properties", "text-line-through-style", OdfNamespaces.Style, value ? "solid" : "none", "style");
    }

    /// <summary>
    /// Gets or sets this member.
    /// 取得或設定文字片段的位置，例如 <c>baseline</c>、<c>super</c> 或 <c>sub</c>。
    /// </summary>
    public string? TextPosition
    {
        get => _doc.StyleEngine.GetStyleProperty(GetStyleName(), "text-position", OdfNamespaces.Style, "text");
        set => _doc.StyleEngine.SetLocalStyleProperty(Node, "text", "text-properties", "text-position", OdfNamespaces.Style, value ?? string.Empty, "style");
    }

    /// <summary>
    /// Gets or sets this member.
    /// 取得或設定一個值，指出文字片段是否為上標。
    /// </summary>
    public bool IsSuperscript
    {
        get => string.Equals(TextPosition, "super", StringComparison.OrdinalIgnoreCase);
        set => TextPosition = value ? "super" : "baseline";
    }

    /// <summary>
    /// Gets or sets this member.
    /// 取得或設定一個值，指出文字片段是否為下標。
    /// </summary>
    public bool IsSubscript
    {
        get => string.Equals(TextPosition, "sub", StringComparison.OrdinalIgnoreCase);
        set => TextPosition = value ? "sub" : "baseline";
    }

    /// <summary>
    /// Gets or sets this member.
    /// 取得或設定文字片段的字色。
    /// </summary>
    public string? Color
    {
        get => _doc.StyleEngine.GetStyleProperty(GetStyleName(), "color", OdfNamespaces.Fo, "text");
        set => _doc.StyleEngine.SetLocalStyleProperty(Node, "text", "text-properties", "color", OdfNamespaces.Fo, value ?? string.Empty, "fo");
    }

    /// <summary>
    /// Gets or sets this member.
    /// 取得或設定文字片段的背景色。
    /// </summary>
    public string? BackgroundColor
    {
        get => _doc.StyleEngine.GetStyleProperty(GetStyleName(), "background-color", OdfNamespaces.Fo, "text");
        set => _doc.StyleEngine.SetLocalStyleProperty(Node, "text", "text-properties", "background-color", OdfNamespaces.Fo, value ?? string.Empty, "fo");
    }

    /// <summary>
    /// Gets or sets this member.
    /// 取得或設定文字大小寫轉換，例如 <c>uppercase</c>。
    /// </summary>
    public string? TextTransform
    {
        get => _doc.StyleEngine.GetStyleProperty(GetStyleName(), "text-transform", OdfNamespaces.Fo, "text");
        set => _doc.StyleEngine.SetLocalStyleProperty(Node, "text", "text-properties", "text-transform", OdfNamespaces.Fo, value ?? string.Empty, "fo");
    }

    /// <summary>
    /// Gets or sets this member.
    /// 取得或設定字型變體，例如 <c>small-caps</c>。
    /// </summary>
    public string? FontVariant
    {
        get => _doc.StyleEngine.GetStyleProperty(GetStyleName(), "font-variant", OdfNamespaces.Fo, "text");
        set => _doc.StyleEngine.SetLocalStyleProperty(Node, "text", "text-properties", "font-variant", OdfNamespaces.Fo, value ?? string.Empty, "fo");
    }

    /// <summary>
    /// Gets or sets this member.
    /// 取得或設定文字片段的東亞（中日韓）字型名稱。
    /// </summary>
    public string? FontNameAsian
    {
        get => _doc.StyleEngine.GetStyleProperty(GetStyleName(), "font-name-asian", OdfNamespaces.Style, "text");
        set => _doc.StyleEngine.SetLocalStyleProperty(Node, "text", "text-properties", "font-name-asian", OdfNamespaces.Style, value ?? string.Empty, "style");
    }

    /// <summary>
    /// Gets or sets this member.
    /// 取得或設定文字片段的複雜文字字型名稱。
    /// </summary>
    public string? FontNameComplex
    {
        get => _doc.StyleEngine.GetStyleProperty(GetStyleName(), "font-name-complex", OdfNamespaces.Style, "text");
        set => _doc.StyleEngine.SetLocalStyleProperty(Node, "text", "text-properties", "font-name-complex", OdfNamespaces.Style, value ?? string.Empty, "style");
    }

    /// <summary>
    /// Gets or sets this member.
    /// 取得或設定文字片段的東亞（中日韓）字型大小。
    /// </summary>
    public string? FontSizeAsian
    {
        get => _doc.StyleEngine.GetStyleProperty(GetStyleName(), "font-size-asian", OdfNamespaces.Fo, "text");
        set => _doc.StyleEngine.SetLocalStyleProperty(Node, "text", "text-properties", "font-size-asian", OdfNamespaces.Fo, value ?? string.Empty, "fo");
    }

    /// <summary>
    /// Gets or sets this member.
    /// 取得或設定文字片段的複雜文字字型大小。
    /// </summary>
    public string? FontSizeComplex
    {
        get => _doc.StyleEngine.GetStyleProperty(GetStyleName(), "font-size-complex", OdfNamespaces.Fo, "text");
        set => _doc.StyleEngine.SetLocalStyleProperty(Node, "text", "text-properties", "font-size-complex", OdfNamespaces.Fo, value ?? string.Empty, "fo");
    }

    private string GetStyleName() => Node.GetAttribute("style-name", OdfNamespaces.Text) ?? string.Empty;

    /// <summary>
    /// Removes delete.
    /// 刪除此文字片段。
    /// </summary>
    public void Delete()
    {
        _doc.DeleteNode(Node);
    }

    /// <summary>
    /// Sets with bold.
    /// 設定此文字片段是否為粗體。
    /// </summary>
    /// <param name="bold">The value to use. / 是否粗體</param>
    /// <returns>The result. / 文字片段本身</returns>
    public OdfTextRun WithBold(bool bold = true)
    {
        IsBold = bold;
        return this;
    }

    /// <summary>
    /// Sets with italic.
    /// 設定此文字片段是否為斜體。
    /// </summary>
    /// <param name="italic">The value to use. / 是否斜體</param>
    /// <returns>The result. / 文字片段本身</returns>
    public OdfTextRun WithItalic(bool italic = true)
    {
        IsItalic = italic;
        return this;
    }

    /// <summary>
    /// Sets with strikethrough.
    /// 設定此文字片段是否加上刪除線。
    /// </summary>
    /// <param name="strikethrough">The value to use. / 是否加上刪除線</param>
    /// <returns>The result. / 文字片段本身</returns>
    public OdfTextRun WithStrikethrough(bool strikethrough = true)
    {
        IsStrikethrough = strikethrough;
        return this;
    }

    /// <summary>
    /// Sets with superscript.
    /// 設定此文字片段是否為上標。
    /// </summary>
    /// <param name="superscript">The value to use. / 是否為上標</param>
    /// <returns>The result. / 文字片段本身</returns>
    public OdfTextRun WithSuperscript(bool superscript = true)
    {
        IsSuperscript = superscript;
        return this;
    }

    /// <summary>
    /// Sets with subscript.
    /// 設定此文字片段是否為下標。
    /// </summary>
    /// <param name="subscript">The value to use. / 是否為下標</param>
    /// <returns>The result. / 文字片段本身</returns>
    public OdfTextRun WithSubscript(bool subscript = true)
    {
        IsSubscript = subscript;
        return this;
    }

    /// <summary>
    /// Sets with font size.
    /// 設定此文字片段的西文、東亞及複雜字型大小。
    /// </summary>
    /// <param name="size">The numeric value. / 字型大小，例如 <c>12pt</c></param>
    /// <returns>The result. / 文字片段本身</returns>
    public OdfTextRun WithFontSize(string size)
    {
        SetFontSize(size);
        return this;
    }

    /// <summary>
    /// Sets with font name.
    /// 設定此文字片段的字型名稱。
    /// </summary>
    /// <param name="westernFont">The value to use. / 西文字型名稱</param>
    /// <param name="asianFont">The value to use. / 東亞（中日韓）字型名稱；未指定時沿用 <paramref name="westernFont"/></param>
    /// <param name="complexFont">The value to use. / 複雜文字字型名稱；未指定時沿用 <paramref name="westernFont"/></param>
    /// <returns>The result. / 文字片段本身</returns>
    public OdfTextRun WithFontName(string westernFont, string? asianFont = null, string? complexFont = null)
    {
        SetFont(westernFont, asianFont, complexFont);
        return this;
    }

    /// <summary>
    /// Sets with color.
    /// 設定此文字片段的字色。
    /// </summary>
    /// <param name="hexColor">The value to use. / 十六進位顏色字串，例如 <c>#FF0000</c></param>
    /// <returns>The result. / 文字片段本身</returns>
    public OdfTextRun WithColor(string hexColor)
    {
        Color = hexColor;
        return this;
    }

    /// <summary>
    /// Sets with background color.
    /// 設定此文字片段的背景色。
    /// </summary>
    /// <param name="hexColor">The value to use. / 十六進位顏色字串，例如 <c>#FFFF00</c></param>
    /// <returns>The result. / 文字片段本身</returns>
    public OdfTextRun WithBackgroundColor(string hexColor)
    {
        BackgroundColor = hexColor;
        return this;
    }

    /// <summary>
    /// Sets with text transform.
    /// 設定此文字片段的大小寫轉換。
    /// </summary>
    /// <param name="transform">The delegate to invoke. / 大小寫轉換值，例如 <c>uppercase</c></param>
    /// <returns>The result. / 文字片段本身</returns>
    public OdfTextRun WithTextTransform(string transform)
    {
        TextTransform = transform;
        return this;
    }

    /// <summary>
    /// Sets with font variant.
    /// 設定此文字片段的字型變體。
    /// </summary>
    /// <param name="variant">The value to use. / 字型變體值，例如 <c>small-caps</c></param>
    /// <returns>The result. / 文字片段本身</returns>
    public OdfTextRun WithFontVariant(string variant)
    {
        FontVariant = variant;
        return this;
    }
}
