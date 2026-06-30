using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Forms;
using OdfKit.Styles;

namespace OdfKit.Text;

/// <summary>
/// Represents an ODF text document.
/// 表示 ODF 文字文件。
/// </summary>
public partial class TextDocument : OdfDocument
{
    private OdfTextBody? _body;
    private OdfDocumentMetadata? _metadata;
    private int _footnoteCounter;
    private int _endnoteCounter;

    /// <summary>
    /// Gets or sets the body root node of the text document.
    /// 取得或設定文字文件的本文根節點。
    /// </summary>
    public OdfNode BodyTextRoot { get; private set; } = null!;

    /// <summary>
    /// Initializes a new instance of the <see cref="TextDocument"/> class.
    /// 初始化 <see cref="TextDocument"/> 類別的新執行個體。
    /// </summary>
    /// <param name="package">The OdfPackage instance. / OdfPackage 封裝包執行個體。</param>
    public TextDocument(OdfPackage package) : base(package)
    {
        if (string.IsNullOrEmpty(package.MimeType))
        {
            package.SetMimeType("application/vnd.oasis.opendocument.text");
        }
        InitializeTextRoot();
        StyleEngine.OnStyleChanging = TrackFormatChange;
    }

    /// <summary>
    /// Creates a new ODT text document.
    /// 建立新的 ODT 文字文件。
    /// </summary>
    /// <returns>A new <see cref="TextDocument"/> instance. / 新的 <see cref="TextDocument"/> 執行個體。</returns>
    public static TextDocument Create()
    {
        return (TextDocument)OdfDocumentFactory.CreateDocument(OdfDocumentKind.Text);
    }

    /// <summary>
    /// Creates a new text document from the specified text template.
    /// 從指定的文字範本文件建立新的文字文件。
    /// </summary>
    /// <param name="template">The text template document. / 文字範本文件。</param>
    /// <param name="clearUserContent">Whether to clear user content such as paragraphs in the template while keeping styles and master pages. / 是否清除範本中的段落等使用者內容，但保留樣式與母片頁面。</param>
    /// <returns>The created <see cref="TextDocument"/> instance. / 建立完成的 <see cref="TextDocument"/> 執行個體。</returns>
    public static TextDocument CreateFromTemplate(TextTemplateDocument template, bool clearUserContent = false)
    {
        return (TextDocument)CreateFromTemplateInternal(template, OdfDocumentKind.Text, "application/vnd.oasis.opendocument.text", clearUserContent);
    }

    /// <inheritdoc/>
    protected override void ClearTemplateUserContent()
    {
        foreach (OdfNode child in new List<OdfNode>(BodyTextRoot.Children))
        {
            BodyTextRoot.RemoveChild(child);
        }
    }

    /// <summary>
    /// Creates an equivalent ODT (ZIP package) text document from a FODT flat XML text document, with identical content.
    /// 從 FODT 扁平 XML 文字文件建立等價的 ODT（ZIP 封裝）文字文件，內容完全相同。
    /// </summary>
    /// <param name="document">The source FODT flat XML text document. / 來源 FODT 扁平 XML 文字文件。</param>
    /// <returns>The created <see cref="TextDocument"/> instance. / 建立完成的 <see cref="TextDocument"/> 執行個體。</returns>
    public static TextDocument CreateFromFlatDocument(FlatTextDocument document) =>
        (TextDocument)ConvertFlatVariantInternal(document, OdfDocumentKind.Text, targetIsFlatXml: false);

    /// <summary>
    /// Creates an equivalent ODT text document from an OTH web template document, with identical content.
    /// 從 OTH 網頁範本文件建立等價的 ODT 文字文件，內容完全相同。
    /// </summary>
    /// <param name="document">The source OTH web template document. / 來源 OTH 網頁範本文件。</param>
    /// <returns>The created <see cref="TextDocument"/> instance. / 建立完成的 <see cref="TextDocument"/> 執行個體。</returns>
    public static TextDocument CreateFromWebDocument(TextWebDocument document) =>
        (TextDocument)CreateFromTemplateInternal(document, OdfDocumentKind.Text, "application/vnd.oasis.opendocument.text", clearUserContent: false);

