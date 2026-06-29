using System.Globalization;
using OdfKit.Chart;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Spreadsheet;
using OdfKit.Styles;

using OdfKit.Compliance;
namespace OdfKit.Text;

/// <summary>
/// Provides APIs for text document builder.
/// 提供 <see cref="TextDocument"/> 的 Fluent 建立 API。
/// </summary>
public sealed class TextDocumentBuilder
{
    private readonly TextDocument _document;
    private OdfStyleSet? _styles;

    internal TextDocumentBuilder(TextDocument document)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
    }

    /// <summary>
    /// Sets with metadata.
    /// 設定文件中繼資料。
    /// </summary>
    /// <param name="configure">The delegate to invoke. / 中繼資料設定委派</param>
    /// <returns>The result. / 目前 builder 執行個體</returns>
    public TextDocumentBuilder WithMetadata(Action<TextDocumentMetadataBuilder> configure)
    {
        if (configure is null)
            throw new ArgumentNullException(nameof(configure));
        configure(new TextDocumentMetadataBuilder(_document.Metadata));
        return this;
    }

    /// <summary>
    /// Sets with styles.
    /// 設定此 builder 後續建立內容會套用的樣式集合。
    /// </summary>
    /// <param name="styles">The value to use. / 樣式集合</param>
    /// <returns>The result. / 目前 builder 執行個體</returns>
    public TextDocumentBuilder WithStyles(OdfStyleSet styles)
    {
        _styles = styles ?? throw new ArgumentNullException(nameof(styles));
        return this;
    }

    /// <summary>
    /// Sets with styles.
    /// 設定此 builder 後續建立內容會套用的樣式集合。
    /// </summary>
    /// <param name="configure">The delegate to invoke. / 樣式集合設定委派</param>
    /// <returns>The result. / 目前 builder 執行個體</returns>
    public TextDocumentBuilder WithStyles(Action<OdfStyleSet> configure)
    {
        if (configure is null)
            throw new ArgumentNullException(nameof(configure));

        var styles = new OdfStyleSet();
        configure(styles);
        return WithStyles(styles);
    }

    /// <summary>
    /// Sets with theme.
    /// 設定此 builder 後續建立內容會套用的設計主題。
    /// </summary>
    /// <param name="theme">The value to use. / 設計主題</param>
    /// <returns>The result. / 目前 builder 執行個體</returns>
    public TextDocumentBuilder WithTheme(OdfDesignTheme theme)
    {
        _styles = OdfStyleSet.FromTheme(theme);
        return this;
    }

    /// <summary>
    /// Adds add heading.
    /// 新增標題。
    /// </summary>
    /// <param name="text">The text or value. / 標題文字</param>
    /// <param name="level">The numeric value. / 大綱階層</param>
    /// <returns>The result. / 目前 builder 執行個體</returns>
    public TextDocumentBuilder AddHeading(string text, int level = 1)
    {
        OdfParagraph heading = _document.Body.Headings.Add(text, level);
        ApplyHeadingStyle(_document, heading, _styles);
        return this;
    }

    /// <summary>
    /// Adds add paragraph.
    /// 新增段落。
    /// </summary>
    /// <param name="text">The text or value. / 段落文字</param>
    /// <param name="configure">The delegate to invoke. / 段落樣式設定委派</param>
    /// <returns>The result. / 目前 builder 執行個體</returns>
    public TextDocumentBuilder AddParagraph(string text, Action<TextRunFormattingBuilder>? configure = null)
    {
        OdfParagraph paragraph = _document.Body.Paragraphs.Add(text);
        ApplyBodyStyle(_document, paragraph, _styles);
        if (configure is not null)
        {
            var format = new TextRunFormattingBuilder();
            configure(format);
            ApplyParagraphFormatting(_document, paragraph, format);
        }

        return this;
    }

    /// <summary>
    /// Adds add paragraph.
    /// 新增由多個文字片段組成的段落。
    /// </summary>
    /// <param name="configure">The delegate to invoke. / 段落內容設定委派</param>
    /// <returns>The result. / 目前 builder 執行個體</returns>
    public TextDocumentBuilder AddParagraph(Action<TextParagraphBuilder> configure)
    {
        if (configure is null)
            throw new ArgumentNullException(nameof(configure));
        OdfParagraph paragraph = _document.Body.Paragraphs.Add();
        ApplyBodyStyle(_document, paragraph, _styles);
        configure(new TextParagraphBuilder(_document, paragraph));
        return this;
    }

    /// <summary>
    /// Adds add cover page.
    /// 新增封面頁，包含標題、可選副標題、作者與日期文字，並在封面後插入分頁。
    /// </summary>
    /// <param name="title">The name or identifier. / 封面標題</param>
    /// <param name="subtitle">The name or identifier. / 封面副標題</param>
    /// <param name="author">The name or identifier. / 作者或組織名稱</param>
    /// <param name="dateText">The text or value. / 日期或期間文字</param>
    /// <returns>The result. / 目前 builder 執行個體</returns>
    public TextDocumentBuilder AddCoverPage(
        string title,
        string? subtitle = null,
        string? author = null,
        string? dateText = null)
    {
        if (title is null)
            throw new ArgumentNullException(nameof(title));

        OdfParagraph lastParagraph = _document.Body.Headings.Add(title, 1);
        ApplyCoverLineStyle(lastParagraph, "28pt", bold: true, marginTop: "6cm", marginBottom: "0.6cm");

        if (!string.IsNullOrWhiteSpace(subtitle))
        {
            lastParagraph = _document.Body.Paragraphs.Add(subtitle!);
            ApplyCoverLineStyle(lastParagraph, "16pt", bold: false, marginTop: "0cm", marginBottom: "1.4cm");
        }

        if (!string.IsNullOrWhiteSpace(author))
        {
            lastParagraph = _document.Body.Paragraphs.Add(author!);
            ApplyCoverLineStyle(lastParagraph, "12pt", bold: false, marginTop: "0cm", marginBottom: "0.2cm");
        }

        if (!string.IsNullOrWhiteSpace(dateText))
        {
            lastParagraph = _document.Body.Paragraphs.Add(dateText!);
            ApplyCoverLineStyle(lastParagraph, "12pt", bold: false, marginTop: "0cm", marginBottom: "0cm");
        }

        _document.StyleEngine.SetLocalStyleProperty(
            lastParagraph.Node,
            "paragraph",
            "paragraph-properties",
            "break-after",
            OdfNamespaces.Fo,
            "page",
            "fo");
        return this;
    }

    /// <summary>
    /// Adds add list.
    /// 新增專案清單。
    /// </summary>
    /// <param name="configure">The delegate to invoke. / 清單內容設定委派</param>
    /// <returns>The result. / 目前 builder 執行個體</returns>
    public TextDocumentBuilder AddList(Action<TextListBuilder> configure)
    {
        if (configure is null)
            throw new ArgumentNullException(nameof(configure));
        OdfList list = _document.Body.Lists.Add();
        configure(new TextListBuilder(list));
        return this;
    }

    /// <summary>
    /// Adds add table of contents.
    /// 新增目錄並立即更新目錄內容。
    /// </summary>
    /// <param name="title">The name or identifier. / 目錄標題</param>
    /// <param name="outlineLevel">The numeric value. / 目錄大綱階層上限</param>
    /// <returns>The result. / 目前 builder 執行個體</returns>
    public TextDocumentBuilder AddTableOfContents(string title = "目錄", int outlineLevel = 10)
    {
        _document.InsertTableOfContents(title, outlineLevel);
        return this;
    }

    /// <summary>
    /// Adds add table.
    /// 新增文字表格。
    /// </summary>
    /// <param name="rows">The numeric value. / 列數</param>
    /// <param name="columns">The numeric value. / 欄數</param>
    /// <param name="configure">The delegate to invoke. / 表格設定委派</param>
    /// <returns>The result. / 目前 builder 執行個體</returns>
    public TextDocumentBuilder AddTable(int rows, int columns, Action<TextTableBuilder>? configure = null)
    {
        OdfTable table = _document.AddTable(rows, columns);
        configure?.Invoke(new TextTableBuilder(table));
        ApplyTableHeaderStyle(_document, table, columns, _styles);
        return this;
    }

    /// <summary>
    /// Adds add section.
    /// 新增多欄區段。
    /// </summary>
    /// <param name="name">The name or identifier. / 區段名稱</param>
    /// <param name="columnCount">The numeric value. / 欄數</param>
    /// <param name="gap">The value to use. / 欄間距</param>
    /// <param name="configure">The delegate to invoke. / 區段內容設定委派</param>
    /// <returns>The result. / 目前 builder 執行個體</returns>
    public TextDocumentBuilder AddSection(
        string name,
        int columnCount,
        OdfLength gap,
        Action<TextSectionBuilder>? configure = null)
    {
        OdfSection section = _document.AddSection(name, columnCount, gap);
        configure?.Invoke(new TextSectionBuilder(_document, section));
        return this;
    }

    /// <summary>
    /// Sets with page setup.
    /// 設定預設頁面樣式的頁首與頁尾。
    /// </summary>
    /// <param name="configure">The delegate to invoke. / 頁面設定委派</param>
    /// <returns>The result. / 目前 builder 執行個體</returns>
    public TextDocumentBuilder WithPageSetup(Action<TextPageSetupBuilder> configure)
    {
        if (configure is null)
            throw new ArgumentNullException(nameof(configure));

        configure(new TextPageSetupBuilder(_document.GetDefaultPageSetup()));
        return this;
    }

    /// <summary>
    /// Creates build.
    /// 建立並傳回文字文件。
    /// </summary>
    /// <returns>The result. / 建立完成的文字文件</returns>
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

        if (format.BackgroundColorValue is not null)
        {
            run.BackgroundColor = format.BackgroundColorValue;
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

        if (format.BackgroundColorValue is not null)
        {
            document.StyleEngine.SetLocalStyleProperty(
                paragraph.Node,
                "paragraph",
                "text-properties",
                "background-color",
                OdfNamespaces.Fo,
                format.BackgroundColorValue,
                "fo");
        }
    }

    private static void ApplyHeadingStyle(TextDocument document, OdfParagraph paragraph, OdfStyleSet? styles)
    {
        if (styles is null)
            return;

        if (styles.HeadingFontSizePoints.HasValue)
        {
            paragraph.SetFontSize(ToPointString(styles.HeadingFontSizePoints.Value));
        }

        if (styles.HeadingColor is not null)
        {
            document.StyleEngine.SetLocalStyleProperty(
                paragraph.Node,
                "paragraph",
                "text-properties",
                "color",
                OdfNamespaces.Fo,
                styles.HeadingColor,
                "fo");
        }
    }

    private static void ApplyBodyStyle(TextDocument document, OdfParagraph paragraph, OdfStyleSet? styles)
    {
        if (styles is null)
            return;

        if (styles.BodyFontSizePoints.HasValue)
        {
            paragraph.SetFontSize(ToPointString(styles.BodyFontSizePoints.Value));
        }

        if (styles.BodyColor is not null)
        {
            document.StyleEngine.SetLocalStyleProperty(
                paragraph.Node,
                "paragraph",
                "text-properties",
                "color",
                OdfNamespaces.Fo,
                styles.BodyColor,
                "fo");
        }
    }

    private static void ApplyTableHeaderStyle(TextDocument document, OdfTable table, int columns, OdfStyleSet? styles)
    {
        if (styles is null || columns <= 0)
            return;

        for (int column = 0; column < columns; column++)
        {
            OdfTableCell cell = table.GetCell(0, column);
            if (styles.TableHeaderBackgroundColor is not null)
            {
                document.StyleEngine.SetLocalStyleProperty(
                    cell.Node,
                    "table-cell",
                    "table-cell-properties",
                    "background-color",
                    OdfNamespaces.Fo,
                    styles.TableHeaderBackgroundColor,
                    "fo");
            }

            if (styles.TableHeaderColor is not null)
            {
                document.StyleEngine.SetLocalStyleProperty(
                    cell.Node,
                    "table-cell",
                    "text-properties",
                    "color",
                    OdfNamespaces.Fo,
                    styles.TableHeaderColor,
                    "fo");
            }

            document.StyleEngine.SetLocalStyleProperty(
                cell.Node,
                "table-cell",
                "text-properties",
                "font-weight",
                OdfNamespaces.Fo,
                styles.TableHeaderBold ? "bold" : "normal",
                "fo");
        }
    }

    private static void ApplyCoverLineStyle(OdfParagraph paragraph, string fontSize, bool bold, string marginTop, string marginBottom)
    {
        paragraph.HorizontalAlignment = "center";
        paragraph.SetFontSize(fontSize);
        paragraph.StyleEngine.SetLocalStyleProperty(
            paragraph.Node,
            "paragraph",
            "text-properties",
            "font-weight",
            OdfNamespaces.Fo,
            bold ? "bold" : "normal",
            "fo");
        paragraph.StyleEngine.SetLocalStyleProperty(
            paragraph.Node,
            "paragraph",
            "paragraph-properties",
            "margin-top",
            OdfNamespaces.Fo,
            marginTop,
            "fo");
        paragraph.StyleEngine.SetLocalStyleProperty(
            paragraph.Node,
            "paragraph",
            "paragraph-properties",
            "margin-bottom",
            OdfNamespaces.Fo,
            marginBottom,
            "fo");
    }

    private static string ToPointString(double points)
    {
        return points.ToString(CultureInfo.InvariantCulture) + "pt";
    }
}

