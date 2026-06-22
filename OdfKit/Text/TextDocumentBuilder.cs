using System.Globalization;
using OdfKit.Core;
using OdfKit.DOM;

using OdfKit.Compliance;
namespace OdfKit.Text;

/// <summary>
/// 提供 <see cref="TextDocument"/> 的 Fluent 建立 API。
/// </summary>
public sealed class TextDocumentBuilder
{
    private readonly TextDocument _document;

    internal TextDocumentBuilder(TextDocument document)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
    }

    /// <summary>
    /// 設定文件中繼資料。
    /// </summary>
    /// <param name="configure">中繼資料設定委派。</param>
    /// <returns>目前 builder 執行個體。</returns>
    public TextDocumentBuilder WithMetadata(Action<TextDocumentMetadataBuilder> configure)
    {
        if (configure is null)
            throw new ArgumentNullException(nameof(configure));
        configure(new TextDocumentMetadataBuilder(_document.Metadata));
        return this;
    }

    /// <summary>
    /// 新增標題。
    /// </summary>
    /// <param name="text">標題文字。</param>
    /// <param name="level">大綱階層。</param>
    /// <returns>目前 builder 執行個體。</returns>
    public TextDocumentBuilder AddHeading(string text, int level = 1)
    {
        _document.Body.Headings.Add(text, level);
        return this;
    }

    /// <summary>
    /// 新增段落。
    /// </summary>
    /// <param name="text">段落文字。</param>
    /// <param name="configure">段落樣式設定委派。</param>
    /// <returns>目前 builder 執行個體。</returns>
    public TextDocumentBuilder AddParagraph(string text, Action<TextRunFormattingBuilder>? configure = null)
    {
        OdfParagraph paragraph = _document.Body.Paragraphs.Add(text);
        if (configure is not null)
        {
            var format = new TextRunFormattingBuilder();
            configure(format);
            ApplyParagraphFormatting(_document, paragraph, format);
        }

        return this;
    }

    /// <summary>
    /// 新增由多個文字片段組成的段落。
    /// </summary>
    /// <param name="configure">段落內容設定委派。</param>
    /// <returns>目前 builder 執行個體。</returns>
    public TextDocumentBuilder AddParagraph(Action<TextParagraphBuilder> configure)
    {
        if (configure is null)
            throw new ArgumentNullException(nameof(configure));
        OdfParagraph paragraph = _document.Body.Paragraphs.Add();
        configure(new TextParagraphBuilder(_document, paragraph));
        return this;
    }

    /// <summary>
    /// 新增項目清單。
    /// </summary>
    /// <param name="configure">清單內容設定委派。</param>
    /// <returns>目前 builder 執行個體。</returns>
    public TextDocumentBuilder AddList(Action<TextListBuilder> configure)
    {
        if (configure is null)
            throw new ArgumentNullException(nameof(configure));
        OdfList list = _document.Body.Lists.Add();
        configure(new TextListBuilder(list));
        return this;
    }

    /// <summary>
    /// 建立並傳回文字文件。
    /// </summary>
    /// <returns>建立完成的文字文件。</returns>
    public TextDocument Build()
    {
        return _document;
    }

    internal static void ApplyTextRunFormatting(TextDocument document, OdfTextRun run, TextRunFormattingBuilder format)
    {
        if (format.BoldValue.HasValue)
            run.IsBold = format.BoldValue.Value;
        if (format.ItalicValue.HasValue)
            run.IsItalic = format.ItalicValue.Value;
        if (format.UnderlineValue.HasValue)
            run.IsUnderline = format.UnderlineValue.Value;
        if (format.FontSizeValue.HasValue)
            run.SetFontSize(ToPointString(format.FontSizeValue.Value));
        if (format.ColorValue is not null)
        {
            document.StyleEngine.SetLocalStyleProperty(
                run.Node,
                "text",
                "text-properties",
                "color",
                OdfNamespaces.Fo,
                format.ColorValue,
                "fo");
        }
    }

    private static void ApplyParagraphFormatting(TextDocument document, OdfParagraph paragraph, TextRunFormattingBuilder format)
    {
        if (format.BoldValue.HasValue)
        {
            document.StyleEngine.SetLocalStyleProperty(
                paragraph.Node,
                "paragraph",
                "text-properties",
                "font-weight",
                OdfNamespaces.Fo,
                format.BoldValue.Value ? "bold" : "normal",
                "fo");
        }

        if (format.ItalicValue.HasValue)
        {
            document.StyleEngine.SetLocalStyleProperty(
                paragraph.Node,
                "paragraph",
                "text-properties",
                "font-style",
                OdfNamespaces.Fo,
                format.ItalicValue.Value ? "italic" : "normal",
                "fo");
        }

        if (format.UnderlineValue.HasValue)
        {
            document.StyleEngine.SetLocalStyleProperty(
                paragraph.Node,
                "paragraph",
                "text-properties",
                "text-underline-style",
                OdfNamespaces.Style,
                format.UnderlineValue.Value ? "solid" : "none",
                "style");
        }

        if (format.FontSizeValue.HasValue)
        {
            paragraph.SetFontSize(ToPointString(format.FontSizeValue.Value));
        }

        if (format.ColorValue is not null)
        {
            document.StyleEngine.SetLocalStyleProperty(
                paragraph.Node,
                "paragraph",
                "text-properties",
                "color",
                OdfNamespaces.Fo,
                format.ColorValue,
                "fo");
        }
    }

    private static string ToPointString(double points)
    {
        return points.ToString(CultureInfo.InvariantCulture) + "pt";
    }
}

