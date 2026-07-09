using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OdfKit.Compliance;
using OdfKit.Core;

namespace OdfKit.Presentation;

/// <summary>
/// Represents an ODF presentation template document (OTP).
/// 表示 ODF 簡報範本文件（OTP）。
/// </summary>
public sealed class PresentationTemplateDocument : PresentationDocument
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PresentationTemplateDocument"/> class.
    /// 初始化 <see cref="PresentationTemplateDocument"/> 類別的新執行個體。
    /// </summary>
    /// <param name="package">The ODF package. / ODF 封裝。</param>
    public PresentationTemplateDocument(OdfPackage package) : base(package)
    {
    }

    /// <summary>
    /// Creates a new OTP presentation template document.
    /// 建立新的 OTP 簡報範本文件。
    /// </summary>
    /// <returns>A new <see cref="PresentationTemplateDocument"/> instance. / 新的 <see cref="PresentationTemplateDocument"/> 執行個體。</returns>
    public static new PresentationTemplateDocument Create()
    {
        return (PresentationTemplateDocument)OdfDocumentFactory.CreateDocument(OdfDocumentKind.PresentationTemplate);
    }

    /// <summary>
    /// Loads an OTP presentation template document from the specified path.
    /// 從指定路徑載入 OTP 簡報範本文件。
    /// </summary>
    /// <param name="path">The OTP document path. / OTP 文件路徑。</param>
    /// <returns>The loaded <see cref="PresentationTemplateDocument"/> instance. / 載入完成的 <see cref="PresentationTemplateDocument"/> 執行個體。</returns>
    public static new PresentationTemplateDocument Load(string path) =>
        Ensure(OdfDocumentFactory.LoadDocument(path));

    /// <summary>
    /// Asynchronously loads an OTP presentation template document from the specified path.
    /// 非同步從指定路徑載入 OTP 簡報範本文件。
    /// </summary>
    /// <returns>A task representing the asynchronous load operation, with the loaded <see cref="PresentationTemplateDocument"/> as its result. / 代表非同步載入作業的工作，其結果為載入完成的 <see cref="PresentationTemplateDocument"/>。</returns>
    public static new Task<PresentationTemplateDocument> LoadAsync(string path) => LoadAsync(path, default);

    /// <summary>
    /// Additional public overload without optional parameters.
    /// 不含選用參數的公開多載。
    /// </summary>
    public static new async Task<PresentationTemplateDocument> LoadAsync(string path, CancellationToken cancellationToken) =>
        Ensure(await OdfDocumentFactory.LoadDocumentAsync(path, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// Loads an OTP presentation template document from the specified stream.
    /// 從指定資料流載入 OTP 簡報範本文件。
    /// </summary>
    /// <returns>The loaded <see cref="PresentationTemplateDocument"/> instance. / 載入完成的 <see cref="PresentationTemplateDocument"/> 執行個體。</returns>
    public static new PresentationTemplateDocument Load(Stream stream) => Load(stream, null);

    /// <summary>
    /// Additional public overload without optional parameters.
    /// 不含選用參數的公開多載。
    /// </summary>
    public static new PresentationTemplateDocument Load(Stream stream, string? fileName) =>
        Ensure(OdfDocumentFactory.LoadDocument(stream, fileName));

    /// <summary>
    /// Asynchronously loads an OTP presentation template document from the specified stream.
    /// 非同步從指定資料流載入 OTP 簡報範本文件。
    /// </summary>
    /// <returns>A task representing the asynchronous load operation, with the loaded <see cref="PresentationTemplateDocument"/> as its result. / 代表非同步載入作業的工作，其結果為載入完成的 <see cref="PresentationTemplateDocument"/>。</returns>
    public static new Task<PresentationTemplateDocument> LoadAsync(Stream stream) => LoadAsync(stream, null, default);

    /// <summary>
    /// Asynchronously loads the document from a stream with a cancellation token.
    /// 以取消語彙基元非同步從資料流載入文件。
    /// </summary>
    /// <param name="stream">The document stream. / 文件資料流。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消語彙基元。</param>
    /// <returns>A task whose result is the loaded document. / 代表非同步載入作業的工作，其結果為載入完成的文件。</returns>
    public static new Task<PresentationTemplateDocument> LoadAsync(Stream stream, CancellationToken cancellationToken) => LoadAsync(stream, null, cancellationToken);

    /// <summary>
    /// Additional public overload without optional parameters.
    /// 不含選用參數的公開多載。
    /// </summary>
    public static new Task<PresentationTemplateDocument> LoadAsync(Stream stream, string? fileName) => LoadAsync(stream, fileName, default);

    /// <summary>
    /// Additional public overload without optional parameters.
    /// 不含選用參數的公開多載。
    /// </summary>
    public static new async Task<PresentationTemplateDocument> LoadAsync(Stream stream, string? fileName, CancellationToken cancellationToken) =>
        Ensure(await OdfDocumentFactory.LoadDocumentAsync(stream, fileName, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// Creates a new OTP presentation template document from an existing ODP presentation document, preserving its slide content, master pages, and styles.
    /// 從現有的 ODP 簡報文件建立新的 OTP 簡報範本文件，完整保留其投影片內容、母片頁面與樣式。
    /// </summary>
    /// <param name="document">The presentation document used as the template content source. / 作為範本內容來源的簡報文件。</param>
    /// <returns>The created <see cref="PresentationTemplateDocument"/> instance. / 建立完成的 <see cref="PresentationTemplateDocument"/> 執行個體。</returns>
    public static PresentationTemplateDocument CreateFromDocument(PresentationDocument document) =>
        (PresentationTemplateDocument)CreateTemplateFromDocumentInternal(
            document,
            OdfDocumentKind.PresentationTemplate,
            "application/vnd.oasis.opendocument.presentation-template");

    private static PresentationTemplateDocument Ensure(OdfDocument document) =>
        OdfDocumentVariantSupport.EnsureKind<PresentationTemplateDocument>(
            document,
            OdfDocumentKind.PresentationTemplate,
            OdfLocalizer.GetMessage("Err_PresentationTemplateDocument_SpecifiedOdfFileOtp"));
}

/// <summary>
/// Represents an ODF flat XML presentation document (FODP).
/// 表示 ODF 扁平 XML 簡報文件（FODP）。
/// </summary>
public sealed class FlatPresentationDocument : PresentationDocument
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FlatPresentationDocument"/> class.
    /// 初始化 <see cref="FlatPresentationDocument"/> 類別的新執行個體。
    /// </summary>
    /// <param name="package">The ODF package or flat XML container. / ODF 封裝或扁平 XML 容器。</param>
    public FlatPresentationDocument(OdfPackage package) : base(package)
    {
    }

    /// <summary>
    /// Creates a new FODP flat XML presentation document.
    /// 建立新的 FODP 扁平 XML 簡報文件。
    /// </summary>
    /// <returns>A new <see cref="FlatPresentationDocument"/> instance. / 新的 <see cref="FlatPresentationDocument"/> 執行個體。</returns>
    public static new FlatPresentationDocument Create()
    {
        return (FlatPresentationDocument)OdfDocumentFactory.CreateDocument(OdfDocumentKind.FlatPresentation);
    }

    /// <summary>
    /// Loads a FODP flat XML presentation document from the specified path.
    /// 從指定路徑載入 FODP 扁平 XML 簡報文件。
    /// </summary>
    /// <param name="path">The FODP document path. / FODP 文件路徑。</param>
    /// <returns>The loaded <see cref="FlatPresentationDocument"/> instance. / 載入完成的 <see cref="FlatPresentationDocument"/> 執行個體。</returns>
    public static new FlatPresentationDocument Load(string path) =>
        Ensure(OdfDocumentFactory.LoadDocument(path));

    /// <summary>
    /// Asynchronously loads a FODP flat XML presentation document from the specified path.
    /// 非同步從指定路徑載入 FODP 扁平 XML 簡報文件。
    /// </summary>
    /// <returns>A task representing the asynchronous load operation, with the loaded <see cref="FlatPresentationDocument"/> as its result. / 代表非同步載入作業的工作，其結果為載入完成的 <see cref="FlatPresentationDocument"/>。</returns>
    public static new Task<FlatPresentationDocument> LoadAsync(string path) => LoadAsync(path, default);

    /// <summary>
    /// Additional public overload without optional parameters.
    /// 不含選用參數的公開多載。
    /// </summary>
    public static new async Task<FlatPresentationDocument> LoadAsync(string path, CancellationToken cancellationToken) =>
        Ensure(await OdfDocumentFactory.LoadDocumentAsync(path, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// Loads a FODP flat XML presentation document from the specified stream.
    /// 從指定資料流載入 FODP 扁平 XML 簡報文件。
    /// </summary>
    /// <returns>The loaded <see cref="FlatPresentationDocument"/> instance. / 載入完成的 <see cref="FlatPresentationDocument"/> 執行個體。</returns>
    public static new FlatPresentationDocument Load(Stream stream) => Load(stream, null);

    /// <summary>
    /// Additional public overload without optional parameters.
    /// 不含選用參數的公開多載。
    /// </summary>
    public static new FlatPresentationDocument Load(Stream stream, string? fileName) =>
        Ensure(OdfDocumentFactory.LoadDocument(stream, fileName));

    /// <summary>
    /// Asynchronously loads a FODP flat XML presentation document from the specified stream.
    /// 非同步從指定資料流載入 FODP 扁平 XML 簡報文件。
    /// </summary>
    /// <returns>A task representing the asynchronous load operation, with the loaded <see cref="FlatPresentationDocument"/> as its result. / 代表非同步載入作業的工作，其結果為載入完成的 <see cref="FlatPresentationDocument"/>。</returns>
    public static new Task<FlatPresentationDocument> LoadAsync(Stream stream) => LoadAsync(stream, null, default);

    /// <summary>
    /// Asynchronously loads the document from a stream with a cancellation token.
    /// 以取消語彙基元非同步從資料流載入文件。
    /// </summary>
    /// <param name="stream">The document stream. / 文件資料流。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消語彙基元。</param>
    /// <returns>A task whose result is the loaded document. / 代表非同步載入作業的工作，其結果為載入完成的文件。</returns>
    public static new Task<FlatPresentationDocument> LoadAsync(Stream stream, CancellationToken cancellationToken) => LoadAsync(stream, null, cancellationToken);

    /// <summary>
    /// Additional public overload without optional parameters.
    /// 不含選用參數的公開多載。
    /// </summary>
    public static new Task<FlatPresentationDocument> LoadAsync(Stream stream, string? fileName) => LoadAsync(stream, fileName, default);

    /// <summary>
    /// Additional public overload without optional parameters.
    /// 不含選用參數的公開多載。
    /// </summary>
    public static new async Task<FlatPresentationDocument> LoadAsync(Stream stream, string? fileName, CancellationToken cancellationToken) =>
        Ensure(await OdfDocumentFactory.LoadDocumentAsync(stream, fileName, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// Creates an equivalent FODP flat XML presentation document from an existing ODP package presentation document, preserving identical content.
    /// 從現有的 ODP（ZIP 封裝）簡報文件建立等價的 FODP 扁平 XML 簡報文件，內容完全相同。
    /// </summary>
    /// <param name="document">The source ODP presentation document. / 來源 ODP 簡報文件。</param>
    /// <returns>The created <see cref="FlatPresentationDocument"/> instance. / 建立完成的 <see cref="FlatPresentationDocument"/> 執行個體。</returns>
    public static FlatPresentationDocument CreateFromDocument(PresentationDocument document) =>
        (FlatPresentationDocument)ConvertFlatVariantInternal(document, OdfDocumentKind.FlatPresentation, targetIsFlatXml: true);

    private static FlatPresentationDocument Ensure(OdfDocument document) =>
        OdfDocumentVariantSupport.EnsureKind<FlatPresentationDocument>(
            document,
            OdfDocumentKind.FlatPresentation,
            OdfLocalizer.GetMessage("Err_FlatPresentationDocument_SpecifiedOdfFileFodp"));
}