/// <summary>
/// Provides APIs for text document metadata builder.
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
    /// Sets title.
    /// 設定標題。
    /// </summary>
    /// <param name="value">The text or value. / 標題</param>
    /// <returns>The result. / 目前 builder 執行個體</returns>
    public TextDocumentMetadataBuilder Title(string value)
    {
        _metadata.Title = value;
        return this;
    }

    /// <summary>
    /// Sets author.
    /// 設定作者。
    /// </summary>
    /// <param name="value">The text or value. / 作者</param>
    /// <returns>The result. / 目前 builder 執行個體</returns>
    public TextDocumentMetadataBuilder Author(string value)
    {
        _metadata.Creator = value;
        return this;
    }

    /// <summary>
    /// Sets subject.
    /// 設定主旨。
    /// </summary>
    /// <param name="value">The text or value. / 主旨</param>
    /// <returns>The result. / 目前 builder 執行個體</returns>
    public TextDocumentMetadataBuilder Subject(string value)
    {
        _metadata.Subject = value;
        return this;
    }

    /// <summary>
    /// Sets description.
    /// 設定描述。
    /// </summary>
    /// <param name="value">The text or value. / 描述</param>
    /// <returns>The result. / 目前 builder 執行個體</returns>
    public TextDocumentMetadataBuilder Description(string value)
    {
        _metadata.Description = value;
        return this;
    }
}

