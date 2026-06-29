using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OdfKit.Compliance;
using OdfKit.Core;

namespace OdfKit.Drawing;

/// <summary>
/// Represents an ODF drawing template document (OTG).
/// 表示 ODF 繪圖範本文件（OTG）。
/// </summary>
public sealed class GraphicsTemplateDocument : DrawingDocument
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GraphicsTemplateDocument"/> class.
    /// 初始化 <see cref="GraphicsTemplateDocument"/> 類別的新執行個體。
    /// </summary>
    /// <param name="package">The ODF package. / ODF 封裝。</param>
    public GraphicsTemplateDocument(OdfPackage package) : base(package)
    {
    }

    /// <summary>
    /// Creates a new OTG drawing template document.
    /// 建立新的 OTG 繪圖範本文件。
    /// </summary>
    /// <returns>A new <see cref="GraphicsTemplateDocument"/> instance. / 新的 <see cref="GraphicsTemplateDocument"/> 執行個體。</returns>
    public static new GraphicsTemplateDocument Create()
    {
        return (GraphicsTemplateDocument)OdfDocumentFactory.CreateDocument(OdfDocumentKind.GraphicsTemplate);
    }

    /// <summary>
    /// Loads an OTG drawing template document from the specified path.
    /// 從指定路徑載入 OTG 繪圖範本文件。
    /// </summary>
    /// <param name="path">The OTG document path. / OTG 文件路徑。</param>
    /// <returns>The loaded <see cref="GraphicsTemplateDocument"/> instance. / 載入完成的 <see cref="GraphicsTemplateDocument"/> 執行個體。</returns>
    public static new GraphicsTemplateDocument Load(string path) =>
        Ensure(OdfDocumentFactory.LoadDocument(path));

    /// <summary>
    /// Asynchronously loads an OTG drawing template document from the specified path.
    /// 非同步從指定路徑載入 OTG 繪圖範本文件。
    /// </summary>
    /// <param name="path">The OTG document path. / OTG 文件路徑。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消語彙基元。</param>
    /// <returns>A task representing the asynchronous load operation, whose result is the loaded <see cref="GraphicsTemplateDocument"/>. / 代表非同步載入作業的工作，其結果為載入完成的 <see cref="GraphicsTemplateDocument"/>。</returns>
    public static new async Task<GraphicsTemplateDocument> LoadAsync(string path, CancellationToken cancellationToken = default) =>
        Ensure(await OdfDocumentFactory.LoadDocumentAsync(path, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// Loads an OTG drawing template document from the specified stream.
    /// 從指定資料流載入 OTG 繪圖範本文件。
    /// </summary>
    /// <param name="stream">The stream containing the OTG document content. / 包含 OTG 文件內容的資料流。</param>
    /// <param name="fileName">The optional file name, used to assist format detection. / 選用的檔案名稱，用於輔助格式偵測。</param>
    /// <returns>The loaded <see cref="GraphicsTemplateDocument"/> instance. / 載入完成的 <see cref="GraphicsTemplateDocument"/> 執行個體。</returns>
    public static new GraphicsTemplateDocument Load(Stream stream, string? fileName = null) =>
        Ensure(OdfDocumentFactory.LoadDocument(stream, fileName));

    /// <summary>
    /// Asynchronously loads an OTG drawing template document from the specified stream.
    /// 非同步從指定資料流載入 OTG 繪圖範本文件。
    /// </summary>
    /// <param name="stream">The stream containing the OTG document content. / 包含 OTG 文件內容的資料流。</param>
    /// <param name="fileName">The optional file name, used to assist format detection. / 選用的檔案名稱，用於輔助格式偵測。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消語彙基元。</param>
    /// <returns>A task representing the asynchronous load operation, whose result is the loaded <see cref="GraphicsTemplateDocument"/>. / 代表非同步載入作業的工作，其結果為載入完成的 <see cref="GraphicsTemplateDocument"/>。</returns>
    public static new async Task<GraphicsTemplateDocument> LoadAsync(Stream stream, string? fileName = null, CancellationToken cancellationToken = default) =>
        Ensure(await OdfDocumentFactory.LoadDocumentAsync(stream, fileName, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// Creates a new OTG drawing template document from an existing ODG drawing document, fully preserving its page content, shapes, and styles.
    /// 從現有的 ODG 繪圖文件建立新的 OTG 繪圖範本文件，完整保留其頁面內容、形狀與樣式。
    /// </summary>
    /// <param name="document">The drawing document used as the template content source. / 作為範本內容來源的繪圖文件。</param>
    /// <returns>The created <see cref="GraphicsTemplateDocument"/> instance. / 建立完成的 <see cref="GraphicsTemplateDocument"/> 執行個體。</returns>
    public static GraphicsTemplateDocument CreateFromDocument(DrawingDocument document) =>
        (GraphicsTemplateDocument)CreateTemplateFromDocumentInternal(
            document,
            OdfDocumentKind.GraphicsTemplate,
            "application/vnd.oasis.opendocument.graphics-template");

    private static GraphicsTemplateDocument Ensure(OdfDocument document) =>
        OdfDocumentVariantSupport.EnsureKind<GraphicsTemplateDocument>(
            document,
            OdfDocumentKind.GraphicsTemplate,
            "指定的 ODF 文件不是 OTG 繪圖範本。");
}

/// <summary>
/// Represents an ODF flat XML drawing document (FODG).
/// 表示 ODF 扁平 XML 繪圖文件（FODG）。
/// </summary>
public sealed class FlatGraphicsDocument : DrawingDocument
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FlatGraphicsDocument"/> class.
    /// 初始化 <see cref="FlatGraphicsDocument"/> 類別的新執行個體。
    /// </summary>
    /// <param name="package">The ODF package or flat XML container. / ODF 封裝或扁平 XML 容器。</param>
    public FlatGraphicsDocument(OdfPackage package) : base(package)
    {
    }

    /// <summary>
    /// Creates a new FODG flat XML drawing document.
    /// 建立新的 FODG 扁平 XML 繪圖文件。
    /// </summary>
    /// <returns>A new <see cref="FlatGraphicsDocument"/> instance. / 新的 <see cref="FlatGraphicsDocument"/> 執行個體。</returns>
    public static new FlatGraphicsDocument Create()
    {
        return (FlatGraphicsDocument)OdfDocumentFactory.CreateDocument(OdfDocumentKind.FlatGraphics);
    }

    /// <summary>
    /// Loads a FODG flat XML drawing document from the specified path.
    /// 從指定路徑載入 FODG 扁平 XML 繪圖文件。
    /// </summary>
    /// <param name="path">The FODG document path. / FODG 文件路徑。</param>
    /// <returns>The loaded <see cref="FlatGraphicsDocument"/> instance. / 載入完成的 <see cref="FlatGraphicsDocument"/> 執行個體。</returns>
    public static new FlatGraphicsDocument Load(string path) =>
        Ensure(OdfDocumentFactory.LoadDocument(path));

    /// <summary>
    /// Asynchronously loads a FODG flat XML drawing document from the specified path.
    /// 非同步從指定路徑載入 FODG 扁平 XML 繪圖文件。
    /// </summary>
    /// <param name="path">The FODG document path. / FODG 文件路徑。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消語彙基元。</param>
    /// <returns>A task representing the asynchronous load operation, whose result is the loaded <see cref="FlatGraphicsDocument"/>. / 代表非同步載入作業的工作，其結果為載入完成的 <see cref="FlatGraphicsDocument"/>。</returns>
    public static new async Task<FlatGraphicsDocument> LoadAsync(string path, CancellationToken cancellationToken = default) =>
        Ensure(await OdfDocumentFactory.LoadDocumentAsync(path, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// Loads a FODG flat XML drawing document from the specified stream.
    /// 從指定資料流載入 FODG 扁平 XML 繪圖文件。
    /// </summary>
    /// <param name="stream">The stream containing the FODG document content. / 包含 FODG 文件內容的資料流。</param>
    /// <param name="fileName">The optional file name, used to assist format detection. / 選用的檔案名稱，用於輔助格式偵測。</param>
    /// <returns>The loaded <see cref="FlatGraphicsDocument"/> instance. / 載入完成的 <see cref="FlatGraphicsDocument"/> 執行個體。</returns>
    public static new FlatGraphicsDocument Load(Stream stream, string? fileName = null) =>
        Ensure(OdfDocumentFactory.LoadDocument(stream, fileName));

    /// <summary>
    /// Asynchronously loads a FODG flat XML drawing document from the specified stream.
    /// 非同步從指定資料流載入 FODG 扁平 XML 繪圖文件。
    /// </summary>
    /// <param name="stream">The stream containing the FODG document content. / 包含 FODG 文件內容的資料流。</param>
    /// <param name="fileName">The optional file name, used to assist format detection. / 選用的檔案名稱，用於輔助格式偵測。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消語彙基元。</param>
    /// <returns>A task representing the asynchronous load operation, whose result is the loaded <see cref="FlatGraphicsDocument"/>. / 代表非同步載入作業的工作，其結果為載入完成的 <see cref="FlatGraphicsDocument"/>。</returns>
    public static new async Task<FlatGraphicsDocument> LoadAsync(Stream stream, string? fileName = null, CancellationToken cancellationToken = default) =>
        Ensure(await OdfDocumentFactory.LoadDocumentAsync(stream, fileName, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// Creates an equivalent FODG flat XML drawing document from an existing ODG (ZIP package) drawing document, with identical content.
    /// 從現有的 ODG（ZIP 封裝）繪圖文件建立等價的 FODG 扁平 XML 繪圖文件，內容完全相同。
    /// </summary>
    /// <param name="document">The source ODG drawing document. / 來源 ODG 繪圖文件。</param>
    /// <returns>The created <see cref="FlatGraphicsDocument"/> instance. / 建立完成的 <see cref="FlatGraphicsDocument"/> 執行個體。</returns>
    public static FlatGraphicsDocument CreateFromDocument(DrawingDocument document) =>
        (FlatGraphicsDocument)ConvertFlatVariantInternal(document, OdfDocumentKind.FlatGraphics, targetIsFlatXml: true);

    private static FlatGraphicsDocument Ensure(OdfDocument document) =>
        OdfDocumentVariantSupport.EnsureKind<FlatGraphicsDocument>(
            document,
            OdfDocumentKind.FlatGraphics,
            "指定的 ODF 文件不是 FODG 扁平 XML 繪圖。");
}
