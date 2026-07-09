using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OdfKit.Compliance;
using OdfKit.Core;

namespace OdfKit.Image;

/// <summary>
/// Represents an ODF image template document (OTI).
/// 表示 ODF 影像範本文件（OTI）。
/// </summary>
public sealed class ImageTemplateDocument : OdfImageDocument
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ImageTemplateDocument"/> class with the specified ODF package.
    /// 使用指定的 ODF 封裝初始化 <see cref="ImageTemplateDocument"/> 類別的新執行個體。
    /// </summary>
    /// <param name="package">The ODF package or flat XML container. / ODF 封裝或扁平 XML 容器。</param>
    public ImageTemplateDocument(OdfPackage package) : base(package)
    {
    }

    /// <summary>
    /// Creates a new OTI image template document.
    /// 建立新的 OTI 影像範本文件。
    /// </summary>
    /// <returns>A new <see cref="ImageTemplateDocument"/> instance. / 新的 <see cref="ImageTemplateDocument"/> 執行個體。</returns>
    public static new ImageTemplateDocument Create()
    {
        return (ImageTemplateDocument)OdfDocumentFactory.CreateDocument(OdfDocumentKind.ImageTemplate);
    }

    /// <summary>
    /// Loads an OTI image template document from the specified path.
    /// 從指定路徑載入 OTI 影像範本文件。
    /// </summary>
    /// <param name="path">The OTI document path. / OTI 文件路徑。</param>
    /// <returns>The loaded <see cref="ImageTemplateDocument"/> instance. / 載入完成的 <see cref="ImageTemplateDocument"/> 執行個體。</returns>
    public static new ImageTemplateDocument Load(string path) =>
        Ensure(OdfDocumentFactory.LoadDocument(path));

    /// <summary>
    /// Asynchronously loads an OTI image template document from the specified path.
    /// 非同步從指定路徑載入 OTI 影像範本文件。
    /// </summary>
    /// <returns>A task representing the asynchronous load operation, whose result is the loaded <see cref="ImageTemplateDocument"/>. / 代表非同步載入作業的工作，其結果為載入完成的 <see cref="ImageTemplateDocument"/>。</returns>
    public static new Task<ImageTemplateDocument> LoadAsync(string path) => LoadAsync(path, default);

    /// <summary>
    /// Short overload of LoadAsync that accepts path and cancellationToken; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 path 與 cancellationToken；其餘可選參數使用預設值並轉呼叫最長 LoadAsync 多載。
    /// </summary>
    public static new async Task<ImageTemplateDocument> LoadAsync(string path, CancellationToken cancellationToken) =>
        Ensure(await OdfDocumentFactory.LoadDocumentAsync(path, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// Loads an OTI image template document from the specified stream.
    /// 從指定資料流載入 OTI 影像範本文件。
    /// </summary>
    /// <returns>The loaded <see cref="ImageTemplateDocument"/> instance. / 載入完成的 <see cref="ImageTemplateDocument"/> 執行個體。</returns>
    public static new ImageTemplateDocument Load(Stream stream) => Load(stream, null);

    /// <summary>
    /// Short overload of Load that accepts stream and fileName; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 stream 與 fileName；其餘可選參數使用預設值並轉呼叫最長 Load 多載。
    /// </summary>
    public static new ImageTemplateDocument Load(Stream stream, string? fileName) =>
        Ensure(OdfDocumentFactory.LoadDocument(stream, fileName));

    /// <summary>
    /// Asynchronously loads an OTI image template document from the specified stream.
    /// 非同步從指定資料流載入 OTI 影像範本文件。
    /// </summary>
    /// <returns>A task representing the asynchronous load operation, whose result is the loaded <see cref="ImageTemplateDocument"/>. / 代表非同步載入作業的工作，其結果為載入完成的 <see cref="ImageTemplateDocument"/>。</returns>
    public static new Task<ImageTemplateDocument> LoadAsync(Stream stream) => LoadAsync(stream, null, default);

    /// <summary>
    /// Asynchronously loads the document from a stream with a cancellation token.
    /// 以取消語彙基元非同步從資料流載入文件。
    /// </summary>
    /// <param name="stream">The document stream. / 文件資料流。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消語彙基元。</param>
    /// <returns>A task whose result is the loaded document. / 代表非同步載入作業的工作，其結果為載入完成的文件。</returns>
    public static new Task<ImageTemplateDocument> LoadAsync(Stream stream, CancellationToken cancellationToken) => LoadAsync(stream, null, cancellationToken);

    /// <summary>
    /// Short overload of LoadAsync that accepts stream and fileName; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 stream 與 fileName；其餘可選參數使用預設值並轉呼叫最長 LoadAsync 多載。
    /// </summary>
    public static new Task<ImageTemplateDocument> LoadAsync(Stream stream, string? fileName) => LoadAsync(stream, fileName, default);

    /// <summary>
    /// Short overload of LoadAsync that accepts stream, fileName, and cancellationToken; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 stream、fileName 與 cancellationToken；其餘可選參數使用預設值並轉呼叫最長 LoadAsync 多載。
    /// </summary>
    public static new async Task<ImageTemplateDocument> LoadAsync(Stream stream, string? fileName, CancellationToken cancellationToken) =>
        Ensure(await OdfDocumentFactory.LoadDocumentAsync(stream, fileName, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// Creates a new OTI image template document from an existing ODI image document, fully preserving its image frame content.
    /// 從現有的 ODI 影像文件建立新的 OTI 影像範本文件，完整保留其影像框架內容。
    /// </summary>
    /// <param name="document">The image document used as the template content source. / 作為範本內容來源的影像文件。</param>
    /// <returns>The created <see cref="ImageTemplateDocument"/> instance. / 建立完成的 <see cref="ImageTemplateDocument"/> 執行個體。</returns>
    public static ImageTemplateDocument CreateFromDocument(OdfImageDocument document) =>
        (ImageTemplateDocument)CreateTemplateFromDocumentInternal(
            document,
            OdfDocumentKind.ImageTemplate,
            "application/vnd.oasis.opendocument.image-template");

    private static ImageTemplateDocument Ensure(OdfDocument document) =>
        OdfDocumentVariantSupport.EnsureKind<ImageTemplateDocument>(
            document,
            OdfDocumentKind.ImageTemplate,
            "指定的 ODF 文件不是 OTI 影像範本。");
}

/// <summary>
/// Represents an ODF flat XML image document (FODI).
/// 表示 ODF 扁平 XML 影像文件（FODI）。
/// </summary>
public sealed class FlatImageDocument : OdfImageDocument
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FlatImageDocument"/> class with the specified ODF package.
    /// 使用指定的 ODF 封裝初始化 <see cref="FlatImageDocument"/> 類別的新執行個體。
    /// </summary>
    /// <param name="package">The ODF package or flat XML container. / ODF 封裝或扁平 XML 容器。</param>
    public FlatImageDocument(OdfPackage package) : base(package)
    {
    }

    /// <summary>
    /// Creates a new FODI flat XML image document.
    /// 建立新的 FODI 扁平 XML 影像文件。
    /// </summary>
    /// <returns>A new <see cref="FlatImageDocument"/> instance. / 新的 <see cref="FlatImageDocument"/> 執行個體。</returns>
    public static new FlatImageDocument Create()
    {
        return (FlatImageDocument)OdfDocumentFactory.CreateDocument(OdfDocumentKind.FlatImage);
    }

    /// <summary>
    /// Loads a FODI flat XML image document from the specified path.
    /// 從指定路徑載入 FODI 扁平 XML 影像文件。
    /// </summary>
    /// <param name="path">The FODI document path. / FODI 文件路徑。</param>
    /// <returns>The loaded <see cref="FlatImageDocument"/> instance. / 載入完成的 <see cref="FlatImageDocument"/> 執行個體。</returns>
    public static new FlatImageDocument Load(string path) =>
        Ensure(OdfDocumentFactory.LoadDocument(path));

    /// <summary>
    /// Asynchronously loads a FODI flat XML image document from the specified path.
    /// 非同步從指定路徑載入 FODI 扁平 XML 影像文件。
    /// </summary>
    /// <returns>A task representing the asynchronous load operation, whose result is the loaded <see cref="FlatImageDocument"/>. / 代表非同步載入作業的工作，其結果為載入完成的 <see cref="FlatImageDocument"/>。</returns>
    public static new Task<FlatImageDocument> LoadAsync(string path) => LoadAsync(path, default);

    /// <summary>
    /// Short overload of LoadAsync that accepts path and cancellationToken; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 path 與 cancellationToken；其餘可選參數使用預設值並轉呼叫最長 LoadAsync 多載。
    /// </summary>
    public static new async Task<FlatImageDocument> LoadAsync(string path, CancellationToken cancellationToken) =>
        Ensure(await OdfDocumentFactory.LoadDocumentAsync(path, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// Loads a FODI flat XML image document from the specified stream.
    /// 從指定資料流載入 FODI 扁平 XML 影像文件。
    /// </summary>
    /// <returns>The loaded <see cref="FlatImageDocument"/> instance. / 載入完成的 <see cref="FlatImageDocument"/> 執行個體。</returns>
    public static new FlatImageDocument Load(Stream stream) => Load(stream, null);

    /// <summary>
    /// Short overload of Load that accepts stream and fileName; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 stream 與 fileName；其餘可選參數使用預設值並轉呼叫最長 Load 多載。
    /// </summary>
    public static new FlatImageDocument Load(Stream stream, string? fileName) =>
        Ensure(OdfDocumentFactory.LoadDocument(stream, fileName));

    /// <summary>
    /// Asynchronously loads a FODI flat XML image document from the specified stream.
    /// 非同步從指定資料流載入 FODI 扁平 XML 影像文件。
    /// </summary>
    /// <returns>A task representing the asynchronous load operation, whose result is the loaded <see cref="FlatImageDocument"/>. / 代表非同步載入作業的工作，其結果為載入完成的 <see cref="FlatImageDocument"/>。</returns>
    public static new Task<FlatImageDocument> LoadAsync(Stream stream) => LoadAsync(stream, null, default);

    /// <summary>
    /// Asynchronously loads the document from a stream with a cancellation token.
    /// 以取消語彙基元非同步從資料流載入文件。
    /// </summary>
    /// <param name="stream">The document stream. / 文件資料流。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消語彙基元。</param>
    /// <returns>A task whose result is the loaded document. / 代表非同步載入作業的工作，其結果為載入完成的文件。</returns>
    public static new Task<FlatImageDocument> LoadAsync(Stream stream, CancellationToken cancellationToken) => LoadAsync(stream, null, cancellationToken);

    /// <summary>
    /// Short overload of LoadAsync that accepts stream and fileName; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 stream 與 fileName；其餘可選參數使用預設值並轉呼叫最長 LoadAsync 多載。
    /// </summary>
    public static new Task<FlatImageDocument> LoadAsync(Stream stream, string? fileName) => LoadAsync(stream, fileName, default);

    /// <summary>
    /// Short overload of LoadAsync that accepts stream, fileName, and cancellationToken; remaining optional parameters use defaults and forward to the full overload.
    /// 便利多載：提供 stream、fileName 與 cancellationToken；其餘可選參數使用預設值並轉呼叫最長 LoadAsync 多載。
    /// </summary>
    public static new async Task<FlatImageDocument> LoadAsync(Stream stream, string? fileName, CancellationToken cancellationToken) =>
        Ensure(await OdfDocumentFactory.LoadDocumentAsync(stream, fileName, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// Creates an equivalent FODI flat XML image document from an existing ODI (ZIP package) image document, with identical content.
    /// 從現有的 ODI（ZIP 封裝）影像文件建立等價的 FODI 扁平 XML 影像文件，內容完全相同。
    /// </summary>
    /// <param name="document">The source ODI image document. / 來源 ODI 影像文件。</param>
    /// <returns>The created <see cref="FlatImageDocument"/> instance. / 建立完成的 <see cref="FlatImageDocument"/> 執行個體。</returns>
    public static FlatImageDocument CreateFromDocument(OdfImageDocument document) =>
        (FlatImageDocument)ConvertFlatVariantInternal(document, OdfDocumentKind.FlatImage, targetIsFlatXml: true);

    private static FlatImageDocument Ensure(OdfDocument document) =>
        OdfDocumentVariantSupport.EnsureKind<FlatImageDocument>(
            document,
            OdfDocumentKind.FlatImage,
            "指定的 ODF 文件不是 FODI 扁平 XML 影像。");
}