/// <summary>
/// Provides APIs for text paragraph builder.
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
    /// Adds append.
    /// 新增純文字片段。
    /// </summary>
    /// <param name="text">The text or value. / 文字內容</param>
    /// <returns>The result. / 目前 builder 執行個體</returns>
    public TextParagraphBuilder Append(string text)
    {
        _paragraph.AddTextRun(text);
        return this;
    }

    /// <summary>
    /// Adds append.
    /// 新增帶格式的文字片段。
    /// </summary>
    /// <param name="text">The text or value. / 文字內容</param>
    /// <param name="configure">The delegate to invoke. / 格式設定委派</param>
    /// <returns>The result. / 目前 builder 執行個體</returns>
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

    /// <summary>
    /// Adds add footnote.
    /// 新增腳註。
    /// </summary>
    /// <param name="citation">The value to use. / 腳註引用標記</param>
    /// <param name="bodyText">The text or value. / 腳註本文</param>
    /// <returns>The result. / 目前 builder 執行個體</returns>
    public TextParagraphBuilder AddFootnote(string citation, string bodyText)
    {
        _paragraph.AddFootnote(citation, bodyText);
        return this;
    }

    /// <summary>
    /// Adds add comment.
    /// 新增註解。
    /// </summary>
    /// <param name="author">The name or identifier. / 作者名稱</param>
    /// <param name="text">The text or value. / 註解內容</param>
    /// <returns>The result. / 目前 builder 執行個體</returns>
    public TextParagraphBuilder AddComment(string author, string text)
    {
        _paragraph.AddComment(new OdfComment(author, text));
        return this;
    }

    /// <summary>
    /// Adds add image.
    /// 新增圖片框架。
    /// </summary>
    /// <param name="imageBytes">The value to use. / 圖片位元組</param>
    /// <param name="width">The name or identifier. / 圖片寬度</param>
    /// <param name="height">The numeric value. / 圖片高度</param>
    /// <param name="name">The name or identifier. / 圖片名稱</param>
    /// <returns>The result. / 目前 builder 執行個體</returns>
    public TextParagraphBuilder AddImage(byte[] imageBytes, OdfLength width, OdfLength height, string? name = null)
    {
        _document.AddImageFrame(_paragraph, imageBytes, width, height, name);
        return this;
    }

    /// <summary>
    /// Adds add chart.
    /// 新增嵌入圖表。
    /// </summary>
    /// <param name="chart">The value to use. / 圖表定義</param>
    /// <param name="width">The name or identifier. / 圖表寬度</param>
    /// <param name="height">The numeric value. / 圖表高度</param>
    /// <returns>The result. / 目前 builder 執行個體</returns>
    public TextParagraphBuilder AddChart(OdfChartDefinition chart, OdfLength width, OdfLength height)
    {
        _document.AddChart(_paragraph, chart, width, height);
        return this;
    }
}

