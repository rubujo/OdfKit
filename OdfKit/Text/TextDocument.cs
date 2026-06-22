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
/// 表示 ODF 文字文件。
/// </summary>
public partial class TextDocument : OdfDocument
{
    private OdfTextBody? _body;
    private OdfDocumentMetadata? _metadata;
    private int _footnoteCounter;
    private int _endnoteCounter;

    /// <summary>
    /// 取得或設定文字文件的本文根節點。
    /// </summary>
    public OdfNode BodyTextRoot { get; private set; } = null!;

    /// <summary>
    /// 初始化 <see cref="TextDocument"/> 類別的新執行個體。
    /// </summary>
    /// <param name="package">OdfPackage 封裝包執行個體</param>
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
    /// 建立新的 ODT 文字文件。
    /// </summary>
    /// <returns>新的 <see cref="TextDocument"/> 執行個體。</returns>
    public static TextDocument Create()
    {
        return (TextDocument)OdfDocumentFactory.CreateDocument(OdfDocumentKind.Text);
    }

    /// <summary>
    /// 從指定的文字範本文件建立新的文字文件。
    /// </summary>
    /// <param name="template">文字範本文件。</param>
    /// <param name="clearUserContent">是否清除範本中的段落等使用者內容，但保留樣式與母片頁面。</param>
    /// <returns>建立完成的 <see cref="TextDocument"/> 執行個體。</returns>
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
    /// 建立新的 ODT 文字文件 Fluent builder。
    /// </summary>
    /// <returns>新的 <see cref="TextDocumentBuilder"/> 執行個體。</returns>
    public static TextDocumentBuilder Builder()
    {
        return new TextDocumentBuilder(Create());
    }

    /// <summary>
    /// 從指定路徑載入 ODT 文字文件。
    /// </summary>
    /// <param name="path">ODT 文件路徑。</param>
    /// <returns>載入完成的 <see cref="TextDocument"/> 執行個體。</returns>
    /// <exception cref="InvalidOperationException">當指定文件不是 ODT 文字文件時擲出。</exception>
    public new static TextDocument Load(string path)
    {
        return EnsureTextDocument(OdfDocumentFactory.LoadDocument(path));
    }

