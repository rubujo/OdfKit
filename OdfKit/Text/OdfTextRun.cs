using System;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.Text;

/// <summary>
/// Represents a text run (span) within a paragraph.
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
    /// Gets or sets the text run's content.
    /// 取得或設定文字片段的內文。
    /// </summary>
    public string Text
    {
        get => Node.TextContent;
        set => Node.TextContent = value;
    }

    /// <summary>
    /// Gets or sets the text run's style name.
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
    /// Gets or sets the text run's Western font name.
    /// 取得或設定文字片段的西文字型名稱。
    /// </summary>
    public string? FontName
    {
        get => _doc.StyleEngine.GetStyleProperty(GetStyleName(), "font-name", OdfNamespaces.Style, "text");
        set => _doc.StyleEngine.SetLocalStyleProperty(Node, "text", "text-properties", "font-name", OdfNamespaces.Style, value ?? string.Empty, "style");
    }

    /// <summary>
    /// Gets or sets the text run's Western font size.
    /// 取得或設定文字片段的西文字型大小。
    /// </summary>
    public string? FontSize
    {
        get => _doc.StyleEngine.GetStyleProperty(GetStyleName(), "font-size", OdfNamespaces.Fo, "text");
        set => _doc.StyleEngine.SetLocalStyleProperty(Node, "text", "text-properties", "font-size", OdfNamespaces.Fo, value ?? string.Empty, "fo");
    }
    /// <summary>
    /// Short overload of SetFont that accepts westernFont; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 westernFont；其餘可選參數使用預設值並轉呼叫最長 SetFont 多載。
    /// </summary>
    public void SetFont(string westernFont) => SetFont(westernFont, null, null);

    /// <summary>
    /// Short overload of SetFont that accepts westernFont and asianFont; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 westernFont 與 asianFont；其餘可選參數使用預設值並轉呼叫最長 SetFont 多載。
    /// </summary>
    public void SetFont(string westernFont, string? asianFont) => SetFont(westernFont, asianFont, null);


    /// <summary>
    /// Sets the text run's font names.
    /// 設定文字片段的字型名稱。
    /// </summary>
    /// <param name="westernFont">The Western font name. / 西文字型名稱。</param>
    /// <param name="asianFont">The East Asian (CJK) font name. / 東亞（中日韓）字型名稱。</param>
    /// <param name="complexFont">The complex script font name. / 複雜文字字型名稱。</param>
    public void SetFont(string westernFont, string? asianFont, string? complexFont)
    {
        FontName = westernFont;
        FontNameAsian = asianFont ?? westernFont;
        FontNameComplex = complexFont ?? westernFont;
    }

    /// <summary>
    /// Short overload of SetFontSize that accepts westernSize; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 westernSize；其餘可選參數使用預設值並轉呼叫最長 SetFontSize 多載。
    /// </summary>
    public void SetFontSize(string westernSize) => SetFontSize(westernSize, null, null);

    /// <summary>
    /// Short overload of SetFontSize that accepts westernSize and asianSize; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 westernSize 與 asianSize；其餘可選參數使用預設值並轉呼叫最長 SetFontSize 多載。
    /// </summary>
    public void SetFontSize(string westernSize, string? asianSize) => SetFontSize(westernSize, asianSize, null);


    /// <summary>
    /// Sets the text run's font sizes.
    /// 設定文字片段的字型大小。
    /// </summary>
    /// <param name="westernSize">The Western font size. / 西文字型大小。</param>
    /// <param name="asianSize">The East Asian font size. / 東亞字型大小。</param>
    /// <param name="complexSize">The complex script font size. / 複雜文字字型大小。</param>
    public void SetFontSize(string westernSize, string? asianSize, string? complexSize)
    {
        FontSize = westernSize;
        FontSizeAsian = asianSize ?? westernSize;
        FontSizeComplex = complexSize ?? westernSize;
    }


    /// <summary>
    /// Gets or sets a value indicating whether the text run is bold.
    /// 取得或設定一個值，指出文字片段是否為粗體。
    /// </summary>
    public bool IsBold
    {
        get => _doc.StyleEngine.GetStyleProperty(GetStyleName(), "font-weight", OdfNamespaces.Fo, "text") == "bold";
        set => _doc.StyleEngine.SetLocalStyleProperty(Node, "text", "text-properties", "font-weight", OdfNamespaces.Fo, value ? "bold" : "normal", "fo");
    }

    /// <summary>
    /// Gets or sets a value indicating whether the text run is italic.
    /// 取得或設定一個值，指出文字片段是否為斜體。
    /// </summary>
    public bool IsItalic
    {
        get => _doc.StyleEngine.GetStyleProperty(GetStyleName(), "font-style", OdfNamespaces.Fo, "text") == "italic";
        set => _doc.StyleEngine.SetLocalStyleProperty(Node, "text", "text-properties", "font-style", OdfNamespaces.Fo, value ? "italic" : "normal", "fo");
    }

    /// <summary>
    /// Gets or sets a value indicating whether the text run is underlined.
    /// 取得或設定一個值，指出文字片段是否加上底線。
    /// </summary>
    public bool IsUnderline
    {
        get => _doc.StyleEngine.GetStyleProperty(GetStyleName(), "text-underline-style", OdfNamespaces.Style, "text") == "solid";
        set => _doc.StyleEngine.SetLocalStyleProperty(Node, "text", "text-properties", "text-underline-style", OdfNamespaces.Style, value ? "solid" : "none", "style");
    }

    /// <summary>
    /// Gets or sets a value indicating whether the text run has strikethrough.
    /// 取得或設定一個值，指出文字片段是否加上刪除線。
    /// </summary>
    public bool IsStrikethrough
    {
        get => _doc.StyleEngine.GetStyleProperty(GetStyleName(), "text-line-through-style", OdfNamespaces.Style, "text") == "solid";
        set => _doc.StyleEngine.SetLocalStyleProperty(Node, "text", "text-properties", "text-line-through-style", OdfNamespaces.Style, value ? "solid" : "none", "style");
    }

    /// <summary>
    /// Gets or sets the text run's position, e.g. <c>baseline</c>, <c>super</c>, or <c>sub</c>.
    /// 取得或設定文字片段的位置，例如 <c>baseline</c>、<c>super</c> 或 <c>sub</c>。
    /// </summary>
    public string? TextPosition
    {
        get => _doc.StyleEngine.GetStyleProperty(GetStyleName(), "text-position", OdfNamespaces.Style, "text");
        set => _doc.StyleEngine.SetLocalStyleProperty(Node, "text", "text-properties", "text-position", OdfNamespaces.Style, value ?? string.Empty, "style");
    }

    /// <summary>
    /// Gets or sets a value indicating whether the text run is superscript.
    /// 取得或設定一個值，指出文字片段是否為上標。
    /// </summary>
    public bool IsSuperscript
    {
        get => string.Equals(TextPosition, "super", StringComparison.OrdinalIgnoreCase);
        set => TextPosition = value ? "super" : "baseline";
    }

    /// <summary>
    /// Gets or sets a value indicating whether the text run is subscript.
    /// 取得或設定一個值，指出文字片段是否為下標。
    /// </summary>
    public bool IsSubscript
    {
        get => string.Equals(TextPosition, "sub", StringComparison.OrdinalIgnoreCase);
        set => TextPosition = value ? "sub" : "baseline";
    }

    /// <summary>
    /// Gets or sets the text run's font color.
    /// 取得或設定文字片段的字色。
    /// </summary>
    public string? Color
    {
        get => _doc.StyleEngine.GetStyleProperty(GetStyleName(), "color", OdfNamespaces.Fo, "text");
        set => _doc.StyleEngine.SetLocalStyleProperty(Node, "text", "text-properties", "color", OdfNamespaces.Fo, value ?? string.Empty, "fo");
    }

    /// <summary>
    /// Gets or sets the text run's background color.
    /// 取得或設定文字片段的背景色。
    /// </summary>
    public string? BackgroundColor
    {
        get => _doc.StyleEngine.GetStyleProperty(GetStyleName(), "background-color", OdfNamespaces.Fo, "text");
        set => _doc.StyleEngine.SetLocalStyleProperty(Node, "text", "text-properties", "background-color", OdfNamespaces.Fo, value ?? string.Empty, "fo");
    }

    /// <summary>
    /// Gets or sets the text case transform, e.g. <c>uppercase</c>.
    /// 取得或設定文字大小寫轉換，例如 <c>uppercase</c>。
    /// </summary>
    public string? TextTransform
    {
        get => _doc.StyleEngine.GetStyleProperty(GetStyleName(), "text-transform", OdfNamespaces.Fo, "text");
        set => _doc.StyleEngine.SetLocalStyleProperty(Node, "text", "text-properties", "text-transform", OdfNamespaces.Fo, value ?? string.Empty, "fo");
    }

    /// <summary>
    /// Gets or sets the font variant, e.g. <c>small-caps</c>.
    /// 取得或設定字型變體，例如 <c>small-caps</c>。
    /// </summary>
    public string? FontVariant
    {
        get => _doc.StyleEngine.GetStyleProperty(GetStyleName(), "font-variant", OdfNamespaces.Fo, "text");
        set => _doc.StyleEngine.SetLocalStyleProperty(Node, "text", "text-properties", "font-variant", OdfNamespaces.Fo, value ?? string.Empty, "fo");
    }

    /// <summary>
    /// Gets or sets the text run's East Asian (CJK) font name.
    /// 取得或設定文字片段的東亞（中日韓）字型名稱。
    /// </summary>
    public string? FontNameAsian
    {
        get => _doc.StyleEngine.GetStyleProperty(GetStyleName(), "font-name-asian", OdfNamespaces.Style, "text");
        set => _doc.StyleEngine.SetLocalStyleProperty(Node, "text", "text-properties", "font-name-asian", OdfNamespaces.Style, value ?? string.Empty, "style");
    }

    /// <summary>
    /// Gets or sets the text run's complex script font name.
    /// 取得或設定文字片段的複雜文字字型名稱。
    /// </summary>
    public string? FontNameComplex
    {
        get => _doc.StyleEngine.GetStyleProperty(GetStyleName(), "font-name-complex", OdfNamespaces.Style, "text");
        set => _doc.StyleEngine.SetLocalStyleProperty(Node, "text", "text-properties", "font-name-complex", OdfNamespaces.Style, value ?? string.Empty, "style");
    }

    /// <summary>
    /// Gets or sets the text run's East Asian (CJK) font size.
    /// 取得或設定文字片段的東亞（中日韓）字型大小。
    /// </summary>
    public string? FontSizeAsian
    {
        get => _doc.StyleEngine.GetStyleProperty(GetStyleName(), "font-size-asian", OdfNamespaces.Style, "text");
        set => _doc.StyleEngine.SetLocalStyleProperty(Node, "text", "text-properties", "font-size-asian", OdfNamespaces.Style, value ?? string.Empty, "style");
    }

    /// <summary>
    /// Gets or sets the text run's complex script font size.
    /// 取得或設定文字片段的複雜文字字型大小。
    /// </summary>
    public string? FontSizeComplex
    {
        get => _doc.StyleEngine.GetStyleProperty(GetStyleName(), "font-size-complex", OdfNamespaces.Style, "text");
        set => _doc.StyleEngine.SetLocalStyleProperty(Node, "text", "text-properties", "font-size-complex", OdfNamespaces.Style, value ?? string.Empty, "style");
    }

    private string GetStyleName() => Node.GetAttribute("style-name", OdfNamespaces.Text) ?? string.Empty;

    /// <summary>
    /// Deletes this text run.
    /// 刪除此文字片段。
    /// </summary>
    public void Delete()
    {
        _doc.DeleteNode(Node);
    }
    /// <summary>
    /// Short overload of WithBold that uses default values for all optional parameters and forwards to the full overload.
    /// 便利多載：WithBold 的所有可選參數使用預設值並轉呼叫最長多載。
    /// </summary>
    public OdfTextRun WithBold() => WithBold(true);


    /// <summary>
    /// Sets whether this text run is bold.
    /// 設定此文字片段是否為粗體。
    /// </summary>
    /// <param name="bold">Whether it is bold. / 是否粗體。</param>
    /// <returns>This text run instance. / 文字片段本身。</returns>
    public OdfTextRun WithBold(bool bold)
    {
        IsBold = bold;
        return this;
    }

    /// <summary>
    /// Short overload of WithItalic that uses default values for all optional parameters and forwards to the full overload.
    /// 便利多載：WithItalic 的所有可選參數使用預設值並轉呼叫最長多載。
    /// </summary>
    public OdfTextRun WithItalic() => WithItalic(true);


    /// <summary>
    /// Sets whether this text run is italic.
    /// 設定此文字片段是否為斜體。
    /// </summary>
    /// <param name="italic">Whether it is italic. / 是否斜體。</param>
    /// <returns>This text run instance. / 文字片段本身。</returns>
    public OdfTextRun WithItalic(bool italic)
    {
        IsItalic = italic;
        return this;
    }

    /// <summary>
    /// Short overload of WithStrikethrough that uses default values for all optional parameters and forwards to the full overload.
    /// 便利多載：WithStrikethrough 的所有可選參數使用預設值並轉呼叫最長多載。
    /// </summary>
    public OdfTextRun WithStrikethrough() => WithStrikethrough(true);


    /// <summary>
    /// Sets whether this text run has strikethrough.
    /// 設定此文字片段是否加上刪除線。
    /// </summary>
    /// <param name="strikethrough">Whether it has strikethrough. / 是否加上刪除線。</param>
    /// <returns>This text run instance. / 文字片段本身。</returns>
    public OdfTextRun WithStrikethrough(bool strikethrough)
    {
        IsStrikethrough = strikethrough;
        return this;
    }

    /// <summary>
    /// Short overload of WithSuperscript that uses default values for all optional parameters and forwards to the full overload.
    /// 便利多載：WithSuperscript 的所有可選參數使用預設值並轉呼叫最長多載。
    /// </summary>
    public OdfTextRun WithSuperscript() => WithSuperscript(true);


    /// <summary>
    /// Sets whether this text run is superscript.
    /// 設定此文字片段是否為上標。
    /// </summary>
    /// <param name="superscript">Whether it is superscript. / 是否為上標。</param>
    /// <returns>This text run instance. / 文字片段本身。</returns>
    public OdfTextRun WithSuperscript(bool superscript)
    {
        IsSuperscript = superscript;
        return this;
    }

    /// <summary>
    /// Short overload of WithSubscript that uses default values for all optional parameters and forwards to the full overload.
    /// 便利多載：WithSubscript 的所有可選參數使用預設值並轉呼叫最長多載。
    /// </summary>
    public OdfTextRun WithSubscript() => WithSubscript(true);


    /// <summary>
    /// Sets whether this text run is subscript.
    /// 設定此文字片段是否為下標。
    /// </summary>
    /// <param name="subscript">Whether it is subscript. / 是否為下標。</param>
    /// <returns>This text run instance. / 文字片段本身。</returns>
    public OdfTextRun WithSubscript(bool subscript)
    {
        IsSubscript = subscript;
        return this;
    }


    /// <summary>
    /// Sets this text run's Western, East Asian, and complex script font sizes.
    /// 設定此文字片段的西文、東亞及複雜字型大小。
    /// </summary>
    /// <param name="size">The font size, e.g. <c>12pt</c>. / 字型大小，例如 <c>12pt</c>。</param>
    /// <returns>This text run instance. / 文字片段本身。</returns>
    public OdfTextRun WithFontSize(string size)
    {
        SetFontSize(size);
        return this;
    }
    /// <summary>
    /// Short overload of WithFontName that accepts westernFont; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 westernFont；其餘可選參數使用預設值並轉呼叫最長 WithFontName 多載。
    /// </summary>
    public OdfTextRun WithFontName(string westernFont) => WithFontName(westernFont, null, null);

    /// <summary>
    /// Short overload of WithFontName that accepts westernFont and asianFont; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 westernFont 與 asianFont；其餘可選參數使用預設值並轉呼叫最長 WithFontName 多載。
    /// </summary>
    public OdfTextRun WithFontName(string westernFont, string? asianFont) => WithFontName(westernFont, asianFont, null);


    /// <summary>
    /// Sets this text run's font names.
    /// 設定此文字片段的字型名稱。
    /// </summary>
    /// <param name="westernFont">The Western font name. / 西文字型名稱。</param>
    /// <param name="asianFont">The East Asian (CJK) font name; defaults to <paramref name="westernFont"/> if not specified. / 東亞（中日韓）字型名稱；未指定時沿用 <paramref name="westernFont"/>。</param>
    /// <param name="complexFont">The complex script font name; defaults to <paramref name="westernFont"/> if not specified. / 複雜文字字型名稱；未指定時沿用 <paramref name="westernFont"/>。</param>
    /// <returns>This text run instance. / 文字片段本身。</returns>
    public OdfTextRun WithFontName(string westernFont, string? asianFont, string? complexFont)
    {
        SetFont(westernFont, asianFont, complexFont);
        return this;
    }


    /// <summary>
    /// Sets this text run's font color.
    /// 設定此文字片段的字色。
    /// </summary>
    /// <param name="hexColor">The hexadecimal color string, e.g. <c>#FF0000</c>. / 十六進位顏色字串，例如 <c>#FF0000</c>。</param>
    /// <returns>This text run instance. / 文字片段本身。</returns>
    public OdfTextRun WithColor(string hexColor)
    {
        Color = hexColor;
        return this;
    }

    /// <summary>
    /// Sets this text run's background color.
    /// 設定此文字片段的背景色。
    /// </summary>
    /// <param name="hexColor">The hexadecimal color string, e.g. <c>#FFFF00</c>. / 十六進位顏色字串，例如 <c>#FFFF00</c>。</param>
    /// <returns>This text run instance. / 文字片段本身。</returns>
    public OdfTextRun WithBackgroundColor(string hexColor)
    {
        BackgroundColor = hexColor;
        return this;
    }

    /// <summary>
    /// Sets this text run's case transform.
    /// 設定此文字片段的大小寫轉換。
    /// </summary>
    /// <param name="transform">The case transform value, e.g. <c>uppercase</c>. / 大小寫轉換值，例如 <c>uppercase</c>。</param>
    /// <returns>This text run instance. / 文字片段本身。</returns>
    public OdfTextRun WithTextTransform(string transform)
    {
        TextTransform = transform;
        return this;
    }

    /// <summary>
    /// Sets this text run's font variant.
    /// 設定此文字片段的字型變體。
    /// </summary>
    /// <param name="variant">The font variant value, e.g. <c>small-caps</c>. / 字型變體值，例如 <c>small-caps</c>。</param>
    /// <returns>This text run instance. / 文字片段本身。</returns>
    public OdfTextRun WithFontVariant(string variant)
    {
        FontVariant = variant;
        return this;
    }
}
