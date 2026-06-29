using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OdfKit.Compliance;
using OdfKit.Core;

namespace OdfKit.Chart;

/// <summary>
/// Represents an ODF chart template document (OTC).
/// 表示 ODF 圖表範本文件（OTC）。
/// </summary>
public sealed class ChartTemplateDocument : ChartDocument
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ChartTemplateDocument"/> class with the specified ODF package.
    /// 使用指定的 ODF 封裝初始化 <see cref="ChartTemplateDocument"/> 類別的新執行個體。
    /// </summary>
    /// <param name="package">The ODF package instance. / ODF 封裝執行個體。</param>
    public ChartTemplateDocument(OdfPackage package) : base(package)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ChartTemplateDocument"/> class with the specified ODF package and sub-path.
    /// 使用指定的 ODF 封裝與子路徑初始化 <see cref="ChartTemplateDocument"/> 類別的新執行個體。
    /// </summary>
    /// <param name="package">The ODF package instance. / ODF 封裝執行個體。</param>
    /// <param name="subPath">The sub-path within the package. / 封裝內的子路徑。</param>
    public ChartTemplateDocument(OdfPackage package, string subPath) : base(package, subPath)
    {
    }

    /// <summary>
    /// Creates a new OTC chart template document.
    /// 建立新的 OTC 圖表範本文件。
    /// </summary>
    /// <returns>A new <see cref="ChartTemplateDocument"/> instance. / 新的 <see cref="ChartTemplateDocument"/> 執行個體。</returns>
    public static new ChartTemplateDocument Create()
    {
        return (ChartTemplateDocument)OdfDocumentFactory.CreateDocument(OdfDocumentKind.ChartTemplate);
    }

    /// <summary>
    /// Loads an OTC chart template document from the specified path.
    /// 從指定路徑載入 OTC 圖表範本文件。
    /// </summary>
    /// <param name="path">The OTC document path. / OTC 文件路徑。</param>
    /// <returns>The loaded <see cref="ChartTemplateDocument"/> instance. / 載入完成的 <see cref="ChartTemplateDocument"/> 執行個體。</returns>
    public static new ChartTemplateDocument Load(string path) =>
        Ensure(OdfDocumentFactory.LoadDocument(path));

    /// <summary>
    /// Asynchronously loads an OTC chart template document from the specified path.
    /// 非同步從指定路徑載入 OTC 圖表範本文件。
    /// </summary>
    /// <param name="path">The OTC document path. / OTC 文件路徑。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消語彙基元。</param>
    /// <returns>A task representing the asynchronous load operation, whose result is the loaded <see cref="ChartTemplateDocument"/>. / 代表非同步載入作業的工作，其結果為載入完成的 <see cref="ChartTemplateDocument"/>。</returns>
    public static new async Task<ChartTemplateDocument> LoadAsync(string path, CancellationToken cancellationToken = default) =>
        Ensure(await OdfDocumentFactory.LoadDocumentAsync(path, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// Loads an OTC chart template document from the specified stream.
    /// 從指定資料流載入 OTC 圖表範本文件。
    /// </summary>
    /// <param name="stream">The stream containing the OTC document content. / 包含 OTC 文件內容的資料流。</param>
    /// <param name="fileName">The optional file name, used to assist format detection. / 選用的檔案名稱，用於輔助格式偵測。</param>
    /// <returns>The loaded <see cref="ChartTemplateDocument"/> instance. / 載入完成的 <see cref="ChartTemplateDocument"/> 執行個體。</returns>
    public static new ChartTemplateDocument Load(Stream stream, string? fileName = null) =>
        Ensure(OdfDocumentFactory.LoadDocument(stream, fileName));

    /// <summary>
    /// Asynchronously loads an OTC chart template document from the specified stream.
    /// 非同步從指定資料流載入 OTC 圖表範本文件。
    /// </summary>
    /// <param name="stream">The stream containing the OTC document content. / 包含 OTC 文件內容的資料流。</param>
    /// <param name="fileName">The optional file name, used to assist format detection. / 選用的檔案名稱，用於輔助格式偵測。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消語彙基元。</param>
    /// <returns>A task representing the asynchronous load operation, whose result is the loaded <see cref="ChartTemplateDocument"/>. / 代表非同步載入作業的工作，其結果為載入完成的 <see cref="ChartTemplateDocument"/>。</returns>
    public static new async Task<ChartTemplateDocument> LoadAsync(Stream stream, string? fileName = null, CancellationToken cancellationToken = default) =>
        Ensure(await OdfDocumentFactory.LoadDocumentAsync(stream, fileName, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// Creates a new OTC chart template document from an existing ODC chart document, fully preserving its chart definition, series, and styles.
    /// 從現有的 ODC 圖表文件建立新的 OTC 圖表範本文件，完整保留其圖表定義、序列與樣式。
    /// </summary>
    /// <param name="document">The chart document used as the template content source. / 作為範本內容來源的圖表文件。</param>
    /// <returns>The created <see cref="ChartTemplateDocument"/> instance. / 建立完成的 <see cref="ChartTemplateDocument"/> 執行個體。</returns>
    public static ChartTemplateDocument CreateFromDocument(ChartDocument document) =>
        (ChartTemplateDocument)CreateTemplateFromDocumentInternal(
            document,
            OdfDocumentKind.ChartTemplate,
            "application/vnd.oasis.opendocument.chart-template");

    private static ChartTemplateDocument Ensure(OdfDocument document) =>
        OdfDocumentVariantSupport.EnsureKind<ChartTemplateDocument>(
            document,
            OdfDocumentKind.ChartTemplate,
            "指定的 ODF 文件不是 OTC 圖表範本。");
}

/// <summary>
/// Represents an ODF flat XML chart document (FODC).
/// 表示 ODF 扁平 XML 圖表文件（FODC）。
/// </summary>
public sealed class FlatChartDocument : ChartDocument
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FlatChartDocument"/> class with the specified ODF package.
    /// 使用指定的 ODF 封裝初始化 <see cref="FlatChartDocument"/> 類別的新執行個體。
    /// </summary>
    /// <param name="package">The ODF package or flat XML container. / ODF 封裝或扁平 XML 容器。</param>
    public FlatChartDocument(OdfPackage package) : base(package)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FlatChartDocument"/> class with the specified ODF package and sub-path.
    /// 使用指定的 ODF 封裝與子路徑初始化 <see cref="FlatChartDocument"/> 類別的新執行個體。
    /// </summary>
    /// <param name="package">The ODF package or flat XML container. / ODF 封裝或扁平 XML 容器。</param>
    /// <param name="subPath">The sub-path within the package. / 封裝內的子路徑。</param>
    public FlatChartDocument(OdfPackage package, string subPath) : base(package, subPath)
    {
    }

    /// <summary>
    /// Creates a new FODC flat XML chart document.
    /// 建立新的 FODC 扁平 XML 圖表文件。
    /// </summary>
    /// <returns>A new <see cref="FlatChartDocument"/> instance. / 新的 <see cref="FlatChartDocument"/> 執行個體。</returns>
    public static new FlatChartDocument Create()
    {
        return (FlatChartDocument)OdfDocumentFactory.CreateDocument(OdfDocumentKind.FlatChart);
    }

    /// <summary>
    /// Loads a FODC flat XML chart document from the specified path.
    /// 從指定路徑載入 FODC 扁平 XML 圖表文件。
    /// </summary>
    /// <param name="path">The FODC document path. / FODC 文件路徑。</param>
    /// <returns>The loaded <see cref="FlatChartDocument"/> instance. / 載入完成的 <see cref="FlatChartDocument"/> 執行個體。</returns>
    public static new FlatChartDocument Load(string path) =>
        Ensure(OdfDocumentFactory.LoadDocument(path));

    /// <summary>
    /// Asynchronously loads a FODC flat XML chart document from the specified path.
    /// 非同步從指定路徑載入 FODC 扁平 XML 圖表文件。
    /// </summary>
    /// <param name="path">The FODC document path. / FODC 文件路徑。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消語彙基元。</param>
    /// <returns>A task representing the asynchronous load operation, whose result is the loaded <see cref="FlatChartDocument"/>. / 代表非同步載入作業的工作，其結果為載入完成的 <see cref="FlatChartDocument"/>。</returns>
    public static new async Task<FlatChartDocument> LoadAsync(string path, CancellationToken cancellationToken = default) =>
        Ensure(await OdfDocumentFactory.LoadDocumentAsync(path, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// Loads a FODC flat XML chart document from the specified stream.
    /// 從指定資料流載入 FODC 扁平 XML 圖表文件。
    /// </summary>
    /// <param name="stream">The stream containing the FODC document content. / 包含 FODC 文件內容的資料流。</param>
    /// <param name="fileName">The optional file name, used to assist format detection. / 選用的檔案名稱，用於輔助格式偵測。</param>
    /// <returns>The loaded <see cref="FlatChartDocument"/> instance. / 載入完成的 <see cref="FlatChartDocument"/> 執行個體。</returns>
    public static new FlatChartDocument Load(Stream stream, string? fileName = null) =>
        Ensure(OdfDocumentFactory.LoadDocument(stream, fileName));

    /// <summary>
    /// Asynchronously loads a FODC flat XML chart document from the specified stream.
    /// 非同步從指定資料流載入 FODC 扁平 XML 圖表文件。
    /// </summary>
    /// <param name="stream">The stream containing the FODC document content. / 包含 FODC 文件內容的資料流。</param>
    /// <param name="fileName">The optional file name, used to assist format detection. / 選用的檔案名稱，用於輔助格式偵測。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消語彙基元。</param>
    /// <returns>A task representing the asynchronous load operation, whose result is the loaded <see cref="FlatChartDocument"/>. / 代表非同步載入作業的工作，其結果為載入完成的 <see cref="FlatChartDocument"/>。</returns>
    public static new async Task<FlatChartDocument> LoadAsync(Stream stream, string? fileName = null, CancellationToken cancellationToken = default) =>
        Ensure(await OdfDocumentFactory.LoadDocumentAsync(stream, fileName, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// Creates an equivalent FODC flat XML chart document from an existing ODC (ZIP package) chart document, with identical content.
    /// 從現有的 ODC（ZIP 封裝）圖表文件建立等價的 FODC 扁平 XML 圖表文件，內容完全相同。
    /// </summary>
    /// <param name="document">The source ODC chart document. / 來源 ODC 圖表文件。</param>
    /// <returns>The created <see cref="FlatChartDocument"/> instance. / 建立完成的 <see cref="FlatChartDocument"/> 執行個體。</returns>
    public static FlatChartDocument CreateFromDocument(ChartDocument document) =>
        (FlatChartDocument)ConvertFlatVariantInternal(document, OdfDocumentKind.FlatChart, targetIsFlatXml: true);

    private static FlatChartDocument Ensure(OdfDocument document) =>
        OdfDocumentVariantSupport.EnsureKind<FlatChartDocument>(
            document,
            OdfDocumentKind.FlatChart,
            "指定的 ODF 文件不是 FODC 扁平 XML 圖表。");
}