    /// <summary>
    /// 非同步從指定路徑載入 ODT 文字文件。
    /// </summary>
    /// <param name="path">ODT 文件路徑。</param>
    /// <param name="cancellationToken">取消語彙基元。</param>
    /// <returns>代表非同步載入作業的工作，其結果為載入完成的 <see cref="TextDocument"/>。</returns>
    /// <remarks>
    /// 若 <paramref name="cancellationToken"/> 已請求取消，作業會立即以 <see cref="OperationCanceledException"/> 結束；
    /// 否則會在 ZIP 解壓與封裝初始化期間協作檢查取消語彙。
    /// </remarks>
    public new static async Task<TextDocument> LoadAsync(string path, CancellationToken cancellationToken = default) =>
        EnsureTextDocument(await OdfDocumentFactory.LoadDocumentAsync(path, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// 從指定資料流載入 ODT 文字文件。
    /// </summary>
    /// <param name="stream">包含 ODT 文件內容的資料流。</param>
    /// <param name="fileName">選用的檔案名稱，用於輔助格式偵測。</param>
    /// <returns>載入完成的 <see cref="TextDocument"/> 執行個體。</returns>
    /// <exception cref="InvalidOperationException">當指定文件不是 ODT 文字文件時擲出。</exception>
    public new static TextDocument Load(Stream stream, string? fileName = null)
    {
        return EnsureTextDocument(OdfDocumentFactory.LoadDocument(stream, fileName));
    }

    /// <summary>
    /// 非同步從指定資料流載入 ODT 文字文件。
    /// </summary>
    /// <param name="stream">包含 ODT 文件內容的資料流。</param>
    /// <param name="fileName">選用的檔案名稱，用於輔助格式偵測。</param>
    /// <param name="cancellationToken">取消語彙基元。</param>
    /// <returns>代表非同步載入作業的工作，其結果為載入完成的 <see cref="TextDocument"/>。</returns>
    public new static async Task<TextDocument> LoadAsync(Stream stream, string? fileName = null, CancellationToken cancellationToken = default) =>
        EnsureTextDocument(await OdfDocumentFactory.LoadDocumentAsync(stream, fileName, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// 取得文字文件本文的高階操作入口。
    /// </summary>
    public OdfTextBody Body => _body ??= new OdfTextBody(this);

    /// <summary>
    /// 取得文件中繼資料的高階操作入口。
    /// </summary>
    public OdfDocumentMetadata Metadata => _metadata ??= new OdfDocumentMetadata(this);

    private static TextDocument EnsureTextDocument(OdfDocument document)
    {
        if (document is TextDocument textDocument && document.DocumentKind == OdfDocumentKind.Text)
        {
            return textDocument;
        }

        document.Dispose();
        throw new InvalidOperationException("指定的 ODF 文件不是 ODT 文字文件。");
    }

    private void InitializeTextRoot()
    {
        var body = FindOrCreateChild(ContentDom, "body", OdfNamespaces.Office, "office");
        BodyTextRoot = FindOrCreateChild(body, "text", OdfNamespaces.Office, "office");
    }

    /// <summary>
    /// 取得預設的內容 XML 字串。
    /// </summary>
    /// <returns>內容 XML 字串</returns>
    protected override string GetDefaultContentXml()
    {
        return "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:style=\"urn:oasis:names:tc:opendocument:xmlns:style:1.0\" xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\" xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\" xmlns:fo=\"urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:meta=\"urn:oasis:names:tc:opendocument:xmlns:meta:1.0\" xmlns:number=\"urn:oasis:names:tc:opendocument:xmlns:datastyle:1.0\" xmlns:presentation=\"urn:oasis:names:tc:opendocument:xmlns:presentation:1.0\" xmlns:svg=\"urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0\" xmlns:chart=\"urn:oasis:names:tc:opendocument:xmlns:chart:1.0\" xmlns:config=\"urn:oasis:names:tc:opendocument:xmlns:config:1.0\" xmlns:form=\"urn:oasis:names:tc:opendocument:xmlns:form:1.0\" office:version=\"" + OdfVersionInfo.DefaultVersionString + "\"><office:body><office:text></office:text></office:body></office:document-content>";
    }

    /// <summary>
    /// 取得預設的樣式 XML 字串。
    /// </summary>
    /// <returns>樣式 XML 字串</returns>
    protected override string GetDefaultStylesXml()
    {
        return "<office:document-styles xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:style=\"urn:oasis:names:tc:opendocument:xmlns:style:1.0\" xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\" xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\" xmlns:fo=\"urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:meta=\"urn:oasis:names:tc:opendocument:xmlns:meta:1.0\" xmlns:number=\"urn:oasis:names:tc:opendocument:xmlns:datastyle:1.0\" xmlns:presentation=\"urn:oasis:names:tc:opendocument:xmlns:presentation:1.0\" xmlns:svg=\"urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0\" xmlns:chart=\"urn:oasis:names:tc:opendocument:xmlns:chart:1.0\" xmlns:config=\"urn:oasis:names:tc:opendocument:xmlns:config:1.0\" office:version=\"" + OdfVersionInfo.DefaultVersionString + "\"><office:styles></office:styles><office:automatic-styles></office:automatic-styles><office:master-styles><style:master-page style:name=\"Standard\" style:page-layout-name=\"Mpm1\"/></office:master-styles></office:document-styles>";
    }


    #region Table covered cells omissions


    /// <summary>
    /// 新增一個表格項目至文件本文結尾。
    /// </summary>
    /// <param name="rows">表格的列數</param>
    /// <param name="cols">表格的欄數</param>
    /// <returns>新建立的表格物件</returns>
    public OdfTable AddTable(int rows, int cols)
    {
        var table = new OdfNode(OdfNodeType.Element, "table", OdfNamespaces.Table, "table");
        BodyTextRoot.AppendChild(table);
        return new OdfTable(table, rows, cols, this);
    }


    #endregion


    #region CJK Font Fallback


    /// <summary>
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
    /// 合併來源文件與目前文件的內容節點。
    /// </summary>
    /// <param name="sourceDoc">來源 OdfDocument 文件</param>
    /// <param name="options">合併設定選項</param>
    /// <param name="renameMap">變更樣式名稱的對照字典</param>
    protected override void MergeContentNodes(OdfDocument sourceDoc, OdfMergeOptions options, Dictionary<string, string> renameMap)
        => TextDocumentContentMergeEngine.MergeContentNodes(CoreCollaborators, sourceDoc, renameMap);


    #endregion


    #region TOC (Table of Contents)


    /// <summary>
    /// 新增目錄項目至文件本文結尾。
    /// </summary>
    /// <param name="title">目錄標題</param>
    /// <param name="outlineLevel">目錄的大綱階層上限</param>
    /// <returns>新建立的目錄物件</returns>
    public OdfTableOfContents AddTableOfContents(string title = "Table of Contents", int outlineLevel = 10)
        => TextDocumentTocEngine.AddTableOfContents(this, CoreCollaborators, title, outlineLevel);


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
    /// 取得預設的頁面設定。
    /// </summary>
    /// <returns>預設頁面設定物件</returns>
    public OdfPageSetup GetDefaultPageSetup()
    {
        return new OdfPageSetup(this);
    }

    /// <summary>
    /// 依主頁面樣式名稱取得可編輯的頁面設定。
    /// </summary>
    /// <param name="masterPageName">主頁面樣式名稱（例如 <c>Standard</c> 或 <c>Landscape</c>）。</param>
    /// <returns>對應的頁面設定物件。</returns>
    /// <exception cref="ArgumentException">找不到指定名稱的主頁面樣式時擲出。</exception>
    public OdfPageSetup GetPageSetup(string masterPageName)
    {
        if (string.IsNullOrWhiteSpace(masterPageName))
            throw new ArgumentException("主頁面樣式名稱不可為空白。", nameof(masterPageName));

        string? layoutName = ResolveMasterPageLayoutName(masterPageName);
        if (layoutName is null)
            throw new ArgumentException($"找不到主頁面樣式「{masterPageName}」。", nameof(masterPageName));

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
    /// 新增一個具名頁面樣式（master-page + page-layout），並可選擇性地配置其設定。
    /// </summary>
    /// <param name="name">主頁面樣式名稱（例如 "Landscape"）</param>
    /// <param name="configure">可選的頁面設定回呼</param>
    public OdfPageStyle AddPageStyle(string name, Action<OdfPageSetup>? configure = null)
    {
        string layoutName = $"MPL_{name}";
        var setup = new OdfPageSetup(this, name, layoutName);
        setup.EnsureNodes();
        configure?.Invoke(setup);
        return new OdfPageStyle(name);
    }

    /// <summary>
    /// 取得所有主頁面樣式的頁首頁尾摘要清單。
    /// </summary>
    public IReadOnlyList<OdfPageSetupInfo> GetPageSetups() =>
        TextDocumentPageSetupReadEngine.GetPageSetups(StylesDom);

    /// <summary>
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
    /// 新增多欄版面配置區段至文件本文結尾。
    /// </summary>
    /// <param name="name">區段名稱</param>
    /// <param name="columnCount">欄位數量</param>
    /// <param name="gap">欄間距寬度</param>
    /// <returns>新建立的區段物件</returns>
    public OdfSection AddSection(string name, int columnCount, OdfLength gap)
    {
        var section = new OdfNode(OdfNodeType.Element, "section", OdfNamespaces.Text, "text");
        section.SetAttribute("name", OdfNamespaces.Text, name, "text");

        string styleName = StyleEngine.GetOrCreateLocalStyle(section, "section").GetAttribute("name", OdfNamespaces.Style) ?? "S1";
        StyleEngine.SetLocalStyleProperty(section, "section", "section-properties", "column-count", OdfNamespaces.Fo, columnCount.ToString(CultureInfo.InvariantCulture), "fo");
        StyleEngine.SetLocalStyleProperty(section, "section", "section-properties", "column-gap", OdfNamespaces.Fo, gap.ToString(), "fo");

        BodyTextRoot.AppendChild(section);
        return new OdfSection(section, this);
    }

    /// <summary>
    /// 在文件本文結尾新增一個指向外部子文件參照的區段（用於主文件）。
    /// </summary>
    /// <param name="name">區段名稱。</param>
    /// <param name="subDocumentUri">外部子文件的相對或絕對 URI/路徑（將寫入 xlink:href）。</param>
    /// <param name="loadOnRequest">
    /// 是否延遲載入子文件內容（寫入 <c>xlink:actuate="onRequest"</c>）；預設為 <see langword="false"/>，
    /// 即開啟主控文件時立即載入（<c>xlink:actuate="onLoad"</c>）。
    /// </param>
    /// <returns>新建立的區段物件。</returns>
    public OdfSection AddSubDocumentReference(string name, string subDocumentUri, bool loadOnRequest = false)
    {
        if (string.IsNullOrEmpty(name))
            throw new ArgumentException("區段名稱不可為空。", nameof(name));
        if (string.IsNullOrEmpty(subDocumentUri))
            throw new ArgumentException("子文件 URI 不可為空。", nameof(subDocumentUri));

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
    /// 取得文件中所有註解的列表。
    /// </summary>
    /// <returns>註解物件列表</returns>
    public List<OdfComment> GetComments()
        => TextDocumentCommentsEngine.GetComments(BodyTextRoot);

    /// <summary>
    /// 取得文件中所有註解的摘要清單。
    /// </summary>
    public IReadOnlyList<OdfCommentInfo> GetCommentInfos() =>
        TextDocumentCommentReadEngine.GetCommentInfos(BodyTextRoot);


    #endregion


    #region XML Helper


    /// <summary>
    /// 在文件中新增字型宣告項目。
    /// </summary>
    /// <param name="name">字型代碼或別名</param>
    /// <param name="fontFamily">實際的字型名稱</param>
    /// <param name="genericFamily">泛用字型系列</param>
    /// <param name="pitch">字距模式</param>
    public void AddFontFace(string name, string fontFamily, string? genericFamily = null, string? pitch = null)
        => TextDocumentFontFaceEngine.AddFontFace(CoreCollaborators, name, fontFamily, genericFamily, pitch);


    #endregion


    #region MailMerge Implementation


    /// <summary>
    /// 以強型別資料來源物件執行郵件合併，屬性名稱對應文件中的合併欄位名稱。
    /// </summary>
    /// <typeparam name="T">資料來源型別。</typeparam>
    /// <param name="dataSource">合併資料來源物件。</param>
    public void MailMerge<T>(T dataSource) where T : notnull
    {
        var engine = new OdfMailMergeEngine(this);
        engine.Execute(BodyTextRoot, dataSource);
    }

    /// <summary>
    /// 以字典資料來源執行郵件合併，Key 對應文件中的合併欄位名稱。
    /// </summary>
    /// <param name="dataSource">以欄位名稱為 Key 的資料字典。</param>
    public void MailMerge(IReadOnlyDictionary<string, object?> dataSource)
    {
        var engine = new OdfMailMergeEngine(this);
        engine.Execute(BodyTextRoot, dataSource);
    }

    /// <summary>
    /// 以強型別記錄集合執行批次郵件合併，每筆記錄產生獨立的文件副本。
    /// </summary>
    /// <typeparam name="T">記錄型別；屬性名稱對應文件中的合併欄位名稱。</typeparam>
    /// <param name="records">資料記錄集合。</param>
    /// <returns>每筆記錄對應一個已合併的 <see cref="TextDocument"/>；呼叫端負責 Dispose。</returns>
    public IReadOnlyList<TextDocument> MailMerge<T>(IEnumerable<T> records) where T : notnull
        => TextDocumentMailMergeBatchEngine.MailMerge(this, records);

    /// <summary>
    /// 以字典記錄集合執行批次郵件合併，每筆記錄產生獨立的文件副本。
    /// </summary>
    /// <param name="records">字典記錄集合，Key 對應合併欄位名稱。</param>
    /// <returns>每筆記錄對應一個已合併的 <see cref="TextDocument"/>；呼叫端負責 Dispose。</returns>
    public IReadOnlyList<TextDocument> MailMerge(IEnumerable<IReadOnlyDictionary<string, object?>> records)
        => TextDocumentMailMergeBatchEngine.MailMerge(this, records);


    #endregion

}
