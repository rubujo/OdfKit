using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OdfKit.Compliance;
using OdfKit.Core;

namespace OdfKit.Drawing;

/// <summary>
/// 表示 ODF 繪圖範本文件（OTG）。
/// </summary>
public sealed class GraphicsTemplateDocument : DrawingDocument
{
    /// <summary>
    /// 初始化 <see cref="GraphicsTemplateDocument"/> 類別的新執行個體。
    /// </summary>
    /// <param name="package">ODF 封裝。</param>
    public GraphicsTemplateDocument(OdfPackage package) : base(package)
    {
    }

    /// <summary>
    /// 建立新的 OTG 繪圖範本文件。
    /// </summary>
    /// <returns>新的 <see cref="GraphicsTemplateDocument"/> 執行個體。</returns>
    public static new GraphicsTemplateDocument Create()
    {
        return (GraphicsTemplateDocument)OdfDocumentFactory.CreateDocument(OdfDocumentKind.GraphicsTemplate);
    }

    /// <summary>
    /// 從指定路徑載入 OTG 繪圖範本文件。
    /// </summary>
    /// <param name="path">OTG 文件路徑。</param>
    /// <returns>載入完成的 <see cref="GraphicsTemplateDocument"/> 執行個體。</returns>
    public static new GraphicsTemplateDocument Load(string path) =>
        Ensure(OdfDocumentFactory.LoadDocument(path));

    /// <summary>
    /// 非同步從指定路徑載入 OTG 繪圖範本文件。
    /// </summary>
    /// <param name="path">OTG 文件路徑。</param>
    /// <param name="cancellationToken">取消語彙基元。</param>
    /// <returns>代表非同步載入作業的工作，其結果為載入完成的 <see cref="GraphicsTemplateDocument"/>。</returns>
    public static new async Task<GraphicsTemplateDocument> LoadAsync(string path, CancellationToken cancellationToken = default) =>
        Ensure(await OdfDocumentFactory.LoadDocumentAsync(path, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// 從指定資料流載入 OTG 繪圖範本文件。
    /// </summary>
    /// <param name="stream">包含 OTG 文件內容的資料流。</param>
    /// <param name="fileName">選用的檔案名稱，用於輔助格式偵測。</param>
    /// <returns>載入完成的 <see cref="GraphicsTemplateDocument"/> 執行個體。</returns>
    public static new GraphicsTemplateDocument Load(Stream stream, string? fileName = null) =>
        Ensure(OdfDocumentFactory.LoadDocument(stream, fileName));

    /// <summary>
    /// 非同步從指定資料流載入 OTG 繪圖範本文件。
    /// </summary>
    /// <param name="stream">包含 OTG 文件內容的資料流。</param>
    /// <param name="fileName">選用的檔案名稱，用於輔助格式偵測。</param>
    /// <param name="cancellationToken">取消語彙基元。</param>
    /// <returns>代表非同步載入作業的工作，其結果為載入完成的 <see cref="GraphicsTemplateDocument"/>。</returns>
    public static new async Task<GraphicsTemplateDocument> LoadAsync(Stream stream, string? fileName = null, CancellationToken cancellationToken = default) =>
        Ensure(await OdfDocumentFactory.LoadDocumentAsync(stream, fileName, cancellationToken).ConfigureAwait(false));

    private static GraphicsTemplateDocument Ensure(OdfDocument document) =>
        OdfDocumentVariantSupport.EnsureKind<GraphicsTemplateDocument>(
            document,
            OdfDocumentKind.GraphicsTemplate,
            "指定的 ODF 文件不是 OTG 繪圖範本。");
}

/// <summary>
/// 表示 ODF 扁平 XML 繪圖文件（FODG）。
/// </summary>
public sealed class FlatGraphicsDocument : DrawingDocument
{
    /// <summary>
    /// 初始化 <see cref="FlatGraphicsDocument"/> 類別的新執行個體。
    /// </summary>
    /// <param name="package">ODF 封裝或扁平 XML 容器。</param>
    public FlatGraphicsDocument(OdfPackage package) : base(package)
    {
    }

    /// <summary>
    /// 建立新的 FODG 扁平 XML 繪圖文件。
    /// </summary>
    /// <returns>新的 <see cref="FlatGraphicsDocument"/> 執行個體。</returns>
    public static new FlatGraphicsDocument Create()
    {
        return (FlatGraphicsDocument)OdfDocumentFactory.CreateDocument(OdfDocumentKind.FlatGraphics);
    }

    /// <summary>
    /// 從指定路徑載入 FODG 扁平 XML 繪圖文件。
    /// </summary>
    /// <param name="path">FODG 文件路徑。</param>
    /// <returns>載入完成的 <see cref="FlatGraphicsDocument"/> 執行個體。</returns>
    public static new FlatGraphicsDocument Load(string path) =>
        Ensure(OdfDocumentFactory.LoadDocument(path));

    /// <summary>
    /// 非同步從指定路徑載入 FODG 扁平 XML 繪圖文件。
    /// </summary>
    /// <param name="path">FODG 文件路徑。</param>
    /// <param name="cancellationToken">取消語彙基元。</param>
    /// <returns>代表非同步載入作業的工作，其結果為載入完成的 <see cref="FlatGraphicsDocument"/>。</returns>
    public static new async Task<FlatGraphicsDocument> LoadAsync(string path, CancellationToken cancellationToken = default) =>
        Ensure(await OdfDocumentFactory.LoadDocumentAsync(path, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// 從指定資料流載入 FODG 扁平 XML 繪圖文件。
    /// </summary>
    /// <param name="stream">包含 FODG 文件內容的資料流。</param>
    /// <param name="fileName">選用的檔案名稱，用於輔助格式偵測。</param>
    /// <returns>載入完成的 <see cref="FlatGraphicsDocument"/> 執行個體。</returns>
    public static new FlatGraphicsDocument Load(Stream stream, string? fileName = null) =>
        Ensure(OdfDocumentFactory.LoadDocument(stream, fileName));

    /// <summary>
    /// 非同步從指定資料流載入 FODG 扁平 XML 繪圖文件。
    /// </summary>
    /// <param name="stream">包含 FODG 文件內容的資料流。</param>
    /// <param name="fileName">選用的檔案名稱，用於輔助格式偵測。</param>
    /// <param name="cancellationToken">取消語彙基元。</param>
    /// <returns>代表非同步載入作業的工作，其結果為載入完成的 <see cref="FlatGraphicsDocument"/>。</returns>
    public static new async Task<FlatGraphicsDocument> LoadAsync(Stream stream, string? fileName = null, CancellationToken cancellationToken = default) =>
        Ensure(await OdfDocumentFactory.LoadDocumentAsync(stream, fileName, cancellationToken).ConfigureAwait(false));

    private static FlatGraphicsDocument Ensure(OdfDocument document) =>
        OdfDocumentVariantSupport.EnsureKind<FlatGraphicsDocument>(
            document,
            OdfDocumentKind.FlatGraphics,
            "指定的 ODF 文件不是 FODG 扁平 XML 繪圖。");
}