    /// <summary>
    /// Creates a new ODT text document fluent builder.
    /// 建立新的 ODT 文字文件 Fluent builder。
    /// </summary>
    /// <returns>A new <see cref="TextDocumentBuilder"/> instance. / 新的 <see cref="TextDocumentBuilder"/> 執行個體。</returns>
    public static TextDocumentBuilder Builder()
    {
        return new TextDocumentBuilder(Create());
    }

    /// <summary>
    /// Loads an ODT text document from the specified path.
    /// 從指定路徑載入 ODT 文字文件。
    /// </summary>
    /// <param name="path">The ODT document path. / ODT 文件路徑。</param>
    /// <returns>The loaded <see cref="TextDocument"/> instance. / 載入完成的 <see cref="TextDocument"/> 執行個體。</returns>
    /// <exception cref="InvalidOperationException">When the specified document is not an ODT text document. / 當指定文件不是 ODT 文字文件時擲出。</exception>
    public new static TextDocument Load(string path) =>
        OdfDocumentVariantSupport.Load<TextDocument>(path, OdfDocumentKind.Text, "Err_TextDocument_SpecifiedOdfFileOdt");

    /// <summary>
    /// Asynchronously loads an ODT text document from the specified path.
    /// 非同步從指定路徑載入 ODT 文字文件。
    /// </summary>
    /// <param name="path">The ODT document path. / ODT 文件路徑。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消語彙基元。</param>
    /// <returns>A task representing the asynchronous load operation, whose result is the loaded <see cref="TextDocument"/>. / 代表非同步載入作業的工作，其結果為載入完成的 <see cref="TextDocument"/>。</returns>
    /// <remarks>
    /// 若 <paramref name="cancellationToken"/> 已請求取消，作業會立即以 <see cref="OperationCanceledException"/> 結束；
    /// 否則會在 ZIP 解壓與封裝初始化期間協作檢查取消語彙。
    /// </remarks>
    public new static Task<TextDocument> LoadAsync(string path, CancellationToken cancellationToken = default) =>
        OdfDocumentVariantSupport.LoadAsync<TextDocument>(path, OdfDocumentKind.Text, "Err_TextDocument_SpecifiedOdfFileOdt", cancellationToken);

    /// <summary>
    /// Loads an ODT text document from the specified stream.
    /// 從指定資料流載入 ODT 文字文件。
    /// </summary>
    /// <param name="stream">The stream containing the ODT document content. / 包含 ODT 文件內容的資料流。</param>
    /// <param name="fileName">The optional file name, used to assist format detection. / 選用的檔案名稱，用於輔助格式偵測。</param>
    /// <returns>The loaded <see cref="TextDocument"/> instance. / 載入完成的 <see cref="TextDocument"/> 執行個體。</returns>
    /// <exception cref="InvalidOperationException">When the specified document is not an ODT text document. / 當指定文件不是 ODT 文字文件時擲出。</exception>
    public new static TextDocument Load(Stream stream, string? fileName = null) =>
        OdfDocumentVariantSupport.Load<TextDocument>(stream, OdfDocumentKind.Text, "Err_TextDocument_SpecifiedOdfFileOdt", fileName);

    /// <summary>
    /// Asynchronously loads an ODT text document from the specified stream.
    /// 非同步從指定資料流載入 ODT 文字文件。
    /// </summary>
    /// <param name="stream">The stream containing the ODT document content. / 包含 ODT 文件內容的資料流。</param>
    /// <param name="fileName">The optional file name, used to assist format detection. / 選用的檔案名稱，用於輔助格式偵測。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消語彙基元。</param>
    /// <returns>A task representing the asynchronous load operation, whose result is the loaded <see cref="TextDocument"/>. / 代表非同步載入作業的工作，其結果為載入完成的 <see cref="TextDocument"/>。</returns>
    public new static Task<TextDocument> LoadAsync(Stream stream, string? fileName = null, CancellationToken cancellationToken = default) =>
        OdfDocumentVariantSupport.LoadAsync<TextDocument>(stream, OdfDocumentKind.Text, "Err_TextDocument_SpecifiedOdfFileOdt", fileName, cancellationToken);

    /// <summary>
    /// Gets the high-level operation entry point for the text document body.
    /// 取得文字文件本文的高階操作入口。
    /// </summary>
    public OdfTextBody Body => _body ??= new OdfTextBody(this);

    /// <summary>
    /// Gets the high-level operation entry point for document metadata.
    /// 取得文件中繼資料的高階操作入口。
    /// </summary>
    public OdfDocumentMetadata Metadata => _metadata ??= new OdfDocumentMetadata(this);

