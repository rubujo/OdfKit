using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OdfKit.Compliance;
using OdfKit.Core;

namespace OdfKit.Chart;

/// <summary>
/// 表示 ODF 圖表範本文件（OTC）。
/// </summary>
public sealed class ChartTemplateDocument : ChartDocument
{
    /// <summary>
    /// 使用指定的 ODF 封裝初始化 <see cref="ChartTemplateDocument"/> 類別的新執行個體。
    /// </summary>
    /// <param name="package">ODF 封裝執行個體。</param>
    public ChartTemplateDocument(OdfPackage package) : base(package)
    {
    }

    /// <summary>
    /// 使用指定的 ODF 封裝與子路徑初始化 <see cref="ChartTemplateDocument"/> 類別的新執行個體。
    /// </summary>
    /// <param name="package">ODF 封裝執行個體。</param>
    /// <param name="subPath">封裝內的子路徑。</param>
    public ChartTemplateDocument(OdfPackage package, string subPath) : base(package, subPath)
    {
    }

    /// <summary>
    /// 建立新的 OTC 圖表範本文件。
    /// </summary>
    /// <returns>新的 <see cref="ChartTemplateDocument"/> 執行個體。</returns>
    public static new ChartTemplateDocument Create()
    {
        return (ChartTemplateDocument)OdfDocumentFactory.CreateDocument(OdfDocumentKind.ChartTemplate);
    }

    /// <summary>
    /// 從指定路徑載入 OTC 圖表範本文件。
    /// </summary>
    /// <param name="path">OTC 文件路徑。</param>
    /// <returns>載入完成的 <see cref="ChartTemplateDocument"/> 執行個體。</returns>
    public static new ChartTemplateDocument Load(string path) =>
        Ensure(OdfDocumentFactory.LoadDocument(path));