/// <summary>
/// Provides APIs for text table builder.
/// 提供文字表格的 Fluent 建立 API。
/// </summary>
public sealed class TextTableBuilder
{
    private readonly OdfTable _table;

    internal TextTableBuilder(OdfTable table)
    {
        _table = table ?? throw new ArgumentNullException(nameof(table));
    }

    /// <summary>
    /// Sets with summary.
    /// 設定表格摘要。
    /// </summary>
    /// <param name="summary">The value to use. / 摘要文字</param>
    /// <returns>The result. / 目前 builder 執行個體</returns>
    public TextTableBuilder WithSummary(string summary)
    {
        _table.Summary = summary;
        return this;
    }

    /// <summary>
    /// Sets set cell.
    /// 設定儲存格文字。
    /// </summary>
    /// <param name="row">The numeric value. / 列索引，採 1 為基準</param>
    /// <param name="column">The numeric value. / 欄索引，採 1 為基準</param>
    /// <param name="text">The text or value. / 儲存格文字</param>
    /// <returns>The result. / 目前 builder 執行個體</returns>
    public TextTableBuilder SetCell(int row, int column, string text)
    {
        EnsureOneBasedIndex(row, nameof(row));
        EnsureOneBasedIndex(column, nameof(column));
        OdfTableCell cell = _table.GetCell(row - 1, column - 1);
        cell.AddParagraph(text);
        return this;
    }