    private void InitializeTextRoot()
    {
        var body = FindOrCreateChild(ContentDom, "body", OdfNamespaces.Office, "office");
        BodyTextRoot = FindOrCreateChild(body, "text", OdfNamespaces.Office, "office");
    }

    /// <summary>
    /// Gets the default content XML string.
    /// 取得預設的內容 XML 字串。
    /// </summary>
    /// <returns>The content XML string. / 內容 XML 字串。</returns>
    protected override string GetDefaultContentXml()
    {
        return "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:style=\"urn:oasis:names:tc:opendocument:xmlns:style:1.0\" xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\" xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\" xmlns:fo=\"urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:meta=\"urn:oasis:names:tc:opendocument:xmlns:meta:1.0\" xmlns:number=\"urn:oasis:names:tc:opendocument:xmlns:datastyle:1.0\" xmlns:presentation=\"urn:oasis:names:tc:opendocument:xmlns:presentation:1.0\" xmlns:svg=\"urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0\" xmlns:chart=\"urn:oasis:names:tc:opendocument:xmlns:chart:1.0\" xmlns:config=\"urn:oasis:names:tc:opendocument:xmlns:config:1.0\" xmlns:form=\"urn:oasis:names:tc:opendocument:xmlns:form:1.0\" office:version=\"" + OdfVersionInfo.DefaultVersionString + "\"><office:body><office:text></office:text></office:body></office:document-content>";
    }

    /// <summary>
    /// Gets the default styles XML string.
    /// 取得預設的樣式 XML 字串。
    /// </summary>
    /// <returns>The styles XML string. / 樣式 XML 字串。</returns>
    protected override string GetDefaultStylesXml()
    {
        return "<office:document-styles xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:style=\"urn:oasis:names:tc:opendocument:xmlns:style:1.0\" xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\" xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\" xmlns:fo=\"urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:meta=\"urn:oasis:names:tc:opendocument:xmlns:meta:1.0\" xmlns:number=\"urn:oasis:names:tc:opendocument:xmlns:datastyle:1.0\" xmlns:presentation=\"urn:oasis:names:tc:opendocument:xmlns:presentation:1.0\" xmlns:svg=\"urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0\" xmlns:chart=\"urn:oasis:names:tc:opendocument:xmlns:chart:1.0\" xmlns:config=\"urn:oasis:names:tc:opendocument:xmlns:config:1.0\" office:version=\"" + OdfVersionInfo.DefaultVersionString + "\"><office:styles></office:styles><office:automatic-styles></office:automatic-styles><office:master-styles><style:master-page style:name=\"Standard\" style:page-layout-name=\"Mpm1\"/></office:master-styles></office:document-styles>";
    }


    #region Table covered cells omissions


    /// <summary>
    /// Appends a table item to the end of the document body.
    /// 新增一個表格專案至文件本文結尾。
    /// </summary>
    /// <param name="rows">The number of table rows. / 表格的列數。</param>
    /// <param name="cols">The number of table columns. / 表格的欄數。</param>
    /// <returns>The newly created table object. / 新建立的表格物件。</returns>
    public OdfTable AddTable(int rows, int cols)
    {
        var table = new OdfNode(OdfNodeType.Element, "table", OdfNamespaces.Table, "table");
        table.SetAttribute("name", OdfNamespaces.Table, CreateTableName(), "table");
        BodyTextRoot.AppendChild(table);
        return new OdfTable(table, rows, cols, this);
    }

    private string CreateTableName()
    {
        int tableCount = 0;
        foreach (OdfNode child in BodyTextRoot.Children)
        {
            if (child.LocalName == "table" && child.NamespaceUri == OdfNamespaces.Table)
            {
                tableCount++;
            }
        }

        return $"Table{(tableCount + 1).ToString(CultureInfo.InvariantCulture)}";
    }

    /// <summary>
    /// Appends a list builder to the end of the text document body.
    /// 在文字文件本文的結尾，追加一個清單建構器。
    /// </summary>
    /// <param name="styleName">The optional list style name. / 選用的清單樣式名稱。</param>
    /// <returns>The list builder. / 清單建構器。</returns>
    public OdfListBuilder AppendList(string? styleName = null)
        => new OdfListBuilder(BodyTextRoot, this, null, styleName);

    #endregion


    #region CJK Font Fallback


