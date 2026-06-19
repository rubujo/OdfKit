using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OdfKit.Compliance;
using OdfKit.Core;

namespace OdfKit.Image;

/// <summary>
/// 表示 ODF 影像範本文件（OTI）。
/// </summary>
public sealed class ImageTemplateDocument : OdfImageDocument
{
    /// <summary>
    /// 使用指定的 ODF 封裝初始化 <see cref="ImageTemplateDocument"/> 類別的新執行個體。
    /// </summary>
    /// <param name="package">ODF 封裝或扁平 XML 容器。</param>
    public ImageTemplateDocument(OdfPackage package) : base(package)
    {
    }

    /// <summary>
    /// 建立新的 OTI 影像範本文件。
    /// </summary>
    /// <returns>新的 <see cref="ImageTemplateDocument"/> 執行個體。</returns>
    public static new ImageTemplateDocument Create()
    {
        return (ImageTemplateDocument)OdfDocumentFactory.CreateDocument(OdfDocumentKind.ImageTemplate);
    }

    /// <summary>
    /// 從指定路徑載入 OTI 影像範本文件。
    /// </summary>
    /// <param name="path">OTI 文件路徑。</param>
    /// <returns>載入完成的 <see cref="ImageTemplateDocument"/> 執行個體。</returns>
    public static new ImageTemplateDocument Load(string path) =>
        Ensure(OdfDocumentFactory.LoadDocument(path));

    /// <summary>
    /// 非同步從指定路徑載入 OTI 影像範本文件。
    /// </summary>
    /// <param name="path">OTI 文件路徑。</param>
    /// <param name="cancellationToken">取消語彙基元。</param>
    /// <returns>代表非同步載入作業的工作，其結果為載入完成的 <see cref="ImageTemplateDocument"/>。</returns>
    public static new async Task<ImageTemplateDocument> LoadAsync(string path, CancellationToken cancellationToken = default) =>
        Ensure(await OdfDocumentFactory.LoadDocumentAsync(path, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// 從指定資料流載入 OTI 影像範本文件。
    /// </summary>
    /// <param name="stream">包含 OTI 文件內容的資料流。</param>
    /// <param name="fileName">選用的檔案名稱，用於輔助格式偵測。</param>
    /// <returns>載入完成的 <see cref="ImageTemplateDocument"/> 執行個體。</returns>
    public static new ImageTemplateDocument Load(Stream stream, string? fileName = null) =>
        Ensure(OdfDocumentFactory.LoadDocument(stream, fileName));

    /// <summary>
    /// 非同步從指定資料流載入 OTI 影像範本文件。
    /// </summary>
    /// <param name="stream">包含 OTI 文件內容的資料流。</param>
    /// <param name="fileName">選用的檔案名稱，用於輔助格式偵測。</param>
    /// <param name="cancellationToken">取消語彙基元。</param>
    /// <returns>代表非同步載入作業的工作，其結果為載入完成的 <see cref="ImageTemplateDocument"/>。</returns>
    public static new async Task<ImageTemplateDocument> LoadAsync(Stream stream, string? fileName = null, CancellationToken cancellationToken = default) =>
        Ensure(await OdfDocumentFactory.LoadDocumentAsync(stream, fileName, cancellationToken).ConfigureAwait(false));

    private static ImageTemplateDocument Ensure(OdfDocument document) =>
        OdfDocumentVariantSupport.EnsureKind<ImageTemplateDocument>(
            document,
            OdfDocumentKind.ImageTemplate,
            "指定的 ODF 文件不是 OTI 影像範本。");
}

/// <summary>
/// 表示 ODF 扁平 XML 影像文件（FODI）。
/// </summary>
public sealed class FlatImageDocument : OdfImageDocument
{
    /// <summary>
    /// 使用指定的 ODF 封裝初始化 <see cref="FlatImageDocument"/> 類別的新執行個體。
    /// </summary>
    /// <param name="package">ODF 封裝或扁平 XML 容器。</param>
    public FlatImageDocument(OdfPackage package) : base(package)
    {
    }

    /// <summary>
    /// 建立新的 FODI 扁平 XML 影像文件。
    /// </summary>
    /// <returns>新的 <see cref="FlatImageDocument"/> 執行個體。</returns>
    public static new FlatImageDocument Create()
    {
        return (FlatImageDocument)OdfDocumentFactory.CreateDocument(OdfDocumentKind.FlatImage);
    }

    /// <summary>
    /// 從指定路徑載入 FODI 扁平 XML 影像文件。
    /// </summary>
    /// <param name="path">FODI 文件路徑。</param>
    /// <returns>載入完成的 <see cref="FlatImageDocument"/> 執行個體。</returns>
    public static new FlatImageDocument Load(string path) =>
        Ensure(OdfDocumentFactory.LoadDocument(path));

    /// <summary>
    /// 非同步從指定路徑載入 FODI 扁平 XML 影像文件。
    /// </summary>
    /// <param name="path">FODI 文件路徑。</param>
    /// <param name="cancellationToken">取消語彙基元。</param>
    /// <returns>代表非同步載入作業的工作，其結果為載入完成的 <see cref="FlatImageDocument"/>。</returns>
    public static new async Task<FlatImageDocument> LoadAsync(string path, CancellationToken cancellationToken = default) =>
        Ensure(await OdfDocumentFactory.LoadDocumentAsync(path, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// 從指定資料流載入 FODI 扁平 XML 影像文件。
    /// </summary>
    /// <param name="stream">包含 FODI 文件內容的資料流。</param>
    /// <param name="fileName">選用的檔案名稱，用於輔助格式偵測。</param>
    /// <returns>載入完成的 <see cref="FlatImageDocument"/> 執行個體。</returns>
    public static new FlatImageDocument Load(Stream stream, string? fileName = null) =>
        Ensure(OdfDocumentFactory.LoadDocument(stream, fileName));

    /// <summary>
    /// 非同步從指定資料流載入 FODI 扁平 XML 影像文件。
    /// </summary>
    /// <param name="stream">包含 FODI 文件內容的資料流。</param>
    /// <param name="fileName">選用的檔案名稱，用於輔助格式偵測。</param>
    /// <param name="cancellationToken">取消語彙基元。</param>
    /// <returns>代表非同步載入作業的工作，其結果為載入完成的 <see cref="FlatImageDocument"/>。</returns>
    public static new async Task<FlatImageDocument> LoadAsync(Stream stream, string? fileName = null, CancellationToken cancellationToken = default) =>
        Ensure(await OdfDocumentFactory.LoadDocumentAsync(stream, fileName, cancellationToken).ConfigureAwait(false));

    private static FlatImageDocument Ensure(OdfDocument document) =>
        OdfDocumentVariantSupport.EnsureKind<FlatImageDocument>(
            document,
            OdfDocumentKind.FlatImage,
            "指定的 ODF 文件不是 FODI 扁平 XML 影像。");
}