    /// <summary>
    /// Provides merge cells.
    /// 合併儲存格。
    /// </summary>
    /// <param name="startRow">The numeric value. / 起始列，採 1 為基準</param>
    /// <param name="startColumn">The numeric value. / 起始欄，採 1 為基準</param>
    /// <param name="rowSpan">The numeric value. / 跨列數</param>
    /// <param name="columnSpan">The numeric value. / 跨欄數</param>
    /// <returns>The result. / 目前 builder 執行個體</returns>
    public TextTableBuilder MergeCells(int startRow, int startColumn, int rowSpan, int columnSpan)
    {
        EnsureOneBasedIndex(startRow, nameof(startRow));
        EnsureOneBasedIndex(startColumn, nameof(startColumn));
        _table.MergeCells(startRow - 1, startColumn - 1, rowSpan, columnSpan);
        return this;
    }

    private static void EnsureOneBasedIndex(int value, string parameterName)
    {
        if (value < 1)
        {
            throw new ArgumentOutOfRangeException(parameterName, OdfLocalizer.GetMessage("Err_SpreadsheetDocumentBuilder_IndexGreaterEqual1"));
        }
    }
}

/// <summary>
/// Provides APIs for text section builder.
/// 提供文字區段的 Fluent 建立 API。
/// </summary>
public sealed class TextSectionBuilder
{
    private readonly TextDocument _document;
    private readonly OdfSection _section;