    /// <summary>
    /// Configures CJK (Chinese/Japanese/Korean) font fallback settings.
    /// 套用中日韓（CJK）字型遞補設定。
    /// </summary>
    public void ApplyCjkFontFallback()
        => TextDocumentCjkFontEngine.ApplyFontFallback(CoreCollaborators);


    #endregion


    #region Dynamic Page / Field Indicators


    /// <summary>
    /// 在指定的段落中新增頁碼欄位。
    /// </summary>
    /// <param name="paragraph">目標段落</param>
    internal void AddPageNumberField(OdfParagraph paragraph)
        => TextDocumentPageFieldsEngine.AddPageNumberField(paragraph);

    /// <summary>
    /// 在指定的段落中新增總頁數欄位。
    /// </summary>
    /// <param name="paragraph">目標段落</param>
    internal void AddPageCountField(OdfParagraph paragraph)
        => TextDocumentPageFieldsEngine.AddPageCountField(paragraph);


    #endregion


    #region Document Merging Logic Override


    /// <summary>
    /// Merges the content nodes of a source document into the current document.
    /// 合併來源文件與目前文件的內容節點。
    /// </summary>
    /// <param name="sourceDoc">The source OdfDocument. / 來源 OdfDocument 文件。</param>
    /// <param name="options">The merge options. / 合併設定選項。</param>
    /// <param name="renameMap">The dictionary mapping renamed style names. / 變更樣式名稱的對照字典。</param>
    protected override void MergeContentNodes(OdfDocument sourceDoc, OdfMergeOptions options, Dictionary<string, string> renameMap)
        => TextDocumentContentMergeEngine.MergeContentNodes(CoreCollaborators, sourceDoc, renameMap);


    #endregion


    #region TOC (Table of Contents)


    /// <summary>
    /// Adds a table of contents item to the end of the document body.
    /// 新增目錄專案至文件本文結尾。
    /// </summary>
    /// <param name="title">The table of contents title. / 目錄標題。</param>
    /// <param name="outlineLevel">The maximum outline level included in the table of contents. / 目錄的大綱階層上限。</param>
    /// <returns>The newly created table of contents object. / 新建立的目錄物件。</returns>
    public OdfTableOfContents AddTableOfContents(string title = "Table of Contents", int outlineLevel = 10)
        => TextDocumentTocEngine.AddTableOfContents(this, CoreCollaborators, title, outlineLevel);

    /// <summary>
    /// Inserts a table of contents at the end of the document and immediately updates it to auto-generate heading hyperlinks and outline content.
    /// 於文件結尾插入目錄，並立即更新以自動生成標題超連結與大綱內容。
    /// </summary>
    /// <param name="title">The table of contents title. / 目錄標題。</param>
    /// <param name="outlineLevel">The maximum outline level included in the table of contents. / 目錄的大綱階層上限。</param>
    /// <returns>The created and updated table of contents object. / 已建立且更新完成的目錄物件。</returns>
    public OdfTableOfContents InsertTableOfContents(string title = "Table of Contents", int outlineLevel = 10)
    {
        var toc = AddTableOfContents(title, outlineLevel);
        toc.Update();
        return toc;
    }

    /// <summary>
    /// Shifts the outline level of all headings in the document (including headings nested within sections) by the specified amount.
    /// 將文件中所有標題（包含區段內巢狀標題）的大綱階層整體位移指定量。
    /// </summary>
    /// <param name="offset">The shift amount; a positive value demotes (increases the number), a negative value promotes. The result is clamped to a minimum of 1. / 位移量；正值表示降階（數字變大），負值表示升階。結果會限制最小為 1。</param>
    /// <remarks>
    /// 適用於將子文件併入主控文件前，調整其標題大綱階層以接續主控文件本身的階層結構
    /// （例如主控文件已有第 1 階層的「上篇」標題，子文件章節標題需位移為第 2 階層才能正確巢狀）。
    /// </remarks>
    public void ShiftHeadingOutlineLevels(int offset)
    {
        if (offset == 0)
        {
            return;
        }

        ShiftHeadingOutlineLevelsRecursive(BodyTextRoot, offset);
    }

