using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OdfKit.Compliance;
using OdfKit.Core;

namespace OdfKit.Formula;

/// <summary>
/// Represents an ODF formula template document (OTF).
/// 表示 ODF 公式範本文件（OTF）。
/// </summary>
public sealed class FormulaTemplateDocument : FormulaDocument
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FormulaTemplateDocument"/> class with the specified ODF package.
    /// 使用指定的 ODF 封裝初始化 <see cref="FormulaTemplateDocument"/> 類別的新執行個體。
    /// </summary>
    /// <param name="package">The ODF package instance. / ODF 封裝執行個體。</param>
    public FormulaTemplateDocument(OdfPackage package) : base(package)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FormulaTemplateDocument"/> class with the specified ODF package and sub-path.
    /// 使用指定的 ODF 封裝與子路徑初始化 <see cref="FormulaTemplateDocument"/> 類別的新執行個體。
    /// </summary>
    /// <param name="package">The ODF package instance. / ODF 封裝執行個體。</param>
    /// <param name="subPath">The sub-path within the package. / 封裝內的子路徑。</param>
    public FormulaTemplateDocument(OdfPackage package, string subPath) : base(package, subPath)
    {
    }

    /// <summary>
    /// Creates a new OTF formula template document.
    /// 建立新的 OTF 公式範本文件。
    /// </summary>
    /// <returns>A new <see cref="FormulaTemplateDocument"/> instance. / 新的 <see cref="FormulaTemplateDocument"/> 執行個體。</returns>
    public static new FormulaTemplateDocument Create()
    {
        return (FormulaTemplateDocument)OdfDocumentFactory.CreateDocument(OdfDocumentKind.FormulaTemplate);
    }

    /// <summary>
    /// Loads an OTF formula template document from the specified path.
    /// 從指定路徑載入 OTF 公式範本文件。
    /// </summary>
    /// <param name="path">The OTF document path. / OTF 文件路徑。</param>
    /// <returns>The loaded <see cref="FormulaTemplateDocument"/> instance. / 載入完成的 <see cref="FormulaTemplateDocument"/> 執行個體。</returns>
    public static new FormulaTemplateDocument Load(string path) =>
        Ensure(OdfDocumentFactory.LoadDocument(path));

    /// <summary>
    /// Asynchronously loads an OTF formula template document from the specified path.
    /// 非同步從指定路徑載入 OTF 公式範本文件。
    /// </summary>
    /// <param name="path">The OTF document path. / OTF 文件路徑。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消語彙基元。</param>
    /// <returns>A task representing the asynchronous load operation, whose result is the loaded <see cref="FormulaTemplateDocument"/>. / 代表非同步載入作業的工作，其結果為載入完成的 <see cref="FormulaTemplateDocument"/>。</returns>
    public static new async Task<FormulaTemplateDocument> LoadAsync(string path, CancellationToken cancellationToken = default) =>
        Ensure(await OdfDocumentFactory.LoadDocumentAsync(path, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// Loads an OTF formula template document from the specified stream.
    /// 從指定資料流載入 OTF 公式範本文件。
    /// </summary>
    /// <param name="stream">The stream containing the OTF document content. / 包含 OTF 文件內容的資料流。</param>
    /// <param name="fileName">The optional file name, used to assist format detection. / 選用的檔案名稱，用於輔助格式偵測。</param>
    /// <returns>The loaded <see cref="FormulaTemplateDocument"/> instance. / 載入完成的 <see cref="FormulaTemplateDocument"/> 執行個體。</returns>
    public static new FormulaTemplateDocument Load(Stream stream, string? fileName = null) =>
        Ensure(OdfDocumentFactory.LoadDocument(stream, fileName));

    /// <summary>
    /// Asynchronously loads an OTF formula template document from the specified stream.
    /// 非同步從指定資料流載入 OTF 公式範本文件。
    /// </summary>
    /// <param name="stream">The stream containing the OTF document content. / 包含 OTF 文件內容的資料流。</param>
    /// <param name="fileName">The optional file name, used to assist format detection. / 選用的檔案名稱，用於輔助格式偵測。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消語彙基元。</param>
    /// <returns>A task representing the asynchronous load operation, whose result is the loaded <see cref="FormulaTemplateDocument"/>. / 代表非同步載入作業的工作，其結果為載入完成的 <see cref="FormulaTemplateDocument"/>。</returns>
    public static new async Task<FormulaTemplateDocument> LoadAsync(Stream stream, string? fileName = null, CancellationToken cancellationToken = default) =>
        Ensure(await OdfDocumentFactory.LoadDocumentAsync(stream, fileName, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// Creates a new OTF formula template document from an existing ODF formula document, preserving its MathML formula content.
    /// 從現有的 ODF 公式文件建立新的 OTF 公式範本文件，完整保留其 MathML 公式內容。
    /// </summary>
    /// <param name="document">The formula document used as the template content source. / 作為範本內容來源的公式文件。</param>
    /// <returns>The created <see cref="FormulaTemplateDocument"/> instance. / 建立完成的 <see cref="FormulaTemplateDocument"/> 執行個體。</returns>
    public static FormulaTemplateDocument CreateFromDocument(FormulaDocument document) =>
        (FormulaTemplateDocument)CreateTemplateFromDocumentInternal(
            document,
            OdfDocumentKind.FormulaTemplate,
            "application/vnd.oasis.opendocument.formula-template");

    private static FormulaTemplateDocument Ensure(OdfDocument document) =>
        OdfDocumentVariantSupport.EnsureKind<FormulaTemplateDocument>(
            document,
            OdfDocumentKind.FormulaTemplate,
            OdfLocalizer.GetMessage("Err_FormulaTemplateDocument_SpecifiedOdfFileOtf"));
}

/// <summary>
/// Represents an ODF flat XML formula document (FDF).
/// 表示 ODF 扁平 XML 公式文件（FDF）。
/// </summary>
public sealed class FlatFormulaDocument : FormulaDocument
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FlatFormulaDocument"/> class with the specified ODF package.
    /// 使用指定的 ODF 封裝初始化 <see cref="FlatFormulaDocument"/> 類別的新執行個體。
    /// </summary>
    /// <param name="package">The ODF package or flat XML container. / ODF 封裝或扁平 XML 容器。</param>
    public FlatFormulaDocument(OdfPackage package) : base(package)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FlatFormulaDocument"/> class with the specified ODF package and sub-path.
    /// 使用指定的 ODF 封裝與子路徑初始化 <see cref="FlatFormulaDocument"/> 類別的新執行個體。
    /// </summary>
    /// <param name="package">The ODF package or flat XML container. / ODF 封裝或扁平 XML 容器。</param>
    /// <param name="subPath">The sub-path within the package. / 封裝內的子路徑。</param>
    public FlatFormulaDocument(OdfPackage package, string subPath) : base(package, subPath)
    {
    }

    /// <summary>
    /// Creates a new FDF flat XML formula document.
    /// 建立新的 FDF 扁平 XML 公式文件。
    /// </summary>
    /// <returns>A new <see cref="FlatFormulaDocument"/> instance. / 新的 <see cref="FlatFormulaDocument"/> 執行個體。</returns>
    public static new FlatFormulaDocument Create()
    {
        return (FlatFormulaDocument)OdfDocumentFactory.CreateDocument(OdfDocumentKind.FlatFormula);
    }

    /// <summary>
    /// Loads an FDF flat XML formula document from the specified path.
    /// 從指定路徑載入 FDF 扁平 XML 公式文件。
    /// </summary>
    /// <param name="path">The FDF document path. / FDF 文件路徑。</param>
    /// <returns>The loaded <see cref="FlatFormulaDocument"/> instance. / 載入完成的 <see cref="FlatFormulaDocument"/> 執行個體。</returns>
    public static new FlatFormulaDocument Load(string path) =>
        Ensure(OdfDocumentFactory.LoadDocument(path));

    /// <summary>
    /// Asynchronously loads an FDF flat XML formula document from the specified path.
    /// 非同步從指定路徑載入 FDF 扁平 XML 公式文件。
    /// </summary>
    /// <param name="path">The FDF document path. / FDF 文件路徑。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消語彙基元。</param>
    /// <returns>A task representing the asynchronous load operation, whose result is the loaded <see cref="FlatFormulaDocument"/>. / 代表非同步載入作業的工作，其結果為載入完成的 <see cref="FlatFormulaDocument"/>。</returns>
    public static new async Task<FlatFormulaDocument> LoadAsync(string path, CancellationToken cancellationToken = default) =>
        Ensure(await OdfDocumentFactory.LoadDocumentAsync(path, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// Loads an FDF flat XML formula document from the specified stream.
    /// 從指定資料流載入 FDF 扁平 XML 公式文件。
    /// </summary>
    /// <param name="stream">The stream containing the FDF document content. / 包含 FDF 文件內容的資料流。</param>
    /// <param name="fileName">The optional file name, used to assist format detection. / 選用的檔案名稱，用於輔助格式偵測。</param>
    /// <returns>The loaded <see cref="FlatFormulaDocument"/> instance. / 載入完成的 <see cref="FlatFormulaDocument"/> 執行個體。</returns>
    public static new FlatFormulaDocument Load(Stream stream, string? fileName = null) =>
        Ensure(OdfDocumentFactory.LoadDocument(stream, fileName));

    /// <summary>
    /// Asynchronously loads an FDF flat XML formula document from the specified stream.
    /// 非同步從指定資料流載入 FDF 扁平 XML 公式文件。
    /// </summary>
    /// <param name="stream">The stream containing the FDF document content. / 包含 FDF 文件內容的資料流。</param>
    /// <param name="fileName">The optional file name, used to assist format detection. / 選用的檔案名稱，用於輔助格式偵測。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消語彙基元。</param>
    /// <returns>A task representing the asynchronous load operation, whose result is the loaded <see cref="FlatFormulaDocument"/>. / 代表非同步載入作業的工作，其結果為載入完成的 <see cref="FlatFormulaDocument"/>。</returns>
    public static new async Task<FlatFormulaDocument> LoadAsync(Stream stream, string? fileName = null, CancellationToken cancellationToken = default) =>
        Ensure(await OdfDocumentFactory.LoadDocumentAsync(stream, fileName, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// Creates an equivalent FDF flat XML formula document from an existing ODF (ZIP package) formula document, preserving the same content.
    /// 從現有的 ODF（ZIP 封裝）公式文件建立等價的 FDF 扁平 XML 公式文件，內容完全相同。
    /// </summary>
    /// <param name="document">The source ODF formula document. / 來源 ODF 公式文件。</param>
    /// <returns>The created <see cref="FlatFormulaDocument"/> instance. / 建立完成的 <see cref="FlatFormulaDocument"/> 執行個體。</returns>
    public static FlatFormulaDocument CreateFromDocument(FormulaDocument document) =>
        (FlatFormulaDocument)ConvertFlatVariantInternal(document, OdfDocumentKind.FlatFormula, targetIsFlatXml: true);

    private static FlatFormulaDocument Ensure(OdfDocument document) =>
        OdfDocumentVariantSupport.EnsureKind<FlatFormulaDocument>(
            document,
            OdfDocumentKind.FlatFormula,
            OdfLocalizer.GetMessage("Err_FlatFormulaDocument_SpecifiedOdfFileFdf"));
}