    internal TextSectionBuilder(TextDocument document, OdfSection section)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _section = section ?? throw new ArgumentNullException(nameof(section));
    }

    /// <summary>
    /// Adds add paragraph.
    /// 新增區段內段落。
    /// </summary>
    /// <param name="text">The text or value. / 段落文字</param>
    /// <returns>The result. / 目前 builder 執行個體</returns>
    public TextSectionBuilder AddParagraph(string text)
    {
        var paragraphNode = OdfNodeFactory.CreateElement("p", OdfNamespaces.Text, "text");
        paragraphNode.TextContent = text;
        _section.Node.AppendChild(paragraphNode);
        return this;
    }

    /// <summary>
    /// Adds add paragraph.
    /// 新增由多個文字片段組成的區段內段落。
    /// </summary>
    /// <param name="configure">The delegate to invoke. / 段落內容設定委派</param>
    /// <returns>The result. / 目前 builder 執行個體</returns>
    public TextSectionBuilder AddParagraph(Action<TextParagraphBuilder> configure)
    {
        if (configure is null)
            throw new ArgumentNullException(nameof(configure));

        var paragraphNode = OdfNodeFactory.CreateElement("p", OdfNamespaces.Text, "text");
        _section.Node.AppendChild(paragraphNode);
        configure(new TextParagraphBuilder(_document, new OdfParagraph(paragraphNode, _document)));
        return this;
    }

    /// <summary>
    /// Sets protected.
    /// 設定區段是否唯讀。
    /// </summary>
    /// <param name="isProtected">The value to use. / 是否唯讀</param>
    /// <returns>The result. / 目前 builder 執行個體</returns>
    public TextSectionBuilder Protected(bool isProtected = true)
    {
        _section.IsProtected = isProtected;
        return this;
    }
}

/// <summary>
/// Provides APIs for text page setup builder.
/// 提供頁面設定的 Fluent 建立 API。
/// </summary>
public sealed class TextPageSetupBuilder
{
    private readonly OdfPageSetup _setup;

    internal TextPageSetupBuilder(OdfPageSetup setup)
    {
        _setup = setup ?? throw new ArgumentNullException(nameof(setup));
    }

    /// <summary>
    /// Sets header.
    /// 設定頁首文字。
    /// </summary>
    /// <param name="text">The text or value. / 頁首文字</param>
    /// <returns>The result. / 目前 builder 執行個體</returns>
    public TextPageSetupBuilder Header(string text)
    {
        _setup.Header.Text = text;
        return this;
    }

    /// <summary>
    /// Sets footer.
    /// 設定頁尾文字。
    /// </summary>
    /// <param name="text">The text or value. / 頁尾文字</param>
    /// <returns>The result. / 目前 builder 執行個體</returns>
    public TextPageSetupBuilder Footer(string text)
    {
        _setup.Footer.Text = text;
        return this;
    }

    /// <summary>
    /// Sets first page header.
    /// 設定首頁專用頁首文字。
    /// </summary>
    /// <param name="text">The text or value. / 首頁頁首文字</param>
    /// <returns>The result. / 目前 builder 執行個體</returns>
    public TextPageSetupBuilder FirstPageHeader(string text)
    {
        _setup.HeaderFirst.Text = text;
        return this;
    }