    private static void ShiftHeadingOutlineLevelsRecursive(OdfNode node, int offset)
    {
        foreach (OdfNode child in node.Children)
        {
            if (child.NodeType is OdfNodeType.Element &&
                child.LocalName == "h" &&
                child.NamespaceUri == OdfNamespaces.Text)
            {
                int current = int.TryParse(child.GetAttribute("outline-level", OdfNamespaces.Text), NumberStyles.Integer, CultureInfo.InvariantCulture, out int level)
                    ? level
                    : 1;
                int updated = Math.Max(1, current + offset);
                child.SetAttribute("outline-level", OdfNamespaces.Text, updated.ToString(CultureInfo.InvariantCulture), "text");
            }

            ShiftHeadingOutlineLevelsRecursive(child, offset);
        }
    }


    #endregion


    #region Mathematical Formulas (MathML)


    /// <summary>
    /// 在指定的段落中新增數學公式。
    /// </summary>
    /// <param name="paragraph">要插入公式的段落</param>
    /// <param name="mathMlXmlString">MathML 結構的 XML 字串內容</param>
    internal void AddFormula(OdfParagraph paragraph, string mathMlXmlString)
        => TextDocumentFormulaEngine.AddFormula(CoreCollaborators, paragraph, mathMlXmlString);


    #endregion


    #region Page Setup & Mirrored Layouts


    /// <summary>
    /// Gets the default page setup.
    /// 取得預設的頁面設定。
    /// </summary>
    /// <returns>The default page setup object. / 預設頁面設定物件。</returns>
    public OdfPageSetup GetDefaultPageSetup()
    {
        return new OdfPageSetup(this);
    }

    /// <summary>
    /// Gets an editable page setup by master page style name.
    /// 依主頁面樣式名稱取得可編輯的頁面設定。
    /// </summary>
    /// <param name="masterPageName">The master page style name (e.g. <c>Standard</c> or <c>Landscape</c>). / 主頁面樣式名稱（例如 <c>Standard</c> 或 <c>Landscape</c>）。</param>
    /// <returns>The corresponding page setup object. / 對應的頁面設定物件。</returns>
    /// <exception cref="ArgumentException">When no master page style with the specified name is found. / 找不到指定名稱的主頁面樣式時擲出。</exception>
    public OdfPageSetup GetPageSetup(string masterPageName)
    {
        if (string.IsNullOrWhiteSpace(masterPageName))
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_TextDocument_MainCannotBeEmpty"), nameof(masterPageName));

        string? layoutName = ResolveMasterPageLayoutName(masterPageName);
        if (layoutName is null)
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_TextDocument_MainPageStyleCannot", masterPageName), nameof(masterPageName));

        return new OdfPageSetup(this, masterPageName, layoutName);
    }

    private string? ResolveMasterPageLayoutName(string masterPageName)
    {
        OdfNode masterStyles = FindOrCreateChild(StylesDom, "master-styles", OdfNamespaces.Office, "office");
        foreach (OdfNode child in masterStyles.Children)
        {
            if (child.LocalName != "master-page" || child.NamespaceUri != OdfNamespaces.Style)
                continue;

            string? name = child.GetAttribute("name", OdfNamespaces.Style);
            if (!string.Equals(name, masterPageName, StringComparison.Ordinal))
                continue;

            return child.GetAttribute("page-layout-name", OdfNamespaces.Style);
        }

        return null;
    }

    /// <summary>
    /// Adds a named page style (master-page + page-layout), optionally configuring its settings.
    /// 新增一個具名頁面樣式（master-page + page-layout），並可選擇性地配置其設定。
    /// </summary>
    /// <param name="name">The master page style name (e.g. "Landscape"). / 主頁面樣式名稱（例如 "Landscape"）。</param>
    /// <param name="configure">The optional page setup configuration callback. / 可選的頁面設定回呼。</param>
    public OdfPageStyle AddPageStyle(string name, Action<OdfPageSetup>? configure = null)
    {
        string layoutName = $"MPL_{name}";
        var setup = new OdfPageSetup(this, name, layoutName);
        setup.EnsureNodes();
        configure?.Invoke(setup);
        return new OdfPageStyle(name);
    }

    /// <summary>
    /// Gets a summary list of headers and footers for all master page styles.
    /// 取得所有主頁面樣式的頁首頁尾摘要清單。
    /// </summary>
    public IReadOnlyList<OdfPageSetupInfo> GetPageSetups() =>
        TextDocumentPageSetupReadEngine.GetPageSetups(StylesDom);

