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
    /// 初始化 <see cref="SpreadsheetDocument"/> 類別的新執行個體。
    /// </summary>
    /// <param name="package">Odf 套件包</param>
    public SpreadsheetDocument(OdfPackage package) : base(package)
    {
        if (string.IsNullOrEmpty(package.MimeType))
        {
            package.SetMimeType("application/vnd.oasis.opendocument.spreadsheet");
        }
        InitializeSheetsRoot();
    }

    /// <summary>
    /// 建立新的 ODS 試算表文件。
    /// </summary>
    /// <returns>新的 <see cref="SpreadsheetDocument"/> 執行個體。</returns>
    public static SpreadsheetDocument Create()
    {
        return (SpreadsheetDocument)OdfDocumentFactory.CreateDocument(OdfDocumentKind.Spreadsheet);
    }

    /// <summary>
    /// 從指定的試算表範本文件建立新的試算表文件。
    /// </summary>
    /// <param name="template">試算表範本文件。</param>
    /// <param name="clearUserContent">是否清除範本中各工作表的資料列，但保留欄寬與工作表結構。</param>
    /// <returns>建立完成的 <see cref="SpreadsheetDocument"/> 執行個體。</returns>
    public static SpreadsheetDocument CreateFromTemplate(SpreadsheetTemplateDocument template, bool clearUserContent = false)
    {
        return (SpreadsheetDocument)CreateFromTemplateInternal(template, OdfDocumentKind.Spreadsheet, "application/vnd.oasis.opendocument.spreadsheet", clearUserContent);
    }

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
    /// 建立新的 ODS 試算表文件 Fluent builder。
    /// </summary>
    /// <returns>新的 <see cref="SpreadsheetDocumentBuilder"/> 執行個體。</returns>
    public static SpreadsheetDocumentBuilder Builder()
    {
        return new SpreadsheetDocumentBuilder(Create());
    }

    /// <summary>
    /// 從指定路徑載入 ODS 試算表文件。
    /// </summary>
    /// <param name="path">ODS 文件路徑。</param>
    /// <returns>載入完成的 <see cref="SpreadsheetDocument"/> 執行個體。</returns>
    /// <exception cref="InvalidOperationException">當指定文件不是 ODS 試算表時擲出。</exception>
    public new static SpreadsheetDocument Load(string path)
    {
        return EnsureSpreadsheet(OdfDocumentFactory.LoadDocument(path));
    }

    /// <summary>
    /// 非同步從指定路徑載入 ODS 試算表文件。
    /// </summary>
    /// <param name="path">ODS 文件路徑。</param>
    /// <param name="cancellationToken">取消語彙基元。</param>
    /// <returns>代表非同步載入作業的工作，其結果為載入完成的 <see cref="SpreadsheetDocument"/>。</returns>
    /// <remarks>
    /// 若 <paramref name="cancellationToken"/> 已請求取消，作業會立即以 <see cref="OperationCanceledException"/> 結束；
    /// 否則會在 ZIP 解壓與封裝初始化期間協作檢查取消語彙。
    /// </remarks>
    public new static async Task<SpreadsheetDocument> LoadAsync(string path, CancellationToken cancellationToken = default) =>
        EnsureSpreadsheet(await OdfDocumentFactory.LoadDocumentAsync(path, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// 從指定資料流載入 ODS 試算表文件。
    /// </summary>
    /// <param name="stream">包含 ODS 文件內容的資料流。</param>
    /// <param name="fileName">選用的檔案名稱，用於輔助格式偵測。</param>
    /// <returns>載入完成的 <see cref="SpreadsheetDocument"/> 執行個體。</returns>
    /// <exception cref="InvalidOperationException">當指定文件不是 ODS 試算表時擲出。</exception>
    public new static SpreadsheetDocument Load(Stream stream, string? fileName = null)
    {
        return EnsureSpreadsheet(OdfDocumentFactory.LoadDocument(stream, fileName));
    }

    /// <summary>
    /// 非同步從指定資料流載入 ODS 試算表文件。
    /// </summary>
    /// <param name="stream">包含 ODS 文件內容的資料流。</param>
    /// <param name="fileName">選用的檔案名稱，用於輔助格式偵測。</param>
    /// <param name="cancellationToken">取消語彙基元。</param>
    /// <returns>代表非同步載入作業的工作，其結果為載入完成的 <see cref="SpreadsheetDocument"/>。</returns>
    public new static async Task<SpreadsheetDocument> LoadAsync(Stream stream, string? fileName = null, CancellationToken cancellationToken = default) =>
        EnsureSpreadsheet(await OdfDocumentFactory.LoadDocumentAsync(stream, fileName, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// 取得工作表集合。
    /// </summary>
    public OdfWorksheetCollection Worksheets => _worksheets ??= new OdfWorksheetCollection(this);

    private static SpreadsheetDocument EnsureSpreadsheet(OdfDocument document)
    {
        if (document is SpreadsheetDocument spreadsheet && document.DocumentKind == OdfDocumentKind.Spreadsheet)
        {
            return spreadsheet;
        }

        document.Dispose();
        throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_SpreadsheetDocument_SpecifiedOdfFileOds"));
    }

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
    /// 取得預設的 content.xml 內容。
    /// </summary>
    /// <returns>預設的 XML 內容字串</returns>
    protected override string GetDefaultContentXml()
    {
        return "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:style=\"urn:oasis:names:tc:opendocument:xmlns:style:1.0\" xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\" xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\" xmlns:fo=\"urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:meta=\"urn:oasis:names:tc:opendocument:xmlns:meta:1.0\" xmlns:number=\"urn:oasis:names:tc:opendocument:xmlns:datastyle:1.0\" xmlns:presentation=\"urn:oasis:names:tc:opendocument:xmlns:presentation:1.0\" xmlns:svg=\"urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0\" xmlns:chart=\"urn:oasis:names:tc:opendocument:xmlns:chart:1.0\" xmlns:config=\"urn:oasis:names:tc:opendocument:xmlns:config:1.0\" office:version=\"" + OdfVersionInfo.DefaultVersionString + "\"><office:body><office:spreadsheet></office:spreadsheet></office:body></office:document-content>";
    }

    /// <summary>
    /// 取得預設的 styles.xml 內容。
    /// </summary>
    /// <returns>預設的 XML 內容字串</returns>
    protected override string GetDefaultStylesXml()
    {
        return "<office:document-styles xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:style=\"urn:oasis:names:tc:opendocument:xmlns:style:1.0\" xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\" xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\" xmlns:fo=\"urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:meta=\"urn:oasis:names:tc:opendocument:xmlns:meta:1.0\" xmlns:number=\"urn:oasis:names:tc:opendocument:xmlns:datastyle:1.0\" xmlns:presentation=\"urn:oasis:names:tc:opendocument:xmlns:presentation:1.0\" xmlns:svg=\"urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0\" xmlns:chart=\"urn:oasis:names:tc:opendocument:xmlns:chart:1.0\" xmlns:config=\"urn:oasis:names:tc:opendocument:xmlns:config:1.0\" office:version=\"" + OdfVersionInfo.DefaultVersionString + "\"><office:styles></office:styles><office:automatic-styles></office:automatic-styles><office:master-styles></office:master-styles></office:document-styles>";
    }

}
