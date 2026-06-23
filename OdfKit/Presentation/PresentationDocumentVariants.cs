using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OdfKit.Compliance;
using OdfKit.Core;

namespace OdfKit.Presentation;

/// <summary>
/// 表示 ODF 簡報範本文件（OTP）。
/// </summary>
public sealed class PresentationTemplateDocument : PresentationDocument
{
    /// <summary>
    /// 初始化 <see cref="PresentationTemplateDocument"/> 類別的新執行個體。
    /// </summary>
    /// <param name="package">ODF 封裝</param>
    public PresentationTemplateDocument(OdfPackage package) : base(package)
    {
    }

    /// <summary>
    /// 建立新的 OTP 簡報範本文件。
    /// </summary>
    /// <returns>新的 <see cref="PresentationTemplateDocument"/> 執行個體</returns>
    public static new PresentationTemplateDocument Create()
    {
        return (PresentationTemplateDocument)OdfDocumentFactory.CreateDocument(OdfDocumentKind.PresentationTemplate);
    }

    /// <summary>
    /// 從指定路徑載入 OTP 簡報範本文件。
    /// </summary>
    /// <param name="path">OTP 文件路徑</param>
    /// <returns>載入完成的 <see cref="PresentationTemplateDocument"/> 執行個體</returns>
    public static new PresentationTemplateDocument Load(string path) =>
        Ensure(OdfDocumentFactory.LoadDocument(path));