    /// <summary>
    /// Gets the list of all defined master page style names.
    /// 取得所有已定義的主頁面樣式名稱清單。
    /// </summary>
    public IReadOnlyList<string> GetPageStyleNames()
    {
        var masterStyles = FindOrCreateChild(StylesDom, "master-styles", OdfNamespaces.Office, "office");
        var names = new List<string>();
        foreach (var child in masterStyles.Children)
        {
            if (child.LocalName == "master-page" && child.NamespaceUri == OdfNamespaces.Style)
            {
                string? n = child.GetAttribute("name", OdfNamespaces.Style);
                if (!string.IsNullOrEmpty(n))
                    names.Add(n!);
            }
        }
        return names;
    }


    #endregion


    #region Multi-Column Sections Layouts


    /// <summary>
    /// Adds a multi-column layout section to the end of the document body.
    /// 新增多欄版面配置區段至文件本文結尾。
    /// </summary>
    /// <param name="name">The section name. / 區段名稱。</param>
    /// <param name="columnCount">The number of columns. / 欄位數量。</param>
    /// <param name="gap">The column gap width. / 欄間距寬度。</param>
    /// <returns>The newly created section object. / 新建立的區段物件。</returns>
    public OdfSection AddSection(string name, int columnCount, OdfLength gap)
    {
        var section = new OdfNode(OdfNodeType.Element, "section", OdfNamespaces.Text, "text");
        section.SetAttribute("name", OdfNamespaces.Text, name, "text");

        OdfNode styleNode = StyleEngine.GetOrCreateLocalStyle(section, "section");
        OdfNode sectionProperties = TextDocumentDomHelper.FindOrCreateChild(
            styleNode,
            "section-properties",
            OdfNamespaces.Style,
            "style");
        OdfNode columns = TextDocumentDomHelper.FindOrCreateChild(
            sectionProperties,
            "columns",
            OdfNamespaces.Style,
            "style");
        columns.SetAttribute("column-count", OdfNamespaces.Fo, columnCount.ToString(CultureInfo.InvariantCulture), "fo");
        columns.SetAttribute("column-gap", OdfNamespaces.Fo, gap.ToString(), "fo");

        BodyTextRoot.AppendChild(section);
        return new OdfSection(section, this);
    }

    /// <summary>
    /// Adds a section referencing an external sub-document at the end of the document body (used for master documents).
    /// 在文件本文結尾新增一個指向外部子文件參照的區段（用於主文件）。
    /// </summary>
    /// <param name="name">The section name. / 區段名稱。</param>
    /// <param name="subDocumentUri">The relative or absolute URI/path of the external sub-document (written to xlink:href). / 外部子文件的相對或絕對 URI/路徑（將寫入 xlink:href）。</param>
    /// <param name="loadOnRequest">
    /// Whether to defer loading of the sub-document content (written as <c>xlink:actuate="onRequest"</c>); defaults to <see langword="false"/>,
    /// i.e. loaded immediately when the master document is opened (<c>xlink:actuate="onLoad"</c>). / 是否延遲載入子文件內容（寫入 <c>xlink:actuate="onRequest"</c>）；預設為 <see langword="false"/>，
    /// 即開啟主控文件時立即載入（<c>xlink:actuate="onLoad"</c>）。
    /// </param>
    /// <returns>The newly created section object. / 新建立的區段物件。</returns>
    public OdfSection AddSubDocumentReference(string name, string subDocumentUri, bool loadOnRequest = false)
    {
        if (string.IsNullOrEmpty(name))
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_TextDocument_SectionCannotBeEmpty"), nameof(name));
        if (string.IsNullOrEmpty(subDocumentUri))
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_TextDocument_SubfileCannotBeEmpty"), nameof(subDocumentUri));

        var sectionNode = new OdfNode(OdfNodeType.Element, "section", OdfNamespaces.Text, "text");
        sectionNode.SetAttribute("name", OdfNamespaces.Text, name, "text");

        var sourceNode = new OdfNode(OdfNodeType.Element, "section-source", OdfNamespaces.Text, "text");
        sourceNode.SetAttribute("type", OdfNamespaces.XLink, "simple", "xlink");
        sourceNode.SetAttribute("href", OdfNamespaces.XLink, subDocumentUri, "xlink");
        sourceNode.SetAttribute("show", OdfNamespaces.XLink, "embed", "xlink");
        sourceNode.SetAttribute("actuate", OdfNamespaces.XLink, loadOnRequest ? "onRequest" : "onLoad", "xlink");

        sectionNode.AppendChild(sourceNode);
        BodyTextRoot.AppendChild(sectionNode);

        return new OdfSection(sectionNode, this);
    }


