using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OdfKit.Compliance;
using OdfKit.Core;

namespace OdfKit.Formula;

/// <summary>
/// 代表高階 ODF 公式文件（Formula Document）的類別。
/// </summary>
public class FormulaDocument : OdfFormulaDocument
{
    /// <summary>
    /// 使用指定的 ODF 封裝初始化 <see cref="FormulaDocument"/> 類別的新執行個體。
    /// </summary>
    /// <param name="package">ODF 封裝執行個體</param>
    public FormulaDocument(OdfPackage package) : base(package)
    {
    }

    /// <summary>
    /// 使用指定的 ODF 封裝與子路徑初始化 <see cref="FormulaDocument"/> 類別的新執行個體。
    /// </summary>
    /// <param name="package">ODF 封裝執行個體</param>
    /// <param name="subPath">封裝內的子路徑</param>
    public FormulaDocument(OdfPackage package, string subPath) : base(package, subPath)
    {
    }

    /// <summary>
    /// 根據指定 MathML XML 建立新的高階公式文件。
    /// </summary>
    /// <param name="mathml">格式正確的 MathML XML</param>
    /// <returns>建立完成的高階 <see cref="FormulaDocument"/> 執行個體</returns>
    /// <exception cref="ArgumentNullException">當 <paramref name="mathml"/> 為 <see langword="null"/> 時擲出</exception>
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
    /// 從指定檔案路徑載入高階公式文件。
    /// </summary>
    /// <param name="path">ODF 公式文件路徑</param>
    /// <returns>載入完成的高階 <see cref="FormulaDocument"/> 執行個體</returns>
    public new static FormulaDocument Load(string path)
    {
        return EnsureFormula(OdfDocumentFactory.LoadDocument(path));
    }

    /// <summary>
    /// 非同步從指定檔案路徑載入高階公式文件。
    /// </summary>
    /// <param name="path">ODF 公式文件路徑</param>
    /// <param name="cancellationToken">取消語彙基元</param>
    /// <returns>代表非同步載入作業的工作，其結果為載入完成的高階 <see cref="FormulaDocument"/></returns>
    public new static async Task<FormulaDocument> LoadAsync(string path, CancellationToken cancellationToken = default) =>
        EnsureFormula(await OdfDocumentFactory.LoadDocumentAsync(path, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// 從指定資料流載入高階公式文件。
    /// </summary>
    /// <param name="stream">包含 ODF 公式文件內容的資料流</param>
    /// <param name="fileName">選用的檔案名稱，用於輔助格式偵測</param>
    /// <returns>載入完成的高階 <see cref="FormulaDocument"/> 執行個體</returns>
    public new static FormulaDocument Load(Stream stream, string? fileName = null)
    {
        return EnsureFormula(OdfDocumentFactory.LoadDocument(stream, fileName));
    }

    /// <summary>
    /// 非同步從指定資料流載入高階公式文件。
    /// </summary>
    /// <param name="stream">包含 ODF 公式文件內容的資料流</param>
    /// <param name="fileName">選用的檔案名稱，用於輔助格式偵測</param>
    /// <param name="cancellationToken">取消語彙基元</param>
    /// <returns>代表非同步載入作業的工作，其結果為載入完成的高階 <see cref="FormulaDocument"/></returns>
    public new static async Task<FormulaDocument> LoadAsync(Stream stream, string? fileName = null, CancellationToken cancellationToken = default) =>
        EnsureFormula(await OdfDocumentFactory.LoadDocumentAsync(stream, fileName, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// 從指定的公式範本文件建立新的高階公式文件。
    /// </summary>
    /// <param name="template">公式範本文件</param>
    /// <returns>建立完成的 <see cref="FormulaDocument"/> 執行個體</returns>
    public static FormulaDocument CreateFromTemplate(FormulaTemplateDocument template) =>
        (FormulaDocument)CreateFromTemplateInternal(template, OdfDocumentKind.Formula, "application/vnd.oasis.opendocument.formula");

    /// <summary>
    /// 從 FDF 扁平 XML 公式文件建立等價的 ODF（ZIP 封裝）公式文件，內容完全相同。
    /// </summary>
    /// <param name="document">來源 FDF 扁平 XML 公式文件</param>
    /// <returns>建立完成的 <see cref="FormulaDocument"/> 執行個體</returns>
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
    /// 設定 MathML 的 XML 字串。
    /// </summary>
    /// <param name="mathml">格式正確的 MathML XML</param>
    public void SetMathML(string mathml)
    {
        MathMlXml = mathml;
    }
}
