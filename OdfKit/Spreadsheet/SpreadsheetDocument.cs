using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OdfKit.Chart;
using OdfKit.Compliance;
using OdfKit.Core;
using OdfKit.DOM;
using OdfKit.Styles;

namespace OdfKit.Spreadsheet;

/// <summary>
/// Represents an ODF spreadsheet document (ODS).
/// 表示 ODF 試算表文件（ODS）。
/// </summary>
public partial class SpreadsheetDocument : OdfDocument
{
    private OdfWorksheetCollection? _worksheets;

    /// <summary>
    /// 取得活頁簿中所有工作表的根節點。
    /// </summary>
    internal OdfNode SheetsRoot { get; private set; } = null!;

    /// <summary>
    /// Initializes a new instance of the <see cref="SpreadsheetDocument"/> class.
    /// 初始化 <see cref="SpreadsheetDocument"/> 類別的新執行個體。
    /// </summary>
    /// <param name="package">The ODF package. / Odf 套件包。</param>
    public SpreadsheetDocument(OdfPackage package) : base(package)
    {
        if (string.IsNullOrEmpty(package.MimeType))
        {
            package.SetMimeType("application/vnd.oasis.opendocument.spreadsheet");
        }
        InitializeSheetsRoot();
    }

    /// <summary>
    /// Creates a new ODS spreadsheet document.
    /// 建立新的 ODS 試算表文件。
    /// </summary>
    /// <returns>A new <see cref="SpreadsheetDocument"/> instance. / 新的 <see cref="SpreadsheetDocument"/> 執行個體。</returns>
    public static SpreadsheetDocument Create()
    {
        return (SpreadsheetDocument)OdfDocumentFactory.CreateDocument(OdfDocumentKind.Spreadsheet);
    }

    /// <summary>
    /// Creates a new spreadsheet document from the specified spreadsheet template.
    /// 從指定的試算表範本文件建立新的試算表文件。
    /// </summary>
    /// <param name="template">The spreadsheet template document. / 試算表範本文件。</param>
    /// <param name="clearUserContent">Whether to clear the data rows of each sheet in the template while keeping column widths and sheet structure. / 是否清除範本中各工作表的資料列，但保留欄寬與工作表結構。</param>
    /// <returns>The created <see cref="SpreadsheetDocument"/> instance. / 建立完成的 <see cref="SpreadsheetDocument"/> 執行個體。</returns>
    public static SpreadsheetDocument CreateFromTemplate(SpreadsheetTemplateDocument template, bool clearUserContent = false)
    {
        return (SpreadsheetDocument)CreateFromTemplateInternal(template, OdfDocumentKind.Spreadsheet, "application/vnd.oasis.opendocument.spreadsheet", clearUserContent);
    }

    /// <summary>
    /// Creates an equivalent ODS (ZIP package) spreadsheet document from a FODS flat XML spreadsheet document, with identical content.
    /// 從 FODS 扁平 XML 試算表文件建立等價的 ODS（ZIP 封裝）試算表文件，內容完全相同。
    /// </summary>
    /// <param name="document">The source FODS flat XML spreadsheet document. / 來源 FODS 扁平 XML 試算表文件。</param>
    /// <returns>The created <see cref="SpreadsheetDocument"/> instance. / 建立完成的 <see cref="SpreadsheetDocument"/> 執行個體。</returns>
    public static SpreadsheetDocument CreateFromFlatDocument(FlatSpreadsheetDocument document) =>
        (SpreadsheetDocument)ConvertFlatVariantInternal(document, OdfDocumentKind.Spreadsheet, targetIsFlatXml: false);

    /// <inheritdoc/>
    protected override void ClearTemplateUserContent()
    {
        foreach (OdfNode sheet in SheetsRoot.Children)
        {
            if (sheet.NodeType is not OdfNodeType.Element ||
                sheet.LocalName != "table" ||
                sheet.NamespaceUri != OdfNamespaces.Table)
            {
                continue;
            }

            foreach (OdfNode child in new List<OdfNode>(sheet.Children))
            {
                if (child.NodeType is OdfNodeType.Element &&
                    child.LocalName == "table-row" &&
                    child.NamespaceUri == OdfNamespaces.Table)
                {
                    sheet.RemoveChild(child);
                }
            }
        }
    }

