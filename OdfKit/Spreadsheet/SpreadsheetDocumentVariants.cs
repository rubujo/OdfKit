using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OdfKit.Compliance;
using OdfKit.Core;

namespace OdfKit.Spreadsheet;

/// <summary>
/// 表示 ODF 試算表範本文件（OTS）。
/// </summary>
public sealed class SpreadsheetTemplateDocument : SpreadsheetDocument
{
    /// <summary>
    /// 初始化 <see cref="SpreadsheetTemplateDocument"/> 類別的新執行個體。
    /// </summary>
    /// <param name="package">ODF 封裝。</param>
    public SpreadsheetTemplateDocument(OdfPackage package) : base(package)
    {
    }

    /// <summary>
    /// 建立新的 OTS 試算表範本文件。
    /// </summary>
    /// <returns>新的 <see cref="SpreadsheetTemplateDocument"/> 執行個體。</returns>
    public static new SpreadsheetTemplateDocument Create()
    {
        return (SpreadsheetTemplateDocument)OdfDocumentFactory.CreateDocument(OdfDocumentKind.SpreadsheetTemplate);
    }

    /// <summary>
    /// 從指定路徑載入 OTS 試算表範本文件。
    /// </summary>
    /// <param name="path">OTS 文件路徑。</param>
    /// <returns>載入完成的 <see cref="SpreadsheetTemplateDocument"/> 執行個體。</returns>
    public static new SpreadsheetTemplateDocument Load(string path) =>
        Ensure(OdfDocumentFactory.LoadDocument(path));

    /// <summary>
    /// 非同步從指定路徑載入 OTS 試算表範本文件。
    /// </summary>
    /// <param name="path">OTS 文件路徑。</param>
    /// <param name="cancellationToken">取消語彙基元。</param>
    /// <returns>代表非同步載入作業的工作，其結果為載入完成的 <see cref="SpreadsheetTemplateDocument"/>。</returns>
    public static new async Task<SpreadsheetTemplateDocument> LoadAsync(string path, CancellationToken cancellationToken = default) =>
        Ensure(await OdfDocumentFactory.LoadDocumentAsync(path, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// 從指定資料流載入 OTS 試算表範本文件。
    /// </summary>
    /// <param name="stream">包含 OTS 文件內容的資料流。</param>
    /// <param name="fileName">選用的檔案名稱，用於輔助格式偵測。</param>
    /// <returns>載入完成的 <see cref="SpreadsheetTemplateDocument"/> 執行個體。</returns>
    public static new SpreadsheetTemplateDocument Load(Stream stream, string? fileName = null) =>
        Ensure(OdfDocumentFactory.LoadDocument(stream, fileName));

    /// <summary>
    /// 非同步從指定資料流載入 OTS 試算表範本文件。
    /// </summary>
    /// <param name="stream">包含 OTS 文件內容的資料流。</param>
    /// <param name="fileName">選用的檔案名稱，用於輔助格式偵測。</param>
    /// <param name="cancellationToken">取消語彙基元。</param>
    /// <returns>代表非同步載入作業的工作，其結果為載入完成的 <see cref="SpreadsheetTemplateDocument"/>。</returns>
    public static new async Task<SpreadsheetTemplateDocument> LoadAsync(Stream stream, string? fileName = null, CancellationToken cancellationToken = default) =>
        Ensure(await OdfDocumentFactory.LoadDocumentAsync(stream, fileName, cancellationToken).ConfigureAwait(false));

    private static SpreadsheetTemplateDocument Ensure(OdfDocument document) =>
        OdfDocumentVariantSupport.EnsureKind<SpreadsheetTemplateDocument>(
            document,
            OdfDocumentKind.SpreadsheetTemplate,
            "指定的 ODF 文件不是 OTS 試算表範本。");
}

/// <summary>
/// 表示 ODF 扁平 XML 試算表文件（FODS）。
/// </summary>
public sealed class FlatSpreadsheetDocument : SpreadsheetDocument
{
    /// <summary>
    /// 初始化 <see cref="FlatSpreadsheetDocument"/> 類別的新執行個體。
    /// </summary>
    /// <param name="package">ODF 封裝或扁平 XML 容器。</param>
    public FlatSpreadsheetDocument(OdfPackage package) : base(package)
    {
    }

    /// <summary>
    /// 建立新的 FODS 扁平 XML 試算表文件。
    /// </summary>
    /// <returns>新的 <see cref="FlatSpreadsheetDocument"/> 執行個體。</returns>
    public static new FlatSpreadsheetDocument Create()
    {
        return (FlatSpreadsheetDocument)OdfDocumentFactory.CreateDocument(OdfDocumentKind.FlatSpreadsheet);
    }

    /// <summary>
    /// 從指定路徑載入 FODS 扁平 XML 試算表文件。
    /// </summary>
    /// <param name="path">FODS 文件路徑。</param>
    /// <returns>載入完成的 <see cref="FlatSpreadsheetDocument"/> 執行個體。</returns>
    public static new FlatSpreadsheetDocument Load(string path) =>
        Ensure(OdfDocumentFactory.LoadDocument(path));

    /// <summary>
    /// 非同步從指定路徑載入 FODS 扁平 XML 試算表文件。
    /// </summary>
    /// <param name="path">FODS 文件路徑。</param>
    /// <param name="cancellationToken">取消語彙基元。</param>
    /// <returns>代表非同步載入作業的工作，其結果為載入完成的 <see cref="FlatSpreadsheetDocument"/>。</returns>
    public static new async Task<FlatSpreadsheetDocument> LoadAsync(string path, CancellationToken cancellationToken = default) =>
        Ensure(await OdfDocumentFactory.LoadDocumentAsync(path, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// 從指定資料流載入 FODS 扁平 XML 試算表文件。
    /// </summary>
    /// <param name="stream">包含 FODS 文件內容的資料流。</param>
    /// <param name="fileName">選用的檔案名稱，用於輔助格式偵測。</param>
    /// <returns>載入完成的 <see cref="FlatSpreadsheetDocument"/> 執行個體。</returns>
    public static new FlatSpreadsheetDocument Load(Stream stream, string? fileName = null) =>
        Ensure(OdfDocumentFactory.LoadDocument(stream, fileName));

    /// <summary>
    /// 非同步從指定資料流載入 FODS 扁平 XML 試算表文件。
    /// </summary>
    /// <param name="stream">包含 FODS 文件內容的資料流。</param>
    /// <param name="fileName">選用的檔案名稱，用於輔助格式偵測。</param>
    /// <param name="cancellationToken">取消語彙基元。</param>
    /// <returns>代表非同步載入作業的工作，其結果為載入完成的 <see cref="FlatSpreadsheetDocument"/>。</returns>
    public static new async Task<FlatSpreadsheetDocument> LoadAsync(Stream stream, string? fileName = null, CancellationToken cancellationToken = default) =>
        Ensure(await OdfDocumentFactory.LoadDocumentAsync(stream, fileName, cancellationToken).ConfigureAwait(false));

    private static FlatSpreadsheetDocument Ensure(OdfDocument document) =>
        OdfDocumentVariantSupport.EnsureKind<FlatSpreadsheetDocument>(
            document,
            OdfDocumentKind.FlatSpreadsheet,
            "指定的 ODF 文件不是 FODS 扁平 XML 試算表。");
}