    #endregion


    #region Comments / Annotations


    /// <summary>
    /// 在指定的段落中新增註解。
    /// </summary>
    /// <param name="paragraph">要新增註解的段落</param>
    /// <param name="comment">註解物件執行個體</param>
    internal void AddComment(OdfParagraph paragraph, OdfComment comment)
        => TextDocumentCommentsEngine.AddComment(paragraph, comment);

    /// <summary>
    /// Gets the list of all comments in the document.
    /// 取得文件中所有註解的列表。
    /// </summary>
    /// <returns>The list of comment objects. / 註解物件列表。</returns>
    public List<OdfComment> GetComments()
        => TextDocumentCommentsEngine.GetComments(BodyTextRoot);

    /// <summary>
    /// Gets a summary list of all comments in the document.
    /// 取得文件中所有註解的摘要清單。
    /// </summary>
    public IReadOnlyList<OdfCommentInfo> GetCommentInfos() =>
        TextDocumentCommentReadEngine.GetCommentInfos(BodyTextRoot);


    #endregion


    #region XML Helper


    /// <summary>
    /// Adds a font declaration item to the document.
    /// 在文件中新增字型宣告專案。
    /// </summary>
    /// <param name="name">The font code or alias. / 字型代碼或別名。</param>
    /// <param name="fontFamily">The actual font family name. / 實際的字型名稱。</param>
    /// <param name="genericFamily">The generic font family. / 泛用字型系列。</param>
    /// <param name="pitch">The pitch mode. / 字距模式。</param>
    public void AddFontFace(string name, string fontFamily, string? genericFamily = null, string? pitch = null)
        => TextDocumentFontFaceEngine.AddFontFace(CoreCollaborators, name, fontFamily, genericFamily, pitch);


    #endregion


    #region MailMerge Implementation


    /// <summary>
    /// Performs a mail merge using a strongly typed data source object, whose property names correspond to the merge field names in the document.
    /// 以強型別資料來源物件執行郵件合併，屬性名稱對應文件中的合併欄位名稱。
    /// </summary>
    /// <typeparam name="T">The data source type. / 資料來源型別。</typeparam>
    /// <param name="dataSource">The merge data source object. / 合併資料來源物件。</param>
    public void MailMerge<T>(T dataSource) where T : notnull
    {
        var engine = new OdfMailMergeEngine(this);
        engine.Execute(BodyTextRoot, dataSource);
    }

    /// <summary>
    /// Performs a mail merge using a dictionary data source, whose keys correspond to the merge field names in the document.
    /// 以字典資料來源執行郵件合併，Key 對應文件中的合併欄位名稱。
    /// </summary>
    /// <param name="dataSource">The data dictionary keyed by field name. / 以欄位名稱為 Key 的資料字典。</param>
    public void MailMerge(IReadOnlyDictionary<string, object?> dataSource)
    {
        var engine = new OdfMailMergeEngine(this);
        engine.Execute(BodyTextRoot, dataSource);
    }

    /// <summary>
    /// Performs a batch mail merge using a collection of strongly typed records, producing an independent document copy for each record.
    /// 以強型別記錄集合執行批次郵件合併，每筆記錄產生獨立的文件副本。
    /// </summary>
    /// <typeparam name="T">The record type; property names correspond to the merge field names in the document. / 記錄型別；屬性名稱對應文件中的合併欄位名稱。</typeparam>
    /// <param name="records">The collection of data records. / 資料記錄集合。</param>
    /// <returns>A merged <see cref="TextDocument"/> for each record; the caller is responsible for disposing them. / 每筆記錄對應一個已合併的 <see cref="TextDocument"/>；呼叫端負責 Dispose。</returns>
    public IReadOnlyList<TextDocument> MailMerge<T>(IEnumerable<T> records) where T : notnull
        => TextDocumentMailMergeBatchEngine.MailMerge(this, records);