    /// <summary>
    /// Sets first page footer.
    /// 設定首頁專用頁尾文字。
    /// </summary>
    /// <param name="text">The text or value. / 首頁頁尾文字</param>
    /// <returns>The result. / 目前 builder 執行個體</returns>
    public TextPageSetupBuilder FirstPageFooter(string text)
    {
        _setup.FooterFirst.Text = text;
        return this;
    }

    /// <summary>
    /// Provides footer page numbers.
    /// 在頁尾新增頁碼與總頁數欄位。
    /// </summary>
    /// <returns>The result. / 目前 builder 執行個體</returns>
    public TextPageSetupBuilder FooterPageNumbers()
    {
        OdfParagraph paragraph = _setup.Footer.GetOrCreateParagraph();
        paragraph.AddTextRun("第 ");
        _setup.Footer.AddPageNumberField();
        paragraph.AddTextRun(" 頁，共 ");
        _setup.Footer.AddPageCountField();
        paragraph.AddTextRun(" 頁");
        return this;
    }
}

/// <summary>
/// Provides APIs for text run formatting builder.
/// 提供文字片段格式的 Fluent 設定 API。
/// </summary>
public sealed class TextRunFormattingBuilder
{
    internal bool? BoldValue { get; private set; }
    internal bool? ItalicValue { get; private set; }
    internal bool? UnderlineValue { get; private set; }
    internal double? FontSizeValue { get; private set; }
    internal string? ColorValue { get; private set; }
    internal string? BackgroundColorValue { get; private set; }

    /// <summary>
    /// Sets bold.
    /// 設定文字為粗體。
    /// </summary>
    /// <returns>The result. / 目前 builder 執行個體</returns>
    public TextRunFormattingBuilder Bold()
    {
        BoldValue = true;
        return this;
    }

    /// <summary>
    /// Sets italic.
    /// 設定文字為斜體。
    /// </summary>
    /// <returns>The result. / 目前 builder 執行個體</returns>
    public TextRunFormattingBuilder Italic()
    {
        ItalicValue = true;
        return this;
    }

    /// <summary>
    /// Sets underline.
    /// 設定文字底線。
    /// </summary>
    /// <returns>The result. / 目前 builder 執行個體</returns>
    public TextRunFormattingBuilder Underline()
    {
        UnderlineValue = true;
        return this;
    }

    /// <summary>
    /// Sets font size.
    /// 設定文字大小。
    /// </summary>
    /// <param name="points">The value to use. / 點數大小</param>
    /// <returns>The result. / 目前 builder 執行個體</returns>
    public TextRunFormattingBuilder FontSize(double points)
    {
        FontSizeValue = points;
        return this;
    }

    /// <summary>
    /// Sets color.
    /// 設定文字色彩。
    /// </summary>
    /// <param name="color">The value to use. / 色彩值，格式為 <c>#RRGGBB</c></param>
    /// <returns>The result. / 目前 builder 執行個體</returns>
    public TextRunFormattingBuilder Color(string color)
    {
        if (!OdfColor.TryParse(color, out _))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_TextDocumentBuilder_ColorValuesRrggbbFormat"), nameof(color));
        }

        ColorValue = color;
        return this;
    }

    /// <summary>
    /// Sets background color.
    /// 設定文字背景色。
    /// </summary>
    /// <param name="color">The value to use. / 色彩值，格式為 <c>#RRGGBB</c></param>
    /// <returns>The result. / 目前 builder 執行個體</returns>
    public TextRunFormattingBuilder BackgroundColor(string color)
    {
        if (!OdfColor.TryParse(color, out _))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_TextDocumentBuilder_ColorValuesRrggbbFormat"), nameof(color));
        }

        BackgroundColorValue = color;
        return this;
    }
}

/// <summary>
/// Provides APIs for text list builder.
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
    /// Adds item.
    /// 新增清單專案。
    /// </summary>
    /// <param name="text">The text or value. / 專案文字</param>
    /// <returns>The result. / 目前 builder 執行個體</returns>
    public TextListBuilder Item(string text)
    {
        _list.AddItem(text);
        return this;
    }
}