    /// <summary>
    /// 非同步從指定路徑載入 OTC 圖表範本文件。
    /// </summary>
    /// <param name="path">OTC 文件路徑。</param>
    /// <param name="cancellationToken">取消語彙基元。</param>
    /// <returns>代表非同步載入作業的工作，其結果為載入完成的 <see cref="ChartTemplateDocument"/>。</returns>
    public static new async Task<ChartTemplateDocument> LoadAsync(string path, CancellationToken cancellationToken = default) =>
        Ensure(await OdfDocumentFactory.LoadDocumentAsync(path, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// 從指定資料流載入 OTC 圖表範本文件。
    /// </summary>
    /// <param name="stream">包含 OTC 文件內容的資料流。</param>
    /// <param name="fileName">選用的檔案名稱，用於輔助格式偵測。</param>
    /// <returns>載入完成的 <see cref="ChartTemplateDocument"/> 執行個體。</returns>
    public static new ChartTemplateDocument Load(Stream stream, string? fileName = null) =>
        Ensure(OdfDocumentFactory.LoadDocument(stream, fileName));

    /// <summary>
    /// 非同步從指定資料流載入 OTC 圖表範本文件。
    /// </summary>
    /// <param name="stream">包含 OTC 文件內容的資料流。</param>
    /// <param name="fileName">選用的檔案名稱，用於輔助格式偵測。</param>
    /// <param name="cancellationToken">取消語彙基元。</param>
    /// <returns>代表非同步載入作業的工作，其結果為載入完成的 <see cref="ChartTemplateDocument"/>。</returns>
    public static new async Task<ChartTemplateDocument> LoadAsync(Stream stream, string? fileName = null, CancellationToken cancellationToken = default) =>
        Ensure(await OdfDocumentFactory.LoadDocumentAsync(stream, fileName, cancellationToken).ConfigureAwait(false));

    private static ChartTemplateDocument Ensure(OdfDocument document) =>
        OdfDocumentVariantSupport.EnsureKind<ChartTemplateDocument>(
            document,
            OdfDocumentKind.ChartTemplate,
            "指定的 ODF 文件不是 OTC 圖表範本。");
}

/// <summary>
/// 表示 ODF 扁平 XML 圖表文件（FODC）。
/// </summary>
public sealed class FlatChartDocument : ChartDocument
{
    /// <summary>
    /// 使用指定的 ODF 封裝初始化 <see cref="FlatChartDocument"/> 類別的新執行個體。
    /// </summary>
    /// <param name="package">ODF 封裝或扁平 XML 容器。</param>
    public FlatChartDocument(OdfPackage package) : base(package)
    {
    }

    /// <summary>
    /// 使用指定的 ODF 封裝與子路徑初始化 <see cref="FlatChartDocument"/> 類別的新執行個體。
    /// </summary>
    /// <param name="package">ODF 封裝或扁平 XML 容器。</param>
    /// <param name="subPath">封裝內的子路徑。</param>
    public FlatChartDocument(OdfPackage package, string subPath) : base(package, subPath)
    {
    }

    /// <summary>
    /// 建立新的 FODC 扁平 XML 圖表文件。
    /// </summary>
    /// <returns>新的 <see cref="FlatChartDocument"/> 執行個體。</returns>
    public static new FlatChartDocument Create()
    {
        return (FlatChartDocument)OdfDocumentFactory.CreateDocument(OdfDocumentKind.FlatChart);
    }

    /// <summary>
    /// 從指定路徑載入 FODC 扁平 XML 圖表文件。
    /// </summary>
    /// <param name="path">FODC 文件路徑。</param>
    /// <returns>載入完成的 <see cref="FlatChartDocument"/> 執行個體。</returns>
    public static new FlatChartDocument Load(string path) =>
        Ensure(OdfDocumentFactory.LoadDocument(path));

    /// <summary>
    /// 非同步從指定路徑載入 FODC 扁平 XML 圖表文件。
    /// </summary>
    /// <param name="path">FODC 文件路徑。</param>
    /// <param name="cancellationToken">取消語彙基元。</param>
    /// <returns>代表非同步載入作業的工作，其結果為載入完成的 <see cref="FlatChartDocument"/>。</returns>
    public static new async Task<FlatChartDocument> LoadAsync(string path, CancellationToken cancellationToken = default) =>
        Ensure(await OdfDocumentFactory.LoadDocumentAsync(path, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// 從指定資料流載入 FODC 扁平 XML 圖表文件。
    /// </summary>
    /// <param name="stream">包含 FODC 文件內容的資料流。</param>
    /// <param name="fileName">選用的檔案名稱，用於輔助格式偵測。</param>
    /// <returns>載入完成的 <see cref="FlatChartDocument"/> 執行個體。</returns>
    public static new FlatChartDocument Load(Stream stream, string? fileName = null) =>
        Ensure(OdfDocumentFactory.LoadDocument(stream, fileName));

    /// <summary>
    /// 非同步從指定資料流載入 FODC 扁平 XML 圖表文件。
    /// </summary>
    /// <param name="stream">包含 FODC 文件內容的資料流。</param>
    /// <param name="fileName">選用的檔案名稱，用於輔助格式偵測。</param>
    /// <param name="cancellationToken">取消語彙基元。</param>
    /// <returns>代表非同步載入作業的工作，其結果為載入完成的 <see cref="FlatChartDocument"/>。</returns>
    public static new async Task<FlatChartDocument> LoadAsync(Stream stream, string? fileName = null, CancellationToken cancellationToken = default) =>
        Ensure(await OdfDocumentFactory.LoadDocumentAsync(stream, fileName, cancellationToken).ConfigureAwait(false));

    private static FlatChartDocument Ensure(OdfDocument document) =>
        OdfDocumentVariantSupport.EnsureKind<FlatChartDocument>(
            document,
            OdfDocumentKind.FlatChart,
            "指定的 ODF 文件不是 FODC 扁平 XML 圖表。");
}
