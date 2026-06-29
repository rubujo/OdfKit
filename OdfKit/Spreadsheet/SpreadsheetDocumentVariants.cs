using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OdfKit.Compliance;
using OdfKit.Core;

namespace OdfKit.Spreadsheet;

/// <summary>
/// Represents an ODF spreadsheet template document (OTS).
/// 表示 ODF 試算表範本文件（OTS）。
/// </summary>
public sealed class SpreadsheetTemplateDocument : SpreadsheetDocument
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SpreadsheetTemplateDocument"/> class.
    /// 初始化 <see cref="SpreadsheetTemplateDocument"/> 類別的新執行個體。
    /// </summary>
    /// <param name="package">The ODF package. / ODF 封裝。</param>
    public SpreadsheetTemplateDocument(OdfPackage package) : base(package)
    {
    }

    /// <summary>
    /// Creates a new OTS spreadsheet template document.
    /// 建立新的 OTS 試算表範本文件。
    /// </summary>
    /// <returns>A new <see cref="SpreadsheetTemplateDocument"/> instance. / 新的 <see cref="SpreadsheetTemplateDocument"/> 執行個體。</returns>
    public static new SpreadsheetTemplateDocument Create()
    {
        return (SpreadsheetTemplateDocument)OdfDocumentFactory.CreateDocument(OdfDocumentKind.SpreadsheetTemplate);
    }

    /// <summary>
    /// Loads an OTS spreadsheet template document from the specified path.
    /// 從指定路徑載入 OTS 試算表範本文件。
    /// </summary>
    /// <param name="path">The OTS document path. / OTS 文件路徑。</param>
    /// <returns>The loaded <see cref="SpreadsheetTemplateDocument"/> instance. / 載入完成的 <see cref="SpreadsheetTemplateDocument"/> 執行個體。</returns>
    public static new SpreadsheetTemplateDocument Load(string path) =>
        Ensure(OdfDocumentFactory.LoadDocument(path));

    /// <summary>
    /// Asynchronously loads an OTS spreadsheet template document from the specified path.
    /// 非同步從指定路徑載入 OTS 試算表範本文件。
    /// </summary>
    /// <param name="path">The OTS document path. / OTS 文件路徑。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消權杖。</param>
    /// <returns>A task representing the asynchronous load operation, with the loaded <see cref="SpreadsheetTemplateDocument"/> as its result. / 代表非同步載入作業的工作，其結果為載入完成的 <see cref="SpreadsheetTemplateDocument"/>。</returns>
    public static new async Task<SpreadsheetTemplateDocument> LoadAsync(string path, CancellationToken cancellationToken = default) =>
        Ensure(await OdfDocumentFactory.LoadDocumentAsync(path, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// Loads an OTS spreadsheet template document from the specified stream.
    /// 從指定資料流載入 OTS 試算表範本文件。
    /// </summary>
    /// <param name="stream">The stream containing OTS document content. / 包含 OTS 文件內容的資料流。</param>
    /// <param name="fileName">The optional file name used to assist format detection. / 選用的檔案名稱，用於輔助格式偵測。</param>
    /// <returns>The loaded <see cref="SpreadsheetTemplateDocument"/> instance. / 載入完成的 <see cref="SpreadsheetTemplateDocument"/> 執行個體。</returns>
    public static new SpreadsheetTemplateDocument Load(Stream stream, string? fileName = null) =>
        Ensure(OdfDocumentFactory.LoadDocument(stream, fileName));

    /// <summary>
    /// Asynchronously loads an OTS spreadsheet template document from the specified stream.
    /// 非同步從指定資料流載入 OTS 試算表範本文件。
    /// </summary>
    /// <param name="stream">The stream containing OTS document content. / 包含 OTS 文件內容的資料流。</param>
    /// <param name="fileName">The optional file name used to assist format detection. / 選用的檔案名稱，用於輔助格式偵測。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消權杖。</param>
    /// <returns>A task representing the asynchronous load operation, with the loaded <see cref="SpreadsheetTemplateDocument"/> as its result. / 代表非同步載入作業的工作，其結果為載入完成的 <see cref="SpreadsheetTemplateDocument"/>。</returns>
    public static new async Task<SpreadsheetTemplateDocument> LoadAsync(Stream stream, string? fileName = null, CancellationToken cancellationToken = default) =>
        Ensure(await OdfDocumentFactory.LoadDocumentAsync(stream, fileName, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// Creates a new OTS spreadsheet template document from an existing ODS spreadsheet document, preserving its sheet content and styles.
    /// 從現有的 ODS 試算表文件建立新的 OTS 試算表範本文件，完整保留其工作表內容與樣式。
    /// </summary>
    /// <param name="document">The spreadsheet document used as the template content source. / 作為範本內容來源的試算表文件。</param>
    /// <returns>The created <see cref="SpreadsheetTemplateDocument"/> instance. / 建立完成的 <see cref="SpreadsheetTemplateDocument"/> 執行個體。</returns>
    public static SpreadsheetTemplateDocument CreateFromDocument(SpreadsheetDocument document) =>
        (SpreadsheetTemplateDocument)CreateTemplateFromDocumentInternal(
            document,
            OdfDocumentKind.SpreadsheetTemplate,
            "application/vnd.oasis.opendocument.spreadsheet-template");

    private static SpreadsheetTemplateDocument Ensure(OdfDocument document) =>
        OdfDocumentVariantSupport.EnsureKind<SpreadsheetTemplateDocument>(
            document,
            OdfDocumentKind.SpreadsheetTemplate,
            OdfLocalizer.GetMessage("Err_SpreadsheetTemplateDocument_SpecifiedOdfFileOts"));
}

/// <summary>
/// Represents an ODF flat XML spreadsheet document (FODS).
/// 表示 ODF 扁平 XML 試算表文件（FODS）。
/// </summary>
public sealed class FlatSpreadsheetDocument : SpreadsheetDocument
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FlatSpreadsheetDocument"/> class.
    /// 初始化 <see cref="FlatSpreadsheetDocument"/> 類別的新執行個體。
    /// </summary>
    /// <param name="package">The ODF package or flat XML container. / ODF 封裝或扁平 XML 容器。</param>
    public FlatSpreadsheetDocument(OdfPackage package) : base(package)
    {
    }

    /// <summary>
    /// Creates a new FODS flat XML spreadsheet document.
    /// 建立新的 FODS 扁平 XML 試算表文件。
    /// </summary>
    /// <returns>A new <see cref="FlatSpreadsheetDocument"/> instance. / 新的 <see cref="FlatSpreadsheetDocument"/> 執行個體。</returns>
    public static new FlatSpreadsheetDocument Create()
    {
        return (FlatSpreadsheetDocument)OdfDocumentFactory.CreateDocument(OdfDocumentKind.FlatSpreadsheet);
    }

    /// <summary>
    /// Loads a FODS flat XML spreadsheet document from the specified path.
    /// 從指定路徑載入 FODS 扁平 XML 試算表文件。
    /// </summary>
    /// <param name="path">The FODS document path. / FODS 文件路徑。</param>
    /// <returns>The loaded <see cref="FlatSpreadsheetDocument"/> instance. / 載入完成的 <see cref="FlatSpreadsheetDocument"/> 執行個體。</returns>
    public static new FlatSpreadsheetDocument Load(string path) =>
        Ensure(OdfDocumentFactory.LoadDocument(path));

    /// <summary>
    /// Asynchronously loads a FODS flat XML spreadsheet document from the specified path.
    /// 非同步從指定路徑載入 FODS 扁平 XML 試算表文件。
    /// </summary>
    /// <param name="path">The FODS document path. / FODS 文件路徑。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消權杖。</param>
    /// <returns>A task representing the asynchronous load operation, with the loaded <see cref="FlatSpreadsheetDocument"/> as its result. / 代表非同步載入作業的工作，其結果為載入完成的 <see cref="FlatSpreadsheetDocument"/>。</returns>
    public static new async Task<FlatSpreadsheetDocument> LoadAsync(string path, CancellationToken cancellationToken = default) =>
        Ensure(await OdfDocumentFactory.LoadDocumentAsync(path, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// Loads a FODS flat XML spreadsheet document from the specified stream.
    /// 從指定資料流載入 FODS 扁平 XML 試算表文件。
    /// </summary>
    /// <param name="stream">The stream containing FODS document content. / 包含 FODS 文件內容的資料流。</param>
    /// <param name="fileName">The optional file name used to assist format detection. / 選用的檔案名稱，用於輔助格式偵測。</param>
    /// <returns>The loaded <see cref="FlatSpreadsheetDocument"/> instance. / 載入完成的 <see cref="FlatSpreadsheetDocument"/> 執行個體。</returns>
    public static new FlatSpreadsheetDocument Load(Stream stream, string? fileName = null) =>
        Ensure(OdfDocumentFactory.LoadDocument(stream, fileName));

    /// <summary>
    /// Asynchronously loads a FODS flat XML spreadsheet document from the specified stream.
    /// 非同步從指定資料流載入 FODS 扁平 XML 試算表文件。
    /// </summary>
    /// <param name="stream">The stream containing FODS document content. / 包含 FODS 文件內容的資料流。</param>
    /// <param name="fileName">The optional file name used to assist format detection. / 選用的檔案名稱，用於輔助格式偵測。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消權杖。</param>
    /// <returns>A task representing the asynchronous load operation, with the loaded <see cref="FlatSpreadsheetDocument"/> as its result. / 代表非同步載入作業的工作，其結果為載入完成的 <see cref="FlatSpreadsheetDocument"/>。</returns>
    public static new async Task<FlatSpreadsheetDocument> LoadAsync(Stream stream, string? fileName = null, CancellationToken cancellationToken = default) =>
        Ensure(await OdfDocumentFactory.LoadDocumentAsync(stream, fileName, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// Creates an equivalent FODS flat XML spreadsheet document from an existing ODS package spreadsheet document, preserving identical content.
    /// 從現有的 ODS（ZIP 封裝）試算表文件建立等價的 FODS 扁平 XML 試算表文件，內容完全相同。
    /// </summary>
    /// <param name="document">The source ODS spreadsheet document. / 來源 ODS 試算表文件。</param>
    /// <returns>The created <see cref="FlatSpreadsheetDocument"/> instance. / 建立完成的 <see cref="FlatSpreadsheetDocument"/> 執行個體。</returns>
    public static FlatSpreadsheetDocument CreateFromDocument(SpreadsheetDocument document) =>
        (FlatSpreadsheetDocument)ConvertFlatVariantInternal(document, OdfDocumentKind.FlatSpreadsheet, targetIsFlatXml: true);

    private static FlatSpreadsheetDocument Ensure(OdfDocument document) =>
        OdfDocumentVariantSupport.EnsureKind<FlatSpreadsheetDocument>(
            document,
            OdfDocumentKind.FlatSpreadsheet,
            OdfLocalizer.GetMessage("Err_FlatSpreadsheetDocument_SpecifiedOdfFileFods"));
}
