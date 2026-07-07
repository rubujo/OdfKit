using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OdfKit.Compliance;
using OdfKit.Core;

namespace OdfKit.Image;

/// <summary>
/// Represents a high-level ODF image document.
/// 表示高階 ODF 影像文件（Image Document）的類別。
/// </summary>
public class ImageDocument : OdfImageDocument
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ImageDocument"/> class with the specified ODF package.
    /// 使用指定的 ODF 封裝初始化 <see cref="ImageDocument"/> 類別的新執行個體。
    /// </summary>
    /// <param name="package">The ODF package instance. / ODF 封裝執行個體。</param>
    public ImageDocument(OdfPackage package) : base(package)
    {
    }

    /// <summary>
    /// Creates a new high-level ODI image document.
    /// 建立新的高階 ODI 影像文件。
    /// </summary>
    /// <returns>A new <see cref="ImageDocument"/> instance. / 新的 <see cref="ImageDocument"/> 執行個體。</returns>
    public new static ImageDocument Create()
    {
        return (ImageDocument)OdfDocumentFactory.CreateDocument(OdfDocumentKind.Image);
    }

    /// <summary>
    /// Creates a new high-level ODI image document from the specified image template document.
    /// 從指定的影像範本文件建立新的高階 ODI 影像文件。
    /// </summary>
    /// <param name="template">The image template document. / 影像範本文件。</param>
    /// <returns>The created <see cref="ImageDocument"/> instance. / 建立完成的 <see cref="ImageDocument"/> 執行個體。</returns>
    public new static ImageDocument CreateFromTemplate(ImageTemplateDocument template) =>
        (ImageDocument)CreateFromTemplateInternal(template, OdfDocumentKind.Image, "application/vnd.oasis.opendocument.image");

    /// <summary>
    /// Creates an equivalent ODI (ZIP package) image document from a FODI flat XML image document, with identical content.
    /// 從 FODI 扁平 XML 影像文件建立等價的 ODI（ZIP 封裝）影像文件，內容完全相同。
    /// </summary>
    /// <param name="document">The source FODI flat XML image document. / 來源 FODI 扁平 XML 影像文件。</param>
    /// <returns>The created <see cref="ImageDocument"/> instance. / 建立完成的 <see cref="ImageDocument"/> 執行個體。</returns>
    public new static ImageDocument CreateFromFlatDocument(FlatImageDocument document) =>
        (ImageDocument)ConvertFlatVariantInternal(document, OdfDocumentKind.Image, targetIsFlatXml: false);

    /// <summary>
    /// Loads a high-level ODI image document from the specified path.
    /// 從指定路徑載入高階 ODI 影像文件。
    /// </summary>
    /// <param name="path">The ODI document path. / ODI 文件路徑。</param>
    /// <returns>The loaded <see cref="ImageDocument"/> instance. / 載入完成的 <see cref="ImageDocument"/> 執行個體。</returns>
    public new static ImageDocument Load(string path)
    {
        return EnsureImageDocument(OdfDocumentFactory.LoadDocument(path));
    }

    /// <summary>
    /// Asynchronously loads a high-level ODI image document from the specified path.
    /// 非同步從指定路徑載入高階 ODI 影像文件。
    /// </summary>
    /// <param name="path">The ODI document path. / ODI 文件路徑。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消語彙基元。</param>
    /// <returns>A task representing the asynchronous load operation, whose result is the loaded <see cref="ImageDocument"/>. / 代表非同步載入作業的工作，其結果為載入完成的 <see cref="ImageDocument"/>。</returns>
    public new static async Task<ImageDocument> LoadAsync(string path, CancellationToken cancellationToken = default) =>
        EnsureImageDocument(await OdfDocumentFactory.LoadDocumentAsync(path, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// Loads a high-level ODI image document from the specified stream.
    /// 從指定資料流載入高階 ODI 影像文件。
    /// </summary>
    /// <param name="stream">The stream containing the ODI document content. / 包含 ODI 文件內容的資料流。</param>
    /// <param name="fileName">The optional file name, used to assist format detection. / 選用的檔案名稱，用於輔助格式偵測。</param>
    /// <returns>The loaded <see cref="ImageDocument"/> instance. / 載入完成的 <see cref="ImageDocument"/> 執行個體。</returns>
    public new static ImageDocument Load(Stream stream, string? fileName = null)
    {
        return EnsureImageDocument(OdfDocumentFactory.LoadDocument(stream, fileName));
    }

    /// <summary>
    /// Asynchronously loads a high-level ODI image document from the specified stream.
    /// 非同步從指定資料流載入高階 ODI 影像文件。
    /// </summary>
    /// <param name="stream">The stream containing the ODI document content. / 包含 ODI 文件內容的資料流。</param>
    /// <param name="fileName">The optional file name, used to assist format detection. / 選用的檔案名稱，用於輔助格式偵測。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消語彙基元。</param>
    /// <returns>A task representing the asynchronous load operation, whose result is the loaded <see cref="ImageDocument"/>. / 代表非同步載入作業的工作，其結果為載入完成的 <see cref="ImageDocument"/>。</returns>
    public new static async Task<ImageDocument> LoadAsync(Stream stream, string? fileName = null, CancellationToken cancellationToken = default) =>
        EnsureImageDocument(await OdfDocumentFactory.LoadDocumentAsync(stream, fileName, cancellationToken).ConfigureAwait(false));

    private static ImageDocument EnsureImageDocument(OdfDocument document)
    {
        if (document is ImageDocument image && document.DocumentKind == OdfDocumentKind.Image)
        {
            return image;
        }

        document.Dispose();
        throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_OdfImageDocument_SpecifiedOdfFileOdi"));
    }
}