    /// <summary>
    /// 非同步從指定路徑載入 OTP 簡報範本文件。
    /// </summary>
    /// <param name="path">OTP 文件路徑</param>
    /// <param name="cancellationToken">取消語彙基元</param>
    /// <returns>代表非同步載入作業的工作，其結果為載入完成的 <see cref="PresentationTemplateDocument"/></returns>
    public static new async Task<PresentationTemplateDocument> LoadAsync(string path, CancellationToken cancellationToken = default) =>
        Ensure(await OdfDocumentFactory.LoadDocumentAsync(path, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// 從指定資料流載入 OTP 簡報範本文件。
    /// </summary>
    /// <param name="stream">包含 OTP 文件內容的資料流</param>
    /// <param name="fileName">選用的檔案名稱，用於輔助格式偵測</param>
    /// <returns>載入完成的 <see cref="PresentationTemplateDocument"/> 執行個體</returns>
    public static new PresentationTemplateDocument Load(Stream stream, string? fileName = null) =>
        Ensure(OdfDocumentFactory.LoadDocument(stream, fileName));

    /// <summary>
    /// 非同步從指定資料流載入 OTP 簡報範本文件。
    /// </summary>
    /// <param name="stream">包含 OTP 文件內容的資料流</param>
    /// <param name="fileName">選用的檔案名稱，用於輔助格式偵測</param>
    /// <param name="cancellationToken">取消語彙基元</param>
    /// <returns>代表非同步載入作業的工作，其結果為載入完成的 <see cref="PresentationTemplateDocument"/></returns>
    public static new async Task<PresentationTemplateDocument> LoadAsync(Stream stream, string? fileName = null, CancellationToken cancellationToken = default) =>
        Ensure(await OdfDocumentFactory.LoadDocumentAsync(stream, fileName, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// 從現有的 ODP 簡報文件建立新的 OTP 簡報範本文件，完整保留其投影片內容、母片頁面與樣式。
    /// </summary>
    /// <param name="document">作為範本內容來源的簡報文件</param>
    /// <returns>建立完成的 <see cref="PresentationTemplateDocument"/> 執行個體</returns>
    public static PresentationTemplateDocument CreateFromDocument(PresentationDocument document) =>
        (PresentationTemplateDocument)CreateTemplateFromDocumentInternal(
            document,
            OdfDocumentKind.PresentationTemplate,
            "application/vnd.oasis.opendocument.presentation-template");

    private static PresentationTemplateDocument Ensure(OdfDocument document) =>
        OdfDocumentVariantSupport.EnsureKind<PresentationTemplateDocument>(
            document,
            OdfDocumentKind.PresentationTemplate,
            "指定的 ODF 文件不是 OTP 簡報範本。");
}

/// <summary>
/// 表示 ODF 扁平 XML 簡報文件（FODP）。
/// </summary>
public sealed class FlatPresentationDocument : PresentationDocument
{
    /// <summary>
    /// 初始化 <see cref="FlatPresentationDocument"/> 類別的新執行個體。
    /// </summary>
    /// <param name="package">ODF 封裝或扁平 XML 容器</param>
    public FlatPresentationDocument(OdfPackage package) : base(package)
    {
    }

    /// <summary>
    /// 建立新的 FODP 扁平 XML 簡報文件。
    /// </summary>
    /// <returns>新的 <see cref="FlatPresentationDocument"/> 執行個體</returns>
    public static new FlatPresentationDocument Create()
    {
        return (FlatPresentationDocument)OdfDocumentFactory.CreateDocument(OdfDocumentKind.FlatPresentation);
    }

    /// <summary>
    /// 從指定路徑載入 FODP 扁平 XML 簡報文件。
    /// </summary>
    /// <param name="path">FODP 文件路徑</param>
    /// <returns>載入完成的 <see cref="FlatPresentationDocument"/> 執行個體</returns>
    public static new FlatPresentationDocument Load(string path) =>
        Ensure(OdfDocumentFactory.LoadDocument(path));

    /// <summary>
    /// 非同步從指定路徑載入 FODP 扁平 XML 簡報文件。
    /// </summary>
    /// <param name="path">FODP 文件路徑</param>
    /// <param name="cancellationToken">取消語彙基元</param>
    /// <returns>代表非同步載入作業的工作，其結果為載入完成的 <see cref="FlatPresentationDocument"/></returns>
    public static new async Task<FlatPresentationDocument> LoadAsync(string path, CancellationToken cancellationToken = default) =>
        Ensure(await OdfDocumentFactory.LoadDocumentAsync(path, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// 從指定資料流載入 FODP 扁平 XML 簡報文件。
    /// </summary>
    /// <param name="stream">包含 FODP 文件內容的資料流</param>
    /// <param name="fileName">選用的檔案名稱，用於輔助格式偵測</param>
    /// <returns>載入完成的 <see cref="FlatPresentationDocument"/> 執行個體</returns>
    public static new FlatPresentationDocument Load(Stream stream, string? fileName = null) =>
        Ensure(OdfDocumentFactory.LoadDocument(stream, fileName));

    /// <summary>
    /// 非同步從指定資料流載入 FODP 扁平 XML 簡報文件。
    /// </summary>
    /// <param name="stream">包含 FODP 文件內容的資料流</param>
    /// <param name="fileName">選用的檔案名稱，用於輔助格式偵測</param>
    /// <param name="cancellationToken">取消語彙基元</param>
    /// <returns>代表非同步載入作業的工作，其結果為載入完成的 <see cref="FlatPresentationDocument"/></returns>
    public static new async Task<FlatPresentationDocument> LoadAsync(Stream stream, string? fileName = null, CancellationToken cancellationToken = default) =>
        Ensure(await OdfDocumentFactory.LoadDocumentAsync(stream, fileName, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// 從現有的 ODP（ZIP 封裝）簡報文件建立等價的 FODP 扁平 XML 簡報文件，內容完全相同。
    /// </summary>
    /// <param name="document">來源 ODP 簡報文件</param>
    /// <returns>建立完成的 <see cref="FlatPresentationDocument"/> 執行個體</returns>
    public static FlatPresentationDocument CreateFromDocument(PresentationDocument document) =>
        (FlatPresentationDocument)ConvertFlatVariantInternal(document, OdfDocumentKind.FlatPresentation, targetIsFlatXml: true);

    private static FlatPresentationDocument Ensure(OdfDocument document) =>
        OdfDocumentVariantSupport.EnsureKind<FlatPresentationDocument>(
            document,
            OdfDocumentKind.FlatPresentation,
            "指定的 ODF 文件不是 FODP 扁平 XML 簡報。");
}
