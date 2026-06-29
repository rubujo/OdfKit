using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OdfKit.Compliance;
using OdfKit.Core;

namespace OdfKit.Formula;

/// <summary>
/// Represents a high-level ODF formula document.
/// 代表高階 ODF 公式文件（Formula Document）的類別。
/// </summary>
public class FormulaDocument : OdfFormulaDocument
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FormulaDocument"/> class with the specified ODF package.
    /// 使用指定的 ODF 封裝初始化 <see cref="FormulaDocument"/> 類別的新執行個體。
    /// </summary>
    /// <param name="package">The ODF package instance. / ODF 封裝執行個體。</param>
    public FormulaDocument(OdfPackage package) : base(package)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FormulaDocument"/> class with the specified ODF package and sub-path.
    /// 使用指定的 ODF 封裝與子路徑初始化 <see cref="FormulaDocument"/> 類別的新執行個體。
    /// </summary>
    /// <param name="package">The ODF package instance. / ODF 封裝執行個體。</param>
    /// <param name="subPath">The sub-path within the package. / 封裝內的子路徑。</param>
    public FormulaDocument(OdfPackage package, string subPath) : base(package, subPath)
    {
    }

    /// <summary>
    /// Creates a new high-level formula document from the specified MathML XML.
    /// 根據指定 MathML XML 建立新的高階公式文件。
    /// </summary>
    /// <param name="mathml">The well-formed MathML XML. / 格式正確的 MathML XML。</param>
    /// <returns>The created high-level <see cref="FormulaDocument"/> instance. / 建立完成的高階 <see cref="FormulaDocument"/> 執行個體。</returns>
    /// <exception cref="ArgumentNullException">When <paramref name="mathml"/> is <see langword="null"/>. / 當 <paramref name="mathml"/> 為 <see langword="null"/> 時擲出。</exception>
    public static FormulaDocument Create(string mathml)
    {
        if (mathml is null)
        {
            throw new ArgumentNullException(nameof(mathml));
        }

        var doc = (FormulaDocument)OdfDocumentFactory.CreateDocument(OdfDocumentKind.Formula);
        doc.SetMathML(mathml);
        return doc;
    }

    /// <summary>
    /// Loads a high-level formula document from the specified file path.
    /// 從指定檔案路徑載入高階公式文件。
    /// </summary>
    /// <param name="path">The ODF formula document path. / ODF 公式文件路徑。</param>
    /// <returns>The loaded high-level <see cref="FormulaDocument"/> instance. / 載入完成的高階 <see cref="FormulaDocument"/> 執行個體。</returns>
    public new static FormulaDocument Load(string path)
    {
        return EnsureFormula(OdfDocumentFactory.LoadDocument(path));
    }

    /// <summary>
    /// Asynchronously loads a high-level formula document from the specified file path.
    /// 非同步從指定檔案路徑載入高階公式文件。
    /// </summary>
    /// <param name="path">The ODF formula document path. / ODF 公式文件路徑。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消語彙基元。</param>
    /// <returns>A task representing the asynchronous load operation, whose result is the loaded high-level <see cref="FormulaDocument"/>. / 代表非同步載入作業的工作，其結果為載入完成的高階 <see cref="FormulaDocument"/>。</returns>
    public new static async Task<FormulaDocument> LoadAsync(string path, CancellationToken cancellationToken = default) =>
        EnsureFormula(await OdfDocumentFactory.LoadDocumentAsync(path, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// Loads a high-level formula document from the specified stream.
    /// 從指定資料流載入高階公式文件。
    /// </summary>
    /// <param name="stream">The stream containing the ODF formula document content. / 包含 ODF 公式文件內容的資料流。</param>
    /// <param name="fileName">The optional file name, used to assist format detection. / 選用的檔案名稱，用於輔助格式偵測。</param>
    /// <returns>The loaded high-level <see cref="FormulaDocument"/> instance. / 載入完成的高階 <see cref="FormulaDocument"/> 執行個體。</returns>
    public new static FormulaDocument Load(Stream stream, string? fileName = null)
    {
        return EnsureFormula(OdfDocumentFactory.LoadDocument(stream, fileName));
    }

    /// <summary>
    /// Asynchronously loads a high-level formula document from the specified stream.
    /// 非同步從指定資料流載入高階公式文件。
    /// </summary>
    /// <param name="stream">The stream containing the ODF formula document content. / 包含 ODF 公式文件內容的資料流。</param>
    /// <param name="fileName">The optional file name, used to assist format detection. / 選用的檔案名稱，用於輔助格式偵測。</param>
    /// <param name="cancellationToken">The cancellation token. / 取消語彙基元。</param>
    /// <returns>A task representing the asynchronous load operation, whose result is the loaded high-level <see cref="FormulaDocument"/>. / 代表非同步載入作業的工作，其結果為載入完成的高階 <see cref="FormulaDocument"/>。</returns>
    public new static async Task<FormulaDocument> LoadAsync(Stream stream, string? fileName = null, CancellationToken cancellationToken = default) =>
        EnsureFormula(await OdfDocumentFactory.LoadDocumentAsync(stream, fileName, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// Creates a new high-level formula document from the specified formula template document.
    /// 從指定的公式範本文件建立新的高階公式文件。
    /// </summary>
    /// <param name="template">The formula template document. / 公式範本文件。</param>
    /// <returns>The created <see cref="FormulaDocument"/> instance. / 建立完成的 <see cref="FormulaDocument"/> 執行個體。</returns>
    public static FormulaDocument CreateFromTemplate(FormulaTemplateDocument template) =>
        (FormulaDocument)CreateFromTemplateInternal(template, OdfDocumentKind.Formula, "application/vnd.oasis.opendocument.formula");

    /// <summary>
    /// Creates an equivalent ODF (ZIP package) formula document from an FDF flat XML formula document, preserving the same content.
    /// 從 FDF 扁平 XML 公式文件建立等價的 ODF（ZIP 封裝）公式文件，內容完全相同。
    /// </summary>
    /// <param name="document">The source FDF flat XML formula document. / 來源 FDF 扁平 XML 公式文件。</param>
    /// <returns>The created <see cref="FormulaDocument"/> instance. / 建立完成的 <see cref="FormulaDocument"/> 執行個體。</returns>
    public static FormulaDocument CreateFromFlatDocument(FlatFormulaDocument document) =>
        (FormulaDocument)ConvertFlatVariantInternal(document, OdfDocumentKind.Formula, targetIsFlatXml: false);

    private static FormulaDocument EnsureFormula(OdfDocument document)
    {
        if (document is FormulaDocument formula && document.DocumentKind == OdfDocumentKind.Formula)
        {
            return formula;
        }

        document.Dispose();
        throw new InvalidOperationException(OdfLocalizer.GetMessage("Err_FormulaDocument_SpecifiedOdfFileHigher"));
    }

    /// <summary>
    /// Sets the MathML XML string.
    /// 設定 MathML 的 XML 字串。
    /// </summary>
    /// <param name="mathml">The well-formed MathML XML. / 格式正確的 MathML XML。</param>
    public void SetMathML(string mathml)
    {
        MathMlXml = mathml;
    }
}