    /// <summary>
    /// Creates a new ODS spreadsheet document fluent builder.
    /// 建立新的 ODS 試算表文件 Fluent builder。
    /// </summary>
    /// <returns>A new <see cref="SpreadsheetDocumentBuilder"/> instance. / 新的 <see cref="SpreadsheetDocumentBuilder"/> 執行個體。</returns>
    public static SpreadsheetDocumentBuilder Builder()
    {
        return new SpreadsheetDocumentBuilder(Create());
    }

    /// <summary>
    /// Loads an ODS spreadsheet document from the specified path.
    /// 從指定路徑載入 ODS 試算表文件。
    /// </summary>
    /// <param name="path">The ODS document path. / ODS 文件路徑。</param>
    /// <returns>The loaded <see cref="SpreadsheetDocument"/> instance. / 載入完成的 <see cref="SpreadsheetDocument"/> 執行個體。</returns>
    /// <exception cref="InvalidOperationException">When the specified document is not an ODS spreadsheet. / 當指定文件不是 ODS 試算表時擲出。</exception>
    public new static SpreadsheetDocument Load(string path) =>
        OdfDocumentVariantSupport.Load<SpreadsheetDocument>(path, OdfDocumentKind.Spreadsheet, "Err_SpreadsheetDocument_SpecifiedOdfFileOds");

    /// <summary>
    /// Asynchronously loads an ODS spreadsheet document from the specified path.
    /// 非同步從指定路徑載入 ODS 試算表文件。
    /// </summary>
    /// <param name="path">The ODS document path. / ODS 文件路徑。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消語彙基元。</param>
    /// <returns>A task representing the asynchronous load operation, whose result is the loaded <see cref="SpreadsheetDocument"/>. / 代表非同步載入作業的工作，其結果為載入完成的 <see cref="SpreadsheetDocument"/>。</returns>
    /// <remarks>
    /// 若 <paramref name="cancellationToken"/> 已請求取消，作業會立即以 <see cref="OperationCanceledException"/> 結束；
    /// 否則會在 ZIP 解壓與封裝初始化期間協作檢查取消語彙。
    /// </remarks>
    public new static Task<SpreadsheetDocument> LoadAsync(string path, CancellationToken cancellationToken = default) =>
        OdfDocumentVariantSupport.LoadAsync<SpreadsheetDocument>(path, OdfDocumentKind.Spreadsheet, "Err_SpreadsheetDocument_SpecifiedOdfFileOds", cancellationToken);

    /// <summary>
    /// Loads an ODS spreadsheet document from the specified stream.
    /// 從指定資料流載入 ODS 試算表文件。
    /// </summary>
    /// <param name="stream">The stream containing the ODS document content. / 包含 ODS 文件內容的資料流。</param>
    /// <param name="fileName">The optional file name, used to assist format detection. / 選用的檔案名稱，用於輔助格式偵測。</param>
    /// <returns>The loaded <see cref="SpreadsheetDocument"/> instance. / 載入完成的 <see cref="SpreadsheetDocument"/> 執行個體。</returns>
    /// <exception cref="InvalidOperationException">When the specified document is not an ODS spreadsheet. / 當指定文件不是 ODS 試算表時擲出。</exception>
    public new static SpreadsheetDocument Load(Stream stream, string? fileName = null) =>
        OdfDocumentVariantSupport.Load<SpreadsheetDocument>(stream, OdfDocumentKind.Spreadsheet, "Err_SpreadsheetDocument_SpecifiedOdfFileOds", fileName);