/// <summary>
/// 提供文字文件中繼資料的 Fluent 設定 API。
/// </summary>
public sealed class TextDocumentMetadataBuilder
{
    private readonly OdfDocumentMetadata _metadata;

    internal TextDocumentMetadataBuilder(OdfDocumentMetadata metadata)
    {
        _metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
    }

    /// <summary>
    /// 設定標題。
    /// </summary>
    /// <param name="value">標題。</param>
    /// <returns>目前 builder 執行個體。</returns>
    public TextDocumentMetadataBuilder Title(string value)
    {
        _metadata.Title = value;
        return this;
    }

    /// <summary>
    /// 設定作者。
    /// </summary>
    /// <param name="value">作者。</param>
    /// <returns>目前 builder 執行個體。</returns>
    public TextDocumentMetadataBuilder Author(string value)
    {
        _metadata.Creator = value;
        return this;
    }

    /// <summary>
    /// 設定主旨。
    /// </summary>
    /// <param name="value">主旨。</param>
    /// <returns>目前 builder 執行個體。</returns>
    public TextDocumentMetadataBuilder Subject(string value)
    {
        _metadata.Subject = value;
        return this;
    }

    /// <summary>
    /// 設定描述。
    /// </summary>
    /// <param name="value">描述。</param>
    /// <returns>目前 builder 執行個體。</returns>
    public TextDocumentMetadataBuilder Description(string value)
    {
        _metadata.Description = value;
        return this;
    }
}

/// <summary>
/// 提供段落文字片段的 Fluent 建立 API。
/// </summary>
public sealed class TextParagraphBuilder
{
    private readonly TextDocument _document;
    private readonly OdfParagraph _paragraph;

    internal TextParagraphBuilder(TextDocument document, OdfParagraph paragraph)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _paragraph = paragraph ?? throw new ArgumentNullException(nameof(paragraph));
    }

    /// <summary>
    /// 新增純文字片段。
    /// </summary>
    /// <param name="text">文字內容。</param>
    /// <returns>目前 builder 執行個體。</returns>
    public TextParagraphBuilder Append(string text)
    {
        _paragraph.AddTextRun(text);
        return this;
    }

    /// <summary>
    /// 新增帶格式的文字片段。
    /// </summary>
    /// <param name="text">文字內容。</param>
    /// <param name="configure">格式設定委派。</param>
    /// <returns>目前 builder 執行個體。</returns>
    public TextParagraphBuilder Append(string text, Action<TextRunFormattingBuilder> configure)
    {
        if (configure is null)
            throw new ArgumentNullException(nameof(configure));
        var format = new TextRunFormattingBuilder();
        configure(format);
        OdfTextRun run = _paragraph.AddTextRun(text);
        TextDocumentBuilder.ApplyTextRunFormatting(_document, run, format);
        return this;
    }
}

/// <summary>
/// 提供文字片段格式的 Fluent 設定 API。
/// </summary>
public sealed class TextRunFormattingBuilder
{
    internal bool? BoldValue { get; private set; }
    internal bool? ItalicValue { get; private set; }
    internal bool? UnderlineValue { get; private set; }
    internal double? FontSizeValue { get; private set; }
    internal string? ColorValue { get; private set; }

    /// <summary>
    /// 設定文字為粗體。
    /// </summary>
    /// <returns>目前 builder 執行個體。</returns>
    public TextRunFormattingBuilder Bold()
    {
        BoldValue = true;
        return this;
    }

    /// <summary>
    /// 設定文字為斜體。
    /// </summary>
    /// <returns>目前 builder 執行個體。</returns>
    public TextRunFormattingBuilder Italic()
    {
        ItalicValue = true;
        return this;
    }

    /// <summary>
    /// 設定文字底線。
    /// </summary>
    /// <returns>目前 builder 執行個體。</returns>
    public TextRunFormattingBuilder Underline()
    {
        UnderlineValue = true;
        return this;
    }

    /// <summary>
    /// 設定文字大小。
    /// </summary>
    /// <param name="points">點數大小。</param>
    /// <returns>目前 builder 執行個體。</returns>
    public TextRunFormattingBuilder FontSize(double points)
    {
        FontSizeValue = points;
        return this;
    }

    /// <summary>
    /// 設定文字色彩。
    /// </summary>
    /// <param name="color">色彩值，格式為 <c>#RRGGBB</c>。</param>
    /// <returns>目前 builder 執行個體。</returns>
    public TextRunFormattingBuilder Color(string color)
    {
        if (!OdfColor.TryParse(color, out _))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_TextDocumentBuilder_ColorValuesRrggbbFormat"), nameof(color));
        }

        ColorValue = color;
        return this;
    }
}

/// <summary>
/// 提供文字清單的 Fluent 建立 API。
/// </summary>
public sealed class TextListBuilder
{
    private readonly OdfList _list;

    internal TextListBuilder(OdfList list)
    {
        _list = list ?? throw new ArgumentNullException(nameof(list));
    }

    /// <summary>
    /// 新增清單項目。
    /// </summary>
    /// <param name="text">項目文字。</param>
    /// <returns>目前 builder 執行個體。</returns>
    public TextListBuilder Item(string text)
    {
        _list.AddItem(text);
        return this;
    }
}