    /// <summary>
    /// Performs a batch mail merge using a collection of dictionary records, producing an independent document copy for each record.
    /// 以字典記錄集合執行批次郵件合併，每筆記錄產生獨立的文件副本。
    /// </summary>
    /// <param name="records">The collection of dictionary records, whose keys correspond to merge field names. / 字典記錄集合，Key 對應合併欄位名稱。</param>
    /// <returns>A merged <see cref="TextDocument"/> for each record; the caller is responsible for disposing them. / 每筆記錄對應一個已合併的 <see cref="TextDocument"/>；呼叫端負責 Dispose。</returns>
    public IReadOnlyList<TextDocument> MailMerge(IEnumerable<IReadOnlyDictionary<string, object?>> records)
        => TextDocumentMailMergeBatchEngine.MailMerge(this, records);

    /// <summary>
    /// Performs a streaming, low-memory (under 1MB) mail merge using the current text document as a template, writing the result to the target stream.
    /// 以流式、低記憶體佔用（小於 1MB）的方式，將目前文字文件作為範本進行郵件合併，並將結果輸出至目標串流。
    /// </summary>
    /// <param name="outputStream">The target output file stream. / 輸出目標檔案串流。</param>
    /// <param name="dataSource">The merge data dictionary, whose keys correspond to merge field names in the template. / 套印資料字典，Key 對應範本中的合併欄位名稱。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消語彙基元。</param>
    /// <returns>A task representing the asynchronous merge operation. / 代表非同步合併作業的工作。</returns>
    /// <remarks>
    /// 此方法會將目前文件儲存，並從其封裝包的底層資料流中以 SAX 流式讀寫重構輸出，不會載入完整 DOM 樹至記憶體中。
    /// </remarks>
    public async Task StreamingMailMergeAsync(
        Stream outputStream,
        IDictionary<string, object?> dataSource,
        CancellationToken cancellationToken = default)
    {
        if (outputStream is null)
        {
            throw new ArgumentNullException(nameof(outputStream));
        }

        if (dataSource is null)
        {
            throw new ArgumentNullException(nameof(dataSource));
        }

        // 確保目前文件的內容已序列化寫入封裝
        Save();

        // 建立暫存的記憶體串流以儲存當前範本文件封裝
        using var tempMemoryStream = new MemoryStream();
        Package.Save(tempMemoryStream);
        tempMemoryStream.Position = 0;

        // 呼叫流式郵件合併引擎執行套印
        await OdfStreamingMailMerge.ApplyTemplateAsync(tempMemoryStream, outputStream, dataSource, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Performs a streaming, low-memory (under 1MB) mail merge using the current text document as a template, writing the result to the target stream.
    /// 以流式、低記憶體佔用（小於 1MB）的方式，將目前文字文件作為範本進行郵件合併，並將結果輸出至目標串流。
    /// </summary>
    /// <param name="outputStream">The target output file stream. / 輸出目標檔案串流。</param>
    /// <param name="dataSource">The merge data dictionary, whose keys correspond to merge field names in the template. / 套印資料字典，Key 對應範本中的合併欄位名稱。</param>
    public void StreamingMailMerge(Stream outputStream, IDictionary<string, object?> dataSource)
    {
        StreamingMailMergeAsync(outputStream, dataSource).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Performs a streaming, low-memory (under 1MB) mail merge by loading a template text document from the specified path, writing the result to the target stream.
    /// 以流式、低記憶體佔用（小於 1MB）的方式，載入指定路徑的範本文字文件進行郵件合併，並將結果輸出至目標串流。
    /// </summary>
    /// <param name="templatePath">The template text document path. / 範本文字文件路徑。</param>
    /// <param name="outputStream">The target output file stream. / 輸出目標檔案串流。</param>
    /// <param name="dataSource">The merge data dictionary, whose keys correspond to merge field names in the template. / 套印資料字典，Key 對應範本中的合併欄位名稱。</param>
    public static void StreamingMailMerge(string templatePath, Stream outputStream, IDictionary<string, object?> dataSource)
    {
        if (string.IsNullOrEmpty(templatePath))
        {
            throw new ArgumentException(OdfLocalizer.GetMessage("Err_TextDocument_TemplatePathCannotBeNullOrEmpty"), nameof(templatePath));
        }

        if (outputStream is null)
        {
            throw new ArgumentNullException(nameof(outputStream));
        }

        if (dataSource is null)
        {
            throw new ArgumentNullException(nameof(dataSource));
        }

        using var templateStream = File.OpenRead(templatePath);
        OdfStreamingMailMerge.ApplyTemplateAsync(templateStream, outputStream, dataSource).GetAwaiter().GetResult();
    }


    #endregion

}