    /// <summary>
    /// Asynchronously loads an ODS spreadsheet document from the specified stream.
    /// 非同步從指定資料流載入 ODS 試算表文件。
    /// </summary>
    /// <param name="stream">The stream containing the ODS document content. / 包含 ODS 文件內容的資料流。</param>
    /// <param name="fileName">The optional file name, used to assist format detection. / 選用的檔案名稱，用於輔助格式偵測。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消語彙基元。</param>
    /// <returns>A task representing the asynchronous load operation, whose result is the loaded <see cref="SpreadsheetDocument"/>. / 代表非同步載入作業的工作，其結果為載入完成的 <see cref="SpreadsheetDocument"/>。</returns>
    public new static Task<SpreadsheetDocument> LoadAsync(Stream stream, string? fileName = null, CancellationToken cancellationToken = default) =>
        OdfDocumentVariantSupport.LoadAsync<SpreadsheetDocument>(stream, OdfDocumentKind.Spreadsheet, "Err_SpreadsheetDocument_SpecifiedOdfFileOds", fileName, cancellationToken);

    /// <summary>
    /// Gets the worksheet collection.
    /// 取得工作表集合。
    /// </summary>
    public OdfWorksheetCollection Worksheets => _worksheets ??= new OdfWorksheetCollection(this);

    /// <summary>
    /// Gets the workbook-level formula calculation and recalculation settings for the ODS spreadsheet.
    /// 取得 ODS 活頁簿層級的公式計算與重算設定。
    /// </summary>
    public OdfSpreadsheetCalculationSettings CalculationSettings =>
        new(OdfTableSheetDomHelper.FindOrCreateSpreadsheetPreludeChild(
            SheetsRoot,
            "calculation-settings",
            OdfNamespaces.Table,
            "table"));

    private void InitializeSheetsRoot()
    {
        var body = FindOrCreateChild(ContentDom, "body", OdfNamespaces.Office, "office");
        SheetsRoot = FindOrCreateChild(body, "spreadsheet", OdfNamespaces.Office, "office");
    }

    internal OdfNode GetOrCreateSettingsItemSet(string name)
    {
        return FindOrCreateSettingsNode(SettingsDom, name);
    }

    /// <summary>
    /// Gets the default content.xml content.
    /// 取得預設的 content.xml 內容。
    /// </summary>
    /// <returns>The default XML content string. / 預設的 XML 內容字串。</returns>
    protected override string GetDefaultContentXml()
    {
        return "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:style=\"urn:oasis:names:tc:opendocument:xmlns:style:1.0\" xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\" xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\" xmlns:fo=\"urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:meta=\"urn:oasis:names:tc:opendocument:xmlns:meta:1.0\" xmlns:number=\"urn:oasis:names:tc:opendocument:xmlns:datastyle:1.0\" xmlns:presentation=\"urn:oasis:names:tc:opendocument:xmlns:presentation:1.0\" xmlns:svg=\"urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0\" xmlns:chart=\"urn:oasis:names:tc:opendocument:xmlns:chart:1.0\" xmlns:config=\"urn:oasis:names:tc:opendocument:xmlns:config:1.0\" office:version=\"" + OdfVersionInfo.DefaultVersionString + "\"><office:body><office:spreadsheet></office:spreadsheet></office:body></office:document-content>";
    }

    /// <summary>
    /// Gets the default styles.xml content.
    /// 取得預設的 styles.xml 內容。
    /// </summary>
    /// <returns>The default XML content string. / 預設的 XML 內容字串。</returns>
    protected override string GetDefaultStylesXml()
    {
        return "<office:document-styles xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:style=\"urn:oasis:names:tc:opendocument:xmlns:style:1.0\" xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\" xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\" xmlns:fo=\"urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:meta=\"urn:oasis:names:tc:opendocument:xmlns:meta:1.0\" xmlns:number=\"urn:oasis:names:tc:opendocument:xmlns:datastyle:1.0\" xmlns:presentation=\"urn:oasis:names:tc:opendocument:xmlns:presentation:1.0\" xmlns:svg=\"urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0\" xmlns:chart=\"urn:oasis:names:tc:opendocument:xmlns:chart:1.0\" xmlns:config=\"urn:oasis:names:tc:opendocument:xmlns:config:1.0\" office:version=\"" + OdfVersionInfo.DefaultVersionString + "\"><office:styles></office:styles><office:automatic-styles></office:automatic-styles><office:master-styles></office:master-styles></office:document-styles>";
    }

}
